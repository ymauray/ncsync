using Nc.Processes;

namespace Nc.Credentials;

/// <summary>
/// Stockage via le trousseau macOS, en shell-out vers `security` (SPECS.md §5).
/// Limite connue : `security add-generic-password -w` recoit le secret en argument de
/// ligne de commande (pas d'entree standard disponible pour -w), donc brievement visible
/// via `ps` pour d'autres utilisateurs locaux de la machine.
/// </summary>
internal sealed class KeychainCredentialStore : ICredentialStore
{
    public void Save(string key, string secret)
    {
        var result = ProcessRunner.Run("security", null, ["add-generic-password", "-U", "-a", "nc", "-s", ServiceName(key), "-w", secret]);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Échec de l'écriture dans le trousseau macOS : {result.StandardError}");
        }
    }

    public string? TryLoad(string key)
    {
        var result = ProcessRunner.Run("security", null, ["find-generic-password", "-a", "nc", "-s", ServiceName(key), "-w"]);
        return result.Success ? result.StandardOutput.TrimEnd('\r', '\n') : null;
    }

    public void Delete(string key) =>
        ProcessRunner.Run("security", null, ["delete-generic-password", "-a", "nc", "-s", ServiceName(key)]);

    private static string ServiceName(string key) => $"nc:{key}";
}
