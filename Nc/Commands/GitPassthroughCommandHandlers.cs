using Nc.Git;
using Nc.Processes;

namespace Nc.Commands;

/// <summary>
/// Commandes qui ne font que relayer une operation `GitClient` deja implementee (Phase 1),
/// sans logique metier propre (SPECS.md §6 : ROADMAP.md phases 7 a 9).
/// </summary>
internal static class GitPassthroughCommandHandlers
{
    public static int Add(string workingDirectory, string[] specs) =>
        Run(new GitClient(workingDirectory).Add(specs));

    public static int Status(string workingDirectory) =>
        Run(new GitClient(workingDirectory).Status());

    public static int Diff(string workingDirectory) =>
        Run(new GitClient(workingDirectory).DiffCached());

    private static int Run(ProcessResult result)
    {
        if (!result.Success)
        {
            Console.Error.WriteLine(result.StandardError);
            return 1;
        }

        Console.Write(result.StandardOutput);
        return 0;
    }
}
