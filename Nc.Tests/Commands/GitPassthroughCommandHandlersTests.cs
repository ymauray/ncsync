using Nc.Commands;
using Nc.Git;

namespace Nc.Tests.Commands;

public sealed class GitPassthroughCommandHandlersTests : IDisposable
{
    private readonly string _repoPath = Directory.CreateTempSubdirectory("nc-git-cmd-tests-").FullName;

    public GitPassthroughCommandHandlersTests() => new GitClient(_repoPath).Init();

    public void Dispose() => DeleteDirectoryForcefully(_repoPath);

    [Fact]
    public void Add_StagesSpecifiedFile()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");

        var exitCode = GitPassthroughCommandHandlers.Add(_repoPath, ["a.txt"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("A  a.txt", new GitClient(_repoPath).Status().StandardOutput);
    }

    [Fact]
    public void Add_WithUnknownPath_ReturnsErrorAndPrintsMessage()
    {
        var output = CaptureConsoleError(() => GitPassthroughCommandHandlers.Add(_repoPath, ["inexistant.txt"]));

        Assert.Equal(1, output.ExitCode);
        Assert.NotEmpty(output.Text);
    }

    [Fact]
    public void Status_WithCleanRepo_PrintsCleanWorkspaceMessage()
    {
        var output = CaptureConsoleOut(() => GitPassthroughCommandHandlers.Status(_repoPath));

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("Rien à synchroniser, l'espace de travail est propre." + Environment.NewLine, output.Text);
    }

    [Fact]
    public void Status_WithUntrackedFile_PrintsPorcelainStatus()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");

        var output = CaptureConsoleOut(() => GitPassthroughCommandHandlers.Status(_repoPath));

        Assert.Equal(0, output.ExitCode);
        Assert.Contains("?? a.txt", output.Text);
    }

    [Fact]
    public void Diff_WithStagedFile_PrintsUnifiedDiff()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        new GitClient(_repoPath).AddAll();

        var output = CaptureConsoleOut(() => GitPassthroughCommandHandlers.Diff(_repoPath));

        Assert.Equal(0, output.ExitCode);
        Assert.Contains("+contenu", output.Text);
    }

    [Fact]
    public void Diff_WithNothingStaged_PrintsNothing()
    {
        var output = CaptureConsoleOut(() => GitPassthroughCommandHandlers.Diff(_repoPath));

        Assert.Equal(0, output.ExitCode);
        Assert.Equal(string.Empty, output.Text);
    }

    private static (int ExitCode, string Text) CaptureConsoleOut(Func<int> action)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        int exitCode;
        try
        {
            exitCode = action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        return (exitCode, writer.ToString());
    }

    private static (int ExitCode, string Text) CaptureConsoleError(Func<int> action)
    {
        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        int exitCode;
        try
        {
            exitCode = action();
        }
        finally
        {
            Console.SetError(originalError);
        }
        return (exitCode, writer.ToString());
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
