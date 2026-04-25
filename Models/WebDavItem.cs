using System.Runtime.InteropServices;
using PBWebDAV.Interfaces;

namespace PBWebDAV.Models
{
    /// <summary>
    /// Concrete, COM-visible implementation of <see cref="IWebDavItem"/>.
    ///
    /// Instances are created only by <c>PropFindXmlParser</c> inside this assembly.
    /// External callers (COM / PowerBuilder) receive them via <see cref="IWebDavItem"/>
    /// and can only read the properties — the setters are internal.
    /// </summary>
    [ComVisible(true)]
    [Guid("E6A8B0C2-F5A7-4C9D-DE2A-BF0E9D8C7B6E")]
    [ClassInterface(ClassInterfaceType.None)]   // expose only IWebDavItem to COM
    [ProgId("PBWebDAV.WebDavItem")]
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
