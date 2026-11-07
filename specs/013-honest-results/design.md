# 013 – Honest operation results: Design

**Status:** Done (2026-11-07). Approved 2026-11-07
**Requirements:** [requirements.md](requirements.md)

## Overview

All three fixes sit where WinGet's result becomes a message: `WinGetPackageInstaller`. R1 and R3 are new arms in the
existing pure mapping functions (`MapInstallResult`, `MapUninstallResult`), checked before the arms that produce
today's wrong text. R2 follows the pattern 004 R6.1 already uses for App Installer: after WinGet says `Ok`, read the
package again, and let a pure function decide whether the outcome stands. Two small model changes carry the results
to the UI: a new `OperationResult.SucceededUnconfirmed` (a warning, R2.2) and a `Retryable` flag on the outcome (no
Retry for R3.1). Nothing changes in the App project except one line in the card's icon switch.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `WinGetPackageInstaller` | R1, R3 arms in `MapInstallResult` / `MapUninstallResult`; R2 re-read after an update (`InstalledAfterUpdateAsync`, `AfterUpdate`) | Changed |
| Core/Services | `ErrorDescriptions` | Windows' text for an HRESULT, or null when Windows has none (R1.1) | New |
| Core/Models | `OperationResult`, `OperationOutcome`, `PackageOperation`, `OperationState` | `SucceededUnconfirmed`; `Retryable`; `OperationState.Unconfirmed`; status text, `IsSuccess`, `CanRetry` | Changed |
| Core/Services | `PackageOperationQueue` | Copies `Retryable` in `Finish` | Changed |
| Core/ViewModels | `ShellViewModel` | `SucceededUnconfirmed` → warning notification | Changed |
| App/Controls | `OperationCard` | `OperationState.Unconfirmed` → the caution icon used for restarts | Changed |

## WinGet API usage

No new calls. The inputs are the ones `InstallAsync` and `UninstallAsync` already read: `InstallResult.Status`,
`InstallerErrorCode`, `ExtendedErrorCode`, and the same for `UninstallResult`. R2 reuses
`CatalogConnection.FindByIdAsync(package.Id, package.Source, localFirst: true, …)` after `connection.Invalidate()`,
and reads `CatalogPackage.InstalledVersion?.Version` and `CatalogPackage.IsUpdateAvailable`, the same fields
`WinGetPackageService` uses to build the Updates list (004 R1.1). So "WinGet still reports an update" means exactly what
the Updates page will show after the refresh.

Error codes, as constants next to the existing ones:

| Constant | Value | Name in WinGet / Windows |
|----------|-------|--------------------------|
| `UpgradeNotSupported` | `0x8A150114` | `APPINSTALLER_CLI_ERROR_INSTALL_UPGRADE_NOT_SUPPORTED` |
| `AppNotFound` | `0x800401F5` | `CO_E_APPNOTFOUND` |
| `FileNotFound` | `0x80070002` | `HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)` |

Why the `ExtendedErrorCode` and not the status for R3.1: WinGet's COM layer turns this refusal into `CatalogError`
(seen 5 times for Miniconda), and the status could change in a later WinGet. The extended error is the specific one.

## Data flow

```
RunAsync
  ├─ InstallAsync / UninstallAsync → Map*Result(status, exitCode, extendedError, …)   R1, R3 decided here
  └─ kind == Upgrade && result == Succeeded
       ├─ App Installer → AfterAppInstallerUpdate (004 R6.1, unchanged)                R2.4
       └─ other         → Invalidate; InstalledAfterUpdateAsync → AfterUpdate          R2.1–R2.3
→ PackageOperationQueue.Finish → PackageOperation (Result, Message, Details, Retryable)
→ card, row, notification, operation log (StatusText)
```

### R1: no exit code

New arm in both mappers, after the in-use arm (R1.3: 003 R7 still wins) and before the generic error arm:

```csharp
InstallResultStatus.InstallError when installerErrorCode == 0 && extendedError != 0
    => Failed(NoExitCodeMessage(kind, extendedError, packageName, ErrorDescriptions.Describe)),
```

`NoExitCodeMessage` is pure and takes the describer as a `Func<int, string?>`, so tests don't depend on Windows'
language:

- `AppNotFound` or `FileNotFound` (R1.2): "WinGet couldn't start a program needed to update EA app: it wasn't found
  (0x800401F5). Repairing or reinstalling EA app from its publisher may fix this." ("to install", "to uninstall" for the
  other kinds.)
- Any other code with a description (R1.1): "The update failed: WinGet reported "Access is denied." (0x80070005)."
- No description: "The update failed: WinGet reported error 0x8A150xyz."

The wording doesn't say the installer never ran. With exit code 0 that's the likely case, but WinGet can also fail
after a successful installer, and the app doesn't guess (P1).

`ErrorDescriptions.Describe(int hresult)` calls `FormatMessageW` (`FORMAT_MESSAGE_FROM_SYSTEM |
FORMAT_MESSAGE_IGNORE_INSERTS`) by `DllImport`, as `RunningPrograms` and `WindowsRegion` already do, and trims the trailing
line break. It returns null when `FormatMessageW` returns 0. That's how WinGet's own `0x8A15…` codes come out: checked
on this PC, `Win32Exception` turns them into "Unknown error (0x8a150114)", which isn't worth showing, and that string
is localized, so matching on it would break. The text is in Windows' display language, like the rest of the system
messages.

### R2: update reported Ok but nothing changed

```csharp
private async Task<InstalledAfterUpdate?> InstalledAfterUpdateAsync(PackageInfo package)   // null: couldn't read
internal static OperationOutcome AfterUpdate(OperationOutcome outcome, string? versionBefore, InstalledAfterUpdate? after)
internal sealed record InstalledAfterUpdate(string? Version, bool UpdateAvailable);
```

`AfterUpdate` returns the outcome unchanged unless `after` isn't null, `after.Version == versionBefore` (ordinal, both
non-null) and `after.UpdateAvailable` (R2.3). Then:

- `Result = SucceededUnconfirmed`
- `Message = "WinGet finished without an error, but Windows still lists Autodesk Fusion 2706.1.14. The installer may still be finishing, or Autodesk Fusion may update itself the next time it starts."`
- `Details` unchanged (R4.1).

`versionBefore` is `PackageInfo.InstalledVersion`, the version the user saw on the row. `InstalledAfterUpdateAsync`
catches every exception and returns null: a failed check never becomes an error (as 003 R7.3).

Only `OperationKind.Upgrade` goes through this (R2.5).

### R3: update not allowed in place

The first arm of `MapInstallResult`, for `kind == Upgrade`, any status but `Ok`:

```csharp
_ when kind == OperationKind.Upgrade && status != InstallResultStatus.Ok && extendedError == UpgradeNotSupported
    => Failed(UpgradeNotSupportedMessage(packageName)) with { Retryable = false },
```

Message: "Miniconda3 can't be updated in place: its publisher asks for it to be uninstalled first. To get the new
version, uninstall it, then install it again." Nothing is queued (R3.2).

### Model changes

- `OperationResult.SucceededUnconfirmed`, after `SucceededAppRestartRequired`.
- `IsSuccess` (on `OperationOutcome` and `PackageOperation`) includes it. That's what makes `InstalledPackagesStore`
  reload, which is right: the installer did run and may have changed something. `CanRetry` stays `Failed or Cancelled`,
  so no Retry (decided in requirements).
- `OperationState.Unconfirmed`; `State` maps the new result to it. The card shows `CautionIcon`, as for
  `RestartRequired`.
- `StatusText`: "Update not confirmed". It's used by the row, the notification title and the log's `Result:` line, so
  none of them says "Updated" (R2.2).
- `OperationOutcome` gets `bool Retryable = true` (an init property, so existing callers don't change). `PackageOperation`
  gets `Retryable` set by `Finish`, and `CanRetry => (Result is Failed && Retryable) || Result is Cancelled`.
- `ShellViewModel.ShowOutcome`: `SucceededUnconfirmed` → `NotificationSeverity.Warning`.

## UI

No new controls. The Activity card shows the caution icon, "Update not confirmed" and the R2.2 message. The row
keeps **Update**, since WinGet still reports one after the refresh (P1, 004 R1.1). For R3.1 the card shows the message and
no **Retry**. Accessible names come from the existing text bindings.

## Error handling and edge cases

- **Installer finishes late** (hands off to a background process): R2.2 shows a warning that the next refresh makes
  out of date. The message says it may still be finishing. Decided in requirements: no wait.
- **Side by side installs:** if a package's new version installs next to the old one and WinGet matches the old entry,
  R2.2 warns after a good update. The message is hedged, the Updates page shows the same thing, and no such package
  has turned up so far. Accepted.
- **Version strings:** compared ordinally as WinGet gives them; both reads come from the same field, so formatting is
  the same.
- **Update all:** each successful update now reconnects to the catalog once more (the `Invalidate` before the read).
  `RunAsync`'s `finally` already invalidates after every operation, so the next operation reconnects anyway. Cost: one
  extra local-first search per successful update. Measure it with Update all during the manual checks.
- **R1 and the in-use codes:** `0x8A150101/0103/0111` and portable failures keep 003 R7's messages (R1.3).
- **Miniconda on the Updates page** (checked in T7): WinGet's CLI lists it under "require explicit targeting for
  upgrade", but the COM API still reports `IsUpdateAvailable`, so the app keeps offering it and R3.1's message explains
  why it fails. Marking such rows is 004 R3.1 (deferred).
- **R3 on install, not update:** WinGet doesn't raise this code for a fresh install. If it did, the generic arms apply.

## Testing strategy

Unit tests (traced to requirement IDs), next to the existing mapping tests in `WinGetErrorMessageTests`:

- R1.1: `InstallError`, exit 0, `0x80070005`, describer returns text → message with text and hex. Describer returns
  null → hex only. Same for `UninstallError`.
- R1.2: `0x800401F5` and `0x80070002` → "couldn't start a program … wasn't found", per kind.
- R1.3: exit code 1603 with an extended error → today's "error code 1603"; `0x8A150101` → the in-use message.
- R2.1–R2.3: `AfterUpdate` with the same version and update available → `SucceededUnconfirmed`; version changed,
  no update, null `after`, null versions → unchanged.
- R2.4, R2.5: App Installer and install / uninstall don't go through `AfterUpdate` (test the decision function
  that picks the path).
- R3.1: `CatalogError` and `InstallError` with `0x8A150114` on Upgrade → the in-place message, `Retryable == false`.
- R4.1: `Details` unchanged in every case above.
- Models: `StatusText`, `State`, `IsSuccess`, `CanRetry` for `SucceededUnconfirmed` and non-retryable failures;
  `ShellViewModel` severity.
- `ErrorDescriptions`: `0x80070005` gives non-empty text, `0x8A150114` gives null (needs Windows, cheap, no WinGet).

Manual checks, in the CartonStash-Test VM only (never on this PC):

- R3.1: install `Anaconda.Miniconda3 --version py312_24.11.1-0`, update from the app → the in-place message, no Retry.
- R2.2: install an older `Autodesk.Fusion` if winget-pkgs still has one, update → "Update not confirmed". If no older
  manifest exists, record the unit tests as the check.
- R1.2: install an older `ElectronicArts.EADesktop`, delete `EAUninstall.exe`, update → the R1.2 message.
- R4.2: one ordinary update and one ordinary uninstall still read "Updated successfully" / "Uninstalled successfully".

## Risks and alternatives considered

- **Compare against the version WinGet was asked to install, instead of the old one.** Rejected: installers often
  report a different `DisplayVersion` than the manifest's `PackageVersion` (WinGet maps them with `AppsAndFeaturesEntries`),
  so "not equal to the new version" would warn after many good updates. "Still the old version *and* still an update"
  is the narrower test.
- **Read WinGet's log for "0 changes to ARP were observed" or "update behavior is denied".** Rejected, as in 003 R7: the
  log is text, not an API, and changes between versions and languages.
- **Treat every `CatalogError` with an extended error as "see the log".** Rejected: most of them really are network or
  source problems, and the internet message is right for those (011).
- **R2 as a failure instead of a warning.** Rejected: WinGet reported success and the installer may really be finishing.
  A warning says what we know.
