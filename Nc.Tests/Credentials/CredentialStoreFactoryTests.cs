using Nc.Credentials;

namespace Nc.Tests.Credentials;

public sealed class CredentialStoreFactoryTests
{
    [Fact]
    public void Create_ReturnsImplementationMatchingCurrentPlatform()
    {
        var store = CredentialStoreFactory.Create();

        if (OperatingSystem.IsWindows())
        {
            Assert.IsType<DpapiCredentialStore>(store);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.IsType<KeychainCredentialStore>(store);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.True(store is SecretToolCredentialStore or EncryptedFileCredentialStore);
        }
    }
}
