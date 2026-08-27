namespace Nc.Git;

internal readonly record struct GitCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}
