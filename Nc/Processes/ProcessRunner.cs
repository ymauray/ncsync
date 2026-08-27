using System.ComponentModel;
using System.Diagnostics;

namespace Nc.Processes;

/// <summary>
/// Invocation generique d'un executable externe (git, security, secret-tool...) avec
/// capture de stdout/stderr et distinction entre "executable introuvable" et "commande
/// executee mais en echec", utile pour detecter la disponibilite d'un outil optionnel.
/// </summary>
internal static class ProcessRunner
{
    public static ProcessResult Run(string fileName, string? workingDirectory, string[] arguments, string? standardInput = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
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
                ?? throw new InvalidOperationException($"Impossible de démarrer le processus {fileName}.");

            if (standardInput is not null)
            {
                process.StandardInput.Write(standardInput);
                process.StandardInput.Close();
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            return new ProcessResult(
                process.ExitCode,
                standardOutputTask.GetAwaiter().GetResult(),
                standardErrorTask.GetAwaiter().GetResult(),
                ExecutableNotFound: false);
        }
        catch (Win32Exception ex)
        {
            return new ProcessResult(-1, string.Empty, $"{fileName} introuvable dans le PATH : {ex.Message}", ExecutableNotFound: true);
        }
    }
}
