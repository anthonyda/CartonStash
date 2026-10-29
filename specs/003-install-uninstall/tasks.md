# 003 – Install / uninstall with progress: Tasks

**Status:** Draft
**Design:** [design.md](design.md)

- [ ] **T1.** Models: `PackageOperation`, `OperationPhase`, `OperationResult`, `OperationProgress`, `OperationOutcome`.
  - Verify: builds; covered by T3 tests.
- [ ] **T2.** `IPackageInstaller` + `WinGetPackageInstaller` (re-find by ID, install, uninstall, agreements, progress, result mapping). *(R1.2, R1.3, R5.2, R5.3)*
  - Verify: mapping unit tests; manual install of a small package.
- [ ] **T3.** `PackageOperationQueue`: FIFO single worker, dedupe, cancel, retry, `Completed`. *(R3.2, R4.1, R4.2, R5.1)*
  - Verify: `PackageOperationQueueTests.*`.
- [ ] **T4.** `IDialogService` + uninstall confirmation, agreements dialog, close guard. *(R2.2, R1.3, R6.2)*
  - Verify: VM unit tests with fake dialogs; manual.
- [ ] **T5.** Row actions in `PackageRow` (Install / Uninstall / progress / Cancel). *(R1.1, R2.1, R3.1)*
  - Verify: manual.
- [ ] **T6.** Activity pane + footer badge; success / failure InfoBars; Installed refresh on completion. *(R3.1, R5.1, R5.2)*
  - Verify: manual.
- [ ] **T7.** Manual end-to-end: install → uninstall a small package; one admin-scope installer shows UAC only for itself. *(R6.1)*
- [ ] **T8.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
