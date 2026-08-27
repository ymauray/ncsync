using Nc.Commands;
using Nc.Git;

namespace Nc.Tests.Commands;

public sealed class ResetCommandHandlerTests : IDisposable
{
    private readonly string _repoPath = Directory.CreateTempSubdirectory("nc-reset-cmd-tests-").FullName;
    private readonly GitClient _git;

    public ResetCommandHandlerTests()
    {
        _git = new GitClient(_repoPath);
        _git.Init();
    }

    public void Dispose() => DeleteDirectoryForcefully(_repoPath);

    [Fact]
    public void Execute_WithoutAnySync_ReturnsErrorAndPrintsMessage()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["a.txt"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Execute_OnModifiedSyncedFile_RestoresContentAndUnstages()
    {
        Sync("a.txt", "original");
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "modifié localement");
        _git.AddAll();

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["a.txt"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("original", File.ReadAllText(Path.Combine(_repoPath, "a.txt")));
        Assert.Empty(_git.Status().StandardOutput);
    }

    [Fact]
    public void Execute_OnUnstagedModification_RestoresContent()
    {
        Sync("a.txt", "original");
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "modifié localement");

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["a.txt"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("original", File.ReadAllText(Path.Combine(_repoPath, "a.txt")));
    }

    [Fact]
    public void Execute_OnNeverSyncedStagedFile_DeletesFile()
    {
        Sync("existant.txt", "contenu");
        File.WriteAllText(Path.Combine(_repoPath, "nouveau.txt"), "nouveau");
        _git.AddAll();

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["nouveau.txt"]);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(_repoPath, "nouveau.txt")));
        Assert.Empty(_git.Status().StandardOutput);
    }

    [Fact]
    public void Execute_OnNeverSyncedUnstagedFile_DeletesFile()
    {
        Sync("existant.txt", "contenu");
        File.WriteAllText(Path.Combine(_repoPath, "nouveau.txt"), "nouveau");

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["nouveau.txt"]);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(_repoPath, "nouveau.txt")));
    }

    [Fact]
    public void Execute_WithMixOfExistingAndNeverSyncedFiles_HandlesBothCorrectly()
    {
        Sync("existant.txt", "original");
        File.WriteAllText(Path.Combine(_repoPath, "existant.txt"), "modifié");
        File.WriteAllText(Path.Combine(_repoPath, "nouveau.txt"), "nouveau");
        _git.AddAll();

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["existant.txt", "nouveau.txt"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("original", File.ReadAllText(Path.Combine(_repoPath, "existant.txt")));
        Assert.False(File.Exists(Path.Combine(_repoPath, "nouveau.txt")));
    }

    [Fact]
    public void Execute_OnSubdirectoryFileGivenWithForwardSlash_RestoresContent()
    {
        Directory.CreateDirectory(Path.Combine(_repoPath, "sous-dossier"));
        Sync(Path.Combine("sous-dossier", "a.txt"), "original");
        File.WriteAllText(Path.Combine(_repoPath, "sous-dossier", "a.txt"), "modifié localement");

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["sous-dossier/a.txt"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("original", File.ReadAllText(Path.Combine(_repoPath, "sous-dossier", "a.txt")));
    }

    [SkippableFact]
    public void Execute_OnSubdirectoryFileGivenWithBackslash_RestoresRatherThanDeletes()
    {
        // `\` n'est un separateur de chemin que sous Windows ; sous Linux/macOS c'est un
        // caractere de nom de fichier ordinaire, donc ce test n'a de sens que sur Windows.
        Skip.IfNot(OperatingSystem.IsWindows(), "L'antislash n'est un separateur de chemin que sous Windows.");

        // Regression : cette forme de chemin (style Windows) faisait passer un fichier
        // pourtant present dans refs/nc/synced par la branche "jamais synchronise", qui le
        // supprimait au lieu de restaurer son contenu (bug rapporte par l'utilisateur).
        Directory.CreateDirectory(Path.Combine(_repoPath, "sous-dossier"));
        Sync(Path.Combine("sous-dossier", "a.txt"), "original");
        File.WriteAllText(Path.Combine(_repoPath, "sous-dossier", "a.txt"), "modifié localement");

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["sous-dossier\\a.txt"]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(_repoPath, "sous-dossier", "a.txt")));
        Assert.Equal("original", File.ReadAllText(Path.Combine(_repoPath, "sous-dossier", "a.txt")));
    }

    [Fact]
    public void Execute_OnDeletedSyncedFile_RestoresIt()
    {
        Sync("a.txt", "contenu");
        File.Delete(Path.Combine(_repoPath, "a.txt"));
        _git.AddAll();

        var exitCode = ResetCommandHandler.Execute(_repoPath, ["a.txt"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("contenu", File.ReadAllText(Path.Combine(_repoPath, "a.txt")));
    }

    private void Sync(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_repoPath, fileName), content);
        _git.AddAll();
        _git.Commit("sync");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());
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
