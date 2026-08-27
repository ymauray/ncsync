using System.Net.Http.Headers;
using System.Text;

namespace Nc.WebDav;

/// <summary>
/// Client WebDAV natif pour l'API de fichiers Nextcloud (decision actee, voir SPECS.md §3) :
/// `HttpClient` + verbes WebDAV construits a la main, pas de librairie WebDAV tierce.
/// Le constructeur accepte un `HttpClient` deja configure pour rester testable avec un
/// `HttpMessageHandler` factice, sans instance Nextcloud reelle.
/// </summary>
internal sealed class NextcloudWebDavClient(HttpClient httpClient)
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");
    private static readonly HttpMethod MkColMethod = new("MKCOL");
    private static readonly HttpMethod MoveMethod = new("MOVE");

    private const string PropFindRequestBody = """
        <?xml version="1.0" encoding="utf-8" ?>
        <d:propfind xmlns:d="DAV:">
          <d:prop>
            <d:getetag/>
            <d:resourcetype/>
          </d:prop>
        </d:propfind>
        """;

    public Uri BaseAddress => httpClient.BaseAddress!;

    public static NextcloudWebDavClient Create(string serverUrl, string username, string password)
    {
        var httpClient = new HttpClient { BaseAddress = BuildBaseUri(serverUrl, username) };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BuildBasicAuthCredentials(username, password));
        return new NextcloudWebDavClient(httpClient);
    }

    internal static string BuildBasicAuthCredentials(string username, string password) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

    internal static Uri BuildBaseUri(string serverUrl, string username)
    {
        var normalized = serverUrl.TrimEnd('/');
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"https://{normalized}";
        }

        return new Uri($"{normalized}/remote.php/dav/files/{Uri.EscapeDataString(username)}/");
    }

    public async Task<IReadOnlyList<WebDavEntry>> PropFindAsync(string path, WebDavDepth depth, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(PropFindMethod, path)
        {
            Content = new StringContent(PropFindRequestBody, Encoding.UTF8, "application/xml"),
        };
        request.Headers.Add("Depth", DepthHeaderValue(depth));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        return WebDavPropFindParser.Parse(xml);
    }

    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken = default) =>
        httpClient.GetAsync(path, cancellationToken);

    public Task<HttpResponseMessage> PutAsync(string path, Stream content, CancellationToken cancellationToken = default) =>
        httpClient.PutAsync(path, new StreamContent(content), cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        httpClient.DeleteAsync(path, cancellationToken);

    public Task<HttpResponseMessage> MoveAsync(string fromPath, string toPath, CancellationToken cancellationToken = default)
    {
        var destination = new Uri(httpClient.BaseAddress!, toPath);
        var request = new HttpRequestMessage(MoveMethod, fromPath);
        request.Headers.Add("Destination", destination.AbsoluteUri);
        request.Headers.Add("Overwrite", "F");
        return httpClient.SendAsync(request, cancellationToken);
    }

    public Task<HttpResponseMessage> MkColAsync(string path, CancellationToken cancellationToken = default) =>
        httpClient.SendAsync(new HttpRequestMessage(MkColMethod, path), cancellationToken);

    private static string DepthHeaderValue(WebDavDepth depth) => depth switch
    {
        WebDavDepth.Zero => "0",
        WebDavDepth.One => "1",
        WebDavDepth.Infinity => "infinity",
        _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, null),
    };
}
