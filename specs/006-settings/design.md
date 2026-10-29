# 006 – Settings: Design

**Status:** Approved (2026-10-29), amended during implementation
**Requirements:** [requirements.md](requirements.md)

## Overview

A `SettingsService` persists a small `AppSettings` record as JSON. A `SettingsViewModel` binds the page.
Source information comes from the WinGet service. Theme is applied by setting `RequestedTheme` on the window's
root element and updating the title-bar button colours.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `AppSettings` | `Theme` (System/Light/Dark), `InstallScope` (Any/User/System), `StartPage` (Search/Installed/Updates, R5) | new; `StartPage` added by amendment R5 |
| Core/Models | `SourceInfo` | Name, Type, Argument (URL), TrustLevel, Explicit, LastUpdated, AgreementsPending | new |
| Core/Services | `ISettingsStore` + `JsonSettingsStore` | Load / save `%LOCALAPPDATA%\CartonStash\settings.json` (*amended 2026-10-30:* folder renamed with the app; the old `WinGetFrontend` file is copied once); tolerant of missing or corrupt files (fall back to defaults, keep a `.bak`) | new |
| Core/Services | `ISettings` (singleton) | Current settings + `Changed` event; used by the 003 installer for scope (R2.1) | new |
| Core/Services | `IPackageService` | `GetSourcesAsync`, `RefreshSourceAsync(name)` (*amended:* one source per call; the view model loops and reports "n of m"), `GetWinGetVersion()`, `AcceptSourceAgreements(name)` | new methods |
| Core/ViewModels | `SettingsViewModel` | Theme, scope, start page (R5), sources, refresh command, versions, diagnostic text | new |
| App | `SettingsPage` | `SettingsCard`-style layout (from WinUI Gallery patterns), `NavigationView` settings item | new |
| App | `ThemeService` | Applies `ElementTheme` + title-bar colours (R1.1) | new |
| App | `IClipboardService` | Copies the diagnostic text (R4.2) | new |

## Start page (amendment R5, approved 2026-10-30)

- `AppSettings.StartPage` is a new enum `StartPage { Search, Installed, Updates }`, default `Search`. Stored by name in the
  same JSON file (`"StartPage": "Updates"`).
- Older settings files have no `StartPage`: System.Text.Json leaves the record's default (`Search`), so no reset notice (R5.4).
  An unknown value (`"StartPage": "Purple"`) counts as a corrupt file, like an unknown theme today.
- `SettingsViewModel.StartPageIndex` binds a ComboBox under **Appearance → Start page** and calls `ISettings.Update` (saved at once).
  Nothing navigates when it changes (R5.3).
- `MainWindow` reads `ISettings.Current.StartPage` once at startup, selects the matching `NavigationViewItem` and navigates the
  frame to that page (R5.2). Today it always opens Search.
- The installed list still preloads in the background at startup (004 badge), so opening on Installed or Updates shows the
  progress bar briefly, then the list. No extra WinGet calls.

## WinGet API usage

- Sources: `GetPackageCatalogs()` → per reference `Info` (`Name`, `Type`, `Argument`, `TrustLevel`, `LastUpdateTime`,
  `Explicit` via `IPackageCatalogInfo2`). Read by index (W2).
- Refresh (R3.2): `PackageCatalogReference.RefreshPackageCatalogAsync()` (`IPackageCatalogReference7`) per source,
  sequentially, with progress "*n* of *m*". Afterwards `CatalogConnection.Invalidate()`.
- Agreements (R3.3): `PackageCatalogReference.SourceAgreements`. Accepting sets `AcceptSourceAgreements = true`
  on the references used by `CatalogConnection` for that session. This replaces 001's advice to run `winget search` in a terminal.
  *Amended:* the COM API doesn't say whether a source's agreements were already accepted, so every source that has
  agreements shows an **Agreements** button (Accept / Decline). The connect error now points to Settings → Sources.
- Versions (R4.1): `PackageManager.Version` (`IPackageManager7`). App version comes from the assembly informational version.

## Data flow

Settings change → `ISettings.Update(...)` → save JSON → `Changed` → `ThemeService` applies the theme; the installer
reads the scope on its next operation. Settings page load → `GetSourcesAsync` + versions.

## UI

```
Settings
 Appearance       Theme                 [ Use system setting ▾ ]
                  Start page            [ Search ▾ ]            (R5: Search / Installed / Updates)
 Installing       Default install scope [ Let WinGet decide ▾ ]
                  ⓘ "All users" may show a UAC prompt for each install.
 Sources          winget     Microsoft.PreIndexed.Package   Trusted   updated 2 h ago
                  msstore    Microsoft.Rest                 Trusted   updated 2 h ago
                  [ Refresh sources ]
 About            CartonStash 0.1.0 · WinGet 1.29.380   [ Copy diagnostic info ]
```

## Error handling and edge cases

- Corrupt settings file (bad JSON or unknown enum values) → defaults + `.bak` copy + a one-time InfoBar "Settings were reset".
- Saves write a temp file and then move it into place, so a crash mid-save can't leave a half-written file.
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
