# 003 – Install / uninstall with progress: Design

**Status:** Approved (2026-10-29), amended during implementation. Amendment R7 (say when a program is running): done 2026-11-06
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

## Amendment R7: say when a program is running (approved 2026-11-06, done 2026-11-06)

Requirements: [amendment R7](requirements.md#amendment-r7-say-when-a-program-is-running-approved-2026-11-06-done-2026-11-06).

### Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `IRunningPrograms` | `IReadOnlyList<string> FileNamesIn(string folder)`: file names of the user's processes whose executable sits in `folder` or below it. Empty when there are none or the folder is unreadable | new |
| Core/Services | `RunningPrograms` | Implementation: `Process.GetProcesses()`, then each process's path from `QueryFullProcessImageNameW` with `PROCESS_QUERY_LIMITED_INFORMATION` (kernel32 P/Invoke, as in `AppPackage`). A process that can't be opened (protected, or already exited) is skipped. Every `Process` is disposed | new |
| Core/Services | `WinGetPackageInstaller` | Takes `IRunningPrograms`. After a failed update or uninstall of a portable package, reads the install folder and asks `IRunningPrograms` (R7.2). Passes the package name and running files to the result mapping | changed |
| Core/Services | `MapInstallResult`, `MapUninstallResult` | New inputs: package name, extended error (uninstall too), running file names. New messages for R7.1 and R7.2, checked before the generic `InstallError` / `UninstallError` lines | changed |
| App | `App.xaml.cs` | `.AddSingleton<IRunningPrograms, RunningPrograms>()` | changed |

No view, view model or queue changes: the Activity pane and notification already show `OperationOutcome.Message`.

### WinGet API usage

- **In-use codes (R7.1):** `InstallResult.ExtendedErrorCode.HResult` / `UninstallResult.ExtendedErrorCode.HResult` equal to
  `0x8A150101` (`INSTALL_PACKAGE_IN_USE`), `0x8A150103` (`INSTALL_FILE_IN_USE`) or `0x8A150111`
  (`INSTALL_PACKAGE_IN_USE_BY_APPLICATION`). WinGet sets these when the manifest maps the installer's exit code to them
  (`ExpectedReturnCodes`). No extra call.
- **Portable failure (R7.2):** extended error `0x8A150052` (`PORTABLE_INSTALL_FAILED`) on an update, `0x8A150057`
  (`PORTABLE_UNINSTALL_FAILED`) on an uninstall. These codes say the package is portable, so the app doesn't need to read the
  installer type.
- **Install folder (R7.2):** `found.InstalledVersion.GetMetadata(PackageVersionMetadataField.InstalledLocation)`, read from
  the `CatalogPackage` the operation already holds, only after one of the two codes above. WinGet takes it from the program's
  Apps & features entry. For Claude Code that is
  `%LOCALAPPDATA%\Microsoft\WinGet\Packages\Anthropic.ClaudeCode_Microsoft.Winget.Source_8wekyb3d8bbwe`, the folder of the
  locked `claude.exe`. An empty value, a missing `InstalledVersion` or a COM error means no folder: R7.3.

### Messages

`{name}` is `PackageInfo.Name`, `{verb}` is "updated", "installed" or "uninstalled", and `{files}` is the running file names
joined with ", ".

| Case | Message |
|------|---------|
| `0x8A150101` or `0x8A150103` (R7.1) | "{name} is running, so it couldn't be {verb}. Close it, then press Retry." |
| `0x8A150111` (R7.1) | "{name} is in use by another program, so it couldn't be {verb}. Close that program, then press Retry." |
| Portable code, running files found (R7.2) | "{name} is running ({files}), so it couldn't be {verb}. Close it, then press Retry." |
| Portable code, nothing found (R7.3) | Today's message, unchanged: "The installer failed with error code …" |

`Details` (the log's WinGet status, exit code and extended error) is built as it is now and isn't touched (R7.5).

### Data flow

WinGet result → `InstallAsync` / `UninstallAsync` → if the extended error is a portable code: read `InstalledLocation` →
`IRunningPrograms.FileNamesIn(folder)` → `MapInstallResult` / `MapUninstallResult` (pure, unit tested) → `OperationOutcome`.
Nothing is read and no process is enumerated on success or on any other failure (decided: after the failure only).

### Error handling and edge cases

- **Folder matching:** compare full paths, ignoring case, with the folder ending in `\`, so `...\Foo` doesn't match
  `...\FooBar\x.exe`. Paths are normalised with `Path.GetFullPath` first.
- **Links:** WinGet puts a symlink in `WinGet\Links` on PATH. A process started through it reports its real path in the
  package folder, so it still matches.
- **Elevated or other users' processes:** `PROCESS_QUERY_LIMITED_INFORMATION` opens the user's elevated processes too.
  Processes of other users or protected processes that can't be opened are skipped. The message may then fall back to
  R7.3, which is still true.
- **The program exits between the failure and the check:** nothing is found, R7.3. Retry then works.
- **Enumeration throws:** caught in `WinGetPackageInstaller`. The outcome falls back to R7.3, never to an error of its own.
- **Never closing the program (R7.4):** `IRunningPrograms` only reads. It never opens a process with rights to end it.

### Testing strategy

| Criteria | How |
|----------|-----|
| R7.1, R7.2, R7.3 | Unit tests on `MapInstallResult` / `MapUninstallResult`: each code, each kind, with and without running files |
| R7.2 folder matching | Unit tests on the path check (prefix, case, `Foo` vs `FooBar`, trailing `\`) |
| R7.3 fallbacks | Unit test with a fake `IRunningPrograms` that throws, and one that returns nothing |
| R7.5 | Unit test: `Details` is the same string as before the change for the same result |
| `InstalledLocation` | Integration test: for an installed portable package, the metadata is a folder that exists. Skipped when none is installed |
| `RunningPrograms` | Integration test: `FileNamesIn` of the test process's own folder includes its own file name |
| R7.2 end to end, R7.4 | Manual, in the test VM: install an older version of a portable GUI package with `winget install --version`, start it, update it in CartonStash, and check the message names its file and the program stays open. Close it, Retry, and check that it updates |

### Risks and alternatives considered

- **Reading WinGet's log for "Access is denied":** rejected (decided 2026-11-06). The wording can change between WinGet
  versions and languages.
- **Checking for running programs before each update:** rejected (decided 2026-11-06). Many installers close their own
  program, and it would add a step to every operation.
- **`Process.MainModule.FileName` instead of `QueryFullProcessImageNameW`:** rejected. It needs more access rights and
  throws for elevated processes and for 32/64-bit mismatches.
- **A portable program locked by something else (antivirus, Explorer preview):** no running file found, so R7.3 gives
  today's message. That is honest, if not more helpful.

## Amendment R8: all-users MSI packages (approved 2026-11-10)

**Requirements:** amendment R8 in [requirements.md](requirements.md), approved 2026-11-10 with Q8 and Q9 as proposed.

### Components

| Layer | Type | Change |
|-------|------|--------|
| Core/Services | `WinGetPackageInstaller` | `ModeFor`; Default mode for an all-users MSI; the `0x8A150030` arm in both result maps; reports the mode |
| Core/Services | `InstalledScopes` (014) | reused: `MsiContextOf(installed, context)` is the R8.1 check |
| Core/Models | `OperationProgress` | `MayAskPermission` (bool), reported once before WinGet starts |
| Core/Models | `PackageOperation` | `MayAskPermission`, set from progress; `PermissionText` for the card (R8.3); `Mode` from the outcome |
| Core/Models | `OperationOutcome` | `Mode` (`OperationMode.Silent` or `.DefaultForAllUsersMsi`) |
| Core/Services | `OperationLogStore` | the "Mode" line (R8.5) |
| App/Controls | `OperationCard.xaml` | one caption line bound to `PermissionText` while active (theme resource, wraps) |

No new WinGet call: `InstallOptions.PackageInstallMode` and `UninstallOptions.PackageUninstallMode` take `Default`
instead of `Silent`. The COM API has no `SilentWithProgress` for either (measured: `Enum.Parse` failed in the probe
tool); `Default` is what WinGet turns into `/passive` for MSI.

### When Default mode applies (R8.1, R8.2)

```csharp
// 003 R8.1: Default mode (msiexec /passive, so Windows can ask for consent) only when the installed version is an MSI
// product Windows Installer has for all users. Everything else stays Silent (R8.2).
internal static OperationMode ModeFor(OperationKind kind, bool installed, InstallScope? msiContext) =>
    installed && msiContext == InstallScope.System && kind is OperationKind.Upgrade or OperationKind.Uninstall or OperationKind.Install
        ? OperationMode.DefaultForAllUsersMsi : OperationMode.Silent;
```

- `installed` is true for an update, an uninstall, and an install over an installed package (014's going back). A
  first install keeps Silent: Pandoc's all-users MSI installed silently with a UAC prompt in 014's runs, so first
  installs aren't broken.
- `msiContext` is 014's `InstalledScopes.MsiContextOf(found.InstalledVersion, _installContext)`, read off the UI
  thread before the operation starts. For an uninstall, `found` is the row's installed entry (002 R6.5), so the
  product codes are that entry's.
- Every update of an all-users MSI gets it, not only `uninstallPrevious` ones. The manifest's upgrade behaviour isn't
  on the COM API's `PackageVersionInfo`, and the measurement shows `/passive` works for the update itself as well.

### While it runs (R8.3)

Before calling WinGet, the installer reports `new OperationProgress(OperationPhase.Queued) { MayAskPermission = true }`
when Default mode applies. The queue's progress handler sets `operation.MayAskPermission`. The card shows, under the
status, while the operation is active and `MayAskPermission` is set:

> Windows may ask for permission. If nothing appears, look for the shield in the taskbar.

It disappears when the operation finishes. Narrator reads it as part of the card (no live region: the prompt itself
takes focus).

### The `0x8A150030` result (R8.4)

`APPINSTALLER_CLI_ERROR_EXEC_UNINSTALL_COMMAND_FAILED` = `0x8A150030`. A new arm, after the in-use arm (R7) and before
the generic ones, in `MapInstallResult` (for `InstallError`) and `MapUninstallResult` (for `UninstallError`):

> WinGet couldn't uninstall {name} {version} (exit code {code}).

plus, when the exit code is 1603 or 1730 and the operation ran Silent:

> {name} may be installed for all users, which needs administrator rights. Retry, and accept Windows' prompt if it
> appears.

When the operation already ran in Default mode, the hint is left out: the prompt was offered. Retry stays (R5.2).
`{version}` is the installed version, which is what WinGet tried to remove. Retry re-reads the MSI context, so a
package the check missed the first time still gets Silent; the hint is for that case, honest about "may".

### The log (R8.5)

After 014's "Scope" line: `Mode: silent` or `Mode: default, for an all-users MSI (Windows may ask for permission)`.

### Testing strategy

| Requirement | Test |
|-------------|------|
| R8.1, R8.2 | `ModeFor`: each kind × installed or not × `System`, `User`, null (unit) |
| R8.3 | Queue: a `MayAskPermission` progress sets the operation's flag; `PermissionText` only while active; cleared on finish (unit) |
| R8.4 | `WinGetErrorMessageTests`: `0x8A150030` with 1603 and 1730, Silent → with the hint; Default → without; another exit code → without; for install (update) and uninstall; not "the installer failed" (unit) |
| R8.5 | `OperationLogStoreTests`: both "Mode" lines (unit) |
| All | VM: Pandoc 3.12 for all users → Update from the Updates page: one UAC prompt, the card's line, updated, one entry. Uninstall it: one prompt, uninstalled. jq and a per-user Pandoc: no prompt, Silent in the log. Another all-users MSI (PowerShell wix) updated with `/passive`: what it shows besides a progress bar |

### Risks and alternatives considered

- **An MSI with dialogs in `/passive`.** `/passive` shows only a progress bar for standard MSIs, but a package's own
  custom actions could show more. The VM run checks PowerShell's MSI; anything else stays Windows Installer's
  behaviour, and the user can cancel it there.
- **The UAC prompt behind the app.** Windows may flash the shield in the taskbar instead of showing the prompt. R8.3's
  line says where to look.
- **Default for everything, or Silent then retry:** rejected in Q8.
