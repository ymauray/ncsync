using Nc.Configuration;
using Nc.Credentials;

namespace Nc.Commands;

internal static class ConfigCommandHandlers
{
    public static int SetUsername(string workingDirectory, string? username, string? globalConfigDirectory = null)
    {
        var store = new IdentityConfigStore(workingDirectory, globalConfigDirectory);

        if (username is null)
        {
            var current = store.Load().Username;
            if (current is not null)
            {
                Console.WriteLine(current);
            }
            return 0;
        }

        store.Save(store.Load() with { Username = username });
        Console.WriteLine($"Nom d'utilisateur enregistré : {username}");
        return 0;
    }

    public static int SetPassword(string workingDirectory, string? password)
    {
        if (password is null)
        {
            return 0;
        }

        CredentialStoreFactory.Create().Save(CredentialKey.ForPath(workingDirectory), password);
        Console.WriteLine("Mot de passe enregistré.");
        return 0;
    }
}
