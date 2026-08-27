using Nc.Configuration;
using Nc.Credentials;
using Nc.Sync;
using Nc.WebDav;

namespace Nc.Commands;

internal static class CloneCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string currentDirectory,
        string remoteSpec,
        string destination,
        CancellationToken cancellationToken = default,
        string? globalConfigDirectory = null)
    {
        RemoteSpec remote;
        try
        {
            remote = RemoteSpec.Parse(remoteSpec);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var destinationDirectory = Path.GetFullPath(destination, currentDirectory);
        try
        {
            NcCloneService.EnsureDestinationIsEmptyOrAbsent(destinationDirectory);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var config = new IdentityConfigStore(currentDirectory, globalConfigDirectory).Load();
        if (config.Username is null)
        {
            Console.Error.WriteLine("Aucun nom d'utilisateur configuré : lancez d'abord « nc config username <nom> ».");
            return 1;
        }

        var credentialStore = CredentialStoreFactory.Create();
        var password = credentialStore.TryLoad(CredentialKey.ForPath(currentDirectory));
        if (password is null)
        {
            Console.Error.WriteLine("Aucun mot de passe configuré : lancez d'abord « nc config password <mot de passe> ».");
            return 1;
        }

        var webDavClient = NextcloudWebDavClient.Create(remote.Server, config.Username, password);

        try
        {
            await NcCloneService.CloneAsync(webDavClient, remote.Path, destinationDirectory, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Échec de la connexion au serveur Nextcloud : {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Échec du clone : {ex.Message}");
            return 1;
        }

        new NcConfigStore(destinationDirectory).Save(new NcConfig
        {
            Username = config.Username,
            ServerUrl = remote.Server,
            RemotePath = remote.Path,
        });
        credentialStore.Save(CredentialKey.ForPath(destinationDirectory), password);

        Console.WriteLine($"Cloné dans {destinationDirectory}");
        return 0;
    }
}
