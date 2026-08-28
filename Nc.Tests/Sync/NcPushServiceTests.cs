using System.Net;
using System.Text;
using Nc.Git;
using Nc.Sync;
using Nc.Tests.WebDav;
using Nc.WebDav;

namespace Nc.Tests.Sync;

public sealed class NcPushServiceTests : IDisposable
{
    private const string BaseAddress = "https://cloud.example.ch/remote.php/dav/files/alice/";
    private const string RemotePath = "/dossier";

    private readonly string _repoPath = Directory.CreateTempSubdirectory("nc-push-tests-").FullName;
    private readonly GitClient _git;

    public NcPushServiceTests()
    {
        _git = new GitClient(_repoPath);
        _git.Init();

        // Simule l'etat laisse par `nc clone` : .gitignore excluant .nc/ (sinon state.json lui-
        // meme serait staged par AddAll et pousse comme un fichier ordinaire), un commit initial
        // synchronise, refs/nc/synced dessus, et un etat de sync connaissant deja l'ETag de ce fichier.
        File.WriteAllText(Path.Combine(_repoPath, ".gitignore"), ".nc/" + Environment.NewLine);
        File.WriteAllText(Path.Combine(_repoPath, "existant.txt"), "deja synchronisé");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());
        new SyncStateStore(_repoPath).Save(new SyncState
        {
            ETagsByPath = new Dictionary<string, string> { ["existant.txt"] = "\"existant-etag\"" },
        });
    }

    public void Dispose() => DeleteDirectoryForcefully(_repoPath);

    [Fact]
    public async Task PushAsync_WithNoStagedChanges_ReturnsZeroWithoutAnyNetworkCall()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Aucune requête réseau n'était attendue."));

        var pushedCount = await PushAsync(handler);

        Assert.Equal(0, pushedCount);
    }

    [Fact]
    public async Task PushAsync_WithAddedFile_UploadsFileAndRecordsETag()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();

        var putPaths = new List<string>();
        var pushedCount = await PushAsync(new FakeHttpMessageHandler(request => Respond(request, putPaths)));

        Assert.Equal(1, pushedCount);
        Assert.Contains("/remote.php/dav/files/alice/dossier/a.txt", putPaths);

        var state = new SyncStateStore(_repoPath).Load();
        Assert.Equal("\"a.txt-new-etag\"", state.ETagsByPath["a.txt"]);
    }

    [Fact]
    public async Task PushAsync_WithAddedFile_CommitsAndAdvancesSyncedRef()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();

        await PushAsync(new FakeHttpMessageHandler(request => Respond(request, [])));

        Assert.Empty(_git.Status().StandardOutput);
        var head = _git.ReadRef("HEAD").StandardOutput.Trim();
        var synced = _git.ReadRef("refs/nc/synced").StandardOutput.Trim();
        Assert.Equal(head, synced);
    }

    [Fact]
    public async Task PushAsync_WithModifiedFile_UpdatesETagInState()
    {
        File.WriteAllText(Path.Combine(_repoPath, "existant.txt"), "contenu modifié");
        _git.AddAll();

        var pushedCount = await PushAsync(new FakeHttpMessageHandler(request => Respond(request, [])));

        Assert.Equal(1, pushedCount);
        var state = new SyncStateStore(_repoPath).Load();
        Assert.Equal("\"existant.txt-new-etag\"", state.ETagsByPath["existant.txt"]);
    }

    [Fact]
    public async Task PushAsync_WithDeletedFile_SendsDeleteAndRemovesETagFromState()
    {
        File.Delete(Path.Combine(_repoPath, "existant.txt"));
        _git.AddAll();

        var deletePaths = new List<string>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                deletePaths.Add(request.RequestUri!.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            throw new InvalidOperationException($"Requête inattendue : {request.Method} {request.RequestUri}");
        });

        var pushedCount = await PushAsync(handler);

        Assert.Equal(1, pushedCount);
        Assert.Contains("/remote.php/dav/files/alice/dossier/existant.txt", deletePaths);
        var state = new SyncStateStore(_repoPath).Load();
        Assert.False(state.ETagsByPath.ContainsKey("existant.txt"));
    }

    [Fact]
    public async Task PushAsync_WithRenamedFile_ThrowsWithoutAnyNetworkCall()
    {
        File.Move(Path.Combine(_repoPath, "existant.txt"), Path.Combine(_repoPath, "renomme.txt"));
        _git.AddAll();

        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Aucune requête réseau n'était attendue."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => PushAsync(handler));
    }

    [Fact]
    public async Task PushAsync_WhenPutFails_DoesNotAdvanceRefOrStateAndLeavesChangesStaged()
    {
        File.WriteAllText(Path.Combine(_repoPath, "b.txt"), "nouveau");
        _git.AddAll();
        var initialSynced = _git.ReadRef("refs/nc/synced").StandardOutput.Trim();

        var handler = new FakeHttpMessageHandler(request => request.Method == HttpMethod.Put
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : throw new InvalidOperationException($"Requête inattendue : {request.Method} {request.RequestUri}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => PushAsync(handler));

        Assert.Equal(initialSynced, _git.ReadRef("refs/nc/synced").StandardOutput.Trim());
        var state = new SyncStateStore(_repoPath).Load();
        Assert.False(state.ETagsByPath.ContainsKey("b.txt"));
        Assert.Contains("b.txt", _git.Status().StandardOutput);
    }

    [Fact]
    public async Task PushAsync_WhenServerReturnsNoETagAfterPut_ThrowsAndDoesNotAdvanceState()
    {
        File.WriteAllText(Path.Combine(_repoPath, "b.txt"), "nouveau");
        _git.AddAll();

        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (request.Method.Method == "PROPFIND")
            {
                const string emptyMultistatus = """<?xml version="1.0"?><d:multistatus xmlns:d="DAV:"></d:multistatus>""";
                return new HttpResponseMessage(HttpStatusCode.MultiStatus)
                {
                    Content = new StringContent(emptyMultistatus, Encoding.UTF8, "application/xml"),
                };
            }

            throw new InvalidOperationException($"Requête inattendue : {request.Method} {request.RequestUri}");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => PushAsync(handler));

        var state = new SyncStateStore(_repoPath).Load();
        Assert.False(state.ETagsByPath.ContainsKey("b.txt"));
    }

    private Task<int> PushAsync(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        var webDavClient = new NextcloudWebDavClient(httpClient);
        return NcPushService.PushAsync(_git, webDavClient, _repoPath, RemotePath, new SyncStateStore(_repoPath));
    }

    // Repond a un PUT (accepte, enregistre le chemin) puis au PROPFIND cible qui suit
    // (renvoie un ETag derive du nom de fichier, pour verifier qu'il finit bien dans state.json).
    private static HttpResponseMessage Respond(HttpRequestMessage request, List<string> putPaths)
    {
        if (request.Method == HttpMethod.Put)
        {
            putPaths.Add(request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }

        if (request.Method.Method == "PROPFIND")
        {
            var fileName = request.RequestUri!.Segments[^1];
            var eTag = $"\"{fileName}-new-etag\"";
            var body = $"""
                <?xml version="1.0"?>
                <d:multistatus xmlns:d="DAV:">
                  <d:response>
                    <d:href>{request.RequestUri.AbsolutePath}</d:href>
                    <d:propstat><d:prop><d:getetag>{eTag}</d:getetag><d:resourcetype/></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
                  </d:response>
                </d:multistatus>
                """;
            return new HttpResponseMessage(HttpStatusCode.MultiStatus)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            };
        }

        throw new InvalidOperationException($"Requête inattendue : {request.Method} {request.RequestUri}");
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
