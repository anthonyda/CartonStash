# 003 – Install / uninstall with progress: Tasks

**Status:** In progress (code done; one manual check pending)
**Design:** [design.md](design.md)

- [x] **T1.** Models: `PackageOperation`, `OperationPhase`, `OperationResult`, `OperationProgress`, `OperationOutcome`.
  - Verify: builds; covered by T3 tests.
- [x] **T2.** `IPackageInstaller` + `WinGetPackageInstaller` (re-find by ID, install, uninstall, agreements, progress, result mapping). *(R1.2, R1.3, R5.2, R5.3)*
  - Verify: `WinGetErrorMessageTests.*` (incl. hash mismatch, restart required); manual install of a small package (see log).
  - Fixed 2026-10-30 (security review): `FindByIdAsync` fell back to the first match from any source, against R1.2. Verify: `Only_the_requested_source_counts`, `Package_is_not_found_in_a_source_that_does_not_have_it`, `Installed_package_is_found_in_its_own_source`.
- [x] **T3.** `PackageOperationQueue`: FIFO single worker, dedupe, cancel, retry, `Completed`, `Drained`. *(R3.2, R4.1, R4.2, R5.1)*
  - Verify: `PackageOperationQueueTests.*`.
- [x] **T4.** `IDialogService` + uninstall confirmation, agreements dialog, close guard. *(R2.2, R1.3, R6.2)*
  - Verify: `PackageActionsTests.*`, `ShellViewModelTests.Close_*` with fake dialogs; manual, see log.
- [x] **T5.** Row actions (`PackageActionsView` in `PackageRow`: Install / Uninstall / progress / Cancel). *(R1.1, R2.1, R3.1)*
  - Verify: `PackageActionsTests.Row_*` + manual, see log.
- [x] **T6.** Activity pane + footer badge; success / failure InfoBars; Installed refresh on completion. *(R3.1, R5.1, R5.2)*
  - Verify: `ShellViewModelTests.*` + manual, see log.
- [ ] **T7.** Manual end-to-end: install → uninstall a small package; one admin-scope installer shows UAC only for itself. *(R6.1)*
  - Install → uninstall done (jqlang.jq). UAC check still pending.
- [ ] **T8.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1 | 2026-10-29 | Pass | UIA: "Install Wget" / "Install jq" buttons on not-installed search rows; none on installed Git |
| R1.2, R3.1 | 2026-10-29 | Pass | jqlang.jq via UI: Downloading… (0.3 s) → Installing… (1.3 s) → Installed (1.8 s); `winget list` confirms 1.8.2 |
| R5.1 | 2026-10-29 | Pass | InfoBar "Install jq: Installed · Installed successfully."; jq appears on the Installed page without pressing Refresh |
| R6.2 | 2026-10-29 | Pass | Closing during the jq download → "1 operation is still running or queued…" [Quit anyway] [Wait]; Wait kept the app open and the install finished |
| R2.1, R2.2 | 2026-10-29 | Pass | Installed page "Uninstall jq" → dialog "Uninstall jq? jq (jqlang.jq) will be removed from this PC." → confirmed → Uninstalled; `winget list` no longer finds it; list back to 329 |
| R5.2 | 2026-10-29 | Pass | JernejSimoncic.Wget failed live (host served HTML, hash mismatch): row "Failed", Activity entry with the reason and Retry. Message improved afterwards (see design) |
| Activity pane | 2026-10-29 | Pass | Lists "Uninstall jq · Uninstalled" and "Install jq · Installed" with messages; opens via mouse, keyboard and UIA select |
| R1.2 (source) | 2026-10-30 | Pass | Read-only probe of the 90 managed IDs on the dev PC: 89 found in their own source, none in another. The fallback never fired, so removing it changes nothing here. `Microsoft.PowerShell` returned no local-first match before and after the fix (separate bug) |
| R3.2 (live) | – | Not run | Covered by `PackageOperationQueueTests.Cancel_*`; jq downloads too fast to cancel by hand |
| R1.3 (live) | – | Not run | Covered by `PackageActionsTests.Install_with_agreements_*`; needs a package with agreements (e.g. an msstore app) |
| R6.1 | – | **Pending** | Needs a machine-scope installer and a human at the UAC prompt |
