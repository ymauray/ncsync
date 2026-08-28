using Nc.WebDav;

namespace Nc.Tests.WebDav;

public sealed class WebDavPathBuilderTests
{
    [Fact]
    public void Combine_JoinsRemoteAndRelativePathWithSlash()
    {
        Assert.Equal("dossier/a.txt", WebDavPathBuilder.Combine("/dossier", "a.txt"));
    }

    [Fact]
    public void Combine_WithNestedRelativePath_PreservesSubfolders()
    {
        Assert.Equal("dossier/sous/b.txt", WebDavPathBuilder.Combine("/dossier", "sous/b.txt"));
    }

    [Fact]
    public void Combine_WithEmptyRemotePath_UsesRelativePathOnly()
    {
        Assert.Equal("a.txt", WebDavPathBuilder.Combine("", "a.txt"));
    }

    [Fact]
    public void Combine_EscapesEachSegmentIndividually()
    {
        Assert.Equal("mon%20dossier/fichier%20final.txt", WebDavPathBuilder.Combine("/mon dossier", "fichier final.txt"));
    }
}
