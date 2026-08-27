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
}
