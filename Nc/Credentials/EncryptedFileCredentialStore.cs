using System.Security.Cryptography;
using System.Text;

namespace Nc.Credentials;

/// <summary>
/// Repli fichier chiffre (AES-256-GCM) utilise quand aucun trousseau OS n'est disponible
/// (SPECS.md §5, typiquement Linux sans Secret Service). La cle de chiffrement est generee
/// une seule fois et conservee localement, avec permissions restreintes sur Unix ; ce n'est
/// pas un coffre-fort (la cle est a cote des donnees), seulement une barriere raisonnable
/// pour un environnement sans meilleure option.
/// </summary>
internal sealed class EncryptedFileCredentialStore : ICredentialStore
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public void Save(string key, string secret)
    {
        Directory.CreateDirectory(CredentialStorePaths.BaseDirectory);

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[secretBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(GetOrCreateKey(), TagSize))
        {
            aes.Encrypt(nonce, secretBytes, ciphertext, tag);
        }

        using var stream = File.Create(PathFor(key));
        stream.Write(nonce);
        stream.Write(tag);
        stream.Write(ciphertext);
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
            var content = File.ReadAllBytes(path);
            var nonce = content.AsSpan(0, NonceSize);
            var tag = content.AsSpan(NonceSize, TagSize);
            var ciphertext = content.AsSpan(NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(GetOrCreateKey(), TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
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

    private static string PathFor(string key) => Path.Combine(CredentialStorePaths.BaseDirectory, $"{key}.enc");

    private static byte[] GetOrCreateKey()
    {
        Directory.CreateDirectory(CredentialStorePaths.BaseDirectory);
        var keyPath = Path.Combine(CredentialStorePaths.BaseDirectory, "credentials.key");
        if (File.Exists(keyPath))
        {
            return File.ReadAllBytes(keyPath);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(keyPath, key);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return key;
    }
}
