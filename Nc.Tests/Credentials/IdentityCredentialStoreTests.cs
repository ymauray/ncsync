using Nc.Credentials;

namespace Nc.Tests.Credentials;

public sealed class IdentityCredentialStoreTests
{
    private const string LocalDirectory = "/dossier-local";

    [Fact]
    public void TryLoad_WithNothingStored_ReturnsNull()
    {
        var store = new IdentityCredentialStore(new FakeCredentialStore(), LocalDirectory);

        Assert.Null(store.TryLoad());
    }

    [Fact]
    public void SaveThenTryLoad_RoundTripsThroughGlobalKey()
    {
        var backingStore = new FakeCredentialStore();
        var store = new IdentityCredentialStore(backingStore, LocalDirectory);

        store.Save("s3cret");

        Assert.Equal("s3cret", store.TryLoad());
        Assert.Equal("s3cret", backingStore.TryLoad(CredentialKey.Global));
    }

    [Fact]
    public void TryLoad_PrefersGlobalOverLocal_WhenBothExist()
    {
        var backingStore = new FakeCredentialStore();
        backingStore.Save(CredentialKey.ForPath(LocalDirectory), "local-secret");
        var store = new IdentityCredentialStore(backingStore, LocalDirectory);
        store.Save("global-secret");

        Assert.Equal("global-secret", store.TryLoad());
    }

    [Fact]
    public void TryLoad_FallsBackToLocal_WhenGlobalMissing()
    {
        var backingStore = new FakeCredentialStore();
        backingStore.Save(CredentialKey.ForPath(LocalDirectory), "local-secret");
        var store = new IdentityCredentialStore(backingStore, LocalDirectory);

        Assert.Equal("local-secret", store.TryLoad());
    }

    [Fact]
    public void Save_WhenGlobalSaveFails_FallsBackToLocalKeyAndPrintsMessage()
    {
        var backingStore = new FakeCredentialStore { FailOnSaveKey = CredentialKey.Global };
        var store = new IdentityCredentialStore(backingStore, LocalDirectory);

        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            store.Save("s3cret");
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal("s3cret", backingStore.TryLoad(CredentialKey.ForPath(LocalDirectory)));
        Assert.Null(backingStore.TryLoad(CredentialKey.Global));
        Assert.Contains("trousseau global", writer.ToString());
    }

    [Fact]
    public void CustomGlobalKey_IsUsedInsteadOfDefaultConstant()
    {
        var backingStore = new FakeCredentialStore();
        var store = new IdentityCredentialStore(backingStore, LocalDirectory, "custom-global-key");

        store.Save("s3cret");

        Assert.Equal("s3cret", backingStore.TryLoad("custom-global-key"));
        Assert.Null(backingStore.TryLoad(CredentialKey.Global));
    }
}
