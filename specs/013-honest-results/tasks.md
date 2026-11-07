# 013 – Honest operation results: Tasks

**Status:** Approved (2026-11-07)
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit.
Manual checks run in the CartonStash-Test VM (or Windows Sandbox), never on the host.

## Core

- [x] **T1.** Model changes: `OperationResult.SucceededUnconfirmed`, `OperationState.Unconfirmed`, `OperationOutcome.Retryable` (init, default true), `PackageOperation.Retryable` set in `PackageOperationQueue.Finish`, and `IsSuccess`, `CanRetry`, `State`, `StatusText` ("Update not confirmed") for both. `ShellViewModel.ShowOutcome` maps the new result to a warning, and `OperationCard` maps `OperationState.Unconfirmed` to `CautionIcon`, in the same commit so the build stays green (Q4). *(R2.2)*
  - Verify: `PackageOperationQueueTests`: `SucceededUnconfirmed` gives `IsSuccess`, no `CanRetry`, `State == Unconfirmed`, "Update not confirmed". A `Failed` outcome with `Retryable = false` gives no `CanRetry`; an ordinary `Failed` and a `Cancelled` still do. `ShellViewModelTests`: warning severity for the new result. `dotnet build`.
- [x] **T2.** `ErrorDescriptions.Describe(int hresult)` on `FormatMessageW` (`DllImport`, `FROM_SYSTEM | IGNORE_INSERTS`), trimmed, null when Windows has no text. *(R1.1)*
  - Verify: `ErrorDescriptionsTests`: `0x80070005` and `0x800401F5` give non-empty text with no trailing line break; `0x8A150114` and `0x8A150101` give null.
- [x] **T3.** R1 arm in `MapInstallResult` and `MapUninstallResult`, after the in-use arm: exit code 0 with an extended error goes to `NoExitCodeMessage(kind, extendedError, packageName, describe)`. Add the `AppNotFound` and `FileNotFound` constants. *(R1.1, R1.2, R1.3, R4.1)*
  - Verify: `WinGetErrorMessageTests`, with a fake describer: `0x80070005` with text → "The update failed: WinGet reported "…" (0x80070005)."; no text → "…reported error 0x…". `0x800401F5` and `0x80070002` → the "couldn't start a program … wasn't found" message for install, update and uninstall. Exit code 1603 with an extended error → "error code 1603". `0x8A150101` with exit 0 → the in-use message. `Details` unchanged.
- [ ] **T4.** R3 arm, first in `MapInstallResult`: an update with any status but `Ok` and extended error `0x8A150114` gives the in-place message with `Retryable = false`. Add the `UpgradeNotSupported` constant. *(R3.1, R3.2, R4.1)*
  - Verify: `WinGetErrorMessageTests`: `CatalogError` and `InstallError` with `0x8A150114` on Upgrade → the Miniconda wording, not retryable. `CatalogError` with another extended error → today's internet message (R4.2). An install with `0x8A150114` → not the in-place message.
- [ ] **T5.** R2: `InstalledAfterUpdate` record, `InstalledAfterUpdateAsync` (Invalidate, `FindByIdAsync` local-first, read `InstalledVersion?.Version` and `IsUpdateAvailable`, null on any exception), the pure `AfterUpdate`, and the call in `RunAsync` for successful updates that aren't App Installer. *(R2.1 to R2.5, R4.1)*
  - Verify: `WinGetErrorMessageTests` (or a new `AfterUpdateTests`): same version and update available → `SucceededUnconfirmed` with the Fusion wording and `Details` unchanged. Version changed, no update, `after` null, either version null → unchanged. The path decision: App Installer goes to `AfterAppInstallerUpdate` only; install and uninstall go to neither. `dotnet test`.

## Docs

- [ ] **T6.** CHANGELOG: a "Fixed" entry in the next version's section for the three messages. *(R1, R2, R3)*
  - Verify: review in the PR.

## Manual checks

- [ ] **T7.** In the VM, on a build from this branch, record the results in the log below. *(R1.2, R2.2, R3.1, R4.2)*
  - Miniconda: `winget install Anaconda.Miniconda3 --version py312_24.11.1-0`, then update in the app → the in-place message, no Retry.
  - Fusion: install an older `Autodesk.Fusion` if winget-pkgs still has one, then update → "Update not confirmed", caution icon, warning notification, log `Result: Update not confirmed`, row still offers Update. If no older manifest exists, record that and rely on T5.
  - EA app: install an older `ElectronicArts.EADesktop`, delete `C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EAUninstall.exe`, then update → the R1.2 message.
  - An ordinary update and an ordinary uninstall still say "Updated successfully" / "Uninstalled successfully". Run Update all on several packages and note whether it's noticeably slower.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
