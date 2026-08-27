using System.Text;
using Nc.WebDav;

namespace Nc.Tests.WebDav;

public sealed class NextcloudWebDavClientTests
{
    private static readonly Uri BaseAddress = new("https://cloud.example.ch/remote.php/dav/files/alice/");

    [Fact]
    public async Task PropFindAsync_SendsPropFindMethodWithDepthHeader()
    {
        var (client, handler) = CreateClient(_ => EmptyMultistatusResponse());

        await client.PropFindAsync("dossier/", WebDavDepth.One);

        Assert.Equal("PROPFIND", handler.LastRequest!.Method.Method);
        Assert.Equal("1", handler.LastRequest.Headers.GetValues("Depth").Single());
    }

    [Fact]
    public async Task PropFindAsync_MapsDepthZeroToHeaderValue() => await AssertDepthHeader(WebDavDepth.Zero, "0");

    [Fact]
    public async Task PropFindAsync_MapsDepthOneToHeaderValue() => await AssertDepthHeader(WebDavDepth.One, "1");

    [Fact]
    public async Task PropFindAsync_MapsDepthInfinityToHeaderValue() => await AssertDepthHeader(WebDavDepth.Infinity, "infinity");

    private static async Task AssertDepthHeader(WebDavDepth depth, string expected)
    {
        var (client, handler) = CreateClient(_ => EmptyMultistatusResponse());

        await client.PropFindAsync("dossier/", depth);

        Assert.Equal(expected, handler.LastRequest!.Headers.GetValues("Depth").Single());
    }

    [Fact]
    public async Task PropFindAsync_ParsesResponseBodyIntoEntries()
    {
        const string xml = """
            <?xml version="1.0"?>
            <d:multistatus xmlns:d="DAV:">
              <d:response>
                <d:href>/remote.php/dav/files/alice/a.txt</d:href>
                <d:propstat>
                  <d:prop><d:getetag>"a"</d:getetag><d:resourcetype/></d:prop>
                  <d:status>HTTP/1.1 200 OK</d:status>
                </d:propstat>
              </d:response>
            </d:multistatus>
            """;
        var (client, _) = CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.MultiStatus)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml"),
        });

        var entries = await client.PropFindAsync(".", WebDavDepth.One);

        var entry = Assert.Single(entries);
        Assert.Equal("\"a\"", entry.ETag);
    }

    [Fact]
    public async Task GetAsync_SendsGetToPath()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("contenu"),
        });

        await client.GetAsync("dossier/fichier.txt");

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(new Uri(BaseAddress, "dossier/fichier.txt"), handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task PutAsync_SendsPutWithStreamContent()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Created));
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("contenu"));

        await client.PutAsync("dossier/fichier.txt", content);

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        var sentBytes = await handler.LastRequest.Content!.ReadAsByteArrayAsync();
        Assert.Equal("contenu", Encoding.UTF8.GetString(sentBytes));
    }

    [Fact]
    public async Task DeleteAsync_SendsDelete()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));

        await client.DeleteAsync("dossier/fichier.txt");

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task MoveAsync_SendsMoveWithAbsoluteDestinationAndNoOverwrite()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Created));

        await client.MoveAsync("ancien.txt", "nouveau.txt");

        Assert.Equal("MOVE", handler.LastRequest!.Method.Method);
        Assert.Equal("https://cloud.example.ch/remote.php/dav/files/alice/nouveau.txt", handler.LastRequest.Headers.GetValues("Destination").Single());
        Assert.Equal("F", handler.LastRequest.Headers.GetValues("Overwrite").Single());
    }

    [Fact]
    public async Task MkColAsync_SendsMkcol()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Created));

        await client.MkColAsync("nouveau-dossier/");

        Assert.Equal("MKCOL", handler.LastRequest!.Method.Method);
    }

    private static HttpResponseMessage EmptyMultistatusResponse() => new(System.Net.HttpStatusCode.MultiStatus)
    {
        Content = new StringContent("""<d:multistatus xmlns:d="DAV:"></d:multistatus>""", Encoding.UTF8, "application/xml"),
    };

    private static (NextcloudWebDavClient Client, FakeHttpMessageHandler Handler) CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        return (new NextcloudWebDavClient(httpClient), handler);
    }
}
