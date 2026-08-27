namespace Nc.Tests.WebDav;

/// <summary>
/// Handler HTTP factice : capture la derniere requete envoyee et renvoie une reponse
/// preconfiguree, pour tester NextcloudWebDavClient sans instance Nextcloud reelle.
/// </summary>
internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(respond(request));
    }
}
