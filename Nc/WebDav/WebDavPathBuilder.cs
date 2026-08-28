namespace Nc.WebDav;

/// <summary>
/// Combine le chemin distant configuré (`.nc/config`, `RemotePath`) avec un chemin local
/// relatif (issu de Git, toujours séparé par `/`) en un chemin de requête WebDAV valide,
/// chaque segment étant percent-encodé individuellement.
/// </summary>
internal static class WebDavPathBuilder
{
    public static string Combine(string remotePath, string relativePath)
    {
        var normalizedRemote = remotePath.Trim('/');
        var normalizedRelative = relativePath.Trim('/');
        var fullPath = normalizedRemote.Length == 0 ? normalizedRelative : $"{normalizedRemote}/{normalizedRelative}";
        return string.Join('/', fullPath.Split('/').Select(Uri.EscapeDataString));
    }
}
