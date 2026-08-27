using System.ComponentModel;
using System.Diagnostics;

namespace Nc.Git;

/// <summary>
/// Wrapper shell-out autour du binaire `git` (decision actee, voir SPECS.md §2).
/// Chaque methode ne fait qu'invoquer une commande git et exposer son resultat brut ;
/// aucune interpretation metier n'est faite ici.
/// </summary>
internal sealed class GitClient(string workingDirectory)
{
    private static readonly string[] CommitIdentityArgs = ["-c", "user.name=nc", "-c", "user.email=nc@localhost"];

    public static GitCommandResult GetVersion() => RunGit(workingDirectory: null, "--version");

    public GitCommandResult Init() => Run("init");

    public GitCommandResult AddAll() => Run("add", "-A");

    public GitCommandResult Add(params string[] specs) => Run(["add", .. specs]);

    public GitCommandResult Status() => Run("status", "--porcelain");

    public GitCommandResult DiffCachedNameStatus() => Run("diff", "--cached", "--name-status", "-M");

    public GitCommandResult DiffCached() => Run("diff", "--cached");

    // -c user.name/user.email explicites : les commits de nc sont des points de synchronisation
    // techniques, pas des contributions attribuables a une identite git locale eventuellement absente.
    public GitCommandResult Commit(string message) => Run([.. CommitIdentityArgs, "commit", "-m", message]);

    public GitCommandResult UpdateRef(string refName, string commitSha) => Run("update-ref", refName, commitSha);

    public GitCommandResult ReadRef(string refName) => Run("rev-parse", "--verify", refName);

    private GitCommandResult Run(params string[] arguments) => RunGit(workingDirectory, arguments);

    private static GitCommandResult RunGit(string? workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Impossible de démarrer le processus git.");

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            return new GitCommandResult(process.ExitCode, standardOutputTask.GetAwaiter().GetResult(), standardErrorTask.GetAwaiter().GetResult());
        }
        catch (Win32Exception ex)
        {
            return new GitCommandResult(-1, string.Empty, $"git introuvable dans le PATH : {ex.Message}");
        }
    }
}
