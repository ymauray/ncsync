using Nc.Commands;
using Nc.Credentials;

namespace Nc.Tests.Commands;

public sealed class CloneCommandHandlerTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-clone-cmd-tests-").FullName;

    // Repertoire global isole, distinct du vrai ~/.config/ncsync : les tests ne doivent jamais
    // lire ni ecrire dans la vraie configuration globale de la machine qui les execute.
    private readonly string _globalConfigDirectory = Directory.CreateTempSubdirectory("nc-clone-cmd-global-tests-").FullName;

    public void Dispose()
    {
        CredentialStoreFactory.Create().Delete(CredentialKey.ForPath(_workingDirectory));
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
    public async Task ExecuteAsync_WithNonEmptyDestination_FailsBeforeRequiringCredentials()
    {
        // Aucun `nc config` n'a ete fait : si la verification de destination echoue bien en
        // premier, le message d'erreur porte sur le dossier, jamais sur des identifiants
        // manquants (qui ne sont pas encore charges a ce stade).
        File.WriteAllText(Path.Combine(_workingDirectory, "deja-la.txt"), "contenu existant");

        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        int exitCode;
        try
        {
            exitCode = await ExecuteAsync("serveur.example:/chemin", ".");
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(1, exitCode);
        Assert.Contains("existe déjà", writer.ToString());
    }

    private Task<int> ExecuteAsync(string remoteSpec, string destination) =>
        CloneCommandHandler.ExecuteAsync(_workingDirectory, remoteSpec, destination, globalConfigDirectory: _globalConfigDirectory);
}
