using Nc.Git;

namespace Nc.Tests.Git;

public sealed class GitDiffNameStatusParserTests
{
    [Fact]
    public void Parse_WithEmptyOutput_ReturnsEmptyList()
    {
        Assert.Empty(GitDiffNameStatusParser.Parse(""));
    }

    [Fact]
    public void Parse_AddedLine_ReturnsAddedEntry()
    {
        var entries = GitDiffNameStatusParser.Parse("A\ta.txt\n");

        var entry = Assert.Single(entries);
        Assert.Equal(GitChangeType.Added, entry.ChangeType);
        Assert.Equal("a.txt", entry.Path);
        Assert.Null(entry.OldPath);
    }

    [Fact]
    public void Parse_ModifiedLine_ReturnsModifiedEntry()
    {
        var entries = GitDiffNameStatusParser.Parse("M\ta.txt\n");

        Assert.Equal(GitChangeType.Modified, Assert.Single(entries).ChangeType);
    }

    [Fact]
    public void Parse_DeletedLine_ReturnsDeletedEntry()
    {
        var entries = GitDiffNameStatusParser.Parse("D\ta.txt\n");

        Assert.Equal(GitChangeType.Deleted, Assert.Single(entries).ChangeType);
    }

    [Fact]
    public void Parse_RenamedLine_ReturnsRenamedEntryWithOldAndNewPath()
    {
        var entries = GitDiffNameStatusParser.Parse("R100\tancien.txt\tnouveau.txt\n");

        var entry = Assert.Single(entries);
        Assert.Equal(GitChangeType.Renamed, entry.ChangeType);
        Assert.Equal("nouveau.txt", entry.Path);
        Assert.Equal("ancien.txt", entry.OldPath);
    }

    [Fact]
    public void Parse_MultipleLines_ReturnsOneEntryPerLine()
    {
        var entries = GitDiffNameStatusParser.Parse("A\ta.txt\nD\tb.txt\nM\tc.txt\n");

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void Parse_UnsupportedStatus_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => GitDiffNameStatusParser.Parse("C100\ta.txt\tb.txt\n"));
    }
}
