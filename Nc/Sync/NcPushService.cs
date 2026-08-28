using System.Net;
using Nc.Git;
using Nc.Processes;
using Nc.WebDav;

namespace Nc.Sync;

/// <summary>
/// Algorithme de `nc push` (SPECS.md §2, §4 et §7) : calcule le diff staged, vérifie l'absence
/// de conflit par ETag, envoie/supprime/renomme les fichiers correspondants via WebDAV, puis
/// scelle un commit et avance `refs/nc/synced` + `.nc/state.json`. Ne s'occupe ni des
/// identifiants ni de la résolution du remote (voir <see cref="Nc.Commands.PushCommandHandler"/>).
///
/// Détection de conflit (Phase 11, SPECS.md §4) : avant toute écriture, chaque fichier
/// modifié/supprimé/renommé déjà connu de `.nc/state.json` est vérifié par un `PROPFIND` ciblé
/// (sur `Path`, ou `OldPath` pour un renommage) comparant son ETag distant actuel à celui
/// enregistré. Un écart (ou une disparition, 404) annule tout le batch — aucune écriture n'est
/// tentée, y compris pour les fichiers non conflictuels du même batch. Les fichiers jamais
/// synchronisés (`Added`, absents de `.nc/state.json`) n'ont pas d'ETag connu à comparer et ne
/// sont donc pas concernés par cette vérification.
///
/// Renommages (Phase 12, SPECS.md §8.3) : mappés vers un `MOVE` WebDAV (`OldPath` → `Path`)
/// plutôt qu'un DELETE+PUT, pour préserver l'historique de version côté serveur.
///
/// État local tout-ou-rien : si une opération WebDAV échoue en cours de batch, ni le commit, ni
/// `refs/nc/synced`, ni `.nc/state.json` ne sont modifiés (le staging Git reste intact pour un
/// `nc push` de reprise). Les fichiers déjà envoyés au serveur avant l'échec y restent (WebDAV
/// n'est pas transactionnel) ; une gestion plus fine de cet état partiel est prévue en Phase 16.
/// </summary>
internal static class NcPushService
{
    private const string SyncedRef = "refs/nc/synced";

    public static async Task<int> PushAsync(
        GitClient git,
        NextcloudWebDavClient webDavClient,
        string workingDirectory,
        string remotePath,
        SyncStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        var diffResult = git.DiffCachedNameStatus();
        diffResult.EnsureSuccess("git diff --cached --name-status -M");

        var entries = GitDiffNameStatusParser.Parse(diffResult.StandardOutput);
        if (entries.Count == 0)
        {
            return 0;
        }

        var state = stateStore.Load();

        await EnsureNoConflictAsync(webDavClient, remotePath, entries, state, cancellationToken);

        var eTagsByPath = new Dictionary<string, string>(state.ETagsByPath);

        foreach (var entry in entries)
        {
            switch (entry.ChangeType)
            {
                case GitChangeType.Added:
                case GitChangeType.Modified:
                    await PutFileAsync(webDavClient, workingDirectory, remotePath, entry.Path, eTagsByPath, cancellationToken);
                    break;
                case GitChangeType.Deleted:
                    await DeleteFileAsync(webDavClient, remotePath, entry.Path, eTagsByPath, cancellationToken);
                    break;
                case GitChangeType.Renamed:
                    await MoveFileAsync(webDavClient, remotePath, entry.OldPath!, entry.Path, eTagsByPath, cancellationToken);
                    break;
            }
        }

        var commitResult = git.Commit($"sync {DateTimeOffset.UtcNow:O}");
        commitResult.EnsureSuccess("git commit");
        var commitSha = git.ReadRef("HEAD").StandardOutput.Trim();
        git.UpdateRef(SyncedRef, commitSha).EnsureSuccess("git update-ref");

        stateStore.Save(state with { ETagsByPath = eTagsByPath });

        return entries.Count;
    }

    private static async Task EnsureNoConflictAsync(
        NextcloudWebDavClient webDavClient,
        string remotePath,
        IReadOnlyList<GitDiffEntry> entries,
        SyncState state,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<string>();

        foreach (var entry in entries)
        {
            // Seuls les fichiers deja connus de .nc/state.json (Modified/Deleted/Renamed) ont un
            // ETag de reference auquel comparer ; un fichier jamais synchronise (Added) n'en a
            // pas. Pour un renommage, c'est OldPath (l'entree existante) qui porte l'ETag connu,
            // pas Path (la destination, qui n'existe pas encore).
            var pathToCheck = entry.ChangeType switch
            {
                GitChangeType.Modified or GitChangeType.Deleted => entry.Path,
                GitChangeType.Renamed => entry.OldPath!,
                _ => null,
            };

            if (pathToCheck is null || !state.ETagsByPath.TryGetValue(pathToCheck, out var knownETag))
            {
                continue;
            }

            var remoteFilePath = WebDavPathBuilder.Combine(remotePath, pathToCheck);
            var currentETag = await TryGetRemoteETagAsync(webDavClient, remoteFilePath, cancellationToken);
            if (currentETag != knownETag)
            {
                conflicts.Add(pathToCheck);
            }
        }

        if (conflicts.Count > 0)
        {
            throw new NcPushConflictException(conflicts);
        }
    }

    // 404 signifie "disparu du serveur depuis la derniere synchronisation connue" : traite
    // comme un conflit par l'appelant (ETag attendu forcement different de null), pas comme une
    // erreur reseau — contrairement aux autres codes d'echec qui restent de vraies erreurs.
    private static async Task<string?> TryGetRemoteETagAsync(NextcloudWebDavClient webDavClient, string remoteFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var entries = await webDavClient.PropFindAsync(remoteFilePath, WebDavDepth.Zero, cancellationToken);
            return entries.FirstOrDefault()?.ETag;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static async Task PutFileAsync(
        NextcloudWebDavClient webDavClient,
        string workingDirectory,
        string remotePath,
        string relativePath,
        Dictionary<string, string> eTagsByPath,
        CancellationToken cancellationToken)
    {
        var remoteFilePath = WebDavPathBuilder.Combine(remotePath, relativePath);
        var localFilePath = Path.Combine(workingDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

        await using (var content = File.OpenRead(localFilePath))
        {
            using var putResponse = await webDavClient.PutAsync(remoteFilePath, content, cancellationToken);
            EnsureHttpSuccess(putResponse, relativePath, "envoi");
        }

        // ETag lu via un PROPFIND ciblé plutôt que les en-têtes de la réponse PUT : même chemin
        // que `nc clone` (format `getetag`), pas de dépendance à un en-tête (ETag/OC-ETag) dont
        // la présence sur une réponse PUT n'a jamais été vérifiée contre une instance réelle.
        var entries = await webDavClient.PropFindAsync(remoteFilePath, WebDavDepth.Zero, cancellationToken);
        var eTag = entries.FirstOrDefault()?.ETag;
        if (eTag is null)
        {
            throw new InvalidOperationException($"Le serveur n'a renvoyé aucun ETag pour « {relativePath} » après l'envoi.");
        }

        eTagsByPath[relativePath] = eTag;
    }

    private static async Task DeleteFileAsync(
        NextcloudWebDavClient webDavClient,
        string remotePath,
        string relativePath,
        Dictionary<string, string> eTagsByPath,
        CancellationToken cancellationToken)
    {
        var remoteFilePath = WebDavPathBuilder.Combine(remotePath, relativePath);
        using var response = await webDavClient.DeleteAsync(remoteFilePath, cancellationToken);
        EnsureHttpSuccess(response, relativePath, "suppression");
        eTagsByPath.Remove(relativePath);
    }

    private static async Task MoveFileAsync(
        NextcloudWebDavClient webDavClient,
        string remotePath,
        string oldRelativePath,
        string newRelativePath,
        Dictionary<string, string> eTagsByPath,
        CancellationToken cancellationToken)
    {
        var oldRemoteFilePath = WebDavPathBuilder.Combine(remotePath, oldRelativePath);
        var newRemoteFilePath = WebDavPathBuilder.Combine(remotePath, newRelativePath);

        using (var response = await webDavClient.MoveAsync(oldRemoteFilePath, newRemoteFilePath, cancellationToken))
        {
            EnsureHttpSuccess(response, $"{oldRelativePath} -> {newRelativePath}", "renommage");
        }

        eTagsByPath.Remove(oldRelativePath);

        // Meme raison que PutFileAsync : ETag relu par PROPFIND cible plutot que suppose stable
        // apres le MOVE (Nextcloud peut recalculer l'ETag du fichier deplace).
        var entries = await webDavClient.PropFindAsync(newRemoteFilePath, WebDavDepth.Zero, cancellationToken);
        var eTag = entries.FirstOrDefault()?.ETag;
        if (eTag is null)
        {
            throw new InvalidOperationException($"Le serveur n'a renvoyé aucun ETag pour « {newRelativePath} » après le renommage.");
        }

        eTagsByPath[newRelativePath] = eTag;
    }

    private static void EnsureHttpSuccess(HttpResponseMessage response, string path, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Échec de la {operation} de « {path} » : {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

}
