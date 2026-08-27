using Nc.Credentials;

namespace Nc.Tests.Credentials;

public sealed class SecretToolCredentialStoreTests : IDisposable
{
    private readonly string _key = $"nc-tests-{Guid.NewGuid():N}";
    private readonly SecretToolCredentialStore _store = new();

    private static bool IsUsable => OperatingSystem.IsLinux() && SecretToolCredentialStore.IsAvailable();

    public void Dispose()
    {
        if (IsUsable)
        {
            _store.Delete(_key);
        }
    }

    [SkippableFact]
    public void SaveThenTryLoad_RoundTripsSecret()
    {
        Skip.IfNot(IsUsable, "secret-tool (Secret Service) n'est disponible que sur Linux, avec un trousseau actif.");

        _store.Save(_key, "s3cret");

        Assert.Equal("s3cret", _store.TryLoad(_key));
    }

    [SkippableFact]
    public void TryLoad_WithoutPriorSave_ReturnsNull()
    {
        Skip.IfNot(IsUsable, "secret-tool (Secret Service) n'est disponible que sur Linux, avec un trousseau actif.");

        Assert.Null(_store.TryLoad(_key));
    }
}
