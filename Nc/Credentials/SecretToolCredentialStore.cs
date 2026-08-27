using Nc.Processes;

namespace Nc.Credentials;

/// <summary>
/// Stockage via Secret Service (libsecret), en shell-out vers `secret-tool` (SPECS.md §5).
/// Le secret est transmis par entree standard, jamais en argument de ligne de commande.
/// </summary>
internal sealed class SecretToolCredentialStore : ICredentialStore
{
    private const string AvailabilityProbeKey = "__nc-availability-probe__";

    public static bool IsAvailable() =>
        !ProcessRunner.Run("secret-tool", null, ["lookup", "nc-key", AvailabilityProbeKey]).ExecutableNotFound;

    public void Save(string key, string secret)
    {
        var result = ProcessRunner.Run("secret-tool", null, ["store", "--label=nc", "nc-key", key], standardInput: secret);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Échec de l'écriture dans Secret Service : {result.StandardError}");
        }
    }

    public string? TryLoad(string key)
    {
        var result = ProcessRunner.Run("secret-tool", null, ["lookup", "nc-key", key]);
        return result.Success ? result.StandardOutput : null;
    }

    public void Delete(string key) => ProcessRunner.Run("secret-tool", null, ["clear", "nc-key", key]);
}
