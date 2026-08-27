using Nc.Credentials;

namespace Nc.Tests.Credentials;

public sealed class KeychainCredentialStoreTests : IDisposable
{
    private readonly string _key = $"nc-tests-{Guid.NewGuid():N}";
    private readonly KeychainCredentialStore _store = new();

    public void Dispose()
    {
        if (OperatingSystem.IsMacOS())
        {
            _store.Delete(_key);
        }
    }

    [SkippableFact]
    public void SaveThenTryLoad_RoundTripsSecret()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "Le trousseau macOS n'est disponible que sur macOS.");

        _store.Save(_key, "s3cret");

        Assert.Equal("s3cret", _store.TryLoad(_key));
    }

    [SkippableFact]
    public void TryLoad_WithoutPriorSave_ReturnsNull()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "Le trousseau macOS n'est disponible que sur macOS.");

        Assert.Null(_store.TryLoad(_key));
    }
}
