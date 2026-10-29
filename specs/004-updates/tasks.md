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

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1, R4.1 | 2026-10-29 | Pass | UIA: badge "1"; Updates (1) lists Miniconda3 `py312_24.11.1-0 → py314_26.9.4-0 · winget` with an Update button |
| R2.2 | 2026-10-29 | Pass | UIA: Update selected disabled until a row is checked; checking doesn't open details |
| R2.3 | 2026-10-29 | Pass | UIA: Update all → "Update all? 1 package will be updated…" [Update 1 package] [Cancel]; Cancel queued nothing |
| R2.1 (live) | – | **Pending** | The only outdated package here is Miniconda (the explicit-targeting case). A human needs to pick a package to really update |
