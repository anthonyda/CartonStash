# 020 – Uninstall, then update: Tasks

**Status:** Done (2026-11-23); R3.1, R3.2 and R2.5's timeout by unit tests only; keyboard, Narrator, dark and high contrast not checked; EA app's real upgrade not measured

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
- [x] **T2.** VM: `winget install Anaconda.Miniconda3 --version py312_24.11.1-0 --scope user`, update from the same
  console app → `0x8A150114`; then uninstall and install the target version with the installed scope → installed,
  WinGet lists no update. Confirms R2's chain before it's built. *(R1.2, R2.2, R2.3)*
  - 2026-11-23: **Pass, with R2.5 added.** `goback` probe as tester. Update: `CatalogError`, exit 0, `0x8A150114`.
    First run: uninstall `Ok` in under a second, install of py314_26.9.4-1 at once → `InstallError`, exit 2,
    `0x8A150006`. Second run, polling: uninstall returned after 3 s; `Un_A.exe` (Miniconda's uninstaller copy) and
    `pythonw` kept working; the ARP entry, the folder and WinGet's installed list were gone at +15 s, `Un_A.exe` at
    +17 s. Install then: `Ok`, py314_26.9.4-1 for the current user, no update left. Requirements R2.5 and the design
    amended: the chain waits until WinGet no longer lists the package.

## Core

- [x] **T3.** `MapInstallResult`, R1: the `0x8A150114` arm sets `OffersUninstallFirst`, message ends "Uninstall, then
  update does both." Only on an update. *(R1.2, R1.3)*
  - Verify: `WinGetErrorMessageTests`: `CatalogError` and `InstallError` with `0x8A150114` on Upgrade → new wording,
    `OffersUninstallFirst`, not retryable; on Install → unchanged.
  - 2026-11-23: done, `An_update_denied_in_place_says_what_to_do`, `An_install_with_that_code_is_not_called_an_update`.
- [x] **T4.** Models: `OperationOptions.AsUpdate`, `InstallOver`; `OperationOutcome.OffersInstallOver`;
  `OperationFollowUp.UninstallThenUpdate`, `InstallOver`; `PackageOperation.CanUninstallThenUpdate`, `CanInstallOver`,
  commands, texts, accessible names; `KindText` "Update" for `AsUpdate`; `ThenText`. Queue copies `OffersInstallOver`.
  *(R1.1, R3.3, R4.3, R5.1)*
  - Verify: `PackageOperationTests`: the two `Can…` flags per result, kind and options; `CanInstallOver` false after an
    `InstallOver` run; names and texts.
  - 2026-11-23: done; the tests are in `UninstallThenUpdateTests` (R1.1, R4.3, R5.1). Also `UpdateTarget`, `ShowThenText`.
- [x] **T5.** `PackageActions`: `UninstallThenUpdateQuestion`; the `UninstallThenUpdate` follow-up queues the uninstall
  with `ThenInstall` = target, `PreviousVersion`, `PreviousScope` (`ScopeToKeep`), `AsUpdate`, `Elevated = false`; the
  `Completed` handler queues the install with `AfterUninstall` and `AsUpdate`. *(R2.1 to R2.4)*
  - Verify: `PackageActionsTests`: confirmation text; Cancel queues nothing; Confirm queues one uninstall with those
    options; the install only after success; options after an elevated failed update still unelevated; busy package →
    nothing.
  - 2026-11-23: done, `UninstallThenUpdateTests` (`It_asks_first…`, `Confirming_uninstalls_waits…`,
    `The_chain_runs_with_the_users_rights`). R2.5's wait added here: `IPackageInstaller.WaitUntilUninstalledAsync`,
    called by the queue in the uninstall's Finishing phase (`A_package_still_listed_after_the_wait_is_not_installed`,
    `Going_back_still_listed_names_the_version`). Busy package: unchanged 014 guard.
- [x] **T6.** Chained uninstall failed or cancelled: no install, R3.1 sentence "{name} {installed} is still installed;
  nothing was updated." `AfterUninstallOutcome` with `AsUpdate` keeps 014 R3.7. *(R3.1, R3.2)*
  - Verify: `PackageOperationQueueTests`.
  - 2026-11-23: done, `PackageOperationQueue.UpdateChainOutcome`; tests in `UninstallThenUpdateTests` (R3.1, R3.2).
- [x] **T7.** `WinGetPackageInstaller.InstallAsync`: `AsUpdate` maps the install's result as an update ("Updated to
  {version}.", 013 R2.1 check); `InstallOver` forces `byInstalling` with the target's ID and the installed scope.
  *(R3.3, R5.3)*
  - Verify: unit tests on the mapping and on the chosen call (`UpdatesByInstalling`-style pure helper).
  - 2026-11-23: done, as amended: `AsUpdate` is worded by the queue ("Updated to {version}."), without 013 R2's check
    (R3.3 amended). `InstallOver` reaches `RunAsync` and `ElevatedRequest`; `RunsAsInstall` decides the call
    (`InstallOverCallTests`).
- [x] **T8.** R5 mapping: on an update, `AppNotFound` / `FileNotFound` with exit code 0 → R5.2 message and
  `OffersInstallOver`, only while `InstallOverSupported`; otherwise 013 R1.2 unchanged. `InstallOverQuestion`; the
  `InstallOver` follow-up queues the update with `InstallOver`. Skipped if T1 drops R5. *(R5.1 to R5.4)*
  - Verify: `WinGetErrorMessageTests`, `PackageActionsTests`.
  - 2026-11-23: done; no `InstallOverSupported` switch, T1 kept R5. `An_update_without_its_uninstaller_offers_install_over_it`,
    `Installing_over_it_again_keeps_the_013_message`; follow-up in `UninstallThenUpdateTests` (R5.1, R5.3).
- [x] **T9.** R4.1: Update all with a `0x8A150114` failure and one `0x800401F5` failure queues nothing more.
  - Verify: `UpdatesViewModelTests`.
  - 2026-11-23: done at the queue: `A_refused_update_does_nothing_on_its_own` (nothing is queued or asked after either
    failure; Update all and the background runner only enqueue plain updates).

## App

- [x] **T10.** `OperationCard.xaml`: **Uninstall, then update** and **Install over it** (accent), `ThenText` caption
  under the progress bar. `PackageActionsView.xaml` / `PackageRowViewModel`: `ShowUninstallThenUpdate` in place of
  Update, `ShowInstallOver` before Update. Theme brushes only. *(R1.1, R4.3, R5.1)*
  - Verify: build; the VM run in T11.
  - 2026-11-23: built. Deviation from the UI proposal: next to Install over it, the row's Update keeps its accent style.

## Manual checks

- [x] **T11.** VM, Store test build: Miniconda3 `py312_24.11.1-0` → update fails with R1.3's message, no Retry, the
  button on card and row; Cancel the confirmation → nothing; confirm → uninstall card with "Then installs…", then
  "Updated to py314_….", row leaves Updates; both logs. *(R1.1 to R3.3, R4.2)*
  - 2026-11-23: **Pass** on the second run (see the log). The first run found two bugs, fixed with tests: the
    confirmation repeated the version the installed entry's name carries (`PackageInfo.NameWith`), and the old failed
    card still offered Uninstall, then update after the chain (`IsSuperseded`, R1.1 amended).
- [x] **T12.** VM: R3.1 and R3.2 by forcing failures (uninstall: keep Miniconda's `python.exe` running if it blocks; install:
  block the download with a firewall rule on WinGet's server after the uninstall) → the messages and **Install
  {previous}**. Record what could only be checked by unit tests. *(R3.1, R3.2)*
  - 2026-11-23: not forced in the VM. R3.1, R3.2 and R2.5's timeout are checked by unit tests only
    (`UninstallThenUpdateTests`); R3.2's wording and button are 014 R3.7's, checked in the VM then.
- [x] **T13.** VM: EA app as in T1 → R5.2 message, Install over it on card and row, confirmation, the install runs
  without the uninstall step (WinGet's log) and ends with 013 R2.2's warning (T1's limit: nothing to upgrade). A real
  upgrade of an old EA app bundle is not reproducible in the VM; open question for the maintainer how to check it.
  *(R5.1 to R5.3)*
  - 2026-11-23: **Pass, within T1's limit.** R5.2's message, Install over it on the card (next to Retry) and the row,
    the confirmation; WinGet's log: no uninstall step, "Starting: EAappInstaller-13.832.0.6345 … /passive"; result
    "Update not confirmed" (013 R2.2), as expected when nothing was old underneath. The real upgrade of an old bundle
    is not measured.
- [x] **T14.** Keyboard and Narrator on the new buttons and dialogs; light, dark, high contrast. *(R4.3)*
  - 2026-11-23: accessible names read through UI Automation in T11/T13 (the names in the log). Light theme seen in the
    screenshots. Keyboard by hand, Narrator, dark and high contrast not checked.
- [x] **T15.** CHANGELOG entry; set statuses to Done; roadmap row.
  - 2026-11-23: CHANGELOG 1.7.0 (version number for the maintainer to confirm).

## After release

- [x] **T16.** 1.7.1: withdraw R5. Remove `OffersInstallOver`, the `InstallOver` option, follow-up, buttons, `RunAsync` and
  `ElevatedRequest` argument; 0x800401F5 / 0x80070002 on an update keep 013 R1.2. *(R5 withdrawn)*
  - Verify: `WinGetErrorMessageTests` (`A_missing_program_says_so` for updates again); the R5 tests removed.

## Manual verification log

| Requirement | Date | Result | Notes |
|-------------|------|--------|-------|
| R1.1 to R2.5, R3.3, R4.2 (T11) | 2026-11-23 | Pass | VM, Store test build 1.6.2 from this branch. Miniconda3 py312_24.11.1-0 (current user): update failed with R1.3's message, no Retry, the button on the card and the row; Cancel closed the dialog with nothing queued; confirmed: uninstall card "Finishing…" with "Then installs py314_26.9.4-1 for the current user." for 18 s (Miniconda's uninstaller window open meanwhile), then "Updating…", "Updated to py314_26.9.4-1." in 47 s; WinGet lists py314_26.9.4-1; the Updates row is gone; three logs; the old card no longer offers the action; no crash |
| R5.1 to R5.3 (T13) | 2026-11-23 | Pass, limit | EA app in the host's state: R5.2's message, Install over it next to Retry, the confirmation ("…for all users, as EA app is now…"); no uninstall step in WinGet's log; "Update not confirmed" because the VM's EA app is already 13.832 underneath |
| R3.1, R3.2, R2.5 timeout | – | Unit tests only | `UninstallThenUpdateTests` |
| R4.3 keyboard, Narrator, dark, high contrast | – | Not checked | Accessible names checked through UI Automation |
| R5.4 (T1) | 2026-11-23 | Pass | WinGet's install of the target version on the installed EA app skips `uninstallPrevious`; the installer runs. Its effect on a really old bundle isn't reproducible in the VM (see T1) |
