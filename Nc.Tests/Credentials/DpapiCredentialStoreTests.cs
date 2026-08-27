using Nc.Credentials;

namespace Nc.Tests.Credentials;

public sealed class DpapiCredentialStoreTests : IDisposable
{
    private readonly string _key = $"nc-tests-{Guid.NewGuid():N}";
    private ICredentialStore? _store;

    public void Dispose() => _store?.Delete(_key);

    [SkippableFact]
    public void SaveThenTryLoad_RoundTripsSecret()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "DPAPI n'est disponible que sur Windows.");
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _store = new DpapiCredentialStore();
        _store.Save(_key, "s3cret");

        Assert.Equal("s3cret", _store.TryLoad(_key));
    }

    [SkippableFact]
    public void TryLoad_WithoutPriorSave_ReturnsNull()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "DPAPI n'est disponible que sur Windows.");
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _store = new DpapiCredentialStore();

        Assert.Null(_store.TryLoad(_key));
    }
}
