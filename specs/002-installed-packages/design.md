# 002 – Installed packages: Design

**Status:** Approved (2026-10-29), amended during implementation
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
| Core/Services | `InstalledPackagesStore` | Owns the loaded list: load once, reload, error. Shared with 004. *Amended:* introduced here instead of being extracted in 004 T2 | new |
| Core/ViewModels | `InstalledViewModel` | `RefreshCommand`, `Filter`, `Visible` (filtered + sorted view), counts, error + retry, over the store | new |
| Core/ViewModels | `ShellViewModel` | Navigation items, current page | new |
| App | `MainWindow` → `NavigationView` + `Frame`; `SearchPage`, `InstalledPage` | Pages are cached (`NavigationCacheMode.Required`) for R4.2 | changed / new |
| App | Page map in `MainWindow` | Maps nav keys → `Page` types. *Amended:* a dictionary; a separate service wasn't worth it | new |

## WinGet API usage

- Local-first composite: `CreateCompositePackageCatalogOptions` with all remote catalogs and
  `CompositeSearchBehavior = LocalCatalogs`. Remote correlation fills `DefaultInstallVersion` and `IsUpdateAvailable`.
  *Amended during implementation:* a composite always includes the installed catalog. Adding
  `GetLocalPackageCatalog(InstalledPackages)` explicitly makes `FindPackagesAsync` fail with `InvalidOptions`.
- `FindPackagesAsync(new FindPackagesOptions())` with **no selectors** returns all installed packages.
- Source: `DefaultInstallVersion?.PackageCatalog.Info.Name`. If null, the package is unmanaged (R3.1). Search uses the same mapping.
- Read collections by index (W2). Status → message mapping is reused from 001.

## Amendment R6: one row per installed program (approved 2026-10-30)

**Two catalogs, joined in Core/Services.** `GetInstalledAsync` connects, in parallel:

1. **Installed-only catalog** `GetLocalPackageCatalog(InstalledPackages)` (not composite): one `CatalogPackage` per installed
   program, IDs like `ARP\Machine\X64\{GUID}` or `MSIX\<PackageFullName>`. Read per entry: local ID, name, installed version,
   `InstalledVersion.ProductCodes`, `InstalledVersion.PackageFamilyNames`.
2. **Local-first composite** (as today): WinGet's matched packages. Read per package: WinGet ID, source, available version,
   `IsUpdateAvailable`, the installed version's product codes / PFNs (which program WinGet evaluated), and the source
   manifest's `DefaultInstallVersion.ProductCodes` / `PackageFamilyNames`.

A pure, unit-tested `InstalledJoin.Join(entries, packages)` (plain records in, `PackageInfo` out) builds the rows:

- **Primary match:** the entry whose local ID equals the composite package's `InstalledVersion.Id` → managed row with
  WinGet ID, source, available version and update flag (R6.3, R6.4). *(T9: `InstalledVersion.Id` is the installed entry's
  local ID for all 330 composite packages on the dev machine, so this is an exact link, no code matching needed.)*
- **Manifest match:** any entry not claimed above whose product code / PFN appears in a package's *manifest*
  (`DefaultInstallVersion.ProductCodes` / `PackageFamilyNames`) → managed row with that WinGet ID and source, **no update flag** (R6.4).
- **No match:** unmanaged row ("Not managed by WinGet"), as today (R2.4 hides it by default).
- Codes compare case-insensitively; product codes without braces are normalised.
- Fallback: a composite package whose `InstalledVersion.Id` matches no entry (not seen in T9) keeps today's single row, so
  nothing WinGet reports can disappear.

**Model.** `PackageInfo` gains `InstalledId` (the installed entry's local ID) and `IsPrimaryMatch`. A row's identity becomes
`Key = InstalledId ?? Id`. The operation queue dedupes and rows track operations by `Key`, so two rows sharing `Microsoft.WSL`
don't share progress or block each other.

**Uninstall (R6.5).** When `InstalledId` is set, the installer finds the program in the installed-only catalog by that local ID
and uninstalls that `CatalogPackage` (WinGet supports uninstalling installed-catalog entries, like `winget uninstall --id ARP\...`).
Upgrade still goes through the WinGet ID and is only offered on the primary match.

**Updates page (004)** keeps listing rows with an update flag, so only primary matches: unchanged.

**Cost.** One more catalog connect and ~362 cross-process reads at load (plus manifest codes for ~90 managed packages).
*T9 measured:* installed-only catalog 362 entries read in ~0.5 s; composite with codes ~2.2 s (incl. connect). Run in parallel. Still loaded once per session in the background (R1.3).

## Amendment R7: version badge colors (approved 2026-10-30)

- New enum `VersionStatus { Unknown, Same, Older, Newer }` on `PackageInfo` (default `Unknown`).
- Computed in Core/Services when the list is read, with WinGet's comparer:
  `DefaultInstallVersion.CompareToVersion(InstalledVersion.Version)` → `Greater` = the source is newer (installed is **Older**),
  `Equal` = **Same**, `Lesser` = **Newer**, `Unknown` = **Unknown** (P1: WinGet decides).
  Only WinGet's own match is compared, using WinGet's installed version (`InstalledVersion.Version` from the composite,
  now carried in `MatchedPackage.InstalledVersion` and also shown on the row, R6.2). Manifest-matched rows stay `Unknown`:
  T16 showed raw MSIX versions (`8000.994.2142.0`) compared with product versions (`1.8.10`) read as "newer", which is
  meaningless.
- Badge color (App): `Same` → success brush; `Older` **and** `HasUpdate` → caution brush; `Newer` → informational brush;
  anything else (incl. `Older` without an offered update) → neutral. `PackageRowViewModel` exposes `VersionBadgeText`
  ("Installed 2.5.6") and `VersionStatusText` ("newer than winget's 2.5.5") for the tooltip and accessible name.
- Cost: one extra cross-process call per managed row at load (~120 here); measured during implementation.

## Amendment R8: status filter chips (approved 2026-10-30)

- `InstalledViewModel` gains `StatusFilter` (`VersionBadge?`, null = All) and four counts (`SameCount`, `UpdateCount`,
  `NewerCount`, `OtherCount`). `ApplyFilter()` applies text (R2.1) + unmanaged (R2.4) first, computes the counts from that
  set (R8.3), then applies `StatusFilter` (R8.2). In memory only, no WinGet call. `CountText` (R2.2) counts the final rows.
- Row state is `PackageRowViewModel.VersionBadge` (R7), so chips and badges can't disagree. Gray "Other" = `Neutral`.
- `InstalledPage`: a horizontal row of `ToggleButton`s styled as pills under the filter box: colored dot (same theme
  brushes as the R7 badge) + label + count. Exactly one can be checked; clicking the checked one clears it. Count 0 → dimmed
  (still focusable). Tooltip + `AutomationProperties.Name` = "Newer: 31 programs newer than the version WinGet's source offers" (R8.4).
- The chip dots use the same brushes as the badge borders (system Success / Caution brushes and the app's
  `VersionNewerBorderBrush` in `App.xaml`). A selected chip uses a neutral fill and outline, set per theme, not the accent color.

## Data flow

Navigate to Installed → first visit triggers `LoadCommand` → `GetInstalledAsync` → `_all` list →
`ApplyFilter()` rebuilds `Visible` (sorted by name, R2.3) → counts. Filter changes call `ApplyFilter()` only (R2.1). `ShowUnmanaged` (default false, kept for the session) is part of the same in-memory filter (R2.4).
Queue `Drained` (spec 003), if any operation succeeded → `Invalidate()` + one reload (R1.3).
*Amended:* the store also starts loading at app start (in the background) so the 004 Updates badge can appear.
It is still the session's single load.

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

- **R6 match quality** depends on manifests listing product codes / PFNs. Programs WinGet merged but whose manifest lists
  nothing fall back to "Not managed by WinGet" (still visible with the R2.4 option). Better than showing a wrong version.
- **R6 alternative rejected:** comparing names / publishers to guess matches (constitution P1: WinGet decides, we don't guess).

- **Loading time** for the local-first composite with remote correlation (a few seconds cold). Mitigation:
  load once and cache (R1.3). Alternative: local-only first, then correlate in the background. Rejected for v1 as too complex.
- **Shell refactor touches spec 001's window.** Acceptable: SearchPage keeps the same view model and behaviour.
