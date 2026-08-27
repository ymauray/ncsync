using Nc.Git;

namespace Nc.Tests.Git;

public sealed class GitClientTests : IDisposable
{
    private readonly string _repoPath;
    private readonly GitClient _git;

    public GitClientTests()
    {
        _repoPath = Directory.CreateTempSubdirectory("nc-git-tests-").FullName;
        _git = new GitClient(_repoPath);
        Assert.True(_git.Init().Success);
    }

    public void Dispose() => DeleteDirectoryForcefully(_repoPath);

    // git rend certains fichiers de .git/objects en lecture seule sous Windows ;
    // Directory.Delete recursive echoue dessus sans ce nettoyage prealable.
    private static void DeleteDirectoryForcefully(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(path, recursive: true);
    }

    [Fact]
    public void GetVersion_Succeeds()
    {
        var result = GitClient.GetVersion();

        Assert.True(result.Success);
        Assert.Contains("git version", result.StandardOutput);
    }

    [Fact]
    public void Init_CreatesGitDirectory() => Assert.True(Directory.Exists(Path.Combine(_repoPath, ".git")));

    [Fact]
    public void AddAll_StagesNewFile()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");

        Assert.True(_git.AddAll().Success);

        var status = _git.Status();
        Assert.True(status.Success);
        Assert.Contains("A  a.txt", status.StandardOutput);
    }

    [Fact]
    public void Add_StagesOnlySpecifiedFile()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_repoPath, "b.txt"), "b");

        Assert.True(_git.Add("a.txt").Success);

        var status = _git.Status();
        Assert.Contains("A  a.txt", status.StandardOutput);
        Assert.Contains("?? b.txt", status.StandardOutput);
    }

    [Fact]
    public void Commit_SucceedsWithoutLocalGitIdentityConfigured()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();

        var commit = _git.Commit("sync initial");

        Assert.True(commit.Success);
        Assert.Empty(_git.Status().StandardOutput);
    }

    [Fact]
    public void DiffCachedNameStatus_ReportsAddedFile()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();

        var diff = _git.DiffCachedNameStatus();

        Assert.True(diff.Success);
        Assert.Contains("A\ta.txt", diff.StandardOutput);
    }

    [Fact]
    public void DiffCached_ReportsFileContent()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();

        var diff = _git.DiffCached();

        Assert.True(diff.Success);
        Assert.Contains("+contenu", diff.StandardOutput);
    }

    [Fact]
    public void UpdateRefThenReadRef_RoundTripsCommitSha()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();
        _git.Commit("sync initial");
        var commitSha = _git.ReadRef("HEAD").StandardOutput.Trim();

        Assert.True(_git.UpdateRef("refs/nc/synced", commitSha).Success);
        var readBack = _git.ReadRef("refs/nc/synced");

        Assert.True(readBack.Success);
        Assert.Equal(commitSha, readBack.StandardOutput.Trim());
    }

    [Fact]
    public void ReadRef_UnknownRef_Fails() => Assert.False(_git.ReadRef("refs/nc/synced").Success);

    [Fact]
    public void PathExistsInRef_ForFileInRef_Succeeds()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());

        Assert.True(_git.PathExistsInRef("refs/nc/synced", "a.txt"));
    }

    [Fact]
    public void PathExistsInRef_ForFileNotInRef_Fails()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());

        Assert.False(_git.PathExistsInRef("refs/nc/synced", "jamais-vu.txt"));
    }

    [Fact]
    public void PathExistsInRef_ForFileInSubdirectoryGivenWithForwardSlash_Succeeds()
    {
        Directory.CreateDirectory(Path.Combine(_repoPath, "sous-dossier"));
        File.WriteAllText(Path.Combine(_repoPath, "sous-dossier", "a.txt"), "contenu");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());

        Assert.True(_git.PathExistsInRef("refs/nc/synced", "sous-dossier/a.txt"));
    }

    [SkippableFact]
    public void PathExistsInRef_ForFileInSubdirectoryGivenWithBackslash_Succeeds()
    {
        // `\` n'est un separateur de chemin que sous Windows ; sous Linux/macOS c'est un
        // caractere de nom de fichier ordinaire, donc ce test n'a de sens que sur Windows.
        Skip.IfNot(OperatingSystem.IsWindows(), "L'antislash n'est un separateur de chemin que sous Windows.");

        Directory.CreateDirectory(Path.Combine(_repoPath, "sous-dossier"));
        File.WriteAllText(Path.Combine(_repoPath, "sous-dossier", "a.txt"), "contenu");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());

        // Regression : `cat-file -e <ref>:<path>` (syntaxe de revision) rejette les chemins a
        // l'antislash meme quand le fichier existe reellement, contrairement a `checkout`.
        Assert.True(_git.PathExistsInRef("refs/nc/synced", "sous-dossier\\a.txt"));
    }

    [Fact]
    public void CheckoutFromRef_RestoresContentAndIndexFromGivenRef()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "original");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "modifie localement");
        _git.AddAll();

        var result = _git.CheckoutFromRef("refs/nc/synced", "a.txt");

        Assert.True(result.Success);
        Assert.Equal("original", File.ReadAllText(Path.Combine(_repoPath, "a.txt")));
        Assert.Empty(_git.Status().StandardOutput);
    }

    [Fact]
    public void CheckoutFromRef_ForPathNotInRef_FailsWithoutTouchingOtherPaths()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "original");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "modifie localement");
        _git.AddAll();

        var result = _git.CheckoutFromRef("refs/nc/synced", "a.txt", "jamais-vu.txt");

        Assert.False(result.Success);
        Assert.Equal("modifie localement", File.ReadAllText(Path.Combine(_repoPath, "a.txt")));
    }

    [Fact]
    public void Unstage_RemovesFileFromIndexWithoutTouchingWorkingTree()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();

        var result = _git.Unstage("a.txt");

        Assert.True(result.Success);
        Assert.Contains("?? a.txt", _git.Status().StandardOutput);
        Assert.True(File.Exists(Path.Combine(_repoPath, "a.txt")));
    }

    [Fact]
    public void Unstage_ForNeverStagedFile_SucceedsAsNoOp() => Assert.True(_git.Unstage("jamais-ajoute.txt").Success);
}
