using Nc.Commands;
using Nc.Credentials;

namespace Nc.Tests.Commands;

public sealed class CloneCommandHandlerTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-clone-cmd-tests-").FullName;

    public void Dispose()
    {
        CredentialStoreFactory.Create().Delete(CredentialKey.ForPath(_workingDirectory));
        Directory.Delete(_workingDirectory, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRemoteFormat_ReturnsErrorWithoutRequiringCredentials()
    {
        var exitCode = await CloneCommandHandler.ExecuteAsync(_workingDirectory, "pas-de-deux-points", ".");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutUsername_ReturnsError()
    {
        var exitCode = await CloneCommandHandler.ExecuteAsync(_workingDirectory, "serveur.example:/chemin", ".");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithUsernameButNoPassword_ReturnsError()
    {
        ConfigCommandHandlers.SetUsername(_workingDirectory, "alice");

        var exitCode = await CloneCommandHandler.ExecuteAsync(_workingDirectory, "serveur.example:/chemin", ".");

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
            exitCode = await CloneCommandHandler.ExecuteAsync(_workingDirectory, "serveur.example:/chemin", ".");
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(1, exitCode);
        Assert.Contains("existe déjà", writer.ToString());
    }
}
