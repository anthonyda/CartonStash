# 020 – Uninstall, then update: Tasks

**Status:** In progress: T1 done (2026-11-23)

## Measure first

- [x] **T1.** VM: set EA app up as on the maintainer's PC (013's recipe: install `ElectronicArts.EADesktop`, ARP
  `DisplayVersion` set back to 13.813.1.6327, `EAUninstall.exe` deleted). From a small unpackaged console app using
  Core's `WinGetPackageInstaller`, run (a) the update as today → expect `0x800401F5`; (b) `InstallPackageAsync` with the
  target version's `PackageVersionId` and the installed scope. Record the result and WinGet's log: does (b) run
  `uninstallPrevious`? Only if (b) runs it, try (c) the same with `Force`. Record the outcome in Q7 and the design;
  set `InstallOverSupported`. *(R5.4, Q7)*
  - 2026-11-23: **Pass for R5.4; R5 kept, no Force.** VM, WinGet 1.29.380, 014's `goback` probe as tester (not
    elevated), EA app installed then set to the host's state (ARP `DisplayVersion` 13.813.1.6327, `EAUninstall.exe`
    deleted). (a) `UpgradePackageAsync`: `InstallError`, exit 0, `0x800401F5` at `ShellExecuteInstallerHandler.cpp(353)`
    in 4 s, as on the host. (b) `InstallPackageAsync` with 13.832.0.6345's `PackageVersionId`, scope System: WinGet's log
    goes straight to "Starting: EAappInstaller-13.832.0.6345 … /passive /norestart", no uninstall step; status `Ok`,
    exit 0. (c) not needed.
  - Limit: the VM's EA app is really 13.832 underneath (Burn's `BundleVersion` 13.832.0.6345, `EADesktop.exe`
    13.832), because EA's thin installer always installs the newest client; only the ARP version was set back. So in
    (b) the installer found nothing to do and exited 0 in half a second, and WinGet still lists 13.813.1.6327: the app
    would show 013 R2.2's "not confirmed" warning. A real upgrade over an old bundle can't be set up in the VM. The
    host (read only, 2026-11-23) has the old bundle for real: `{250aff32-…}`, `BundleVersion` 13.813.1.6327, its cached
    `EAappInstaller.exe` present, the client in `EA Desktop\13.813.1.6327\` (no `EA Desktop\EA Desktop\` folder, hence
    the missing `EAUninstall.exe`). Burn upgrades a related bundle through that cached copy, not `EAUninstall.exe`, so
    (b) should work there, but that is not measured. See T13.
- [ ] **T2.** VM: `winget install Anaconda.Miniconda3 --version py312_24.11.1-0 --scope user`, update from the same
  console app → `0x8A150114`; then uninstall and install the target version with the installed scope → installed,
  WinGet lists no update. Confirms R2's chain before it's built. *(R1.2, R2.2, R2.3)*

## Core

- [ ] **T3.** `MapInstallResult`, R1: the `0x8A150114` arm sets `OffersUninstallFirst`, message ends "Uninstall, then
  update does both." Only on an update. *(R1.2, R1.3)*
  - Verify: `WinGetErrorMessageTests`: `CatalogError` and `InstallError` with `0x8A150114` on Upgrade → new wording,
    `OffersUninstallFirst`, not retryable; on Install → unchanged.
- [ ] **T4.** Models: `OperationOptions.AsUpdate`, `InstallOver`; `OperationOutcome.OffersInstallOver`;
  `OperationFollowUp.UninstallThenUpdate`, `InstallOver`; `PackageOperation.CanUninstallThenUpdate`, `CanInstallOver`,
  commands, texts, accessible names; `KindText` "Update" for `AsUpdate`; `ThenText`. Queue copies `OffersInstallOver`.
  *(R1.1, R3.3, R4.3, R5.1)*
  - Verify: `PackageOperationTests`: the two `Can…` flags per result, kind and options; `CanInstallOver` false after an
    `InstallOver` run; names and texts.
- [ ] **T5.** `PackageActions`: `UninstallThenUpdateQuestion`; the `UninstallThenUpdate` follow-up queues the uninstall
  with `ThenInstall` = target, `PreviousVersion`, `PreviousScope` (`ScopeToKeep`), `AsUpdate`, `Elevated = false`; the
  `Completed` handler queues the install with `AfterUninstall` and `AsUpdate`. *(R2.1 to R2.4)*
  - Verify: `PackageActionsTests`: confirmation text; Cancel queues nothing; Confirm queues one uninstall with those
    options; the install only after success; options after an elevated failed update still unelevated; busy package →
    nothing.
- [ ] **T6.** Chained uninstall failed or cancelled: no install, R3.1 sentence "{name} {installed} is still installed;
  nothing was updated." `AfterUninstallOutcome` with `AsUpdate` keeps 014 R3.7. *(R3.1, R3.2)*
  - Verify: `PackageOperationQueueTests`.
- [ ] **T7.** `WinGetPackageInstaller.InstallAsync`: `AsUpdate` maps the install's result as an update ("Updated to
  {version}.", 013 R2.1 check); `InstallOver` forces `byInstalling` with the target's ID and the installed scope.
  *(R3.3, R5.3)*
  - Verify: unit tests on the mapping and on the chosen call (`UpdatesByInstalling`-style pure helper).
- [ ] **T8.** R5 mapping: on an update, `AppNotFound` / `FileNotFound` with exit code 0 → R5.2 message and
  `OffersInstallOver`, only while `InstallOverSupported`; otherwise 013 R1.2 unchanged. `InstallOverQuestion`; the
  `InstallOver` follow-up queues the update with `InstallOver`. Skipped if T1 drops R5. *(R5.1 to R5.4)*
  - Verify: `WinGetErrorMessageTests`, `PackageActionsTests`.
- [ ] **T9.** R4.1: Update all with a `0x8A150114` failure and one `0x800401F5` failure queues nothing more.
  - Verify: `UpdatesViewModelTests`.

## App

- [ ] **T10.** `OperationCard.xaml`: **Uninstall, then update** and **Install over it** (accent), `ThenText` caption
  under the progress bar. `PackageActionsView.xaml` / `PackageRowViewModel`: `ShowUninstallThenUpdate` in place of
  Update, `ShowInstallOver` before Update. Theme brushes only. *(R1.1, R4.3, R5.1)*
  - Verify: build; the VM run in T11.

## Manual checks

- [ ] **T11.** VM, Store test build: Miniconda3 `py312_24.11.1-0` → update fails with R1.3's message, no Retry, the
  button on card and row; Cancel the confirmation → nothing; confirm → uninstall card with "Then installs…", then
  "Updated to py314_….", row leaves Updates; both logs. *(R1.1 to R3.3, R4.2)*
- [ ] **T12.** VM: R3.1 and R3.2 by forcing failures (uninstall: keep Miniconda's `python.exe` running if it blocks; install:
  block the download with a firewall rule on WinGet's server after the uninstall) → the messages and **Install
  {previous}**. Record what could only be checked by unit tests. *(R3.1, R3.2)*
- [ ] **T13.** VM: EA app as in T1 → R5.2 message, Install over it on card and row, confirmation, the install runs
  without the uninstall step (WinGet's log) and ends with 013 R2.2's warning (T1's limit: nothing to upgrade). A real
  upgrade of an old EA app bundle is not reproducible in the VM; open question for the maintainer how to check it.
  *(R5.1 to R5.3)*
- [ ] **T14.** Keyboard and Narrator on the new buttons and dialogs; light, dark, high contrast. *(R4.3)*
- [ ] **T15.** CHANGELOG entry; set statuses to Done; roadmap row.

## Manual verification log

| Requirement | Date | Result | Notes |
|-------------|------|--------|-------|
| R5.4 (T1) | 2026-11-23 | Pass | WinGet's install of the target version on the installed EA app skips `uninstallPrevious`; the installer runs. Its effect on a really old bundle isn't reproducible in the VM (see T1) |
