using Nc.Git;
using Nc.Processes;
using Nc.WebDav;

namespace Nc.Sync;

/// <summary>
/// Algorithme de `nc clone` : PROPFIND recursif, telechargement des fichiers, initialisation
/// du depot git local, ecriture de l'etat de synchronisation initial (SPECS.md §2 et §6).
/// Ne s'occupe pas des identifiants ni du parsing du remote (voir Nc.Commands.CloneCommandHandler) :
/// reste testable avec un HttpMessageHandler factice et un dossier temporaire reel.
/// </summary>
internal static class NcCloneService
{
    public static async Task CloneAsync(NextcloudWebDavClient webDavClient, string remotePath, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        EnsureDestinationIsEmptyOrAbsent(destinationDirectory);
        Directory.CreateDirectory(destinationDirectory);

        var normalizedRemotePath = remotePath.Trim('/');
        var requestPath = normalizedRemotePath.Length == 0 ? "." : $"{normalizedRemotePath}/";
        var requestedAbsolutePath = new Uri(webDavClient.BaseAddress, requestPath).AbsolutePath;

        var entries = await webDavClient.PropFindAsync(requestPath, WebDavDepth.Infinity, cancellationToken);

        var eTagsByPath = new Dictionary<string, string>();

        foreach (var entry in entries)
        {
            var relativePath = WebDavHrefResolver.ToRelativePath(requestedAbsolutePath, entry.Href);
            if (relativePath.Length == 0 || entry.IsCollection)
            {
                continue;
            }

            var localPath = Path.Combine(destinationDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            using var response = await webDavClient.GetAsync(entry.Href, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using (var fileStream = File.Create(localPath))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            if (entry.ETag is not null)
            {
                eTagsByPath[relativePath] = entry.ETag;
            }
        }

        await File.WriteAllTextAsync(Path.Combine(destinationDirectory, ".gitignore"), ".nc/" + Environment.NewLine, cancellationToken);

        var git = new GitClient(destinationDirectory);
        EnsureSuccess(git.Init(), "git init");
        EnsureSuccess(git.AddAll(), "git add -A");
        EnsureSuccess(git.Commit("sync initial"), "git commit");
        var commitSha = git.ReadRef("HEAD").StandardOutput.Trim();
        EnsureSuccess(git.UpdateRef("refs/nc/synced", commitSha), "git update-ref");

        new SyncStateStore(destinationDirectory).Save(new SyncState { ETagsByPath = eTagsByPath });
    }

    // Mimique `git clone` : refuse un dossier de destination existant et non vide. `.nc/` est
    // ignore dans cette verification car il peut deja exister suite a un `nc config` prealable
    // dans ce meme dossier (workflow documente dans CAHIER_DES_CHARGES.md §3) — ce n'est pas du
    // contenu utilisateur, seulement la configuration nc elle-meme.
    // Interne (pas private) : CloneCommandHandler l'appelle aussi en verification prealable,
    // avant meme de charger les identifiants, pour echouer vite sur un probleme purement local.
    internal static void EnsureDestinationIsEmptyOrAbsent(string destinationDirectory)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            return;
        }

        var hasForeignContent = Directory.EnumerateFileSystemEntries(destinationDirectory)
            .Select(Path.GetFileName)
            .Any(name => name != ".nc");

        if (hasForeignContent)
        {
            throw new InvalidOperationException($"« {destinationDirectory} » existe déjà et n'est pas un dossier vide.");
        }
    }

    private static void EnsureSuccess(ProcessResult result, string step)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException($"Échec de « {step} » : {result.StandardError}");
        }
    }
}
