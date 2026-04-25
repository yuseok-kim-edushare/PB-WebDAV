using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using PBWebDAV.Models;

namespace PBWebDAV.Internal
{
    /// <summary>
    /// Parses the XML body of a WebDAV PROPFIND 207 Multi-Status response.
    ///
    /// The WebDAV spec (RFC 4918) uses the "DAV:" XML namespace.
    /// Server implementations vary slightly (relative vs absolute hrefs,
    /// missing optional props, etc.) — this parser is defensively written.
    /// </summary>
    internal static class PropFindXmlParser
    {
        // RFC 4918 §14 — all WebDAV elements live in the "DAV:" namespace.
        private static readonly XNamespace Dav = "DAV:";

        /// <summary>
        /// Parses <paramref name="xmlContent"/> and returns all
        /// <c>DAV:response</c> elements as <see cref="WebDavItem"/> objects.
        ///
        /// The first item is typically the requested collection itself
        /// (you can identify it by <c>IsCollection == true</c>).
        /// Returns an empty list if parsing fails.
        /// </summary>
        internal static List<WebDavItem> Parse(string xmlContent)
        {
            var items = new List<WebDavItem>();

            if (string.IsNullOrWhiteSpace(xmlContent))
                return items;

            try
            {
                XDocument doc = XDocument.Parse(xmlContent);

                foreach (XElement response in doc.Descendants(Dav + "response"))
                {
                    WebDavItem? item = ParseResponse(response);
                    if (item is not null)
                        items.Add(item);
                }
            }
            catch
            {
                // Return whatever was parsed before the error.
            }

            return items;
        }

        // ── Per-response parsing ──────────────────────────────────────────────

        private static WebDavItem? ParseResponse(XElement response)
        {
            string? href = response.Element(Dav + "href")?.Value;
            if (string.IsNullOrEmpty(href))
                return null;

            // Prefer the propstat with HTTP 200; fall back to any propstat.
            XElement? propstat = response
                .Elements(Dav + "propstat")
                .FirstOrDefault(ps =>
                    ps.Element(Dav + "status")?.Value
                      .IndexOf("200", StringComparison.Ordinal) >= 0)
                ?? response.Element(Dav + "propstat");

            XElement? prop = propstat?.Element(Dav + "prop");

            // Parse status code from the propstat <D:status> text, e.g. "HTTP/1.1 200 OK"
            int statusCode = ParseStatusCode(propstat?.Element(Dav + "status")?.Value);

            bool isCollection =
                prop?.Element(Dav + "resourcetype")?.Element(Dav + "collection") is not null;

            long.TryParse(
                prop?.Element(Dav + "getcontentlength")?.Value,
                out long contentLength);

            return new WebDavItem
            {
                Href          = href!,
                DisplayName   = prop?.Element(Dav + "displayname")?.Value
                                ?? DeriveName(href!),
                IsCollection  = isCollection,
                ContentLength = contentLength,
                ContentType   = prop?.Element(Dav + "getcontenttype")?.Value  ?? string.Empty,
                LastModified  = prop?.Element(Dav + "getlastmodified")?.Value ?? string.Empty,
                ETag          = prop?.Element(Dav + "getetag")?.Value          ?? string.Empty,
                CreationDate  = prop?.Element(Dav + "creationdate")?.Value     ?? string.Empty,
                StatusCode    = statusCode,
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Extracts the numeric status code from a string like "HTTP/1.1 200 OK".
        /// </summary>
        private static int ParseStatusCode(string? statusLine)
        {
            if (string.IsNullOrEmpty(statusLine))
                return 0;

            // Format: "HTTP/1.1 <code> <reason>"
            string[] parts = statusLine!.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int code))
                return code;

            return 0;
        }

        /// <summary>
        /// Derives a display name from the href when DAV:displayname is absent.
        /// Strips trailing slashes (collections) and URL-decodes percent-encoding.
        /// </summary>
        private static string DeriveName(string href)
        {
            string trimmed = href.TrimEnd('/');
            int slash = trimmed.LastIndexOf('/');
            string raw = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;
            return Uri.UnescapeDataString(raw);
        }
    }
}
