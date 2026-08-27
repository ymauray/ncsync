using Nc.Commands;
using Nc.Credentials;

namespace Nc.Tests.Commands;

public sealed class CloneCommandHandlerTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-clone-cmd-tests-").FullName;

    // Repertoire/cle globaux isoles, distincts des vrais ~/.config/ncsync et de la cle
    // globale reelle : les tests ne doivent jamais lire ni ecrire la vraie configuration
    // globale de la machine qui les execute.
    private readonly string _globalConfigDirectory = Directory.CreateTempSubdirectory("nc-clone-cmd-global-tests-").FullName;
    private readonly string _globalCredentialKey = $"nc-tests-global-{Guid.NewGuid():N}";

    public void Dispose()
    {
        CredentialStoreFactory.Create().Delete(CredentialKey.ForPath(_workingDirectory));
        CredentialStoreFactory.Create().Delete(_globalCredentialKey);
        Directory.Delete(_workingDirectory, recursive: true);
        Directory.Delete(_globalConfigDirectory, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRemoteFormat_ReturnsErrorWithoutRequiringCredentials()
    {
        var exitCode = await ExecuteAsync("pas-de-deux-points", ".");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutUsername_ReturnsError()
    {
        var exitCode = await ExecuteAsync("serveur.example:/chemin", ".");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithUsernameButNoPassword_ReturnsError()
    {
        ConfigCommandHandlers.SetUsername(_workingDirectory, "alice", _globalConfigDirectory);

        var exitCode = await ExecuteAsync("serveur.example:/chemin", ".");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithGlobalUsernameAndPassword_PassesCredentialChecks()
    {
        ConfigCommandHandlers.SetUsername(_workingDirectory, "alice", _globalConfigDirectory);
        ConfigCommandHandlers.SetPassword(_workingDirectory, "s3cret", _globalCredentialKey);

        var writer = await CaptureErrorAsync(() => ExecuteAsync("serveur.invalide.exemple.test:/chemin", "."));

        // Echoue plus loin (connexion reseau), jamais sur des identifiants manquants :
        // preuve que la resolution username/mot de passe via les cles globales a fonctionne.
        Assert.DoesNotContain("nom d'utilisateur configuré", writer);
        Assert.DoesNotContain("mot de passe configuré", writer);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonEmptyDestination_FailsBeforeRequiringCredentials()
    {
        // Aucun `nc config` n'a ete fait : si la verification de destination echoue bien en
        // premier, le message d'erreur porte sur le dossier, jamais sur des identifiants
        // manquants (qui ne sont pas encore charges a ce stade).
        File.WriteAllText(Path.Combine(_workingDirectory, "deja-la.txt"), "contenu existant");

        int exitCode = 0;
        var writer = await CaptureErrorAsync(async () => exitCode = await ExecuteAsync("serveur.example:/chemin", "."));

        Assert.Equal(1, exitCode);
        Assert.Contains("existe déjà", writer);
    }

    private Task<int> ExecuteAsync(string remoteSpec, string destination) => CloneCommandHandler.ExecuteAsync(
        _workingDirectory,
        remoteSpec,
        destination,
        globalConfigDirectory: _globalConfigDirectory,
        globalCredentialKey: _globalCredentialKey);

    private static async Task<string> CaptureErrorAsync(Func<Task> action)
    {
        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            await action();
        }
        finally
        {
            Console.SetError(originalError);
        }
        return writer.ToString();
    }
}
