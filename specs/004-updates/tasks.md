# 004 – Updates: Tasks

**Status:** Draft
**Design:** [design.md](design.md)

- [x] **T1.** Investigate pin / explicit-targeting info in the WinGet COM API. Record findings in design.md; amend R3 if needed. *(R3.1, R3.2)*
  - Result: not exposed in 1.29. Options A / B / C are in design.md, waiting for a decision.
- [ ] **T2.** Extract `InstalledPackagesStore` from `InstalledViewModel` (002 behaviour unchanged). *(R1.2)*
  - Verify: 002 tests green.
- [ ] **T3.** `UpgradeAsync` in the installer + `OperationKind.Upgrade` + `EnqueueMany`. *(R2.1)*
  - Verify: queue unit tests; mapping test for `NoApplicableUpgrade`.
- [ ] **T4.** `UpdatesViewModel`: list, selection, Update / selected / all (with confirmation), empty state. *(R1.1, R2.2–R2.4, R3.2, R5.1)*
  - Verify: `UpdatesViewModelTests.*`.
- [ ] **T5.** `UpdatesPage` + nav badge count. *(R4.1)*
  - Verify: manual.
- [ ] **T6.** Manual: update one outdated package end to end; Miniconda marked "Manual update only". *(R2.1, R3.1)*
- [ ] **T7.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
