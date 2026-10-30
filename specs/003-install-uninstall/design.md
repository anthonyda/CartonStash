# 003 – Install / uninstall with progress: Design

**Status:** Approved (2026-10-29), amended during implementation
**Requirements:** [requirements.md](requirements.md)

## Overview

All changes to the system go through one **operation queue** (`PackageOperationQueue`), a singleton that
runs one WinGet operation at a time (R4.1) and publishes progress as plain models. Spec 004 adds Upgrade
to the same queue. View models observe the queue; they never call WinGet directly.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `PackageOperation` (observable) | `Kind` (Install/Uninstall/Upgrade), `Package`, `Phase`, `Percent`, `Result`, `Message`, `CanCancel` | new |
| Core/Models | `OperationPhase`, `OperationResult` | Enums: Queued, Downloading, Running, Finishing, Done · Succeeded, Failed, Cancelled, SucceededRestartRequired | new |
| Core/Services | `IPackageInstaller` | `RunAsync(kind, PackageInfo, acceptAgreements, IProgress<OperationProgress>, ct)` → `OperationOutcome`; `GetAgreementsAsync(PackageInfo)`. *Amended:* one method for all kinds, so the queue doesn't switch on kind | new |
| Core/Services | `WinGetPackageInstaller` | COM implementation (below) | new |
| Core/Services | `PackageOperationQueue` | FIFO + single worker, dedupe by package ID (R4.2), `Enqueue`, `Cancel`, `Retry`, `Operations`, `Completed` event, `HasPendingWork` | new |
| Core/ViewModels | `ActivityViewModel` | Binds `Operations`; clear finished | new |
| Core/ViewModels | `PackageActions` | Confirm uninstall, show agreements, then enqueue. Shared by rows, details pane, Updates. *Amended:* new class | new |
| Core/ViewModels | `PackageRowViewModel` | Row = `PackageInfo` + its latest `PackageOperation`; Install / Uninstall / Update commands and visibility. *Amended:* rows are view models, kept current from the queue's `Enqueued` event | new |
| App | `PackageRow` | Action button area: Install / Uninstall / in-progress ring + Cancel | changed |
| App | Activity panel | `NavigationView` footer item "Activity (n)" opens a right-side overlay `SplitView` pane listing operations. Selecting the item (click, keyboard or UIA) opens it and keeps the current page selected | new |
| App | `IDialogService` | Confirm uninstall (R2.2), show agreements (R1.3), confirm close (R6.2) | new |

## WinGet API usage

- **Get the CatalogPackage again:** connect to the composite catalog (002 `CatalogConnection`) and
  `FindPackagesAsync` with `PackageMatchField.Id` + `Equals`, filtered to the row's source. COM objects never
  leave the service (A2).
- **Install:** `PackageManager.InstallPackageAsync(pkg, new InstallOptions { PackageInstallMode = Silent,
  PackageInstallScope = settings.Scope, AcceptPackageAgreements = userAccepted })`.
  Progress events: `InstallProgress.State` (Queued / Downloading / Installing / PostInstall / Finished),
  `DownloadProgress` and `InstallationProgress` (0–1) → `OperationProgress`.
- **Agreements (R1.3):** `DefaultInstallVersion.GetCatalogPackageMetadata().Agreements`. If there are any, show them
  first. Result `PackageAgreementsNotAccepted` is a safety net.
- **Uninstall:** `UninstallPackageAsync(pkg, new UninstallOptions { PackageUninstallMode = Silent })` with `UninstallProgress`.
- **Cancel (R3.2):** cancelling the `IAsyncOperationWithProgress` (via `AsTask(ct, progress)`). Allowed only while
  the state is Queued or Downloading.
- **Result mapping (R5.2, R5.3):** `InstallResultStatus` (`Ok`, `DownloadError`, `InstallError` + `InstallerErrorCode`,
  `BlockedByPolicy`, `NoApplicableInstallers`, `PackageAgreementsNotAccepted`, …) → readable message.
  `RebootRequired` → `SucceededRestartRequired`.
- **Elevation (R6.1):** WinGet starts the installer, and the installer requests UAC itself. The app never elevates.

## Data flow

Row button → VM command → (confirm / agreements dialog) → `queue.Enqueue(op)` → worker → installer service →
progress updates the `PackageOperation` on the UI thread (via `SynchronizationContext` captured at startup)
→ outcome → `Completed` → `CatalogConnection.Invalidate()` + Installed reload + InfoBar / Activity entry.

## UI

- Row action area (right edge): `[Install]` → `◔ Downloading 42% [✕]` → `◔ Installing…` → `✓ Installed`.
- Activity pane: one line per operation with phase, percent, result message, Retry / Clear.
- App close with pending work → `ContentDialog`: "2 operations are still running. Wait / Quit anyway" (R6.2).

## Error handling and edge cases

- The same package already queued → command disabled, row shows its state (R4.2).
- Package gone from the row's source (removed, or sources changed since the list loaded) → the operation fails with "WinGet can
  no longer find the package", even if another source has the same ID. The app never swaps in a source the user didn't choose (R1.2).
- Hash mismatch (`DownloadError` with `0x8A150011`): seen live when a download host served an HTML page. Mapped to its own
  message ("doesn't match the package's checksum") instead of blaming the connection.
- Installer hangs: no timeout in v1. Cancel is unavailable once the installer is running, as WinGet documents. Noted as a risk.
- The app crashes mid-operation: WinGet's server keeps running the installer. On next start the Installed list shows the truth (P1).

## Testing strategy

| Criteria | How |
|----------|-----|
| R4.1, R4.2, R3.2, R5.1, R5.2 | Unit tests on `PackageOperationQueue` with a fake `IPackageInstaller` (scripted progress / outcomes) |
| R2.2, R1.3, R6.2 | Unit tests on VMs with a fake `IDialogService` (accept / decline paths) |
| Result mapping | Unit tests on status → message mapping |
| R1.2, R2.1, R3.1, R6.1 | Manual: install + uninstall a small package (e.g. `JernejSimoncic.Wget`, `7zip.7zip`) |

Integration tests **do not** install software automatically. Real installs are manual checks only.

## Risks and alternatives considered

- **Parallel operations:** rejected. Most installers can't run side by side (MSI mutex), and WinGet serializes anyway.
- **Running the whole app elevated:** rejected (A5).
- **Progress granularity:** some installers report no installation %. Show an indeterminate ring then.
