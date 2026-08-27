using Nc.WebDav;

namespace Nc.Tests.WebDav;

public sealed class WebDavPropFindParserTests
{
    [Fact]
    public void Parse_SingleFileResponse_ReturnsEntryWithHrefAndETag()
    {
        const string xml = """
            <?xml version="1.0"?>
            <d:multistatus xmlns:d="DAV:">
              <d:response>
                <d:href>/remote.php/dav/files/alice/dossier/fichier.txt</d:href>
                <d:propstat>
                  <d:prop>
                    <d:getetag>"abc123"</d:getetag>
                    <d:resourcetype/>
                  </d:prop>
                  <d:status>HTTP/1.1 200 OK</d:status>
                </d:propstat>
              </d:response>
            </d:multistatus>
            """;

        var entries = WebDavPropFindParser.Parse(xml);

        var entry = Assert.Single(entries);
        Assert.Equal("/remote.php/dav/files/alice/dossier/fichier.txt", entry.Href);
        Assert.Equal("\"abc123\"", entry.ETag);
        Assert.False(entry.IsCollection);
    }

    [Fact]
    public void Parse_CollectionResponse_MarksIsCollectionTrue()
    {
        const string xml = """
            <?xml version="1.0"?>
            <d:multistatus xmlns:d="DAV:">
              <d:response>
                <d:href>/remote.php/dav/files/alice/dossier/</d:href>
                <d:propstat>
                  <d:prop>
                    <d:getetag>"dir-etag"</d:getetag>
                    <d:resourcetype><d:collection/></d:resourcetype>
                  </d:prop>
                  <d:status>HTTP/1.1 200 OK</d:status>
                </d:propstat>
              </d:response>
            </d:multistatus>
            """;

        var entry = Assert.Single(WebDavPropFindParser.Parse(xml));

        Assert.True(entry.IsCollection);
    }

    [Fact]
    public void Parse_MultipleResponses_ReturnsAllEntries()
    {
        const string xml = """
            <?xml version="1.0"?>
            <d:multistatus xmlns:d="DAV:">
              <d:response>
                <d:href>/remote.php/dav/files/alice/dossier/</d:href>
                <d:propstat>
                  <d:prop><d:getetag>"dir"</d:getetag><d:resourcetype><d:collection/></d:resourcetype></d:prop>
                  <d:status>HTTP/1.1 200 OK</d:status>
                </d:propstat>
              </d:response>
              <d:response>
                <d:href>/remote.php/dav/files/alice/dossier/a.txt</d:href>
                <d:propstat>
                  <d:prop><d:getetag>"a"</d:getetag><d:resourcetype/></d:prop>
                  <d:status>HTTP/1.1 200 OK</d:status>
                </d:propstat>
              </d:response>
              <d:response>
                <d:href>/remote.php/dav/files/alice/dossier/b.txt</d:href>
                <d:propstat>
                  <d:prop><d:getetag>"b"</d:getetag><d:resourcetype/></d:prop>
                  <d:status>HTTP/1.1 200 OK</d:status>
                </d:propstat>
              </d:response>
            </d:multistatus>
            """;

        var entries = WebDavPropFindParser.Parse(xml);

        Assert.Equal(3, entries.Count);
        Assert.Equal(["\"dir\"", "\"a\"", "\"b\""], entries.Select(e => e.ETag));
    }

    [Fact]
    public void Parse_IgnoresNon200Propstat()
    {
        const string xml = """
            <?xml version="1.0"?>
            <d:multistatus xmlns:d="DAV:">
              <d:response>
                <d:href>/remote.php/dav/files/alice/fichier.txt</d:href>
                <d:propstat>
                  <d:prop><d:getetag>"real-etag"</d:getetag></d:prop>
                  <d:status>HTTP/1.1 200 OK</d:status>
                </d:propstat>
                <d:propstat>
                  <d:prop><d:quota-used-bytes/></d:prop>
                  <d:status>HTTP/1.1 404 Not Found</d:status>
                </d:propstat>
              </d:response>
            </d:multistatus>
            """;

        var entry = Assert.Single(WebDavPropFindParser.Parse(xml));

        Assert.Equal("\"real-etag\"", entry.ETag);
    }

    [Fact]
    public void Parse_NoMatchingResponses_ReturnsEmptyList()
    {
        const string xml = """<d:multistatus xmlns:d="DAV:"></d:multistatus>""";

        Assert.Empty(WebDavPropFindParser.Parse(xml));
    }
}
