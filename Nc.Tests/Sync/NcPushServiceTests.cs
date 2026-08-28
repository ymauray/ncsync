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
    private const string RemoteFilePrefix = "/remote.php/dav/files/alice/dossier/";

    private readonly string _repoPath = Directory.CreateTempSubdirectory("nc-push-tests-").FullName;
    private readonly GitClient _git;

    // Etat du "serveur" simule par FakeHttpMessageHandler : ETag actuel par chemin relatif.
    // Initialise au meme etat que .nc/state.json (voir constructeur), puis mute au fil des
    // PUT/DELETE geres par Respond(), pour que PROPFIND reflete toujours le dernier etat "envoye".
    private readonly Dictionary<string, string> _remoteETagsByPath = new()
    {
        ["existant.txt"] = "\"existant-etag\"",
        ["autre.txt"] = "\"autre-etag\"",
    };

    public NcPushServiceTests()
    {
        _git = new GitClient(_repoPath);
        _git.Init();

        // Simule l'etat laisse par `nc clone` : .gitignore excluant .nc/ (sinon state.json lui-
        // meme serait staged par AddAll et pousse comme un fichier ordinaire), un commit initial
        // synchronise, refs/nc/synced dessus, et un etat de sync connaissant deja l'ETag de ces
        // deux fichiers (identique au "serveur" simule dans _remoteETagsByPath au depart).
        File.WriteAllText(Path.Combine(_repoPath, ".gitignore"), ".nc/" + Environment.NewLine);
        File.WriteAllText(Path.Combine(_repoPath, "existant.txt"), "deja synchronisé");
        File.WriteAllText(Path.Combine(_repoPath, "autre.txt"), "aussi deja synchronisé");
        _git.AddAll();
        _git.Commit("sync initial");
        _git.UpdateRef("refs/nc/synced", _git.ReadRef("HEAD").StandardOutput.Trim());
        new SyncStateStore(_repoPath).Save(new SyncState { ETagsByPath = new Dictionary<string, string>(_remoteETagsByPath) });
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

        var pushedCount = await PushAsync(new FakeHttpMessageHandler(request => Respond(request, _remoteETagsByPath)));

        Assert.Equal(1, pushedCount);
        var state = new SyncStateStore(_repoPath).Load();
        Assert.Equal("\"a.txt-new-etag\"", state.ETagsByPath["a.txt"]);
    }

    [Fact]
    public async Task PushAsync_WithAddedFile_CommitsAndAdvancesSyncedRef()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "contenu");
        _git.AddAll();

        await PushAsync(new FakeHttpMessageHandler(request => Respond(request, _remoteETagsByPath)));

        Assert.Empty(_git.Status().StandardOutput);
        var head = _git.ReadRef("HEAD").StandardOutput.Trim();
        var synced = _git.ReadRef("refs/nc/synced").StandardOutput.Trim();
        Assert.Equal(head, synced);
    }

    [Fact]
    public async Task PushAsync_WithModifiedFile_ChecksNoConflictThenUpdatesETagInState()
    {
        File.WriteAllText(Path.Combine(_repoPath, "existant.txt"), "contenu modifié");
        _git.AddAll();

        // Le "serveur" est toujours a l'ETag connu (aucune modification distante) : pas de
        // conflit attendu, la verification prealable doit laisser passer le PUT qui suit.
        var pushedCount = await PushAsync(new FakeHttpMessageHandler(request => Respond(request, _remoteETagsByPath)));

        Assert.Equal(1, pushedCount);
        var state = new SyncStateStore(_repoPath).Load();
        Assert.Equal("\"existant.txt-new-etag\"", state.ETagsByPath["existant.txt"]);
    }

    [Fact]
    public async Task PushAsync_WithDeletedFile_ChecksNoConflictThenSendsDeleteAndRemovesETagFromState()
    {
        File.Delete(Path.Combine(_repoPath, "existant.txt"));
        _git.AddAll();

        var pushedCount = await PushAsync(new FakeHttpMessageHandler(request => Respond(request, _remoteETagsByPath)));

        Assert.Equal(1, pushedCount);
        Assert.False(_remoteETagsByPath.ContainsKey("existant.txt"));
        var state = new SyncStateStore(_repoPath).Load();
        Assert.False(state.ETagsByPath.ContainsKey("existant.txt"));
    }

    [Fact]
    public async Task PushAsync_WithRenamedFile_SendsMoveRequestAndUpdatesETag()
    {
        File.Move(Path.Combine(_repoPath, "existant.txt"), Path.Combine(_repoPath, "renomme.txt"));
        _git.AddAll();

        var pushedCount = await PushAsync(new FakeHttpMessageHandler(request => Respond(request, _remoteETagsByPath)));

        Assert.Equal(1, pushedCount);
        var state = new SyncStateStore(_repoPath).Load();
        Assert.False(state.ETagsByPath.ContainsKey("existant.txt"));
        Assert.Equal("\"renomme.txt-new-etag\"", state.ETagsByPath["renomme.txt"]);
    }

    [Fact]
    public async Task PushAsync_WithRenamedFile_UsesMoveNotDeletePlusPut()
    {
        File.Move(Path.Combine(_repoPath, "existant.txt"), Path.Combine(_repoPath, "renomme.txt"));
        _git.AddAll();

        var methodsUsed = new List<string>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            methodsUsed.Add(request.Method.Method);
            return Respond(request, _remoteETagsByPath);
        });

        await PushAsync(handler);

        Assert.Contains("MOVE", methodsUsed);
        Assert.DoesNotContain("DELETE", methodsUsed);
        Assert.DoesNotContain("PUT", methodsUsed);
    }

    [Fact]
    public async Task PushAsync_WithRenamedFile_SendsDestinationHeaderWithNewPath()
    {
        File.Move(Path.Combine(_repoPath, "existant.txt"), Path.Combine(_repoPath, "renomme.txt"));
        _git.AddAll();

        HttpRequestMessage? moveRequest = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method.Method == "MOVE")
            {
                moveRequest = request;
            }

            return Respond(request, _remoteETagsByPath);
        });

        await PushAsync(handler);

        Assert.NotNull(moveRequest);
        Assert.Equal("/remote.php/dav/files/alice/dossier/existant.txt", moveRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(
            "https://cloud.example.ch/remote.php/dav/files/alice/dossier/renomme.txt",
            moveRequest.Headers.GetValues("Destination").Single());
    }

    [Fact]
    public async Task PushAsync_WhenRenamedFileHasRemoteConflictOnOldPath_ThrowsConflictWithoutAnyWrite()
    {
        File.Move(Path.Combine(_repoPath, "existant.txt"), Path.Combine(_repoPath, "renomme.txt"));
        _git.AddAll();

        // Simule un autre client Nextcloud ayant modifie le fichier original depuis la derniere sync.
        _remoteETagsByPath["existant.txt"] = "\"changé-par-quelqu'un-d'autre\"";

        var handler = new FakeHttpMessageHandler(request => request.Method.Method == "PROPFIND"
            ? RespondPropFind(request, _remoteETagsByPath)
            : throw new InvalidOperationException($"Aucune écriture n'était attendue : {request.Method} {request.RequestUri}"));

        var ex = await Assert.ThrowsAsync<NcPushConflictException>(() => PushAsync(handler));

        Assert.Equal(["existant.txt"], ex.ConflictingPaths);
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

    [Fact]
    public async Task PushAsync_WhenRemoteETagDiffersFromKnown_ThrowsConflictWithoutAnyWrite()
    {
        File.WriteAllText(Path.Combine(_repoPath, "existant.txt"), "modifié localement");
        _git.AddAll();

        // Simule un autre client Nextcloud ayant modifie ce fichier depuis la derniere sync.
        _remoteETagsByPath["existant.txt"] = "\"changé-par-quelqu'un-d'autre\"";

        var handler = new FakeHttpMessageHandler(request => request.Method.Method == "PROPFIND"
            ? RespondPropFind(request, _remoteETagsByPath)
            : throw new InvalidOperationException($"Aucune écriture n'était attendue : {request.Method} {request.RequestUri}"));

        var initialSynced = _git.ReadRef("refs/nc/synced").StandardOutput.Trim();

        var ex = await Assert.ThrowsAsync<NcPushConflictException>(() => PushAsync(handler));

        Assert.Contains("existant.txt", ex.Message);
        Assert.Equal(["existant.txt"], ex.ConflictingPaths);
        Assert.Equal(initialSynced, _git.ReadRef("refs/nc/synced").StandardOutput.Trim());
        Assert.Contains("existant.txt", _git.Status().StandardOutput);
    }

    [Fact]
    public async Task PushAsync_WhenRemoteFileWasDeletedSinceLastSync_TreatsMissingFileAsConflict()
    {
        File.WriteAllText(Path.Combine(_repoPath, "existant.txt"), "modifié localement");
        _git.AddAll();

        // Simule une suppression cote serveur depuis la derniere sync : plus d'ETag connu.
        _remoteETagsByPath.Remove("existant.txt");

        var handler = new FakeHttpMessageHandler(request => request.Method.Method == "PROPFIND"
            ? RespondPropFind(request, _remoteETagsByPath)
            : throw new InvalidOperationException($"Aucune écriture n'était attendue : {request.Method} {request.RequestUri}"));

        await Assert.ThrowsAsync<NcPushConflictException>(() => PushAsync(handler));
    }

    [Fact]
    public async Task PushAsync_WithConflictAmongOtherwiseCleanEntries_AbortsWholeBatchWithoutAnyWrite()
    {
        // existant.txt : modifie localement, pas de conflit (ETag serveur inchange).
        File.WriteAllText(Path.Combine(_repoPath, "existant.txt"), "modifié localement, sans conflit");
        // autre.txt : modifie localement ET en conflit (ETag serveur different de celui connu).
        File.WriteAllText(Path.Combine(_repoPath, "autre.txt"), "modifié localement, en conflit");
        _remoteETagsByPath["autre.txt"] = "\"changé-par-quelqu'un-d'autre\"";
        // c.txt : nouveau fichier, jamais synchronise, donc jamais concerne par la verification.
        File.WriteAllText(Path.Combine(_repoPath, "c.txt"), "nouveau fichier");
        _git.AddAll();

        var handler = new FakeHttpMessageHandler(request => request.Method.Method == "PROPFIND"
            ? RespondPropFind(request, _remoteETagsByPath)
            : throw new InvalidOperationException($"Aucune écriture n'était attendue : {request.Method} {request.RequestUri}"));

        var ex = await Assert.ThrowsAsync<NcPushConflictException>(() => PushAsync(handler));

        // Seul le fichier reellement en conflit est liste ; existant.txt (sans conflit) et
        // c.txt (jamais synchronise, hors verification) ne bloquent ni ne sont mentionnes,
        // mais l'ensemble du batch est quand meme annule (aucune ecriture, meme pour eux).
        Assert.Equal(["autre.txt"], ex.ConflictingPaths);
    }

    private Task<int> PushAsync(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        var webDavClient = new NextcloudWebDavClient(httpClient);
        return NcPushService.PushAsync(_git, webDavClient, _repoPath, RemotePath, new SyncStateStore(_repoPath));
    }

    // Simule un serveur WebDAV minimal, stateful : PUT/DELETE mutent remoteETagsByPath, PROPFIND
    // reflete l'etat courant (404 si le chemin n'y figure plus/pas encore).
    private static HttpResponseMessage Respond(HttpRequestMessage request, Dictionary<string, string> remoteETagsByPath)
    {
        if (request.Method == HttpMethod.Put)
        {
            var relativePath = ExtractRelativePath(request.RequestUri!);
            remoteETagsByPath[relativePath] = $"\"{relativePath}-new-etag\"";
            return new HttpResponseMessage(HttpStatusCode.Created);
        }

        if (request.Method == HttpMethod.Delete)
        {
            remoteETagsByPath.Remove(ExtractRelativePath(request.RequestUri!));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (request.Method.Method == "MOVE")
        {
            var oldRelativePath = ExtractRelativePath(request.RequestUri!);
            var destination = new Uri(request.Headers.GetValues("Destination").Single());
            var newRelativePath = ExtractRelativePath(destination);
            remoteETagsByPath.Remove(oldRelativePath);
            remoteETagsByPath[newRelativePath] = $"\"{newRelativePath}-new-etag\"";
            return new HttpResponseMessage(HttpStatusCode.Created);
        }

        if (request.Method.Method == "PROPFIND")
        {
            return RespondPropFind(request, remoteETagsByPath);
        }

        throw new InvalidOperationException($"Requête inattendue : {request.Method} {request.RequestUri}");
    }

    private static HttpResponseMessage RespondPropFind(HttpRequestMessage request, Dictionary<string, string> remoteETagsByPath)
    {
        var relativePath = ExtractRelativePath(request.RequestUri!);
        if (!remoteETagsByPath.TryGetValue(relativePath, out var eTag))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var body = $"""
            <?xml version="1.0"?>
            <d:multistatus xmlns:d="DAV:">
              <d:response>
                <d:href>{request.RequestUri!.AbsolutePath}</d:href>
                <d:propstat><d:prop><d:getetag>{eTag}</d:getetag><d:resourcetype/></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
              </d:response>
            </d:multistatus>
            """;
        return new HttpResponseMessage(HttpStatusCode.MultiStatus)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/xml"),
        };
    }

    private static string ExtractRelativePath(Uri uri) => uri.AbsolutePath[RemoteFilePrefix.Length..];

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
