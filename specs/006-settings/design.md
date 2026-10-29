# 006 – Settings: Design

**Status:** Draft
**Requirements:** [requirements.md](requirements.md)

## Overview

A `SettingsService` persists a small `AppSettings` record as JSON. A `SettingsViewModel` binds the page.
Source information comes from the WinGet service. Theme is applied by setting `RequestedTheme` on the window's
root element and updating the title-bar button colours.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `AppSettings` | `Theme` (System/Light/Dark), `InstallScope` (Any/User/System) | new |
| Core/Models | `SourceInfo` | Name, Type, Argument (URL), TrustLevel, Explicit, LastUpdated, AgreementsPending | new |
| Core/Services | `ISettingsStore` + `JsonSettingsStore` | Load / save `%LOCALAPPDATA%\WinGetFrontend\settings.json`; tolerant of missing or corrupt files (fall back to defaults, keep a `.bak`) | new |
| Core/Services | `ISettings` (singleton) | Current settings + `Changed` event; used by the 003 installer for scope (R2.1) | new |
| Core/Services | `IPackageService` | `GetSourcesAsync`, `RefreshSourcesAsync(IProgress)`, `GetWinGetVersion()`, `AcceptSourceAgreements(name)` | new methods |
| Core/ViewModels | `SettingsViewModel` | Theme, scope, sources, refresh command, versions, diagnostic text | new |
| App | `SettingsPage` | `SettingsCard`-style layout (from WinUI Gallery patterns), `NavigationView` settings item | new |
| App | `ThemeService` | Applies `ElementTheme` + title-bar colours (R1.1) | new |
| App | `IClipboardService` | Copies the diagnostic text (R4.2) | new |

## WinGet API usage

- Sources: `GetPackageCatalogs()` → per reference `Info` (`Name`, `Type`, `Argument`, `TrustLevel`, `LastUpdateTime`,
  `Explicit` via `IPackageCatalogInfo2`). Read by index (W2).
- Refresh (R3.2): `PackageCatalogReference.RefreshPackageCatalogAsync()` (`IPackageCatalogReference7`) per source,
  sequentially, with progress "*n* of *m*". Afterwards `CatalogConnection.Invalidate()`.
- Agreements (R3.3): `PackageCatalogReference.SourceAgreements`. Accepting sets `AcceptSourceAgreements = true`
  on the references used by `CatalogConnection` for that session. This replaces 001's advice to run `winget search` in a terminal.
- Versions (R4.1): `PackageManager.Version` (`IPackageManager7`). App version comes from the assembly informational version.

## Data flow

Settings change → `ISettings.Update(...)` → save JSON → `Changed` → `ThemeService` applies the theme; the installer
reads the scope on its next operation. Settings page load → `GetSourcesAsync` + versions.

## UI

```
Settings
 Appearance       Theme                 [ Use system setting ▾ ]
 Installing       Default install scope [ Let WinGet decide ▾ ]
                  ⓘ "All users" may show a UAC prompt for each install.
 Sources          winget     Microsoft.PreIndexed.Package   Trusted   updated 2 h ago
                  msstore    Microsoft.Rest                 Trusted   updated 2 h ago
                  [ Refresh sources ]
 About            WinGet Frontend 0.1.0 · WinGet 1.29.380   [ Copy diagnostic info ]
```

## Error handling and edge cases

- Corrupt settings file → defaults + a one-time InfoBar "Settings were reset".
- Source refresh failure → per-source error line, others continue.
- Scope "User" with a package that only has machine installers → WinGet returns `NoApplicableInstallers` →
  the 003 message suggests changing the scope in Settings.

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.2, R2.1, corrupt file | Unit tests on `JsonSettingsStore` (temp dir) and on the installer reading scope via a fake `ISettings` |
| R3.1, R4.1 | Integration test: sources include `winget`; version not empty |
| R3.2 | Integration test (refresh `winget` source succeeds), marked slow |
| R4.2 | Unit test on diagnostic text format |
| R1.1, R2.2, R3.3 | Manual |

## Risks and alternatives considered

- **`ApplicationData.Current` for settings:** unavailable in unpackaged apps (throws). Hence JSON in LocalAppData until 007.
