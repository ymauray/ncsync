namespace Nc.WebDav;

/// <summary>
/// Une entree (fichier ou dossier) telle que rapportee par une reponse PROPFIND.
/// `ETag` conserve la valeur brute (guillemets inclus) telle que renvoyee par le serveur.
/// </summary>
internal sealed record WebDavEntry(string Href, string? ETag, bool IsCollection);
