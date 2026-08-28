using Nc.Git;
using Nc.Processes;
using Nc.WebDav;

namespace Nc.Sync;

/// <summary>
/// Algorithme de `nc pull` (cas nominal — SPECS.md §2). `PROPFIND` récursif de tout l'arbre
/// distant (même approche que `nc clone`), comparé à `.nc/state.json` : les fichiers dont
/// l'ETag distant diffère de celui connu (ou absents de l'état connu) sont téléchargés, les
/// fichiers connus mais disparus du serveur sont supprimés localement. Puis
/// `git add -A && git commit -m "pull <timestamp>"`, avancement de `refs/nc/synced` et
/// remplacement intégral de `.nc/state.json` par l'instantané distant actuel (comme au clone).
///
/// Pas de protection contre l'écrasement de modifications locales non poussées ici (Phase 14,
/// SPECS.md §4) : un fichier local modifié mais non commité, si son pendant distant a aussi
/// changé, est écrasé sans avertissement — décision explicite de l'utilisateur pour rester dans
/// le périmètre « cas nominal » de la Phase 13, symétrique à l'absence de détection de conflit
/// de la Phase 10 de `nc push` — cf. ROADMAP.md, journal des décisions.
/// </summary>
internal static class NcPullService
{
    private const string SyncedRef = "refs/nc/synced";

    public static async Task<int> PullAsync(
        GitClient git,
        NextcloudWebDavClient webDavClient,
        string workingDirectory,
        string remotePath,
        SyncStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        var state = stateStore.Load();

        var normalizedRemotePath = remotePath.Trim('/');
        var requestPath = normalizedRemotePath.Length == 0 ? "." : $"{normalizedRemotePath}/";
        var requestedAbsolutePath = new Uri(webDavClient.BaseAddress, requestPath).AbsolutePath;

        var remoteEntries = await webDavClient.PropFindAsync(requestPath, WebDavDepth.Infinity, cancellationToken);

        var remoteETagsByPath = new Dictionary<string, string>();
        foreach (var entry in remoteEntries)
        {
            var relativePath = WebDavHrefResolver.ToRelativePath(requestedAbsolutePath, entry.Href);
            if (relativePath.Length == 0 || entry.IsCollection || entry.ETag is null)
            {
                continue;
            }

            remoteETagsByPath[relativePath] = entry.ETag;
        }

        var changedPaths = remoteETagsByPath
            .Where(kvp => !state.ETagsByPath.TryGetValue(kvp.Key, out var knownETag) || knownETag != kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();
        var deletedPaths = state.ETagsByPath.Keys.Where(path => !remoteETagsByPath.ContainsKey(path)).ToList();

        if (changedPaths.Count == 0 && deletedPaths.Count == 0)
        {
            return 0;
        }

        foreach (var relativePath in changedPaths)
        {
            await DownloadFileAsync(webDavClient, workingDirectory, remotePath, relativePath, cancellationToken);
        }

        foreach (var relativePath in deletedPaths)
        {
            var localFilePath = Path.Combine(workingDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localFilePath))
            {
                File.Delete(localFilePath);
            }
        }

        git.AddAll().EnsureSuccess("git add -A");
        git.Commit($"pull {DateTimeOffset.UtcNow:O}").EnsureSuccess("git commit");
        var commitSha = git.ReadRef("HEAD").StandardOutput.Trim();
        git.UpdateRef(SyncedRef, commitSha).EnsureSuccess("git update-ref");

        stateStore.Save(state with { ETagsByPath = remoteETagsByPath });

        return changedPaths.Count + deletedPaths.Count;
    }

    private static async Task DownloadFileAsync(
        NextcloudWebDavClient webDavClient,
        string workingDirectory,
        string remotePath,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var remoteFilePath = WebDavPathBuilder.Combine(remotePath, relativePath);
        var localFilePath = Path.Combine(workingDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)!);

        using var response = await webDavClient.GetAsync(remoteFilePath, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Échec du téléchargement de « {relativePath} » : {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        await using var fileStream = File.Create(localFilePath);
        await response.Content.CopyToAsync(fileStream, cancellationToken);
    }
}
