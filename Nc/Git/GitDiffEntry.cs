namespace Nc.Git;

/// <summary>
/// Une ligne de `git diff --cached --name-status -M`, interprétée. `Path` est le chemin actuel
/// (destination pour un renommage) ; `OldPath` n'est renseigné que pour <see cref="GitChangeType.Renamed"/>.
/// </summary>
internal sealed record GitDiffEntry(GitChangeType ChangeType, string Path, string? OldPath = null);
