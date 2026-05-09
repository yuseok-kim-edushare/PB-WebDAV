# PB-WebDAV

Memory-optimal WebDAV client library for **PowerBuilder 2019 R3**, built on .NET Framework 4.8 with `System.IO.Pipelines`.  
Exposes a COM dual interface so it works as both a direct .NET assembly reference **and** an OLE COM object.

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

---

## Information

### Target Framework

| Item | Value |
|---|---|
| Framework | .NET Framework **4.8** |
| Runtime download | [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) |
| Required OS | Windows 7 SP1 / Windows Server 2008 R2 SP1 or later |
| PowerBuilder | PB 2019 R3 (direct .NET assembly) or any COM-capable PB version |

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

## Build Information

Requirements: **Windows** with **.NET SDK** (any modern version — SDK-style csproj with `net48` TFM).

```powershell
# Restore & build
dotnet restore PB-WebDAV.csproj
dotnet build   PB-WebDAV.csproj -c Release

# Output: bin\Release\net48\PBWebDAV.dll
```

CI/CD commands are in [`.github/workflows/`](.github/workflows/).

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
