namespace Nc.Sync;

/// <summary>
/// Contenu de `.nc/state.json` (SPECS.md §2 et §4) : les ETags distants connus au dernier
/// point de synchronisation, indexes par chemin relatif. Le point de synchronisation
/// lui-meme (quel commit local a ete synchronise) vit exclusivement dans le ref git dedie
/// `refs/nc/synced` (voir GitClient.UpdateRef/ReadRef) — pas duplique ici, pour n'avoir
/// qu'une seule source de verite sur cette information.
/// </summary>
internal sealed record SyncState
{
    public Dictionary<string, string> ETagsByPath { get; init; } = [];
}
