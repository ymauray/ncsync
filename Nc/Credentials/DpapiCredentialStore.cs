using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Nc.Credentials;

/// <summary>
/// Stockage via DPAPI (Windows), scope utilisateur courant (SPECS.md §5).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class DpapiCredentialStore : ICredentialStore
{
    public void Save(string key, string secret)
    {
        Directory.CreateDirectory(CredentialStorePaths.BaseDirectory);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PathFor(key), protectedBytes);
    }

    public string? TryLoad(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void Delete(string key)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string PathFor(string key) => Path.Combine(CredentialStorePaths.BaseDirectory, $"{key}.dpapi");
}
