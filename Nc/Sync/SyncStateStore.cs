using Nc.Storage;

namespace Nc.Sync;

/// <summary>
/// Lecture/ecriture de `.nc/state.json` dans un dossier de travail donne.
/// </summary>
internal sealed class SyncStateStore(string workingDirectory)
{
    private string StatePath => Path.Combine(workingDirectory, ".nc", "state.json");

    public SyncState Load() => JsonFileStore.Load<SyncState>(StatePath);

    public void Save(SyncState state) => JsonFileStore.Save(StatePath, state);
}
