using Nc.WebDav;

namespace Nc.Tests.WebDav;

public sealed class WebDavHrefResolverTests
{
    private const string RequestedAbsolutePath = "/remote.php/dav/files/alice/dossier";

    [Fact]
    public void ToRelativePath_ForRequestedFolderItself_ReturnsEmptyString() =>
        Assert.Equal(string.Empty, WebDavHrefResolver.ToRelativePath(RequestedAbsolutePath, "/remote.php/dav/files/alice/dossier/"));

    [Fact]
    public void ToRelativePath_ForDirectChild_ReturnsFileName() =>
        Assert.Equal("fichier.txt", WebDavHrefResolver.ToRelativePath(RequestedAbsolutePath, "/remote.php/dav/files/alice/dossier/fichier.txt"));

    [Fact]
    public void ToRelativePath_ForNestedChild_ReturnsRelativeSubPath() =>
        Assert.Equal("sous-dossier/fichier.txt", WebDavHrefResolver.ToRelativePath(RequestedAbsolutePath, "/remote.php/dav/files/alice/dossier/sous-dossier/fichier.txt"));

    [Fact]
    public void ToRelativePath_ForNestedCollection_TrimsTrailingSlash() =>
        Assert.Equal("sous-dossier", WebDavHrefResolver.ToRelativePath(RequestedAbsolutePath, "/remote.php/dav/files/alice/dossier/sous-dossier/"));

    [Fact]
    public void ToRelativePath_DecodesPercentEncodedCharacters() =>
        Assert.Equal("fichier avec espace.txt", WebDavHrefResolver.ToRelativePath(RequestedAbsolutePath, "/remote.php/dav/files/alice/dossier/fichier%20avec%20espace.txt"));

    [Fact]
    public void ToRelativePath_OutsideRequestedFolder_ReturnsEmptyString() =>
        Assert.Equal(string.Empty, WebDavHrefResolver.ToRelativePath(RequestedAbsolutePath, "/remote.php/dav/files/alice/autre-dossier/fichier.txt"));
}
