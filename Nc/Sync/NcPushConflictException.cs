namespace Nc.Sync;

/// <summary>
/// Levée par <see cref="NcPushService.PushAsync"/> quand un ou plusieurs fichiers du batch ont
/// été modifiés côté serveur depuis la dernière synchronisation connue (ETag différent de celui
/// de `.nc/state.json`, ou fichier disparu du serveur) — SPECS.md §4. Hérite de
/// <see cref="InvalidOperationException"/> pour rester capturée par le même bloc `catch` que les
/// autres échecs de push côté <see cref="Nc.Commands.PushCommandHandler"/>.
/// </summary>
internal sealed class NcPushConflictException(IReadOnlyList<string> conflictingPaths)
    : InvalidOperationException(BuildMessage(conflictingPaths))
{
    public IReadOnlyList<string> ConflictingPaths { get; } = conflictingPaths;

    private static string BuildMessage(IReadOnlyList<string> conflictingPaths) =>
        "Conflit détecté (modifié ou supprimé sur le serveur depuis la dernière synchronisation), push annulé pour : " +
        string.Join(", ", conflictingPaths);
}
