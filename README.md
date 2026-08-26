# PB-WebDAV

Memory-optimal WebDAV client library for **PowerBuilder 2019 R3**, built on .NET Framework 4.8 with `System.IO.Pipelines`.  
Exposes a COM dual interface so it works as both a direct .NET assembly reference **and** an OLE COM object.

Also ships a **Native AOT** build (net10.0-windows) that publishes `PBWebDAV.dll` as a self-contained native shared library with C-callable `WebDAV_*` exports — no .NET runtime install required on the target machine.

*다른 언어로 읽기: [한국어](README.ko.md)*

[![CI Build](https://github.com/yuseok-kim-edushare/PB-WebDAV/actions/workflows/ci.yaml/badge.svg)](https://github.com/yuseok-kim-edushare/PB-WebDAV/actions/workflows/ci.yaml)

---

## Purpose

PowerBuilder 2019 R3 has no built-in WebDAV support.  
This library fills the gap by providing:

- **File upload / download** — streamed through `System.IO.Pipelines` (zero-copy, pool-backed, no large intermediate buffers)
- **Directory listing** — PROPFIND Depth:1 with full property parsing (`displayname`, `contentlength`, `lastmodified`, `etag`, `creationdate`, …)
- **Resource management** — DELETE, MKCOL, COPY, MOVE, HEAD (existence check)
- **Authentication** — Basic credentials + optional HTTP proxy
- **COM interop** — Dual interface (`IDispatch` + vtable) so PB `OLEObject` works directly
- **Native AOT exports** — Same functionality exposed as `WebDAV_*` C functions from a self-contained native DLL, callable via PB `LOCAL EXTERNAL FUNCTION` or from C/C++

---

## Information

### Target Frameworks

The project multi-targets `net48` and `net10.0-windows`; both build from the same `PB-WebDAV.csproj`.

| Item | Value |
|---|---|
| Framework (COM/.NET assembly) | .NET Framework **4.8** |
| Framework (Native AOT) | **.NET 10** (`net10.0-windows`, `PublishAot=true`, `NativeLib=Shared`) |
| Runtime identifiers (AOT) | `win-x64`, `win-x86` |
| Runtime download (net48) | [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) |
| Required OS | Windows 7 SP1 / Windows Server 2008 R2 SP1 or later |
| PowerBuilder | PB 2019 R3 (direct .NET assembly, COM, or native AOT external functions) |

> The Native AOT build is self-contained (no .NET runtime install needed) and produces a native `PBWebDAV.dll` per architecture (`win-x64` / `win-x86`) — pick the one matching your PowerBuilder/host bitness.

### Key Dependencies

| Package | Purpose |
|---|---|
| `System.IO.Pipelines` 10.0.7 | Pool-backed segment I/O |
| `System.Memory` 4.6.3 | `Span<T>` / `Memory<T>` back-port |
| `System.Buffers` 4.6.1 | `ArrayPool<T>` |
| `System.Net.Http` (in-box) | `HttpClient` for all WebDAV verbs |

> The release DLL is ILRepacked into a **single self-contained `PBWebDAV.dll`** — no extra files to deploy.

---

## PowerBuilder Usage

### Option 1 — Direct .NET Assembly (Recommended for PB 2019 R3)

1. In PowerBuilder IDE → **System Options → .NET Assembly** → add `PBWebDAV.dll`
2. In code:

```powerscript
PBWebDAV.WebDavClient     oClient
long                      nCount, i

oClient = CREATE PBWebDAV.WebDavClient
oClient.Initialize("https://dav.example.com/files/", "alice", "s3cr3t")

nCount = oClient.ListDirectory("/documents/")
FOR i = 1 TO nCount - 1   // index 0 is the collection itself
    MessageBox("Item", oClient.GetItemDisplayName(i) + " / " + &
               String(oClient.GetItemContentLength(i)) + " bytes")
NEXT

oClient.DownloadFile("/documents/report.pdf", "C:\Temp\report.pdf")
oClient.UploadFile("C:\Temp\data.xlsx", "/documents/data.xlsx")

DESTROY oClient
```

### Option 2 — COM / OLEObject (Fallback)

Register the DLL first (run as **Administrator**):

```bat
:: 32-bit PowerBuilder 2019 R3
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe PBWebDAV.dll /tlb /codebase

:: 64-bit host
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe PBWebDAV.dll /tlb /codebase
```

Then in PowerBuilder:

```powerscript
OLEObject oClient
oClient = CREATE OLEObject
oClient.ConnectToNewObject("PBWebDAV.WebDavClient")

oClient.Initialize("https://dav.example.com/files/", "alice", "s3cr3t")

long nCount
nCount = oClient.ListDirectory("/documents/")

IF nCount < 0 THEN
    MessageBox("Error", oClient.GetLastError())
END IF

DESTROY oClient
```

### Option 3 — Native AOT External Functions (no .NET/COM runtime required)

Use the `native-aot/win-x64` or `native-aot/win-x86` `PBWebDAV.dll` (matching your PB/host bitness) and declare the exports as `LOCAL EXTERNAL FUNCTION`:

```powerscript
FUNCTION int WebDAV_Initialize(string baseUrl, string username, string password) LIBRARY "PBWebDAV.dll"
FUNCTION int WebDAV_ListDirectory(string remotePath) LIBRARY "PBWebDAV.dll"
FUNCTION int WebDAV_GetItemHref(int index, REF string buffer, int capacity) LIBRARY "PBWebDAV.dll"
FUNCTION int WebDAV_GetLastError(REF string buffer, int capacity) LIBRARY "PBWebDAV.dll"
FUNCTION int WebDAV_DownloadFile(string remotePath, string localPath) LIBRARY "PBWebDAV.dll"
// ... see the Native Export Reference below for the full list
```

```powerscript
long ll_count, i
string ls_buf

WebDAV_Initialize("https://dav.example.com/files/", "alice", "s3cr3t")
ll_count = WebDAV_ListDirectory("/documents/")

FOR i = 1 TO ll_count - 1
    ls_buf = Space(1024)
    WebDAV_GetItemHref(i, ls_buf, Len(ls_buf))
    MessageBox("Item", ls_buf)
NEXT

WebDAV_Destroy()
```

> String-returning exports use the **caller-buffer pattern**: pass a pre-sized `Space(n)` buffer and its length; the return value is the required length (excluding the null terminator). A full working PowerBuilder sample for the native AOT path will be added later.

---

## API Quick Reference

### `IWebDavClient`

| Method | Description |
|---|---|
| `Initialize(url, user, pass)` | Connect (anonymous if user/pass empty) |
| `InitializeWithProxy(url, user, pass, proxyUrl, proxyUser, proxyPass)` | Connect via HTTP proxy |
| `SetTimeout(seconds)` | Override 30 s default |
| `ListDirectory(path)` → `int` | PROPFIND Depth:1; returns item count, –1 on error |
| `GetItemCount()` → `int` | Count from last ListDirectory |
| `GetItemHref(index)` → `string` | Href of item at index (0 = collection) |
| `GetItemDisplayName(index)` → `string` | Display name of item at index |
| `GetItemIsCollection(index)` → `bool` | True when item is a directory |
| `GetItemContentLength(index)` → `long` | File size in bytes |
| `GetItemContentType(index)` → `string` | MIME type |
| `GetItemLastModified(index)` → `string` | RFC 1123 last-modified date |
| `GetItemETag(index)` → `string` | ETag value |
| `GetItemCreationDate(index)` → `string` | ISO 8601 creation date |
| `GetItemStatusCode(index)` → `int` | HTTP status of this propstat entry |
| `DownloadFile(remote, local)` → `bool` | GET → local file via pipeline |
| `UploadFile(local, remote)` → `bool` | local file → PUT via pipeline |
| `DeleteItem(path)` → `bool` | HTTP DELETE |
| `CreateDirectory(path)` → `bool` | HTTP MKCOL |
| `CopyItem(src, dst, overwrite)` → `bool` | Server-side COPY |
| `MoveItem(src, dst, overwrite)` → `bool` | Server-side MOVE |
| `ItemExists(path)` → `bool` | HEAD check |
| `GetLastError()` → `string` | Human-readable error from last call |
| `GetLastStatusCode()` → `int` | HTTP status from last call |

> **Note:** `IWebDavItem` and `WebDavItem` are internal implementation details — not exposed to COM or PowerBuilder.
> Use the `GetItemXxx(index)` methods above to access item properties; they return COM-safe primitives only.

---

## Native Export Reference (Native AOT)

All exports live in `NativeExports.cs`, use `Stdcall` (PowerBuilder's default Windows calling convention), and accept UTF-16 (`wchar_t*` / PB `string`) parameters. `bool` results are returned as `int` (1 = true, 0 = false). State is a process-wide singleton created by `WebDAV_Initialize` / `WebDAV_InitializeWithProxy` and released by `WebDAV_Destroy`.

| Export | Description |
|---|---|
| `WebDAV_Initialize(url, user, pass)` → `int` | Create the singleton client; 1 = success |
| `WebDAV_InitializeWithProxy(url, user, pass, proxyUrl, proxyUser, proxyPass)` → `int` | Same, via HTTP proxy |
| `WebDAV_SetTimeout(seconds)` | Override 30 s default |
| `WebDAV_ListDirectory(path)` → `int` | PROPFIND Depth:1; returns item count, –1 on error |
| `WebDAV_GetItemCount()` → `int` | Count from last ListDirectory |
| `WebDAV_GetItemHref(index, buffer, capacity)` → `int` | Required length; copies into buffer if it fits |
| `WebDAV_GetItemDisplayName(index, buffer, capacity)` → `int` | Same pattern |
| `WebDAV_GetItemIsCollection(index)` → `int` | 1 = directory |
| `WebDAV_GetItemContentLength(index)` → `long` | File size in bytes |
| `WebDAV_GetItemContentType(index, buffer, capacity)` → `int` | MIME type |
| `WebDAV_GetItemLastModified(index, buffer, capacity)` → `int` | RFC 1123 date |
| `WebDAV_GetItemETag(index, buffer, capacity)` → `int` | ETag |
| `WebDAV_GetItemCreationDate(index, buffer, capacity)` → `int` | ISO 8601 date |
| `WebDAV_GetItemStatusCode(index)` → `int` | HTTP status of this propstat entry |
| `WebDAV_DownloadFile(remote, local)` → `int` | GET → local file |
| `WebDAV_UploadFile(local, remote)` → `int` | local file → PUT |
| `WebDAV_DeleteItem(path)` → `int` | HTTP DELETE |
| `WebDAV_CreateDirectory(path)` → `int` | HTTP MKCOL |
| `WebDAV_CopyItem(src, dst, overwrite)` → `int` | Server-side COPY |
| `WebDAV_MoveItem(src, dst, overwrite)` → `int` | Server-side MOVE |
| `WebDAV_ItemExists(path)` → `int` | HEAD check |
| `WebDAV_GetLastError(buffer, capacity)` → `int` | Human-readable error from last call |
| `WebDAV_GetLastStatusCode()` → `int` | HTTP status from last call |
| `WebDAV_Destroy()` | Releases the singleton client |

> Call any getter with `capacity = 0` first to query the required buffer length, then re-call with a `Space(n)` buffer sized accordingly.

---

## Build Information

Requirements: **Windows** with **.NET 10 SDK** (multi-targets `net48` and `net10.0-windows` from the same csproj).

```powershell
# Restore
dotnet restore PB-WebDAV.csproj

# COM / .NET assembly build (net48)
dotnet build PB-WebDAV.csproj -c Release -f net48
# Output: bin\Release\net48\PBWebDAV.dll

# Native AOT build (net10.0-windows), one per architecture
dotnet publish PB-WebDAV.csproj -c Release -f net10.0-windows -r win-x64
dotnet publish PB-WebDAV.csproj -c Release -f net10.0-windows -r win-x86
# Output: bin\Release\net10.0-windows\{win-x64|win-x86}\publish\PBWebDAV.dll
```

CI/CD commands are in [`.github/workflows/`](.github/workflows/). Release archives (`PBWebDAV-{version}.zip`) contain:

```
net48/               ILRepack-merged COM/.NET assembly DLL + PBWebDAV.tlb
native-aot/win-x64/  Native AOT self-contained shared library (x64)
native-aot/win-x86/  Native AOT self-contained shared library (x86)
```

---

## Troubleshooting & Logging

When something goes wrong (e.g., connection issues, authentication failures, Server-Side logic errors), you don't need to check PB application event viewers or Windows AD events.

The library automatically generates daily log files tracking all requests, errors, and system warnings in the following location:
- **`C:\Temp\pb-webdav-dll.yyyy-MM-dd.log`** (e.g. `C:\Temp\pb-webdav-dll.2026-04-25.log`)

Whenever you need technical support or want to investigate an issue, simply check or attach this file.

---

## License

MIT License — Copyright (c) 2026 김유석(Yu Seok Kim)  
See [LICENSE](LICENSE) for details.
