using Nc.Credentials;

namespace Nc.Tests.Credentials;

public sealed class CredentialKeyTests
{
    [Fact]
    public void ForPath_SamePath_ReturnsSameKey()
    {
        var tempDir = Directory.CreateTempSubdirectory("nc-credkey-tests-").FullName;

        Assert.Equal(CredentialKey.ForPath(tempDir), CredentialKey.ForPath(tempDir));

        Directory.Delete(tempDir);
    }

    [Fact]
    public void ForPath_DifferentPaths_ReturnDifferentKeys()
    {
        var a = Directory.CreateTempSubdirectory("nc-credkey-tests-a-").FullName;
        var b = Directory.CreateTempSubdirectory("nc-credkey-tests-b-").FullName;

        Assert.NotEqual(CredentialKey.ForPath(a), CredentialKey.ForPath(b));

        Directory.Delete(a);
        Directory.Delete(b);
    }

    [Fact]
    public void ForPath_ReturnsLowercaseHex()
    {
        var tempDir = Directory.CreateTempSubdirectory("nc-credkey-tests-").FullName;

        var key = CredentialKey.ForPath(tempDir);

        Assert.Equal(64, key.Length);
        Assert.Matches("^[0-9a-f]{64}$", key);

        Directory.Delete(tempDir);
    }
}
