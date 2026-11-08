# 014 – Install a chosen version: Tasks

**Status:** In progress (T1 done 2026-11-08)
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit, and keep the build green at
every commit (Q4). Manual checks run in the CartonStash-Test VM (or Windows Sandbox), never on the host.

## Measure first

- [x] **T1.** In the VM, for each package in the design's "Going back" table: install its latest version, then go back
  to an older one with a small console tool on the COM API (kept in the VM's guest folder, not in the repo):
  `InstallPackageAsync` and `UpgradePackageAsync` with `PackageVersionId`, each without and with `Force`. Restore the
  checkpoint between packages. Record WinGet's status, extended error, installer exit code and what Windows lists
  afterwards. Then update design.md's "Going back" section with the call to use, the `Force` decision (Q3) and the
  refusal codes, and amend the requirements if a result needs it. *(R3, Q3)*
  - Verify: the results table in the log below; design.md updated and reviewed before T4 starts.

## Core

- [x] **T2.** `PackageVersion` and `VersionDirection`. `PackageDetails.Versions` becomes `IReadOnlyList<PackageVersion>`.
  `ReadVersions` sets the direction through a pure `Direction(CompareResult?, bool installed, bool primaryMatch)`.
  `VersionsText` reads `.Version`. *(R1.1, R1.2)*
  - Verify: `VersionDirectionTests`: `Greater` → `Older`, `Lesser` → `Newer`, `Equal` → `Installed`, `Unknown` →
    `Unknown`, not installed → `NotInstalled`, not a primary match → `Unknown`. Existing `VersionsText` tests pass.
    `dotnet build`.
- [x] **T3.** The version through the queue: `string? version = null` on `IPackageInstaller.GetAgreementsAsync` and
  `RunAsync`; `PackageOperation.Version` and `ExcludeOnSuccess`; `PackageOperationQueue.Enqueue(..., version,
  excludeOnSuccess)` passes the version on, and Retry reuses it; `Title` names the version; the log's
  "Chosen version" line. Update the fake installer to record the version. *(R2.1, R2.3, R2.4)*
  - Verify: `PackageOperationQueueTests`: the installer receives the version; Retry passes the same version and
    acceptance; a null version is passed by every existing path. `PackageOperation` title with and without a version.
    `OperationLogStoreTests`: "Chosen version: 0.101.0" and "Chosen version: default".
- [x] **T4.** `WinGetPackageInstaller`: find the `PackageVersionId` in `AvailableVersions` by index (W2), read that
  version's agreements, set `InstallOptions.PackageVersionId`. Going back and `Unknown` use `InstallPackageAsync`,
  `Newer` uses `UpgradePackageAsync`, and `Force` is never set (T1). A missing version fails with "This version is no
  longer available from {source}." and isn't retryable. 013's `AfterUpdate` also runs for an install with a version
  over an installed package. Add the R3.3 arm to `MapInstallResult`: any `InstallError` while going back, after the
  in-use arm, retryable. *(R2.1, R2.2, R2.5, R3.3)*
  - Verify: `WinGetErrorMessageTests`: `InstallError` with MSI 1603 / `0x8A150049`, Inno exit 1 / `0x8A150006` and
    `0x80070002` while going back → the R3.3 wording, retryable; the same on an ordinary install → today's message; an
    in-use code while going back → 003 R7's message. `AfterUpdateTests`: a chosen-version install over an installed
    package, same version after with an update → `SucceededUnconfirmed`; a first install → unchanged. The "no longer
    available" outcome is a pure function, tested.
- [x] **T5.** `IDialogService.ConfirmWithOptionAsync(title, message, confirmText, optionText, optionChecked)` returning
  `(bool Confirmed, bool OptionChecked)`. `DialogService` in the App builds it as a ContentDialog with a CheckBox
  (keyboard reachable, its label as the accessible name). The test fake records the call and returns a set answer. *(R3.1, R3.2)*
  - Verify: `dotnet build`; the dialog itself in T12.
- [x] **T6.** `PackageActions.InstallVersionAsync(package, version)`: nothing while the package is busy; for `Older` and
  `Unknown`, the design's confirmation (no box when already excluded); the chosen version's agreements; then enqueue
  with the kind for the direction. `PackageActions` takes `ISettings`, and its `Completed` handler adds the exclusion
  for `ExcludeOnSuccess` on `Succeeded` or `SucceededRestartRequired`. Update DI and the test wiring. *(R2.2, R3.1, R3.2, R3.4)*
  - Verify: `PackageActionsVersionTests`: `Newer` → no dialog, upgrade queued with the version; `Older` → dialog with
    both versions, Cancel queues nothing; box checked → excluded after `Succeeded`, not after `SucceededUnconfirmed`,
    `Failed` or `Cancelled`; box unchecked → never excluded; already excluded → no box; agreements asked with the chosen
    version; declined agreements queue nothing; no uninstall is queued by `InstallVersionAsync`.
- [ ] **T7.** Uninstall, then install: `PackageOperation.GoingBack`, `PreviousVersion`, `ThenInstall`, `AfterUninstall`,
  `CanUninstallThenInstall`, `CanInstallPrevious` and their commands; the queue's `UninstallThenInstallRequested` and
  `InstallPreviousRequested`; `PackageActions.UninstallThenInstallAsync` (the design's confirmation, then the uninstall);
  the chained install from the `Completed` handler; `AfterUninstallOutcome` in `Finish`. The two buttons on
  `OperationCard`, in the same commit so the build stays green. *(R3.4 to R3.7)*
  - Verify: `UninstallThenInstallTests`: offered only after a going-back failure that isn't 003 R7's in-use case;
    Cancel queues nothing; confirm queues the uninstall of the installed entry, then the install with the version, the
    acceptance and `ExcludeOnSuccess` only after the uninstall succeeds; a failed or cancelled uninstall queues nothing
    more; a failed install after it gets the "no longer installed" wording and `CanInstallPrevious`, which queues the
    previous version with no flags; the exclusion is added after the chained install succeeds.
- [ ] **T8.** `PackageDetailsViewModel`: `ShownVersions` (10, or all after `ShowAllVersionsCommand`), `HasMoreVersions`,
  `SelectedVersion`, `VersionActionText`, `VersionActionName`, `ShowVersionAction`, `CanRunVersion` and `OffersVersionActions`
  as plain bools and strings, and `RunVersionCommand` calling `PackageActions`. The view model takes `PackageActions`.
  Expanding resets when another package opens. *(R1.1, R1.3, R1.4, R1.5)*
  - Verify: `PackageDetailsVersionsTests`: 25 versions → 10 shown, then 25 after Show all, then 10 for the next package;
    the action text and name for each direction ("Go back to PowerToys 0.101.0"); the installed version has no action; no
    actions for one version, for an unmanaged package and while the row is busy; the command calls `PackageActions`
    with the selected version.

## App

- [ ] **T9.** `PackageDetailsPane.xaml`: the "Available versions" section as in the design. A non-scrolling `ListView`
  bound to `ShownVersions`, the action button under it, **Show all versions**, and today's text when
  `OffersVersionActions` is false. Theme resources only. *(R1.1, R1.3, R1.4, R1.5)*
  - Verify: `dotnet build`; the pane in T12.

## Tests against WinGet

- [ ] **T10.** Integration test: `GetDetailsAsync` on a package installed on the test runner returns one `Installed`
  version, and `Older` and `Newer` directions that match the order of the list. Tagged `Category=Integration`. *(R1.2)*
  - Verify: `dotnet test --filter Category=Integration` passes.

## Docs

- [ ] **T11.** 005's out-of-scope line marked as replaced by 014. A CHANGELOG "New" entry in the next version's
  section. A README feature-list sentence about installing a chosen version. *(Replaces 005 out of scope)*
  - Verify: review in the PR.

## Manual checks

- [ ] **T12.** In the VM, on a build from this branch, record the results in the log below. *(R1 to R3, Q5)*
  - Not installed: install an older version of a T1 package, then check it shows as installed with an update available.
  - Update to a middle version (neither the installed one nor the latest).
  - Go back: the confirmation names both versions; Cancel queues nothing; confirm with the box checked → the package is
    in Settings' excluded list and on the Updates page as excluded; with the box unchecked → not excluded.
  - Git (Inno refuses, from T1) → the R3.3 message, Retry, the codes in the log. Then **Uninstall, then install**:
    the confirmation, Cancel queues nothing, confirm → Git goes back, and is excluded if the box was checked.
  - Windows Terminal (fails after the uninstall, from T1) → the "no longer installed" result, then **Install
    {previous version}** brings it back.
  - Retry after a cancel reinstalls the same version. The card, notification and log name the version.
  - A package with more than 10 versions: Show all versions. A Store package: no actions.
  - Keyboard only: Tab to the list, arrow keys, Tab to the button, Enter. The button's accessible name and the
    checkbox's are read correctly (UI Automation). Light, dark and high contrast.
- [ ] **T13.** Close: every criterion maps to a passing test or a log entry here, status set to `Done` in all three
  files and in the roadmap.
  - Verify: review.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| T1, portable | 2026-11-08 | Goes back | `jqlang.jq` 1.8.2 → 1.8.1 with `InstallPackageAsync`; update offered again afterwards |
| T1, NSIS | 2026-11-08 | Goes back | `Notepad++.Notepad++` (nullsoft) 8.9.9 → 8.9.8.1; uninstall then install also works |
| T1, MSI (WiX) | 2026-11-08 | Refused; works after uninstall | `Notepad++.Notepad++` (wix) and `Microsoft.PowerShell` (wix): exit 1603, `0x8A150049`, "A newer version … is already installed" |
| T1, Inno | 2026-11-08 | Refused; works after uninstall | `Git.Git` 2.56.0.4 → 2.56.0.3: exit 1, `0x8A150006`; the silent downgrade prompt answers No |
| T1, MSIX | 2026-11-08 | Refused; works after uninstall | `Microsoft.PowerShell` (msix) 7.6.9.0 → 7.6.8.0: `0x80070002` on register |
| T1, MSIX in Windows | 2026-11-08 | Refused; fails after uninstall too | `Microsoft.WindowsTerminal` 1.25 → 1.24: `0x80070002`, and after the uninstall nothing is installed |
| T1, upgrade call | 2026-11-08 | Refuses older | `UpgradePackageAsync` with an older version: `NoApplicableUpgrade`, `0x8A15004F`, for every type |
| T1, `Force` | 2026-11-08 | No effect | Same result with and without it, for every call and type |
| T1, middle version | 2026-11-08 | Works | Install an older version from nothing, then `UpgradePackageAsync` to the next one: portable, NSIS, MSI |
