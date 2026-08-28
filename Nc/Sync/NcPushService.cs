using System.Diagnostics;
using Nc.Git;
using Nc.Processes;
using Nc.WebDav;

namespace Nc.Sync;

/// <summary>
/// Algorithme de `nc push` (cas nominal, sans conflit — SPECS.md §2 et §7) : calcule le diff
/// staged, envoie/supprime les fichiers correspondants via WebDAV, puis scelle un commit et
/// avance `refs/nc/synced` + `.nc/state.json`. Ne s'occupe ni des identifiants ni de la
/// résolution du remote (voir <see cref="Nc.Commands.PushCommandHandler"/>).
///
/// Pas de détection de conflit par ETag ici (Phase 11) et pas de vrai mapping MOVE pour les
/// renommages (Phase 12) : un batch contenant un renommage est refusé explicitement, en entier,
/// avant toute requête réseau — cf. ROADMAP.md, journal des décisions.
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
        EnsureGitSuccess(diffResult, "git diff --cached --name-status -M");

        var entries = GitDiffNameStatusParser.Parse(diffResult.StandardOutput);
        if (entries.Count == 0)
        {
            return 0;
        }

        var renamedPaths = entries.Where(e => e.ChangeType == GitChangeType.Renamed).Select(e => $"{e.OldPath} -> {e.Path}").ToList();
        if (renamedPaths.Count > 0)
        {
            throw new InvalidOperationException(
                "Renommage(s) détecté(s), pas encore pris en charge par « nc push » (Phase 12 à venir) : " +
                string.Join(", ", renamedPaths));
        }

        var state = stateStore.Load();
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
                    throw new UnreachableException("Les renommages sont refusés en amont de cette boucle.");
            }
        }

        var commitResult = git.Commit($"sync {DateTimeOffset.UtcNow:O}");
        EnsureGitSuccess(commitResult, "git commit");
        var commitSha = git.ReadRef("HEAD").StandardOutput.Trim();
        EnsureGitSuccess(git.UpdateRef(SyncedRef, commitSha), "git update-ref");

        stateStore.Save(state with { ETagsByPath = eTagsByPath });

        return entries.Count;
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

    private static void EnsureHttpSuccess(HttpResponseMessage response, string path, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Échec de la {operation} de « {path} » : {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private static void EnsureGitSuccess(ProcessResult result, string step)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException($"Échec de « {step} » : {result.StandardError}");
        }
    }
}
