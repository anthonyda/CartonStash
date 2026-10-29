# CartonStash

*An app manager for WinGet.*

A native Windows 11 app for searching, installing and updating software through
[WinGet](https://github.com/microsoft/winget-cli), built with WinUI 3 on .NET 10.

## Requirements

- Windows 10 19041+ / Windows 11 with App Installer (WinGet) 1.29+
- .NET 10 SDK
- Visual Studio 2026 with the *WinUI application development* workload (optional; the CLI works too)

## Build and run

```
dotnet build
dotnet run --project src/CartonStash.App
dotnet test
```

## How we work

Features are specified before they are built. See [specs/README.md](specs/README.md).
