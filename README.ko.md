# PB-WebDAV

PowerBuilder 2019 R3를 위한 메모리 최적화 WebDAV 클라이언트 라이브러리입니다.  
.NET Framework 4.8 기반의 `System.IO.Pipelines`을 사용하며, COM 듀얼 인터페이스를 통해 직접 .NET 어셈블리 참조 또는 OLE COM 오브젝트로 사용할 수 있습니다.

또한 **Native AOT** 빌드(net10.0-windows)도 함께 제공되어, `PBWebDAV.dll`을 .NET 런타임 설치 없이 동작하는 자체 포함 네이티브 공유 라이브러리로 게시하고 C 호출 규약의 `WebDAV_*` 함수를 노출합니다.

*Read this in other languages: [English](README.md)*

[![CI Build](https://github.com/yuseok-kim-edushare/PB-WebDAV/actions/workflows/ci.yaml/badge.svg)](https://github.com/yuseok-kim-edushare/PB-WebDAV/actions/workflows/ci.yaml)

---

## 목적

PowerBuilder 2019 R3에는 WebDAV 기능이 내장되어 있지 않습니다.  
이 라이브러리는 다음 기능을 제공합니다:

- **파일 업로드 / 다운로드** — `System.IO.Pipelines`을 통한 스트리밍 (제로 카피, 풀 기반 버퍼)
- **디렉터리 목록** — PROPFIND Depth:1, 전체 속성 파싱 지원
- **리소스 관리** — DELETE, MKCOL, COPY, MOVE, HEAD
- **인증** — Basic 자격증명 + HTTP 프록시 지원
- **COM 인터op** — 듀얼 인터페이스(`IDispatch` + vtable) 지원
- **Native AOT 익스포트** — 동일한 기능을 자체 포함 네이티브 DLL의 `WebDAV_*` C 함수로 노출하며, PB `LOCAL EXTERNAL FUNCTION` 또는 C/C++에서 호출 가능

---

## 정보

### 대상 프레임워크

이 프로젝트는 동일한 `PB-WebDAV.csproj`에서 `net48`과 `net10.0-windows`를 멀티 타깃으로 빌드합니다.

| 항목 | 값 |
|---|---|
| 프레임워크 (COM/.NET 어셈블리) | .NET Framework **4.8** |
| 프레임워크 (Native AOT) | **.NET 10** (`net10.0-windows`, `PublishAot=true`, `NativeLib=Shared`) |
| 대상 아키텍처 (AOT) | `win-x64`, `win-x86` |
| 런타임 다운로드 (net48) | [.NET Framework 4.8](https://dotnet.microsoft.com/ko-kr/download/dotnet-framework/net48) |
| 필요 OS | Windows 7 SP1 / Windows Server 2008 R2 SP1 이상 |
| PowerBuilder | PB 2019 R3 (직접 .NET 어셈블리, COM, 또는 Native AOT 외부 함수) |

> Native AOT 빌드는 자체 포함(self-contained)이라 .NET 런타임 설치가 필요 없으며, 아키텍처별(`win-x64` / `win-x86`) 네이티브 `PBWebDAV.dll`을 생성합니다 — PowerBuilder/호스트 비트 수에 맞는 것을 선택하세요.

### 주요 의존성

| 패키지 | 용도 |
|---|---|
| `System.IO.Pipelines` 10.0.7 | 풀 기반 세그먼트 I/O |
| `System.Memory` 4.6.3 | `Span<T>` / `Memory<T>` 백포트 |
| `System.Buffers` 4.6.1 | `ArrayPool<T>` |
| `System.Net.Http` (내장) | 모든 WebDAV 동작용 `HttpClient` |

> 릴리스 DLL은 ILRepack으로 **단일 자체 포함 `PBWebDAV.dll`** 로 병합됩니다.

---

## PowerBuilder 사용 방법

### 방법 1 — 직접 .NET 어셈블리 참조 (PB 2019 R3 권장)

1. PowerBuilder IDE → **System Options → .NET Assembly** → `PBWebDAV.dll` 추가
2. 코드 예시:

```powerscript
PBWebDAV.WebDavClient oClient
long                  nCount, i

oClient = CREATE PBWebDAV.WebDavClient
oClient.Initialize("https://dav.example.com/files/", "alice", "s3cr3t")

nCount = oClient.ListDirectory("/documents/")
FOR i = 1 TO nCount - 1   // index 0은 컬렉션(디렉터리) 자신
    MessageBox("항목", oClient.GetItemDisplayName(i) + " / " + &
               String(oClient.GetItemContentLength(i)) + " bytes")
NEXT

oClient.DownloadFile("/documents/report.pdf", "C:\Temp\report.pdf")
DESTROY oClient
```

### 방법 2 — COM / OLEObject (대체 방법)

**관리자 권한**으로 DLL을 먼저 등록합니다:

```bat
:: 32비트 PowerBuilder 2019 R3
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe PBWebDAV.dll /tlb /codebase
```

### 방법 3 — Native AOT 외부 함수 (.NET/COM 런타임 불필요)

비트 수에 맞는 `native-aot/win-x64` 또는 `native-aot/win-x86`의 `PBWebDAV.dll`을 사용하고, 익스포트를 `LOCAL EXTERNAL FUNCTION`으로 선언합니다:

```powerscript
FUNCTION int WebDAV_Initialize(string baseUrl, string username, string password) LIBRARY "PBWebDAV.dll"
FUNCTION int WebDAV_ListDirectory(string remotePath) LIBRARY "PBWebDAV.dll"
FUNCTION int WebDAV_GetItemHref(int index, REF string buffer, int capacity) LIBRARY "PBWebDAV.dll"
FUNCTION int WebDAV_GetLastError(REF string buffer, int capacity) LIBRARY "PBWebDAV.dll"
FUNCTION int WebDAV_DownloadFile(string remotePath, string localPath) LIBRARY "PBWebDAV.dll"
// ... 전체 목록은 아래 Native 익스포트 참조 표 참고
```

```powerscript
long ll_count, i
string ls_buf

WebDAV_Initialize("https://dav.example.com/files/", "alice", "s3cr3t")
ll_count = WebDAV_ListDirectory("/documents/")

FOR i = 1 TO ll_count - 1
    ls_buf = Space(1024)
    WebDAV_GetItemHref(i, ls_buf, Len(ls_buf))
    MessageBox("항목", ls_buf)
NEXT

WebDAV_Destroy()
```

> 문자열을 반환하는 익스포트는 **호출자 버퍼 패턴**을 사용합니다: 미리 크기를 지정한 `Space(n)` 버퍼와 길이를 전달하면, 반환값은 (널 종료 문자 제외) 필요한 길이입니다. Native AOT용 PowerBuilder 동작 샘플은 추후 첨부될 예정입니다.

---

## 항목 접근 API

PowerBuilder 및 COM 환경에서는 관리 클래스(`IWebDavItem`) 반환 타입을 처리할 수 없습니다.  
따라서 항목 속성은 각각 기본 타입을 반환하는 flat getter 메서드로 제공됩니다:

| 메서드 | 설명 |
|---|---|
| `GetItemHref(index)` → `string` | 항목의 Href (index 0 = 컬렉션 자신) |
| `GetItemDisplayName(index)` → `string` | 표시 이름 |
| `GetItemIsCollection(index)` → `bool` | 디렉터리 여부 |
| `GetItemContentLength(index)` → `long` | 파일 크기 (bytes) |
| `GetItemContentType(index)` → `string` | MIME 타입 |
| `GetItemLastModified(index)` → `string` | RFC 1123 최종 수정일 |
| `GetItemETag(index)` → `string` | ETag |
| `GetItemCreationDate(index)` → `string` | ISO 8601 생성일 |
| `GetItemStatusCode(index)` → `int` | HTTP 상태 코드 |

---

## Native 익스포트 참조 (Native AOT)

모든 익스포트는 `NativeExports.cs`에 있으며, PowerBuilder의 Windows 기본 호출 규약인 `Stdcall`과 UTF-16(`wchar_t*` / PB `string`) 파라미터를 사용합니다. `bool` 결과는 `int`(1 = true, 0 = false)로 반환됩니다. 상태는 `WebDAV_Initialize` / `WebDAV_InitializeWithProxy`로 생성되고 `WebDAV_Destroy`로 해제되는 프로세스 전역 싱글턴입니다.

| 익스포트 | 설명 |
|---|---|
| `WebDAV_Initialize(url, user, pass)` → `int` | 싱글턴 클라이언트 생성; 1 = 성공 |
| `WebDAV_InitializeWithProxy(url, user, pass, proxyUrl, proxyUser, proxyPass)` → `int` | 프록시를 통한 연결 |
| `WebDAV_SetTimeout(seconds)` | 기본값(30초) 재정의 |
| `WebDAV_ListDirectory(path)` → `int` | PROPFIND Depth:1; 항목 수 반환, 실패 시 –1 |
| `WebDAV_GetItemCount()` → `int` | 마지막 ListDirectory 결과 개수 |
| `WebDAV_GetItemHref(index, buffer, capacity)` → `int` | 필요 길이 반환; 버퍼가 충분하면 복사 |
| `WebDAV_GetItemDisplayName(index, buffer, capacity)` → `int` | 동일 패턴 |
| `WebDAV_GetItemIsCollection(index)` → `int` | 1 = 디렉터리 |
| `WebDAV_GetItemContentLength(index)` → `long` | 파일 크기(바이트) |
| `WebDAV_GetItemContentType(index, buffer, capacity)` → `int` | MIME 타입 |
| `WebDAV_GetItemLastModified(index, buffer, capacity)` → `int` | RFC 1123 최종 수정일 |
| `WebDAV_GetItemETag(index, buffer, capacity)` → `int` | ETag |
| `WebDAV_GetItemCreationDate(index, buffer, capacity)` → `int` | ISO 8601 생성일 |
| `WebDAV_GetItemStatusCode(index)` → `int` | 해당 propstat 항목의 HTTP 상태 |
| `WebDAV_DownloadFile(remote, local)` → `int` | GET → 로컬 파일 |
| `WebDAV_UploadFile(local, remote)` → `int` | 로컬 파일 → PUT |
| `WebDAV_DeleteItem(path)` → `int` | HTTP DELETE |
| `WebDAV_CreateDirectory(path)` → `int` | HTTP MKCOL |
| `WebDAV_CopyItem(src, dst, overwrite)` → `int` | 서버 측 COPY |
| `WebDAV_MoveItem(src, dst, overwrite)` → `int` | 서버 측 MOVE |
| `WebDAV_ItemExists(path)` → `int` | HEAD 확인 |
| `WebDAV_GetLastError(buffer, capacity)` → `int` | 마지막 호출의 오류 메시지 |
| `WebDAV_GetLastStatusCode()` → `int` | 마지막 호출의 HTTP 상태 |
| `WebDAV_Destroy()` | 싱글턴 클라이언트 해제 |

> 먼저 `capacity = 0`으로 호출해 필요한 버퍼 길이를 조회한 뒤, 그 길이에 맞춘 `Space(n)` 버퍼로 다시 호출하세요.

---

## 빌드 정보

필요 환경: **Windows** + **.NET 10 SDK** (동일한 csproj에서 `net48`과 `net10.0-windows`를 멀티 타깃으로 빌드)

```powershell
# 복원
dotnet restore PB-WebDAV.csproj

# COM / .NET 어셈블리 빌드 (net48)
dotnet build PB-WebDAV.csproj -c Release -f net48
# 출력: bin\Release\net48\PBWebDAV.dll

# Native AOT 빌드 (net10.0-windows), 아키텍처별로 각각 실행
dotnet publish PB-WebDAV.csproj -c Release -f net10.0-windows -r win-x64
dotnet publish PB-WebDAV.csproj -c Release -f net10.0-windows -r win-x86
# 출력: bin\Release\net10.0-windows\{win-x64|win-x86}\publish\PBWebDAV.dll
```

CI/CD 명령어는 [`.github/workflows/`](.github/workflows/)에서 확인하세요. 릴리스 압축 파일(`PBWebDAV-{version}.zip`)의 구성:

```
net48/               ILRepack으로 병합된 COM/.NET 어셈블리 DLL + PBWebDAV.tlb
native-aot/win-x64/  Native AOT 자체 포함 공유 라이브러리 (x64)
native-aot/win-x86/  Native AOT 자체 포함 공유 라이브러리 (x86)
```

---

## 트러블슈팅 및 로그 확인

개발, 배포 혹은 운영 중에 원인 불명의 연결 실패나 파일 무결성 오류가 발생할 경우, 윈도우 이벤트 뷰어를 확인하거나 관리자 권한을 얻을 필요가 없습니다. 

본 WebDAV 클라이언트는 오류나 경고가 발생할 때 자동으로 다음 경로에 일자별 기록을 남깁니다.
- **`C:\Temp\pb-webdav-dll.yyyy-MM-dd.log`** (예: `C:\Temp\pb-webdav-dll.2026-04-25.log`)

지원 팀이나 다른 파트의 개발자와 소통할 때, 해당 날짜의 텍스트 로그 파일만 공유 받아 첨부하면 신속한 문제 파악이 가능합니다.

---

## 라이선스

MIT License — Copyright (c) 2026 김유석(Yu Seok Kim)
