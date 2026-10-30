# 008 – Pin management: Tasks

**Status:** Draft, not started (spec not approved)
**Issue:** #1 (close it with the commit that finishes T13)
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit.
T1 and T2 need Windows (user's PC); their results may change the design, which is updated first.

- [ ] **T0.** Apply the constitution amendment (A2 amended, A6 added) once this spec is approved.
  - Verify: review of `constitution.md` diff.
- [ ] **T1.** On Windows: capture fixtures and facts. `winget pin list` with 0 pins, with one pin of each type,
  with a long Id, in English and French display language; `winget upgrade` with and without the explicit section; running `winget.exe` from an installed MSIX; exit codes of: pin exists, package not found,
  invalid range, remove of missing pin; console width with redirected output; whether pins need elevation;
  whether `--installed` combines with `--version` and `--blocking`. Record in design.md. *(R1.1, R1.4, R2.4, R2.6, R5.3)*
  - Verify: fixtures committed under `tests/CartonStash.Core.Tests/Fixtures/`, design.md updated.
- [ ] **T2.** On Windows: does COM `UpgradePackageAsync` honour Pin, Block and Gate pins? Record; update R4.3 / design if not. *(R4.1–R4.3)*
  - Verify: findings in design.md.
- [ ] **T3.** `PinType`, `PinInfo`, `PackageInfo.Pin`. *(R1.1)*
  - Verify: build.
- [ ] **T4.** `WinGetTableParser`, `PinListParser`, `ExplicitUpgradeParser` + fixture tests. *(R1.1, R1.4, R6.1, R6.3)*
  - Verify: `PinListParserTests.*`, `ExplicitUpgradeParserTests.*`.
- [ ] **T5.** `IProcessRunner`, `WinGetLocator`, `WinGetCliPinService` (args, exit codes, timeout, cancellation, serialisation). *(R2.2, R2.3, R3.1, R3.2, R5.4)*
  - Verify: `WinGetCliPinServiceTests.*`; integration round-trip test on Windows.
- [ ] **T6.** Join pins and the explicit list into `InstalledPackagesStore` (primary rows only), `ReloadPinsAsync`, pin-status-unavailable state. *(R1.1, R1.4, R5.1, R6.3, R7.1)*
  - Verify: `InstalledPackagesStoreTests.*`.
- [ ] **T7.** `PinActionsViewModel`: menu enablement, replace confirmation, range prefill, error keeps dialog. *(R2.1–R2.6, R3.1, R5.2)*
  - Verify: `PinActionsViewModelTests.*`.
- [ ] **T8.** Installed Pinned chip; Updates exclusion and skipped count; Block disables Update; "Manual update only" badge. *(R1.3, R4.1–R4.3, R6.1, R6.2)*
  - Verify: `InstalledViewModelTests.*`, `UpdatesViewModelTests.*`.
- [ ] **T9.** Settings: Remove all pins with confirmation. *(R3.2)*
  - Verify: `SettingsViewModelTests.*`.
- [ ] **T10.** App views: badge, Pin flyout, range dialog, InfoBar. *(R1.2, R2.1, R5.2)*
  - Verify: manual (light, dark, high contrast, keyboard, Narrator).
- [ ] **T11.** Manual end to end on Windows: each pin type, replace, unpin, remove all, Update all skips pinned, Miniconda marked. *(all)*
- [ ] **T12.** Same on an installed MSIX. *(R7.2)*
- [ ] **T13.** Mark `Done`; 004 R3 replaced by this spec; close #1.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
