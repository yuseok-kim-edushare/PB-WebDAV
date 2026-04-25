using System.Runtime.InteropServices;

namespace PBWebDAV.Interfaces
{
    /// <summary>
    /// Primary WebDAV client interface exported to COM / PowerBuilder.
    ///
    /// Design rules for COM dual interfaces:
    ///  - No method overloads (COM vtable cannot disambiguate).
    ///  - DispIds must never change once published.
    ///  - All parameter types must be COM-safe primitives or other [ComVisible] interfaces.
    /// </summary>
    [ComVisible(true)]
    [Guid("B3D5E7F9-C2E4-4F6A-AB9D-8C7B6A5E4F3B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IWebDavClient
    {
        // ── Lifecycle ──────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises an unauthenticated (or Basic-auth) connection.
        /// Must be called before any other method.
        /// </summary>
        /// <param name="baseUrl">Root URL of the WebDAV server, e.g. https://host/dav/</param>
        /// <param name="username">Leave empty for anonymous.</param>
        /// <param name="password">Leave empty for anonymous.</param>
        [DispId(1)]
        bool Initialize(string baseUrl, string username, string password);

        /// <summary>
        /// Initialises a connection routed through an HTTP proxy.
        /// </summary>
        [DispId(2)]
        bool InitializeWithProxy(
            string baseUrl,
            string username,
            string password,
            string proxyUrl,
            string proxyUsername,
            string proxyPassword);

        /// <summary>Sets the request/response timeout in seconds (default 30).</summary>
        [DispId(3)]
        void SetTimeout(int timeoutSeconds);

        // ── Directory listing (PROPFIND Depth:1) ──────────────────────────────

        /// <summary>
        /// Lists immediate children of <paramref name="remotePath"/>.
        /// Returns the number of items found (including the collection itself at index 0).
        /// Returns –1 on failure; call GetLastError() for details.
        /// </summary>
        [DispId(4)]
        int ListDirectory(string remotePath);

        /// <summary>
        /// Returns the number of items from the last successful ListDirectory call.
        /// </summary>
        [DispId(5)]
        int GetItemCount();

        /// <summary>
        /// Returns a single item from the last ListDirectory result.
        /// Index 0 is the collection itself; 1..N-1 are the children.
        /// </summary>
        [DispId(6)]
        IWebDavItem GetItem(int index);

        // ── File transfer (GET / PUT) ──────────────────────────────────────────

        /// <summary>
        /// Downloads a remote file to a local path.
        /// Uses System.IO.Pipelines for memory-optimal, zero-copy streaming.
        /// </summary>
        [DispId(7)]
        bool DownloadFile(string remotePath, string localPath);

        /// <summary>
        /// Uploads a local file to the remote path (HTTP PUT).
        /// Uses System.IO.Pipelines for memory-optimal, zero-copy streaming.
        /// </summary>
        [DispId(8)]
        bool UploadFile(string localPath, string remotePath);

        // ── Resource management ────────────────────────────────────────────────

        /// <summary>Deletes a file or empty collection (HTTP DELETE).</summary>
        [DispId(9)]
        bool DeleteItem(string remotePath);

        /// <summary>Creates a collection (directory) via HTTP MKCOL.</summary>
        [DispId(10)]
        bool CreateDirectory(string remotePath);

        /// <summary>
        /// Copies a resource server-side via HTTP COPY.
        /// Set <paramref name="overwrite"/> to true to replace an existing destination.
        /// </summary>
        [DispId(11)]
        bool CopyItem(string sourcePath, string destPath, bool overwrite);

        /// <summary>
        /// Moves / renames a resource server-side via HTTP MOVE.
        /// Set <paramref name="overwrite"/> to true to replace an existing destination.
        /// </summary>
        [DispId(12)]
        bool MoveItem(string sourcePath, string destPath, bool overwrite);

        /// <summary>
        /// Returns true if the remote resource exists (HEAD request returns 2xx).
        /// </summary>
        [DispId(13)]
        bool ItemExists(string remotePath);

        // ── Error state ────────────────────────────────────────────────────────

        /// <summary>Returns the human-readable error message from the last failed call.</summary>
        [DispId(14)]
        string GetLastError();

        /// <summary>Returns the HTTP status code from the last operation (0 if not yet called).</summary>
        [DispId(15)]
        int GetLastStatusCode();
    }
}
