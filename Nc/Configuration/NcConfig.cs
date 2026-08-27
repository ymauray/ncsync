namespace Nc.Configuration;

/// <summary>
/// Contenu de `.nc/config` (SPECS.md §5). Seuls les champs en clair vivent ici ; le mot de
/// passe/app password est stocke separement via <see cref="Nc.Credentials.ICredentialStore"/>.
/// </summary>
internal sealed record NcConfig
{
    public string? Username { get; init; }
}
