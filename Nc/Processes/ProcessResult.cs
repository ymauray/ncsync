namespace Nc.Processes;

internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool ExecutableNotFound)
{
    public bool Success => ExitCode == 0 && !ExecutableNotFound;
}
