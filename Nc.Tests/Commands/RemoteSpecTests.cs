using Nc.Commands;

namespace Nc.Tests.Commands;

public sealed class RemoteSpecTests
{
    [Fact]
    public void Parse_SplitsServerAndPathOnFirstColon()
    {
        var remote = RemoteSpec.Parse("mon-serveur-nextcloud.ch:/Chemin/vers/dossier");

        Assert.Equal("mon-serveur-nextcloud.ch", remote.Server);
        Assert.Equal("/Chemin/vers/dossier", remote.Path);
    }

    [Fact]
    public void Parse_WithoutColon_Throws() =>
        Assert.Throws<FormatException>(() => RemoteSpec.Parse("mon-serveur-nextcloud.ch"));

    [Fact]
    public void Parse_WithEmptyServer_Throws() =>
        Assert.Throws<FormatException>(() => RemoteSpec.Parse(":/chemin"));

    [Fact]
    public void Parse_WithEmptyPath_ReturnsEmptyPath()
    {
        var remote = RemoteSpec.Parse("mon-serveur-nextcloud.ch:");

        Assert.Equal(string.Empty, remote.Path);
    }
}
