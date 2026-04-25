using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PBWebDAV.Interfaces;
using PBWebDAV.Internal;
using PBWebDAV.Models;

namespace PBWebDAV
{
    /// <summary>
    /// COM-visible WebDAV client.
    ///
    /// PowerBuilder 2019 R3 usage
    /// ──────────────────────────
    ///   Direct .NET proxy (preferred):
    ///     Add the DLL ref in PB's System Options → .NET Assembly.
    ///     Dim oClient As PBWebDAV.WebDavClient
    ///     oClient = Create PBWebDAV.WebDavClient
    ///
    ///   COM fallback (register first with RegAsm.exe /tlb /codebase):
    ///     OLEObject oClient
    ///     oClient = Create OLEObject
    ///     oClient.ConnectToNewObject("PBWebDAV.WebDavClient")
    ///
    /// Thread-safety
    /// ─────────────
    ///   This class is NOT thread-safe.  PowerBuilder calls from the event/STA
    ///   thread are fine; do not share one instance across threads.
    ///
    /// Async → sync bridge
    /// ───────────────────
    ///   All I/O uses async HttpClient + System.IO.Pipelines internally.
    ///   At the COM boundary every async call is marshalled through Task.Run()
    ///   so it executes on a thread-pool thread (no STA context) and then
    ///   .GetAwaiter().GetResult() blocks until completion — this avoids the
    ///   classic STA deadlock that plagues naive .Result calls.
    /// </summary>
    [ComVisible(true)]
    [Guid("C4E6F8A0-D3F5-4A7B-BC0E-9D8C7B6A5F4C")]
    [ClassInterface(ClassInterfaceType.None)]   // expose only IWebDavClient to COM
    [ProgId("PBWebDAV.WebDavClient")]
    public sealed class WebDavClient : IWebDavClient, IDisposable
    {
        // ── WebDAV-specific HTTP methods ──────────────────────────────────────
        private static readonly HttpMethod PropFind = new HttpMethod("PROPFIND");
        private static readonly HttpMethod MkCol    = new HttpMethod("MKCOL");
        private static readonly HttpMethod Copy     = new HttpMethod("COPY");
        private static readonly HttpMethod Move     = new HttpMethod("MOVE");

        // Minimal PROPFIND body — request all live properties.
        private const string PropFindBody =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<D:propfind xmlns:D=\"DAV:\"><D:allprop/></D:propfind>";

        // ── Instance state ────────────────────────────────────────────────────
        private HttpClient?       _http;
        private string            _baseUrl     = string.Empty;
        private List<WebDavItem>  _listing     = new List<WebDavItem>();
        private string            _lastError   = string.Empty;
        private int               _lastStatus;
        private int               _timeoutSec  = 30;
        private bool              _disposed;

        // COM requires a public parameterless constructor on CoClasses.
        public WebDavClient() { }

        // Store credentials to allow re-initialization on Timeout change.
        private string? _username;
        private string? _password;
        private string? _proxyUrl;
        private string? _proxyUser;
        private string? _proxyPass;

        static WebDavClient()
        {
            try
            {
                string logDir = @"C:\Temp";
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                
                string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
                string logFile = Path.Combine(logDir, $"pb-webdav-dll.{dateStr}.log");
                
                var listener = new System.Diagnostics.TextWriterTraceListener(logFile, "PBWebDAVFileListener");
                System.Diagnostics.Trace.Listeners.Add(listener);
                System.Diagnostics.Trace.AutoFlush = true;
            }
            catch
            {
                // Ignore initialization errors for the logger to avoid crashing the host process
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool Initialize(string baseUrl, string username, string password)
            => InitCore(baseUrl, username, password, proxyUrl: null, proxyUser: null, proxyPass: null);

        /// <inheritdoc/>
        public bool InitializeWithProxy(
            string baseUrl,
            string username,
            string password,
            string proxyUrl,
            string proxyUsername,
            string proxyPassword)
            => InitCore(baseUrl, username, password, proxyUrl, proxyUsername, proxyPassword);

        /// <inheritdoc/>
        public void SetTimeout(int timeoutSeconds)
        {
            _timeoutSec = timeoutSeconds > 0 ? timeoutSeconds : 30;
            if (_http != null && !string.IsNullOrEmpty(_baseUrl))
            {
                InitCore(_baseUrl, _username ?? "", _password ?? "", _proxyUrl, _proxyUser, _proxyPass);
            }
        }

        // ── Directory listing ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public int ListDirectory(string remotePath)
        {
            if (!EnsureReady()) return -1;

            return RunSync(async () =>
            {
                using var request = new HttpRequestMessage(PropFind, BuildUri(remotePath));
                request.Headers.Add("Depth", "1");
                request.Content = new StringContent(PropFindBody, Encoding.UTF8, "application/xml");

                using var response = await _http!
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead)
                    .ConfigureAwait(false);

                _lastStatus = (int)response.StatusCode;

                if (_lastStatus != 207)
                {
                    _lastError = FormatHttpError("PROPFIND", response);
                    _listing.Clear();
                    return -1;
                }

                string xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                try
                {
                    _listing = PropFindXmlParser.Parse(xml);
                    _lastError = string.Empty;
                    return _listing.Count;
                }
                catch (Exception ex)
                {
                    _lastError = $"XML Parsing Error: {ex.Message}";
                    System.Diagnostics.Trace.TraceError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PBWebDAV] {_lastError}");
                    _listing.Clear();
                    return -1;
                }
            });
        }

        /// <inheritdoc/>
        public int GetItemCount() => _listing.Count;

        // ── Per-property item accessors ───────────────────────────────────────

        /// <inheritdoc/>
        public string GetItemHref(int index)         { var item = SafeGetItem(index); return item != null ? item.Href : string.Empty; }
        /// <inheritdoc/>
        public string GetItemDisplayName(int index)  { var item = SafeGetItem(index); return item != null ? item.DisplayName : string.Empty; }
        /// <inheritdoc/>
        public bool   GetItemIsCollection(int index) { var item = SafeGetItem(index); return item != null && item.IsCollection; }
        /// <inheritdoc/>
        public long   GetItemContentLength(int index){ var item = SafeGetItem(index); return item != null ? item.ContentLength : -1; }
        /// <inheritdoc/>
        public string GetItemContentType(int index)  { var item = SafeGetItem(index); return item != null ? item.ContentType : string.Empty; }
        /// <inheritdoc/>
        public string GetItemLastModified(int index) { var item = SafeGetItem(index); return item != null ? item.LastModified : string.Empty; }
        /// <inheritdoc/>
        public string GetItemETag(int index)         { var item = SafeGetItem(index); return item != null ? item.ETag : string.Empty; }
        /// <inheritdoc/>
        public string GetItemCreationDate(int index) { var item = SafeGetItem(index); return item != null ? item.CreationDate : string.Empty; }
        /// <inheritdoc/>
        public int    GetItemStatusCode(int index)   { var item = SafeGetItem(index); return item != null ? item.StatusCode : -1; }

        private WebDavItem? SafeGetItem(int index)
        {
            if (index < 0 || index >= _listing.Count)
            {
                _lastError = $"Index {index} is out of range. Item count: {_listing.Count}";
                System.Diagnostics.Trace.TraceWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PBWebDAV] {_lastError}");
                return null;
            }
            return _listing[index];
        }

        // ── File transfer ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool DownloadFile(string remotePath, string localPath)
        {
            if (!EnsureReady()) return false;

            return RunSync(async () =>
            {
                using var response = await _http!
                    .GetAsync(BuildUri(remotePath), HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                _lastStatus = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    _lastError = FormatHttpError("GET", response);
                    return false;
                }

                // Stream the response body into the file through System.IO.Pipelines.
                // PipelineStreamCopier reads pool-backed segments; no large intermediate
                // buffer is ever allocated.
                using Stream responseStream =
                    await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                // For very large or potentially malicious downloads without Content-Length, Limit checks are tricky without breaking streams,
                // but checking headers if available helps.
                if (response.Content.Headers.ContentLength.HasValue && 
                    response.Content.Headers.ContentLength.Value > 1024L * 1024L * 1024L * 10L) // 10 GB limit
                {
                   _lastError = $"File is too large ({response.Content.Headers.ContentLength.Value} bytes). Maximum allowed is 10GB.";
                   System.Diagnostics.Trace.TraceWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PBWebDAV] {_lastError}");
                   return false; 
                }

                await PipelineStreamCopier
                    .CopyToFileAsync(responseStream, localPath)
                    .ConfigureAwait(false);

                _lastError = string.Empty;
                return true;
            });
        }

        /// <inheritdoc/>
        public bool UploadFile(string localPath, string remotePath)
        {
            if (!EnsureReady()) return false;

            if (!File.Exists(localPath))
            {
                _lastError = $"Local file not found: {localPath}";
                System.Diagnostics.Trace.TraceWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PBWebDAV] {_lastError}");
                return false;
            }

            return RunSync(async () =>
            {
                // Upload directly from a FileStream into the StreamContent.
                // StreamContent will inherently call ReadAsync iteratively without loading the
                // entire file into memory; adding an intermediate Pipe layer provides no
                // benefit here and only adds complexity.
                using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65_536, useAsync: true);
                using var content = new StreamContent(fs, bufferSize: 65_536);

                // Let the server infer the content-type; set octet-stream as a safe default.
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                using var request = new HttpRequestMessage(HttpMethod.Put, BuildUri(remotePath))
                {
                    Content = content
                };

                using var response = await _http!.SendAsync(request).ConfigureAwait(false);
                _lastStatus = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    _lastError = FormatHttpError("PUT", response);
                    return false;
                }

                _lastError = string.Empty;
                return true;
            });
        }

        // ── Resource management ───────────────────────────────────────────────

        /// <inheritdoc/>
        public bool DeleteItem(string remotePath)
        {
            if (!EnsureReady()) return false;

            return RunSync(async () =>
            {
                using var response = await _http!
                    .DeleteAsync(BuildUri(remotePath))
                    .ConfigureAwait(false);

                _lastStatus = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    _lastError = FormatHttpError("DELETE", response);
                    return false;
                }

                _lastError = string.Empty;
                return true;
            });
        }

        /// <inheritdoc/>
        public bool CreateDirectory(string remotePath)
        {
            if (!EnsureReady()) return false;

            return RunSync(async () =>
            {
                using var request = new HttpRequestMessage(MkCol, BuildUri(remotePath));
                using var response = await _http!.SendAsync(request).ConfigureAwait(false);

                _lastStatus = (int)response.StatusCode;

                // 201 Created is the normal success code for MKCOL.
                if (_lastStatus != 201 && !response.IsSuccessStatusCode)
                {
                    _lastError = FormatHttpError("MKCOL", response);
                    return false;
                }

                _lastError = string.Empty;
                return true;
            });
        }

        /// <inheritdoc/>
        public bool CopyItem(string sourcePath, string destPath, bool overwrite)
            => ServerSideTransfer(Copy, sourcePath, destPath, overwrite, "COPY");

        /// <inheritdoc/>
        public bool MoveItem(string sourcePath, string destPath, bool overwrite)
            => ServerSideTransfer(Move, sourcePath, destPath, overwrite, "MOVE");

        /// <inheritdoc/>
        public bool ItemExists(string remotePath)
        {
            if (!EnsureReady()) return false;

            return RunSync(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, BuildUri(remotePath));
                using var response = await _http!.SendAsync(request).ConfigureAwait(false);

                _lastStatus = (int)response.StatusCode;
                _lastError  = string.Empty;
                return response.IsSuccessStatusCode;
            });
        }

        // ── Error state ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string GetLastError()      => _lastError;

        /// <inheritdoc/>
        public int    GetLastStatusCode() => _lastStatus;

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _http?.Dispose();
            _http = null;
        }

        // ── COM registration hooks ────────────────────────────────────────────
        // Called automatically by RegAsm.exe when registering / unregistering.

        [ComRegisterFunction]
        private static void OnRegister(Type t)
        {
            // No extra registry keys needed for basic COM activation.
        }

        [ComUnregisterFunction]
        private static void OnUnregister(Type t)
        {
            // Mirror anything written in OnRegister.
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private bool InitCore(
            string  baseUrl,
            string  username,
            string  password,
            string? proxyUrl,
            string? proxyUser,
            string? proxyPass)
        {
            _username = username;
            _password = password;
            _proxyUrl = proxyUrl;
            _proxyUser = proxyUser;
            _proxyPass = proxyPass;

            _http?.Dispose();
            _http   = null;
            _listing.Clear();
            _lastError  = string.Empty;
            _lastStatus = 0;

            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect    = false,
                UseDefaultCredentials = false,
                // TLS 1.2 is the default on .NET 4.8; no extra configuration needed.
            };

            if (!string.IsNullOrEmpty(username))
            {
                handler.Credentials    = new NetworkCredential(username, password);
                handler.PreAuthenticate = true;
            }

            if (!string.IsNullOrEmpty(proxyUrl))
            {
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(proxyUrl!, false)
                {
                    Credentials = !string.IsNullOrEmpty(proxyUser)
                        ? new NetworkCredential(proxyUser, proxyPass)
                        : null
                };
            }

            _http = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(_timeoutSec)
            };

            return true;
        }

        private bool EnsureReady()
        {
            if (_http is null)
            {
                _lastError = "WebDavClient is not initialised. Call Initialize() first.";
                System.Diagnostics.Trace.TraceError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PBWebDAV] {_lastError}");
                return false;
            }

            if (_disposed)
            {
                _lastError = "WebDavClient has been disposed.";
                System.Diagnostics.Trace.TraceError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PBWebDAV] {_lastError}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shared implementation for COPY and MOVE (both differ only by HTTP method).
        /// </summary>
        private bool ServerSideTransfer(
            HttpMethod method,
            string     sourcePath,
            string     destPath,
            bool       overwrite,
            string     verb)
        {
            if (!EnsureReady()) return false;

            return RunSync(async () =>
            {
                Uri destUri = BuildUri(destPath);

                using var request = new HttpRequestMessage(method, BuildUri(sourcePath));
                request.Headers.Add("Destination", destUri.AbsoluteUri);
                request.Headers.Add("Overwrite",   overwrite ? "T" : "F");
                request.Headers.Add("Depth",       "infinity");

                using var response = await _http!.SendAsync(request).ConfigureAwait(false);
                _lastStatus = (int)response.StatusCode;

                // 201 (Created) or 204 (No Content) both indicate success.
                if (_lastStatus != 201 && _lastStatus != 204)
                {
                    _lastError = FormatHttpError(verb, response);
                    return false;
                }

                _lastError = string.Empty;
                return true;
            });
        }

        /// <summary>
        /// Resolves a path against the base URL.
        /// Accepts absolute URIs, server-relative paths, and relative names.
        /// </summary>
        private Uri BuildUri(string path)
        {
            Uri result;
            if (Uri.TryCreate(path, UriKind.Absolute, out Uri? abs))
            {
                result = abs;
            }
            else
            {
                string combined = _baseUrl + "/" + path.TrimStart('/');
                result = new Uri(combined);
            }

            if (!string.IsNullOrEmpty(_baseUrl) && Uri.TryCreate(_baseUrl, UriKind.Absolute, out Uri? baseUri))
            {
                if (!string.Equals(result.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Security: Target host '{result.Host}' does not match base URL host '{baseUri.Host}'.");
                }
            }

            return result;
        }

        private static string FormatHttpError(string verb, HttpResponseMessage response)
        {
            string msg = $"{verb} failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            System.Diagnostics.Trace.TraceError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PBWebDAV] {msg}");
            return msg;
        }

        /// <summary>
        /// Safely bridges async → sync across the COM/STA boundary.
        ///
        /// Running the async delegate on a thread-pool thread via <c>Task.Run</c>
        /// means it has no ambient synchronisation context, so awaits inside do not
        /// attempt to marshal back to the calling STA thread — eliminating the
        /// deadlock that would otherwise occur with <c>.Result</c> or
        /// <c>.GetAwaiter().GetResult()</c> called directly on the STA thread.
        /// </summary>
        private static T RunSync<T>(Func<Task<T>> func)
            => Task.Run(func).GetAwaiter().GetResult();
    }
}
