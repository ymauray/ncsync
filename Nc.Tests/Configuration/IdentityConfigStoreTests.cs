using Nc.Configuration;

namespace Nc.Tests.Configuration;

public sealed class IdentityConfigStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nc-identity-tests-").FullName;
    private string LocalDirectory => Path.Combine(_root, "local");
    private string GlobalDirectory => Path.Combine(_root, "global");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Load_WithoutGlobalOrLocalFile_ReturnsEmptyConfig() =>
        Assert.Null(new IdentityConfigStore(LocalDirectory, GlobalDirectory).Load().Username);

    [Fact]
    public void SaveThenLoad_RoundTripsThroughGlobalDirectory()
    {
        var store = new IdentityConfigStore(LocalDirectory, GlobalDirectory);

        store.Save(new NcConfig { Username = "myname" });

        Assert.Equal("myname", store.Load().Username);
    }

    [Fact]
    public void Save_WritesUnderGlobalDirectory_NotLocal()
    {
        new IdentityConfigStore(LocalDirectory, GlobalDirectory).Save(new NcConfig { Username = "myname" });

        Assert.True(File.Exists(Path.Combine(GlobalDirectory, "config")));
        Assert.False(File.Exists(Path.Combine(LocalDirectory, ".nc", "config")));
    }

    [Fact]
    public void Load_PrefersGlobalOverLocal_WhenBothExist()
    {
        new NcConfigStore(LocalDirectory).Save(new NcConfig { Username = "local-name" });
        var store = new IdentityConfigStore(LocalDirectory, GlobalDirectory);
        store.Save(new NcConfig { Username = "global-name" });

        Assert.Equal("global-name", store.Load().Username);
    }

    [Fact]
    public void Load_FallsBackToLocal_WhenGlobalFileMissing()
    {
        new NcConfigStore(LocalDirectory).Save(new NcConfig { Username = "local-name" });

        Assert.Equal("local-name", new IdentityConfigStore(LocalDirectory, GlobalDirectory).Load().Username);
    }

    [Fact]
    public void Save_WhenGlobalDirectoryCannotBeCreated_FallsBackToLocalConfigAndPrintsMessage()
    {
        // Un fichier occupe deja le chemin qui devrait etre le dossier global :
        // Directory.CreateDirectory echoue de facon deterministe et cross-platform.
        Directory.CreateDirectory(_root);
        File.WriteAllText(GlobalDirectory, "je ne suis pas un dossier");
        var store = new IdentityConfigStore(LocalDirectory, GlobalDirectory);

        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            store.Save(new NcConfig { Username = "myname" });
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal("myname", new NcConfigStore(LocalDirectory).Load().Username);
        Assert.Contains(GlobalDirectory, writer.ToString());
    }
}
