using Nc.Sync;

namespace Nc.Tests.Sync;

public sealed class SyncStateStoreTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-syncstate-tests-").FullName;

    public void Dispose() => Directory.Delete(_workingDirectory, recursive: true);

    [Fact]
    public void Load_WithoutPriorSave_ReturnsEmptyState() =>
        Assert.Empty(new SyncStateStore(_workingDirectory).Load().ETagsByPath);

    [Fact]
    public void SaveThenLoad_RoundTripsETagsByPath()
    {
        var store = new SyncStateStore(_workingDirectory);
        var state = new SyncState { ETagsByPath = { ["dossier/a.txt"] = "\"etag-a\"", ["dossier/b.txt"] = "\"etag-b\"" } };

        store.Save(state);

        var loaded = store.Load();
        Assert.Equal("\"etag-a\"", loaded.ETagsByPath["dossier/a.txt"]);
        Assert.Equal("\"etag-b\"", loaded.ETagsByPath["dossier/b.txt"]);
    }

    [Fact]
    public void Save_WritesStateJsonInsideNcSubdirectory()
    {
        new SyncStateStore(_workingDirectory).Save(new SyncState());

        Assert.True(File.Exists(Path.Combine(_workingDirectory, ".nc", "state.json")));
    }

    [Fact]
    public void Save_Twice_OverwritesPreviousETags()
    {
        var store = new SyncStateStore(_workingDirectory);
        store.Save(new SyncState { ETagsByPath = { ["a.txt"] = "\"first\"" } });

        store.Save(new SyncState { ETagsByPath = { ["a.txt"] = "\"second\"" } });

        Assert.Equal("\"second\"", store.Load().ETagsByPath["a.txt"]);
    }
}
