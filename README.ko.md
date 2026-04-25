# PB-WebDAV

PowerBuilder 2019 R3를 위한 메모리 최적화 WebDAV 클라이언트 라이브러리입니다.  
.NET Framework 4.8 기반의 `System.IO.Pipelines`을 사용하며, COM 듀얼 인터페이스를 통해 직접 .NET 어셈블리 참조 또는 OLE COM 오브젝트로 사용할 수 있습니다.

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

---

## 정보

### 대상 프레임워크

| 항목 | 값 |
|---|---|
| 프레임워크 | .NET Framework **4.8** |
| 런타임 다운로드 | [.NET Framework 4.8](https://dotnet.microsoft.com/ko-kr/download/dotnet-framework/net48) |
| 필요 OS | Windows 7 SP1 / Windows Server 2008 R2 SP1 이상 |
| PowerBuilder | PB 2019 R3 (직접 .NET 어셈블리) 또는 COM 지원 PB 버전 |

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

## 빌드 정보

필요 환경: **Windows** + **.NET SDK** (최신 버전)

```powershell
dotnet restore PB-WebDAV.csproj
dotnet build   PB-WebDAV.csproj -c Release
```

CI/CD 명령어는 [`.github/workflows/`](.github/workflows/)에서 확인하세요.

---

## 라이선스

MIT License — Copyright (c) 2026 김유석(Yu Seok Kim)
