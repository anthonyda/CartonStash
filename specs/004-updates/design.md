# 004 – Updates: Design

**Status:** Draft
**Requirements:** [requirements.md](requirements.md)

## Overview

The Updates page is a filtered view over the installed data loaded in 002 (`HasUpdate == true`). Upgrades are a
third operation kind in the 003 queue. The main unknown is R3 (explicit targeting), which needs an
investigation task before the design is final.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `InstalledPackagesStore` | Extracted from `InstalledViewModel`: owns the loaded installed list, `ReloadAsync`, `Changed` event. Shared by Installed and Updates (R1.2) | new (refactor of 002) |
| Core/Models | `PackageInfo` | add `RequiresExplicitUpgrade`, `ExplicitUpgradeReason` | changed |
| Core/Services | `IPackageInstaller.UpgradeAsync` | `UpgradePackageAsync` with `InstallOptions` (silent, scope from Settings) | new method |
| Core/Services | `PackageOperationQueue` | `OperationKind.Upgrade`; `EnqueueMany` | changed |
| Core/ViewModels | `UpdatesViewModel` | Filtered list, selection, Update / Update selected / Update all, count, empty state | new |
| Core/ViewModels | `ShellViewModel` | `UpdatesCount` for the nav badge (R4.1) | changed |
| App | `UpdatesPage` | Checkbox list (`SelectionMode="Multiple"`), command bar, confirmation dialog | new |

## WinGet API usage

- Data: `CatalogPackage.IsUpdateAvailable`, `InstalledVersion.Version`, `DefaultInstallVersion.Version`.
- Upgrade: `PackageManager.UpgradePackageAsync(pkg, InstallOptions)` → same progress / result mapping as install.
  Extra status `NoApplicableUpgrade` → "No applicable update for this PC".
- **Investigation for R3 (T1), done 2026-10-29:** reflected over the ComInterop 1.29.380 projection.
  There are **no pin-related types or members**, and no `RequireExplicitUpgrade`. The only `Explicit` members are on
  *catalogs* (`IPackageCatalogInfo2.Explicit`, `I{Add,Edit}PackageCatalogOptions.Explicit`), meaning "source
  excluded from default searches". That's relevant to spec 006, not here.
  **R3 cannot be met through the COM API.** Options for review (see requirements, Open questions):
  - **A. Drop R3.** "Update all" queues every package WinGet reports. WinGet itself then refuses pinned ones, and
    the per-package result explains why. Simplest and fully within the constitution.
  - **B. Constitution exception:** read `winget pin list` output in one isolated, tested adapter to mark pinned
    packages. Covers pins but not manifest `RequireExplicitUpgrade` (the likely Miniconda case).
  - **C. Defer R3** until a WinGet release exposes this in the COM API. Ship 004 as option A in the meantime.
  *Recommendation: C (= A now, revisit later).*

## Data flow

`InstalledPackagesStore.Changed` → `UpdatesViewModel` rebuilds `Updates = all.Where(HasUpdate)` (sorted by name)
→ `ShellViewModel.UpdatesCount`. Update all → confirm (n packages, excluding explicit ones) → `queue.EnqueueMany`
→ each completion triggers a store reload (debounced to once per queue drain, so updating 20 packages doesn't reload 20 times).

## UI

```
Updates (3)                    [Update selected] [Update all]
☐ Git           2.56.0.3 → 2.57.0     winget     [Update]
☐ Miniconda3    py312… → py314…  ⚠ Manual update only  [Update]
☐ 7-Zip         24.08 → 25.01         winget     [Update]
```

## Error handling and edge cases

- Upgrade where the old version can't be found or the installer type changed → the WinGet result maps to the 003 messages.
- A package updated outside the app while the page is open → stale until Refresh. Acceptable (P1 says we re-read, not guess).
- Reload debounce: one reload after the queue is empty, plus the per-package UI state from the queue in the meantime.

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.1, R2.2–R2.4, R3.2, R4.1, R5.1 | Unit tests on `UpdatesViewModel` + `InstalledPackagesStore` with fakes |
| R2.1 | Manual: update one outdated package |
| R3.1 | Manual with `Anaconda.Miniconda3` (if T1 finds an API) |

## Risks and alternatives considered

- R3 may be impossible through the COM API. Handled by the T1 investigation and a possible amendment before implementation.
