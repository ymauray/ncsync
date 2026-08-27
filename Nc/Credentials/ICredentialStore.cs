namespace Nc.Credentials;

/// <summary>
/// Stockage chiffre d'un secret (mot de passe / app password Nextcloud), une implementation
/// par plateforme (voir SPECS.md §5). `key` identifie le secret de maniere stable et unique
/// (voir <see cref="CredentialKey"/>) ; ce n'est jamais le secret lui-meme.
/// </summary>
internal interface ICredentialStore
{
    void Save(string key, string secret);

    string? TryLoad(string key);

    void Delete(string key);
}
