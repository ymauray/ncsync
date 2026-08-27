namespace Nc.WebDav;

/// <summary>
/// Convertit le `href` absolu d'une entree PROPFIND en chemin relatif au dossier demande,
/// pour savoir ou ecrire chaque fichier localement. Fonction pure, testable sans HTTP.
/// </summary>
internal static class WebDavHrefResolver
{
    /// <summary>
    /// `requestedAbsolutePath` est le chemin absolu (sans host) de la ressource demandee par le
    /// PROPFIND, ex. "/remote.php/dav/files/alice/dossier/". `entryHref` est le `href` brut
    /// (percent-encode) d'une entree de la reponse. Retourne une chaine vide pour l'entree qui
    /// represente le dossier demande lui-meme (pas un enfant a telecharger).
    /// </summary>
    public static string ToRelativePath(string requestedAbsolutePath, string entryHref)
    {
        var decodedHref = Uri.UnescapeDataString(entryHref);
        var basePath = requestedAbsolutePath.TrimEnd('/') + "/";

        if (!decodedHref.StartsWith(basePath, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return decodedHref[basePath.Length..].TrimEnd('/');
    }
}
