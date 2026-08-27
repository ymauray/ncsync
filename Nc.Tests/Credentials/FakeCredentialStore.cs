using Nc.Credentials;

namespace Nc.Tests.Credentials;

/// <summary>
/// Double de test en memoire : permet de simuler de facon deterministe l'echec de
/// l'ecriture sur une cle precise, ce qu'un vrai ICredentialStore ne permet pas de forcer
/// de maniere fiable et cross-platform.
/// </summary>
internal sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _values = [];

    public string? FailOnSaveKey { get; set; }

    public void Save(string key, string secret)
    {
        if (key == FailOnSaveKey)
        {
            throw new InvalidOperationException("échec simulé");
        }

        _values[key] = secret;
    }

    public string? TryLoad(string key) => _values.GetValueOrDefault(key);

    public void Delete(string key) => _values.Remove(key);
}
