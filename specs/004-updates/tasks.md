# 004 – Updates: Tasks

**Status:** In progress (code done; one manual check pending)
**Design:** [design.md](design.md)

- [x] **T1.** Investigate pin / explicit-targeting info in the WinGet COM API. Record findings in design.md; amend R3 if needed. *(R3.1, R3.2)*
  - Result: not exposed in 1.29. Decision: R3 deferred, question left open.
- [x] **T2.** `InstalledPackagesStore` shared by Installed and Updates (built directly in 002). *(R1.2)*
  - Verify: 002 tests green; `UpdatesViewModelTests.Shares_the_installed_list`.
- [x] **T3.** Upgrade in the installer + `OperationKind.Upgrade` + `EnqueueMany`. *(R2.1)*
  - Verify: `PackageOperationQueueTests.Completed_and_drained_are_raised`; `WinGetErrorMessageTests.Install_results_are_readable` (`NoApplicableUpgrade`).
- [x] **T4.** `UpdatesViewModel`: list, selection, Update / selected / all (with confirmation), empty state. *(R1.1, R2.2–R2.4, R5.1)*
  - Verify: `UpdatesViewModelTests.*`.
- [x] **T5.** `UpdatesPage` + nav badge count. *(R4.1)*
  - Verify: `ShellViewModelTests.Updates_badge_counts_once_loaded` + manual, see log.
- [ ] **T6.** Manual: update one outdated package end to end. *(R2.1)*
- [ ] **T7.** Mark `Done` (with R3 noted as deferred).

### Amendment R6: updating App Installer (approved 2026-11-04)

- [x] **T8.** Probe in the test VM, on a throwaway build: restart with `AppInstance.Restart(args)`, then by starting our own exe
  and closing normally. For each: how long WinGet's old server stays, whether the new copy has the package identity and the
  arguments, and whether a notification click still reaches it. Also App Installer's installed entry ID as WinGet reports it.
  - Verify: results recorded in design.md, with the option chosen. The probe code is not kept.
  - Done 2026-11-04: option A. Right after the App Installer update, the old copy ended in 0.36 s and WinGet's old server
    stopped 10.4 s later, as after a normal close; the reopened app ran 1.29.380.0. The new copy keeps the Store identity, gets
    the arguments and takes the "main" key, which is what notification clicks rely on; a click itself wasn't tried.
- [x] **T9.** `WinGetHost`: `IsAppInstaller`, `PendingVersion`. *(R6.1, R6.2)*
  - Verify: `WinGetHostTests.*`.
- [x] **T10.** `SucceededAppRestartRequired`: the installer's lookup after `Ok`, the outcome, card state and status text. *(R6.1)*
  - Verify: `WinGetErrorMessageTests` for the three lookup cases; `PackageOperationTests` for state and text.
- [ ] **T11.** App Installer queued last by Update all and Update selected. *(R6.5)*
  - Verify: `PackageActionsTests.App_Installer_is_queued_last`.
- [ ] **T12.** Row state: **Restart CartonStash to finish**, left out of Update all. *(R6.2)*
  - Verify: `PackageRowViewModelTests.*`, `UpdatesViewModelTests.*`.
- [ ] **T13.** `IAppRestarter`, `ShellViewModel.RestartCommand` with the close guard, the buttons on the card, the notification and the row. *(R6.3)*
  - Verify: `ShellViewModelTests.Restart_*`; manual with keyboard, light, dark and high contrast.
- [ ] **T14.** `AppRestarter`, `Program` and `FinishUpdateWindow`: start the new copy, wait for the old copy and servers, open on the same page; the R6.4 message. *(R6.3, R6.4)*
  - Verify: argument parsing tests; the portable app still starts (007 R1.6).
- [ ] **T15.** Manual in the test VM, Store build: restore `clean`, **Update all** with App Installer, check the warning and the row, Restart CartonStash: 1.29.380.0, gone from Updates, same page. Again with the `winget` command keeping WinGet open (R6.4). *(R6.1–R6.5)*
  - Verify: logged below.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1, R4.1 | 2026-10-29 | Pass | UIA: badge "1"; Updates (1) lists Miniconda3 `py312_24.11.1-0 → py314_26.9.4-0 · winget` with an Update button |
| R2.2 | 2026-10-29 | Pass | UIA: Update selected disabled until a row is checked; checking doesn't open details |
| R2.3 | 2026-10-29 | Pass | UIA: Update all → "Update all? 1 package will be updated…" [Update 1 package] [Cancel]; Cancel queued nothing |
| R2.1 (live) | – | **Pending** | The only outdated package here is Miniconda (the explicit-targeting case). A human needs to pick a package to really update |
