namespace Nc.Configuration;

/// <summary>
/// Contenu de `.nc/config` (SPECS.md §5). Seuls les champs en clair vivent ici ; le mot de
/// passe/app password est stocke separement via <see cref="Nc.Credentials.ICredentialStore"/>.
/// </summary>
internal sealed record NcConfig
{
    public string? Username { get; init; }

    /// <summary>Serveur tel que fourni dans le remote au clone (ex: "mon-serveur.ch"), sans normalisation.</summary>
    public string? ServerUrl { get; init; }

    /// <summary>Chemin distant tel que fourni dans le remote au clone (ex: "/Chemin/vers/dossier").</summary>
    public string? RemotePath { get; init; }
}
