using System.Net;
using System.Text;
using Nc.Sync;
using Nc.Tests.WebDav;
using Nc.WebDav;

namespace Nc.Tests.Sync;

public sealed class NcCloneServiceTests : IDisposable
{
    private const string MultistatusResponse = """
        <?xml version="1.0"?>
        <d:multistatus xmlns:d="DAV:">
          <d:response>
            <d:href>/remote.php/dav/files/alice/dossier/</d:href>
            <d:propstat><d:prop><d:getetag>"dir"</d:getetag><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
          </d:response>
          <d:response>
            <d:href>/remote.php/dav/files/alice/dossier/a.txt</d:href>
            <d:propstat><d:prop><d:getetag>"a-etag"</d:getetag><d:resourcetype/></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
          </d:response>
          <d:response>
            <d:href>/remote.php/dav/files/alice/dossier/sous/</d:href>
            <d:propstat><d:prop><d:getetag>"sub"</d:getetag><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
          </d:response>
          <d:response>
            <d:href>/remote.php/dav/files/alice/dossier/sous/b.txt</d:href>
            <d:propstat><d:prop><d:getetag>"b-etag"</d:getetag><d:resourcetype/></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
          </d:response>
        </d:multistatus>
        """;

    private readonly string _destination = Directory.CreateTempSubdirectory("nc-clone-tests-").FullName;

    public void Dispose() => DeleteDirectoryForcefully(_destination);

    [Fact]
    public async Task CloneAsync_DownloadsFilesRecursivelyWithCorrectContent()
    {
        await CloneAsync();

        Assert.Equal("contenu a", await File.ReadAllTextAsync(Path.Combine(_destination, "a.txt")));
        Assert.Equal("contenu b", await File.ReadAllTextAsync(Path.Combine(_destination, "sous", "b.txt")));
    }

    [Fact]
    public async Task CloneAsync_DoesNotCreateLocalEntryForCollections()
    {
        await CloneAsync();

        Assert.False(File.Exists(Path.Combine(_destination, "dossier")));
    }

    [Fact]
    public async Task CloneAsync_WritesGitignoreExcludingNcDirectory()
    {
        await CloneAsync();

        Assert.Equal(".nc/" + Environment.NewLine, await File.ReadAllTextAsync(Path.Combine(_destination, ".gitignore")));
    }

    [Fact]
    public async Task CloneAsync_InitializesGitRepositoryWithCleanStatus()
    {
        await CloneAsync();

        var git = new Nc.Git.GitClient(_destination);
        Assert.True(Directory.Exists(Path.Combine(_destination, ".git")));
        Assert.Empty(git.Status().StandardOutput);
    }

    [Fact]
    public async Task CloneAsync_AdvancesSyncedRefToHead()
    {
        await CloneAsync();

        var git = new Nc.Git.GitClient(_destination);
        var head = git.ReadRef("HEAD").StandardOutput.Trim();
        var synced = git.ReadRef("refs/nc/synced").StandardOutput.Trim();
        Assert.Equal(head, synced);
    }

    [Fact]
    public async Task CloneAsync_WritesETagsForFilesButNotFolders()
    {
        await CloneAsync();

        var state = new SyncStateStore(_destination).Load();
        Assert.Equal("\"a-etag\"", state.ETagsByPath["a.txt"]);
        Assert.Equal("\"b-etag\"", state.ETagsByPath["sous/b.txt"]);
        Assert.Equal(2, state.ETagsByPath.Count);
    }

    [Fact]
    public async Task CloneAsync_CreatesDestinationDirectoryIfMissing()
    {
        var nestedDestination = Path.Combine(_destination, "nested-target");

        await CloneAsync(nestedDestination);

        Assert.True(File.Exists(Path.Combine(nestedDestination, "a.txt")));
    }

    [Fact]
    public async Task CloneAsync_WhenDestinationIsEmptyExistingDirectory_Succeeds()
    {
        // _destination existe deja (Directory.CreateTempSubdirectory) et est vide : comme
        // `git clone`, cloner dedans doit fonctionner.
        await CloneAsync();

        Assert.True(File.Exists(Path.Combine(_destination, "a.txt")));
    }

    [Fact]
    public async Task CloneAsync_WhenDestinationContainsOnlyNcFolder_Succeeds()
    {
        // Simule un `nc config` prealable dans ce meme dossier (workflow documente) :
        // .nc/ existe deja, ce n'est pas du contenu utilisateur bloquant.
        Directory.CreateDirectory(Path.Combine(_destination, ".nc"));
        File.WriteAllText(Path.Combine(_destination, ".nc", "config"), "{}");

        await CloneAsync();

        Assert.True(File.Exists(Path.Combine(_destination, "a.txt")));
    }

    [Fact]
    public async Task CloneAsync_WhenDestinationHasForeignContent_ThrowsWithoutAnyNetworkCall()
    {
        File.WriteAllText(Path.Combine(_destination, "deja-la.txt"), "contenu existant");
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Aucune requête réseau n'était attendue."));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://cloud.example.ch/remote.php/dav/files/alice/") };
        var webDavClient = new NextcloudWebDavClient(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NcCloneService.CloneAsync(webDavClient, "/dossier", _destination));
    }

    private Task CloneAsync(string? destination = null)
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method.Method == "PROPFIND")
            {
                return new HttpResponseMessage(HttpStatusCode.MultiStatus)
                {
                    Content = new StringContent(MultistatusResponse, Encoding.UTF8, "application/xml"),
                };
            }

            if (request.Method == HttpMethod.Get)
            {
                var body = request.RequestUri!.AbsolutePath switch
                {
                    "/remote.php/dav/files/alice/dossier/a.txt" => "contenu a",
                    "/remote.php/dav/files/alice/dossier/sous/b.txt" => "contenu b",
                    _ => throw new InvalidOperationException($"GET inattendu : {request.RequestUri}"),
                };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            }

            throw new InvalidOperationException($"Requête inattendue : {request.Method} {request.RequestUri}");
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://cloud.example.ch/remote.php/dav/files/alice/") };
        var webDavClient = new NextcloudWebDavClient(httpClient);

        return NcCloneService.CloneAsync(webDavClient, "/dossier", destination ?? _destination);
    }

    // git rend certains fichiers de .git/objects en lecture seule sous Windows ;
    // Directory.Delete recursive echoue dessus sans ce nettoyage prealable (cf. GitClientTests).
    private static void DeleteDirectoryForcefully(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(path, recursive: true);
    }
}
