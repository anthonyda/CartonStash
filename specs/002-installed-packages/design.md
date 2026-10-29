# 002 – Installed packages: Design

**Status:** Approved (2026-10-29)
**Requirements:** [requirements.md](requirements.md)

## Overview

This spec adds the **app shell** (a `NavigationView` with pages), which every later spec builds on,
and the **Installed** page. The service gains `GetInstalledAsync`. WinGet connections are cached in a new
`CatalogConnection` helper, which also fixes the "reconnect on every search" item noted in 001.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `CatalogConnection` (internal) | Lazily connects and caches two composite catalogs: *remote-first* (search) and *local-first* (installed). `Invalidate()` after operations. Thread-safe (`SemaphoreSlim`). | new |
| Core/Services | `IPackageService.GetInstalledAsync(ct)` | All installed packages as `PackageInfo` | new method |
| Core/Models | `PackageInfo` | `Source == ""` means not managed by WinGet (R3.1); add `IsManaged` | changed |
| Core/ViewModels | `InstalledViewModel` | Load once, `RefreshCommand`, `Filter`, `Visible` (filtered + sorted view), counts, error + retry | new |
| Core/ViewModels | `ShellViewModel` | Navigation items, current page | new |
| App | `MainWindow` → `NavigationView` + `Frame`; `SearchPage`, `InstalledPage` | Pages are cached (`NavigationCacheMode.Required`) for R4.2 | changed / new |
| App | `INavigationService` | Maps page keys → `Page` types | new |

## WinGet API usage

- Local-first composite: `CreateCompositePackageCatalogOptions` with all remote catalogs **plus**
  `GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages)`, and
  `CompositeSearchBehavior = LocalCatalogs`. Remote correlation fills `DefaultInstallVersion` and `IsUpdateAvailable`.
- `FindPackagesAsync(new FindPackagesOptions())` with **no selectors** returns all installed packages.
- Source: `DefaultInstallVersion?.PackageCatalog.Info.Name`. If null, the package is unmanaged (R3.1).
- Read collections by index (W2). Status → message mapping is reused from 001.

## Data flow

Navigate to Installed → first visit triggers `LoadCommand` → `GetInstalledAsync` → `_all` list →
`ApplyFilter()` rebuilds `Visible` (sorted by name, R2.3) → counts. Filter changes call `ApplyFilter()` only (R2.1).
`IPackageOperations.Completed` (spec 003) → `CatalogConnection.Invalidate()` + reload (R1.3).

## UI

```
┌────────────┬───────────────────────────────────────────────────┐
│ 🔍 Search   │ Installed                       [⟳ Refresh]       │
│ 📦 Installed│ [ Filter installed packages…  ]   42 of 318       │
│            │ Git            Git.Git        2.56.0.3   winget    │
│            │ Contoso Tool   ARP\…\Contoso  1.2        Not managed│
│ ⚙ Settings │ Miniconda3     Anaconda…      py312…  Update avail. │
└────────────┴───────────────────────────────────────────────────┘
```

- The row template is shared with Search as a `PackageRow` UserControl to avoid duplication.
- Filter box `AutomationProperties.Name="Filter installed packages"`; count uses a LiveRegion so Narrator announces changes.

## Error handling and edge cases

- 300+ packages: `ListView` virtualization; filtering stays in memory, no WinGet calls.
- Unmanaged entries have IDs like `ARP\Machine\X64\{GUID}`. Show them as-is.
- A load still running when the user presses Refresh → cancel it and start again.

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.3, R2.1–R2.3, R3.1, R5.1 | Unit tests on `InstalledViewModel` with fake service |
| R1.1, R1.2 | Integration test: `GetInstalledAsync` contains `Git.Git` with a non-null installed version |
| R4.1, R4.2, R1.4 | Manual (UIA script): switch pages, state kept |

## Risks and alternatives considered

- **Loading time** for the local-first composite with remote correlation (a few seconds cold). Mitigation:
  load once and cache (R1.3). Alternative: local-only first, then correlate in the background. Rejected for v1 as too complex.
- **Shell refactor touches spec 001's window.** Acceptable: SearchPage keeps the same view model and behaviour.
