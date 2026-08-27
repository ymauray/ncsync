using System.Security.Cryptography;
using System.Text;

namespace Nc.Credentials;

/// <summary>
/// Derive une cle stable et sans caractere problematique (nom de fichier, attribut de
/// trousseau...) a partir du dossier de travail nc, pour qu'un meme dossier retrouve
/// toujours le meme secret et que deux dossiers distincts ne collisionnent jamais.
/// </summary>
internal static class CredentialKey
{
    /// <summary>Cle de l'identite globale (mot de passe par defaut, reutilisable depuis n'importe quel dossier).</summary>
    public const string Global = "global";

    public static string ForPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var normalized = OperatingSystem.IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash);
    }
}
