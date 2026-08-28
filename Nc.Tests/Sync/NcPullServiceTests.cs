using System.Net;
using System.Text;
using Nc.Git;
using Nc.Sync;
using Nc.Tests.WebDav;
using Nc.WebDav;

namespace Nc.Tests.Sync;

public sealed class NcPullServiceTests : IDisposable
{
    private const string BaseAddress = "https://cloud.example.ch/remote.php/dav/files/alice/";
    private const string RemotePath = "/dossier";
    private const string RemoteFilePrefix = "/remote.php/dav/files/alice/dossier/";

    private readonly string _repoPath = Directory.CreateTempSubdirectory("nc-pull-tests-").FullName;
    private readonly GitClient _git;

    public NcPullServiceTests()
    {
        _git = new GitClient(_repoPath);
        _git.Init();

        // Simule l'etat laisse par `nc clone` : .gitignore excluant .nc/, un commit initial
        // synchronise, refs/nc/synced dessus, et un etat de sync connaissant deja "existant.txt".
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
    public async Task PullAsync_WhenRemoteUnchanged_ReturnsZeroWithoutTouchingGitOrDisk()
    {
        var pulledCount = await PullAsync([("existant.txt", "\"existant-etag\"")]);

        Assert.Equal(0, pulledCount);
        Assert.Equal("deja synchronisé", await File.ReadAllTextAsync(Path.Combine(_repoPath, "existant.txt")));
        Assert.Empty(_git.Status().StandardOutput);
    }

    [Fact]
    public async Task PullAsync_WithChangedRemoteFile_DownloadsNewContent()
    {
        var pulledCount = await PullAsync(
            [("existant.txt", "\"existant-etag-v2\"")],
            [("existant.txt", "contenu v2")]);

        Assert.Equal(1, pulledCount);
        Assert.Equal("contenu v2", await File.ReadAllTextAsync(Path.Combine(_repoPath, "existant.txt")));
    }

    [Fact]
    public async Task PullAsync_WithChangedRemoteFile_UpdatesStateETag()
    {
        await PullAsync([("existant.txt", "\"existant-etag-v2\"")], [("existant.txt", "contenu v2")]);

        var state = new SyncStateStore(_repoPath).Load();
        Assert.Equal("\"existant-etag-v2\"", state.ETagsByPath["existant.txt"]);
    }

    [Fact]
    public async Task PullAsync_WithChangedRemoteFile_CommitsAndAdvancesSyncedRef()
    {
        await PullAsync([("existant.txt", "\"existant-etag-v2\"")], [("existant.txt", "contenu v2")]);

        Assert.Empty(_git.Status().StandardOutput);
        var head = _git.ReadRef("HEAD").StandardOutput.Trim();
        var synced = _git.ReadRef("refs/nc/synced").StandardOutput.Trim();
        Assert.Equal(head, synced);
    }

    [Fact]
    public async Task PullAsync_WithNewRemoteFile_DownloadsItAndAddsToState()
    {
        var pulledCount = await PullAsync(
            [("existant.txt", "\"existant-etag\""), ("nouveau.txt", "\"nouveau-etag\"")],
            [("nouveau.txt", "contenu nouveau")]);

        Assert.Equal(1, pulledCount);
        Assert.Equal("contenu nouveau", await File.ReadAllTextAsync(Path.Combine(_repoPath, "nouveau.txt")));
        var state = new SyncStateStore(_repoPath).Load();
        Assert.Equal("\"nouveau-etag\"", state.ETagsByPath["nouveau.txt"]);
    }

    [Fact]
    public async Task PullAsync_WithNewRemoteFileInSubfolder_CreatesSubfolderAndDownloads()
    {
        var pulledCount = await PullAsync(
            [("existant.txt", "\"existant-etag\""), ("sous/b.txt", "\"b-etag\"")],
            [("sous/b.txt", "contenu b")]);

        Assert.Equal(1, pulledCount);
        Assert.Equal("contenu b", await File.ReadAllTextAsync(Path.Combine(_repoPath, "sous", "b.txt")));
    }

    [Fact]
    public async Task PullAsync_WithFileRemovedFromServer_DeletesItLocallyAndFromState()
    {
        var pulledCount = await PullAsync([]);

        Assert.Equal(1, pulledCount);
        Assert.False(File.Exists(Path.Combine(_repoPath, "existant.txt")));
        var state = new SyncStateStore(_repoPath).Load();
        Assert.False(state.ETagsByPath.ContainsKey("existant.txt"));
    }

    [Fact]
    public async Task PullAsync_WithFileRemovedFromServer_CommitsTheDeletion()
    {
        await PullAsync([]);

        Assert.Empty(_git.Status().StandardOutput);
    }

    [Fact]
    public async Task PullAsync_WithMixOfChangedNewAndDeletedFiles_ReturnsTotalCountAndAppliesAll()
    {
        var pulledCount = await PullAsync(
            [("existant.txt", "\"existant-etag-v2\""), ("nouveau.txt", "\"nouveau-etag\"")],
            [("existant.txt", "modifié"), ("nouveau.txt", "nouveau")]);

        Assert.Equal(2, pulledCount);
    }

    [Fact]
    public async Task PullAsync_WhenDownloadFails_DoesNotAdvanceRefOrState()
    {
        var initialSynced = _git.ReadRef("refs/nc/synced").StandardOutput.Trim();

        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method.Method == "PROPFIND")
            {
                return PropFindResponse([("existant.txt", "\"existant-etag-v2\"")]);
            }

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            throw new InvalidOperationException($"Requête inattendue : {request.Method} {request.RequestUri}");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => PullAsync(handler));

        Assert.Equal(initialSynced, _git.ReadRef("refs/nc/synced").StandardOutput.Trim());
        var state = new SyncStateStore(_repoPath).Load();
        Assert.Equal("\"existant-etag\"", state.ETagsByPath["existant.txt"]);
    }

    // Bas niveau : PROPFIND renvoie remoteEntries, GET sert le contenu de fileContents (par
    // chemin relatif) ; toute autre requête (dont un GET non attendu) échoue le test.
    private Task<int> PullAsync((string Path, string ETag)[] remoteEntries, (string Path, string Content)[]? fileContents = null)
    {
        var contents = (fileContents ?? []).ToDictionary(f => f.Path, f => f.Content);
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method.Method == "PROPFIND")
            {
                return PropFindResponse(remoteEntries);
            }

            if (request.Method == HttpMethod.Get)
            {
                var relativePath = ExtractRelativePath(request.RequestUri!);
                if (contents.TryGetValue(relativePath, out var content))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
                }

                throw new InvalidOperationException($"GET inattendu : {request.RequestUri}");
            }

            throw new InvalidOperationException($"Requête inattendue : {request.Method} {request.RequestUri}");
        });

        return PullAsync(handler);
    }

    private Task<int> PullAsync(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        var webDavClient = new NextcloudWebDavClient(httpClient);
        return NcPullService.PullAsync(_git, webDavClient, _repoPath, RemotePath, new SyncStateStore(_repoPath));
    }

    private static HttpResponseMessage PropFindResponse((string Path, string ETag)[] entries)
    {
        var responses = string.Join(Environment.NewLine, entries.Select(e => $"""
              <d:response>
                <d:href>{RemoteFilePrefix}{e.Path}</d:href>
                <d:propstat><d:prop><d:getetag>{e.ETag}</d:getetag><d:resourcetype/></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
              </d:response>
            """));
        var body = $"""
            <?xml version="1.0"?>
            <d:multistatus xmlns:d="DAV:">
            {responses}
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
