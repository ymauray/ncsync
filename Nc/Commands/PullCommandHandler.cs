using Nc.Configuration;
using Nc.Credentials;
using Nc.Git;
using Nc.Sync;
using Nc.WebDav;

namespace Nc.Commands;

internal static class PullCommandHandler
{
    private const string SyncedRef = "refs/nc/synced";

    public static async Task<int> ExecuteAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var config = new NcConfigStore(workingDirectory).Load();
        if (config.Username is null || config.ServerUrl is null || config.RemotePath is null)
        {
            Console.Error.WriteLine("Ce dossier n'est pas un clone nc valide (avez-vous fait « nc clone » ici ?).");
            return 1;
        }

        var git = new GitClient(workingDirectory);
        if (!git.ReadRef(SyncedRef).Success)
        {
            Console.Error.WriteLine("Aucune synchronisation connue dans ce dossier (avez-vous fait « nc clone » ici ?).");
            return 1;
        }

        var password = CredentialStoreFactory.Create().TryLoad(CredentialKey.ForPath(workingDirectory));
        if (password is null)
        {
            Console.Error.WriteLine("Aucun mot de passe trouvé pour ce dossier : relancez « nc clone » pour le reconfigurer.");
            return 1;
        }

        var webDavClient = NextcloudWebDavClient.Create(config.ServerUrl, config.Username, password);
        var stateStore = new SyncStateStore(workingDirectory);

        try
        {
            var pulledCount = await NcPullService.PullAsync(git, webDavClient, workingDirectory, config.RemotePath, stateStore, cancellationToken);
            if (pulledCount == 0)
            {
                Console.WriteLine("Rien à récupérer, l'espace de synchronisation est à jour.");
                return 0;
            }

            Console.WriteLine($"{pulledCount} changement(s) récupéré(s) depuis le serveur.");
            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Échec de la connexion au serveur Nextcloud : {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Échec du pull : {ex.Message}");
            return 1;
        }
    }
}
