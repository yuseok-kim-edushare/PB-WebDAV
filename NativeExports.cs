#if NET
using System;
using System.Runtime.InteropServices;

namespace PBWebDAV
{
    /// <summary>
    /// Native AOT (C-export) entry points. When published with net10.0-windows +
    /// NativeLib=Shared, these functions are exported from the native PBWebDAV.dll
    /// and callable from PowerBuilder LOCAL EXTERNAL FUNCTION declarations or C/C++.
    ///
    /// Conventions
    ///  - Cdecl, UTF-16 (wchar_t*, null-terminated) strings.
    ///  - bool results are returned as int (1 = true / 0 = false).
    ///  - String getters use the caller-buffer pattern: pass a buffer and its capacity
    ///    (in chars, including the null terminator); the return value is the required
    ///    length in chars excluding the terminator. Call with capacity 0 to query size.
    ///  - State is a process-wide singleton created by WebDAV_Initialize and released
    ///    by WebDAV_Destroy. One connection at a time.
    /// </summary>
    public static unsafe class NativeExports
    {
        private static WebDavClient? s_client;

        private static string Str(char* p) => p == null ? string.Empty : new string(p);

        // Returns required length (excluding null); copies + null-terminates when capacity allows.
        private static int CopyToBuffer(string? value, char* buffer, int capacity)
        {
            value ??= string.Empty;
            if (buffer != null && capacity > 0)
            {
                int copyLen = Math.Min(value.Length, capacity - 1);
                value.AsSpan(0, copyLen).CopyTo(new Span<char>(buffer, copyLen));
                buffer[copyLen] = '\0';
            }
            return value.Length;
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_Initialize", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int Initialize(char* baseUrl, char* username, char* password)
        {
            try
            {
                s_client?.Dispose();
                s_client = new WebDavClient();
                return s_client.Initialize(Str(baseUrl), Str(username), Str(password)) ? 1 : 0;
            }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_InitializeWithProxy", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int InitializeWithProxy(char* baseUrl, char* username, char* password, char* proxyUrl, char* proxyUsername, char* proxyPassword)
        {
            try
            {
                s_client?.Dispose();
                s_client = new WebDavClient();
                return s_client.InitializeWithProxy(
                    Str(baseUrl), Str(username), Str(password),
                    Str(proxyUrl), Str(proxyUsername), Str(proxyPassword)) ? 1 : 0;
            }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_SetTimeout", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static void SetTimeout(int timeoutSeconds)
        {
            try { s_client?.SetTimeout(timeoutSeconds); } catch { }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_ListDirectory", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int ListDirectory(char* remotePath)
        {
            try { return s_client?.ListDirectory(Str(remotePath)) ?? -1; }
            catch { return -1; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemCount", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemCount()
        {
            try { return s_client?.GetItemCount() ?? 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemHref", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemHref(int index, char* buffer, int capacity)
        {
            try { return CopyToBuffer(s_client?.GetItemHref(index), buffer, capacity); }
            catch { return CopyToBuffer(string.Empty, buffer, capacity); }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemDisplayName", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemDisplayName(int index, char* buffer, int capacity)
        {
            try { return CopyToBuffer(s_client?.GetItemDisplayName(index), buffer, capacity); }
            catch { return CopyToBuffer(string.Empty, buffer, capacity); }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemIsCollection", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemIsCollection(int index)
        {
            try { return (s_client?.GetItemIsCollection(index) ?? false) ? 1 : 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemContentLength", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static long GetItemContentLength(int index)
        {
            try { return s_client?.GetItemContentLength(index) ?? -1; }
            catch { return -1; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemContentType", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemContentType(int index, char* buffer, int capacity)
        {
            try { return CopyToBuffer(s_client?.GetItemContentType(index), buffer, capacity); }
            catch { return CopyToBuffer(string.Empty, buffer, capacity); }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemLastModified", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemLastModified(int index, char* buffer, int capacity)
        {
            try { return CopyToBuffer(s_client?.GetItemLastModified(index), buffer, capacity); }
            catch { return CopyToBuffer(string.Empty, buffer, capacity); }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemETag", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemETag(int index, char* buffer, int capacity)
        {
            try { return CopyToBuffer(s_client?.GetItemETag(index), buffer, capacity); }
            catch { return CopyToBuffer(string.Empty, buffer, capacity); }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemCreationDate", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemCreationDate(int index, char* buffer, int capacity)
        {
            try { return CopyToBuffer(s_client?.GetItemCreationDate(index), buffer, capacity); }
            catch { return CopyToBuffer(string.Empty, buffer, capacity); }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetItemStatusCode", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetItemStatusCode(int index)
        {
            try { return s_client?.GetItemStatusCode(index) ?? 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_DownloadFile", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int DownloadFile(char* remotePath, char* localPath)
        {
            try { return (s_client?.DownloadFile(Str(remotePath), Str(localPath)) ?? false) ? 1 : 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_UploadFile", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int UploadFile(char* localPath, char* remotePath)
        {
            try { return (s_client?.UploadFile(Str(localPath), Str(remotePath)) ?? false) ? 1 : 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_DeleteItem", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int DeleteItem(char* remotePath)
        {
            try { return (s_client?.DeleteItem(Str(remotePath)) ?? false) ? 1 : 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_CreateDirectory", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int CreateDirectory(char* remotePath)
        {
            try { return (s_client?.CreateDirectory(Str(remotePath)) ?? false) ? 1 : 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_CopyItem", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int CopyItem(char* sourcePath, char* destPath, int overwrite)
        {
            try { return (s_client?.CopyItem(Str(sourcePath), Str(destPath), overwrite != 0) ?? false) ? 1 : 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_MoveItem", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int MoveItem(char* sourcePath, char* destPath, int overwrite)
        {
            try { return (s_client?.MoveItem(Str(sourcePath), Str(destPath), overwrite != 0) ?? false) ? 1 : 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_ItemExists", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int ItemExists(char* remotePath)
        {
            try { return (s_client?.ItemExists(Str(remotePath)) ?? false) ? 1 : 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetLastError", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetLastError(char* buffer, int capacity)
        {
            try
            {
                var message = s_client == null ? "Not initialized. Call WebDAV_Initialize first." : s_client.GetLastError();
                return CopyToBuffer(message, buffer, capacity);
            }
            catch { return CopyToBuffer(string.Empty, buffer, capacity); }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_GetLastStatusCode", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int GetLastStatusCode()
        {
            try { return s_client?.GetLastStatusCode() ?? 0; }
            catch { return 0; }
        }

        [UnmanagedCallersOnly(EntryPoint = "WebDAV_Destroy", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static void Destroy()
        {
            try
            {
                s_client?.Dispose();
                s_client = null;
            }
            catch { }
        }
    }
}
#endif
