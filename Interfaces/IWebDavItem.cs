using System.Runtime.InteropServices;

namespace PBWebDAV.Interfaces
{
    /// <summary>
    /// Read-only view of a single WebDAV resource returned by ListDirectory.
    /// Used internally; PowerBuilder accesses item data via the flat GetItemXxx(index)
    /// methods on WebDavClient instead of this interface directly.
    /// </summary>
    [ComVisible(false)]
    public interface IWebDavItem
    {
        string Href          { get; }
        string DisplayName   { get; }
        bool   IsCollection  { get; }
        long   ContentLength { get; }
        string ContentType   { get; }
        string LastModified  { get; }
        string ETag          { get; }
        string CreationDate  { get; }
        int    StatusCode    { get; }
    }
}
