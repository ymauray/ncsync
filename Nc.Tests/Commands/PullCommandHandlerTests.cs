using Nc.Commands;
using Nc.Configuration;
using Nc.Credentials;
using Nc.Git;

namespace Nc.Tests.Commands;

public sealed class PullCommandHandlerTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-pull-cmd-tests-").FullName;
    private readonly GitClient _git;

    public PullCommandHandlerTests()
    {
        _git = new GitClient(_workingDirectory);
        _git.Init();
    }

    public void Dispose()
    {
        CredentialStoreFactory.Create().Delete(CredentialKey.ForPath(_workingDirectory));
        DeleteDirectoryForcefully(_workingDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutNcConfig_ReturnsErrorMentioningClone()
    {
        var (exitCode, writer) = await CaptureErrorAsync(() => PullCommandHandler.ExecuteAsync(_workingDirectory));

        Assert.Equal(1, exitCode);
        Assert.Contains("clone", writer);
    }

    [Fact]
    public async Task ExecuteAsync_WithConfigButNoSyncedRef_ReturnsErrorMentioningSync()
    {
        WriteConfig();

        var (exitCode, writer) = await CaptureErrorAsync(() => PullCommandHandler.ExecuteAsync(_workingDirectory));

        Assert.Equal(1, exitCode);
        Assert.Contains("synchronisation", writer);
    }

    [Fact]
    public async Task ExecuteAsync_WithConfigAndSyncButNoPassword_ReturnsErrorMentioningPassword()
    {
        WriteConfig();
        SealInitialSync();

        var (exitCode, writer) = await CaptureErrorAsync(() => PullCommandHandler.ExecuteAsync(_workingDirectory));

        Assert.Equal(1, exitCode);
        Assert.Contains("mot de passe", writer);
    }

    [Fact]
    public async Task ExecuteAsync_WithConfigSyncAndPassword_PassesLocalChecksAndFailsOnNetwork()
    {
        WriteConfig(serverUrl: "serveur.invalide.exemple.test");
        SealInitialSync();
        CredentialStoreFactory.Create().Save(CredentialKey.ForPath(_workingDirectory), "s3cret");

        var (exitCode, writer) = await CaptureErrorAsync(() => PullCommandHandler.ExecuteAsync(_workingDirectory));

        Assert.Equal(1, exitCode);
        // Echoue plus loin (connexion reseau), jamais sur la config/synced/mot de passe locaux :
        // preuve que ces trois verifications ont ete franchies avant d'atteindre le reseau.
        Assert.DoesNotContain("n'est pas un clone", writer);
        Assert.DoesNotContain("Aucune synchronisation", writer);
        Assert.DoesNotContain("Aucun mot de passe", writer);
    }

    private void WriteConfig(string serverUrl = "serveur.example") =>
        new NcConfigStore(_workingDirectory).Save(new NcConfig
        {
            Username = "alice",
            ServerUrl = serverUrl,
            RemotePath = "/dossier",
        });

    private void SealInitialSync()
    {
        // .gitignore excluant .nc/ : reproduit ce que `nc clone` ecrit toujours, pour que
        // `.nc/config` (deja ecrit par WriteConfig) ne se retrouve pas lui-meme staged/pousse.
        File.WriteAllText(Path.Combine(_workingDirectory, ".gitignore"), ".nc/" + Environment.NewLine);
        File.WriteAllText(Path.Combine(_workingDirectory, "existant.txt"), "contenu");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());
    }

    private static async Task<(int ExitCode, string Error)> CaptureErrorAsync(Func<Task<int>> action)
    {
        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    // git rend certains fichiers de .git/objects en lecture seule sous Windows ;
    // Directory.Delete recursive echoue dessus sans ce nettoyage prealable (cf. GitClientTests).
    private static void DeleteDirectoryForcefully(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(path, recursive: true);
    }
}
