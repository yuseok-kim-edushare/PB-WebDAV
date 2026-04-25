using System.Runtime.InteropServices;

namespace PBWebDAV.Interfaces
{
    /// <summary>
    /// Read-only view of a single WebDAV resource returned by ListDirectory.
    /// All string dates are kept in their original RFC 1123 / ISO 8601 form
    /// so PowerBuilder can parse them as needed.
    /// </summary>
    [ComVisible(true)]
    [Guid("D5F7A9B1-E4F6-4B8C-CD1F-AE9D8C7B6A5D")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IWebDavItem
    {
        /// <summary>Server-relative or absolute href as returned by the server.</summary>
        [DispId(1)]
        string Href { get; }

        /// <summary>Human-readable display name (DAV:displayname or last path segment).</summary>
        [DispId(2)]
        string DisplayName { get; }

        /// <summary>True when the resource is a collection (directory).</summary>
        [DispId(3)]
        bool IsCollection { get; }

        /// <summary>Content length in bytes; 0 for collections.</summary>
        [DispId(4)]
        long ContentLength { get; }

        /// <summary>MIME type string, e.g. "text/plain".</summary>
        [DispId(5)]
        string ContentType { get; }

        /// <summary>Last-modified date as RFC 1123 string, e.g. "Mon, 25 Apr 2026 10:00:00 GMT".</summary>
        [DispId(6)]
        string LastModified { get; }

        /// <summary>Entity tag (opaque quote-wrapped string) or empty.</summary>
        [DispId(7)]
        string ETag { get; }

        /// <summary>Resource creation date as ISO 8601 string or empty.</summary>
        [DispId(8)]
        string CreationDate { get; }

        /// <summary>HTTP status code of this propstat entry (usually 200).</summary>
        [DispId(9)]
        int StatusCode { get; }
    }
}
