using System.Xml.Linq;

namespace Nc.WebDav;

/// <summary>
/// Parsing pur (sans dependance HTTP) d'une reponse `multistatus` PROPFIND en une liste
/// d'entrees. Separe de <see cref="NextcloudWebDavClient"/> pour rester testable avec de
/// simples chaines XML, sans mock HTTP.
/// </summary>
internal static class WebDavPropFindParser
{
    private static readonly XNamespace Dav = "DAV:";

    public static IReadOnlyList<WebDavEntry> Parse(string multistatusXml)
    {
        var document = XDocument.Parse(multistatusXml);
        var entries = new List<WebDavEntry>();

        foreach (var response in document.Descendants(Dav + "response"))
        {
            var href = response.Element(Dav + "href")?.Value;
            if (href is null)
            {
                continue;
            }

            var propStat = response.Elements(Dav + "propstat")
                .FirstOrDefault(p => (p.Element(Dav + "status")?.Value ?? string.Empty).Contains("200"));
            var prop = propStat?.Element(Dav + "prop");

            var eTag = prop?.Element(Dav + "getetag")?.Value;
            var isCollection = prop?.Element(Dav + "resourcetype")?.Element(Dav + "collection") is not null;

            entries.Add(new WebDavEntry(href, eTag, isCollection));
        }

        return entries;
    }
}
