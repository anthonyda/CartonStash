# 001 – Package search: Design

**Status:** Approved
**Requirements:** [requirements.md](requirements.md)

## Overview

Search goes through `IPackageService.SearchAsync`, implemented by `WinGetPackageService` on the
WinGet COM API. It queries a **composite catalog** (all remote sources merged with the local
installed catalog), so one call returns both the available and the installed version (R2.x).
`SearchViewModel` owns the query, results and UI state. `MainWindow` binds to it.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `PackageInfo` | Immutable row data: `IsInstalled`, `HasUpdate` | exists |
| Core/Services | `IPackageService`, `WinGetPackageService` | Composite catalog connect, find, map to `PackageInfo` | exists |
| Core/ViewModels | `SearchViewModel` | Query, `SearchCommand` (async, cancelable), results, error, empty state | extend |
| App | `MainWindow` | Search box, button, progress, InfoBar, results list | extend |

## WinGet API usage

1. `PackageManager.GetPackageCatalogs()` → every reference added to `CreateCompositePackageCatalogOptions.Catalogs`,
   with `CompositeSearchBehavior.RemotePackagesFromAllCatalogs`.
2. `CreateCompositePackageCatalog(options).ConnectAsync()`; `ConnectResultStatus != Ok` → `WinGetException`.
3. `FindPackagesAsync` with `ResultLimit = 50` (R1.3) and one selector:
   `PackageMatchField.CatalogDefault` + `ContainsCaseInsensitive` (name / ID / moniker, R1.1).
4. Per match: `CatalogPackage.DefaultInstallVersion` → available version + source;
   `CatalogPackage.InstalledVersion` → installed version;
   `CatalogPackage.IsUpdateAvailable` → `PackageInfo.UpdateAvailable` (R2.2).
   *Amended during implementation:* WinGet decides "update available" (constitution P1). Comparing version
   strings ourselves gets `2.10` vs `2.9` and `Unknown` wrong.
5. Status → message mapping (R3.4): `CatalogError` → "Couldn't reach the package sources…",
   `SourceAgreementsNotAccepted` → "accept source agreements…", `BlockedByPolicy` → "blocked by policy…",
   anything else → "WinGet reported *status*". The HRESULT stays on `WinGetException.HResult` for logs, not in the UI.

Collections are read by index (constitution W2).

**Later improvement:** cache the connected composite catalog for the app's lifetime instead of
reconnecting on every search. Measure first.

## Data flow

Enter / Search → `SearchCommand` (RelayCommand, `AllowConcurrentExecutions = true`; the view model keeps its own
`CancellationTokenSource`) → cancel the previous run (R3.2) → `SearchAsync(query, ct)` → if this run was superseded,
drop its results → otherwise results replace `Results` → state becomes Results / Empty / Error.
`IsSearching` drives the progress bar (`SearchCommand.IsRunning` would flicker between overlapping runs).

## UI

```
[ Search packages                                   ]
[ Name, ID or moniker…                 ] [ Search ]
[==== progress (while running) ====]
[ InfoBar: error (R3.4)                          ]
┌──────────────────────────────────────────────────┐
│ Git                    2.56.0.3   Installed ✓     │
│ Git.Git                winget                     │
│ GitHub CLI             2.80.0     Update ↑        │
└──────────────────────────────────────────────────┘
        "No packages found for 'xyz'" (R3.3)
```

- Search box `AutomationProperties.Name="Search packages"`; results list `AutomationProperties.Name="Search results"` (R3.5).
- Badges: "Installed" (R2.1) and "Update available" (R2.2) use theme resources so they work in high contrast.

## Error handling and edge cases

- Source unreachable / agreements not accepted → `WinGetException` → InfoBar message (R3.4).
- Cancellation is silent: no error is shown.
- Packages with version "Unknown" are shown as-is; `HasUpdate` is `IsInstalled && UpdateAvailable` (WinGet's flag).

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.2, R3.2, R3.3, R3.4 | Unit tests on `SearchViewModel` with a fake `IPackageService` |
| R2.1, R2.2 | Unit tests on `PackageInfo.IsInstalled` / `HasUpdate` |
| R1.1, R1.3, R1.4 | Integration test `WinGetPackageServiceIntegrationTests` + manual check |
| R3.1, R3.5 | Manual check (progress visible; Tab order + Narrator) |

## Risks and alternatives considered

- **Parsing `winget.exe` output:** rejected. The output is unstable, localized and slow.
- **`Microsoft.WinGet.Client` PowerShell module:** rejected. It needs a PowerShell host and adds overhead.
- **Composite catalog connect latency (~1–3 s cold):** acceptable for v1; cache later if measured slow.
