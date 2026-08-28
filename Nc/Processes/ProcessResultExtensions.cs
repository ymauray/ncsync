namespace Nc.Processes;

internal static class ProcessResultExtensions
{
    public static void EnsureSuccess(this ProcessResult result, string step)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException($"Échec de « {step} » : {result.StandardError}");
        }
    }
}
