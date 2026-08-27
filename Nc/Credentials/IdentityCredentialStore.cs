namespace Nc.Credentials;

/// <summary>
/// Source de verite pour le mot de passe nc, symetrique a IdentityConfigStore (nom
/// d'utilisateur) : cle globale en priorite (reutilisable depuis n'importe quel dossier),
/// repli sur la cle locale (par dossier) en cas d'echec d'ecriture sur la cle globale, ou
/// d'absence en lecture.
/// </summary>
internal sealed class IdentityCredentialStore(ICredentialStore credentialStore, string localFallbackDirectory, string? globalKey = null)
{
    private readonly string _globalKey = globalKey ?? CredentialKey.Global;

    public string? TryLoad() =>
        credentialStore.TryLoad(_globalKey) ?? credentialStore.TryLoad(CredentialKey.ForPath(localFallbackDirectory));

    public void Save(string password)
    {
        try
        {
            credentialStore.Save(_globalKey, password);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Impossible d'enregistrer le mot de passe dans le trousseau global ({ex.Message}) : utilisation du dossier courant à la place.");
            credentialStore.Save(CredentialKey.ForPath(localFallbackDirectory), password);
        }
    }
}
