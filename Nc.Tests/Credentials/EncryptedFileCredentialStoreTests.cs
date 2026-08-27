using Nc.Credentials;

namespace Nc.Tests.Credentials;

public sealed class EncryptedFileCredentialStoreTests : IDisposable
{
    private readonly EncryptedFileCredentialStore _store = new();
    private readonly string _key = $"nc-tests-{Guid.NewGuid():N}";

    public void Dispose() => _store.Delete(_key);

    [Fact]
    public void TryLoad_WithoutPriorSave_ReturnsNull() => Assert.Null(_store.TryLoad(_key));

    [Fact]
    public void SaveThenTryLoad_RoundTripsSecret()
    {
        _store.Save(_key, "s3cret");

        Assert.Equal("s3cret", _store.TryLoad(_key));
    }

    [Fact]
    public void Save_Twice_OverwritesPreviousSecret()
    {
        _store.Save(_key, "first");
        _store.Save(_key, "second");

        Assert.Equal("second", _store.TryLoad(_key));
    }

    [Fact]
    public void Delete_RemovesSecret()
    {
        _store.Save(_key, "s3cret");

        _store.Delete(_key);

        Assert.Null(_store.TryLoad(_key));
    }

    [Fact]
    public void Delete_WithoutPriorSave_DoesNotThrow() => _store.Delete(_key);
}
