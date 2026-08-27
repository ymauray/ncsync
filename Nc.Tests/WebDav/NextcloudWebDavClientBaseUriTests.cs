using Nc.WebDav;

namespace Nc.Tests.WebDav;

public sealed class NextcloudWebDavClientBaseUriTests
{
    [Fact]
    public void BuildBaseUri_WithoutScheme_DefaultsToHttps() =>
        Assert.Equal(
            "https://cloud.example.ch/remote.php/dav/files/alice/",
            NextcloudWebDavClient.BuildBaseUri("cloud.example.ch", "alice").AbsoluteUri);

    [Fact]
    public void BuildBaseUri_WithExplicitHttpScheme_IsPreserved() =>
        Assert.Equal(
            "http://cloud.example.ch/remote.php/dav/files/alice/",
            NextcloudWebDavClient.BuildBaseUri("http://cloud.example.ch", "alice").AbsoluteUri);

    [Fact]
    public void BuildBaseUri_WithTrailingSlash_DoesNotDuplicateSlash() =>
        Assert.Equal(
            "https://cloud.example.ch/remote.php/dav/files/alice/",
            NextcloudWebDavClient.BuildBaseUri("https://cloud.example.ch/", "alice").AbsoluteUri);

    [Fact]
    public void BuildBaseUri_EscapesUsername() =>
        Assert.Equal(
            "https://cloud.example.ch/remote.php/dav/files/al%20ice/",
            NextcloudWebDavClient.BuildBaseUri("cloud.example.ch", "al ice").AbsoluteUri);

    [Fact]
    public void BuildBasicAuthCredentials_EncodesUsernameColonPasswordAsBase64() =>
        Assert.Equal(
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("alice:s3cret")),
            NextcloudWebDavClient.BuildBasicAuthCredentials("alice", "s3cret"));

    [Fact]
    public void Create_ReturnsUsableClient() => Assert.NotNull(NextcloudWebDavClient.Create("cloud.example.ch", "alice", "s3cret"));
}
