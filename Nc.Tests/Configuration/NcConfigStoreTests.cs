using Nc.Configuration;

namespace Nc.Tests.Configuration;

public sealed class NcConfigStoreTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-config-tests-").FullName;

    public void Dispose() => Directory.Delete(_workingDirectory, recursive: true);

    [Fact]
    public void Load_WithoutPriorSave_ReturnsEmptyConfig()
    {
        var store = new NcConfigStore(_workingDirectory);

        var config = store.Load();

        Assert.Null(config.Username);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsUsername()
    {
        var store = new NcConfigStore(_workingDirectory);

        store.Save(new NcConfig { Username = "myname" });

        Assert.Equal("myname", store.Load().Username);
    }

    [Fact]
    public void Save_CreatesConfigInsideNcSubdirectory()
    {
        new NcConfigStore(_workingDirectory).Save(new NcConfig { Username = "myname" });

        Assert.True(File.Exists(Path.Combine(_workingDirectory, ".nc", "config")));
    }

    [Fact]
    public void Save_Twice_OverwritesPreviousValue()
    {
        var store = new NcConfigStore(_workingDirectory);

        store.Save(new NcConfig { Username = "first" });
        store.Save(new NcConfig { Username = "second" });

        Assert.Equal("second", store.Load().Username);
    }
}
