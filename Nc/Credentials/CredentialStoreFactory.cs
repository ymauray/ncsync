namespace Nc.Credentials;

/// <summary>
/// Selection de l'implementation ICredentialStore adaptee a la plateforme courante
/// (SPECS.md §5) : DPAPI sur Windows, trousseau sur macOS, Secret Service sur Linux avec
/// repli fichier chiffre si aucun trousseau n'est disponible.
/// </summary>
internal static class CredentialStoreFactory
{
    public static ICredentialStore Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new DpapiCredentialStore();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new KeychainCredentialStore();
        }

        if (OperatingSystem.IsLinux())
        {
            return SecretToolCredentialStore.IsAvailable() ? new SecretToolCredentialStore() : new EncryptedFileCredentialStore();
        }

        throw new PlatformNotSupportedException("Plateforme non prise en charge pour le stockage des identifiants.");
    }
}
