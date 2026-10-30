# 009 – Activity pane: Tasks

**Status:** Draft, not started (spec not approved)
**Design:** [design.md](design.md)
**Issues:** #2, #3, #4 (close each with the commit that finishes its part)

Tick a task only when its verification passes. Keep tasks small enough for one commit.

- [ ] **T1.** Probe on the dev PC: does WinGet honour `LogOutputPath` for an MSI / Inno package, from the portable app and from an
  installed MSIX (LocalFolder path)? What does a portable package do? Record in design.md. *(R2.2, R2.3)*
  - Verify: findings in design.md; design updated first if they change it.
- [ ] **T2.** `PackageOperation`: times, state, state text, time text; `OperationOutcome.Details`; installer fills it. *(R1.3, R1.4, R2.2)*
  - Verify: unit tests.
- [ ] **T3.** Queue: active first, then newest (R1.5); stamps times.
  - Verify: `PackageOperationQueueTests.*`.
- [ ] **T4.** Card template in the Activity pane; `PackageIcon` for a plain package. *(R1.1, R1.2, R1.6, R1.7)*
  - Verify: manual (screenshots dark + light, keyboard). Closes #4.
- [ ] **T5.** `OperationLogStore` (paths, summary, read, prune) and `logPath` through the installer. *(R2.2, R2.3, R2.5, R2.6)*
  - Verify: `OperationLogStoreTests.*`, options test.
- [ ] **T6.** Log button and dialog (Copy, Open folder). *(R2.1, R2.2, R2.4)*
  - Verify: `ActivityViewModelTests.*`; manual with a real and a failing package. Closes #2.
- [ ] **T7.** `ActivityPinned` setting; Pin toggle; `SplitView` switching; narrow fallback; details pane check. *(R3.1–R3.6)*
  - Verify: `SettingsTests.*`; manual (UIA, restart keeps pin, resize below 1000 px). Closes #3.
- [ ] **T8.** Same checks on an installed MSIX.
- [ ] **T9.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
