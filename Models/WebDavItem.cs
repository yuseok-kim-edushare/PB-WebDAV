using System.Runtime.InteropServices;
using PBWebDAV.Interfaces;

namespace PBWebDAV.Models
{
    /// <summary>
    /// Internal implementation of <see cref="IWebDavItem"/>.
    /// Created only by <c>PropFindXmlParser</c>; PowerBuilder accesses data
    /// via the flat <c>GetItemXxx(index)</c> methods on <c>WebDavClient</c>.
    /// </summary>
    [ComVisible(false)]
    public sealed class WebDavItem : IWebDavItem
    {
        // COM requires a public parameterless constructor on CoClasses.
        public WebDavItem() { }

        public string Href         { get; internal set; } = string.Empty;
        public string DisplayName  { get; internal set; } = string.Empty;
        public bool   IsCollection { get; internal set; }
        public long   ContentLength{ get; internal set; }
        public string ContentType  { get; internal set; } = string.Empty;
        public string LastModified { get; internal set; } = string.Empty;
        public string ETag         { get; internal set; } = string.Empty;
        public string CreationDate { get; internal set; } = string.Empty;
        public int    StatusCode   { get; internal set; }
    }
}
