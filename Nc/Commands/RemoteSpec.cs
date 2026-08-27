namespace Nc.Commands;

/// <summary>
/// Decompose un remote `nc clone` de la forme `serveur:/chemin/vers/dossier` (CAHIER_DES_CHARGES.md §3).
/// </summary>
internal sealed record RemoteSpec(string Server, string Path)
{
    public static RemoteSpec Parse(string spec)
    {
        var separatorIndex = spec.IndexOf(':');
        if (separatorIndex < 0)
        {
            throw new FormatException($"Format de remote invalide (attendu serveur:/chemin) : {spec}");
        }

        var server = spec[..separatorIndex];
        var path = spec[(separatorIndex + 1)..];

        if (server.Length == 0)
        {
            throw new FormatException($"Format de remote invalide, serveur manquant : {spec}");
        }

        return new RemoteSpec(server, path);
    }
}
