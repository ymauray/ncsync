using Nc.Storage;

namespace Nc.Tests.Storage;

public sealed class JsonFileStoreTests : IDisposable
{
    private sealed record Sample
    {
        public string? Value { get; init; }
    }

    private readonly string _directory = Directory.CreateTempSubdirectory("nc-jsonstore-tests-").FullName;
    private string FilePath => Path.Combine(_directory, "nested", "data.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Load_WithoutExistingFile_ReturnsNewInstance() => Assert.Null(JsonFileStore.Load<Sample>(FilePath).Value);

    [Fact]
    public void SaveThenLoad_RoundTripsValue()
    {
        JsonFileStore.Save(FilePath, new Sample { Value = "hello" });

        Assert.Equal("hello", JsonFileStore.Load<Sample>(FilePath).Value);
    }

    [Fact]
    public void Save_CreatesMissingParentDirectories()
    {
        JsonFileStore.Save(FilePath, new Sample { Value = "hello" });

        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void Save_Twice_OverwritesPreviousValue()
    {
        JsonFileStore.Save(FilePath, new Sample { Value = "first" });
        JsonFileStore.Save(FilePath, new Sample { Value = "second" });

        Assert.Equal("second", JsonFileStore.Load<Sample>(FilePath).Value);
    }
}
