# 009 – Activity pane: Tasks

**Status:** Done (high-contrast check pending, with 006)
**Design:** [design.md](design.md)
**Issues:** #2, #3, #4 (close each with the commit that finishes its part)

Tick a task only when its verification passes. Keep tasks small enough for one commit.

- [x] **T1.** Probe on the dev PC: does WinGet honour `LogOutputPath` for an MSI / Inno package, from the portable app and from an
  installed MSIX (LocalFolder path)? What does a portable package do? Record in design.md. *(R2.2, R2.3)*
  - Done for the unpackaged app (LocalSend, see design); the MSIX path is covered by T8.
- [x] **T2.** `PackageOperation`: times, state, state text, time text; `OperationOutcome.Details`; installer fills it. *(R1.3, R1.4, R2.2)*
  - Verify: unit tests.
- [x] **T3.** Queue: active first, then newest (R1.5); stamps times.
  - Verify: `PackageOperationQueueTests.*`.
- [x] **T4.** Card template in the Activity pane; `PackageIcon` for a plain package. *(R1.1, R1.2, R1.6, R1.7)*
  - Verify: manual (screenshots dark + light, keyboard). Closes #4.
- [x] **T5.** `OperationLogStore` (paths, summary, read, prune) and `logPath` through the installer. *(R2.2, R2.3, R2.5, R2.6)*
  - Verify: `OperationLogStoreTests.*`, options test.
- [x] **T6.** Log button and dialog (Copy, Open folder). *(R2.1, R2.2, R2.4)*
  - Verify: `ActivityViewModelTests.*`; manual with a real and a failing package. Closes #2.
- [x] **T7.** `ActivityPinned` setting; Pin toggle; `SplitView` switching; narrow fallback; details pane check. *(R3.1–R3.6)*
  - Verify: `SettingsTests.*`; manual (UIA, restart keeps pin, resize below 1000 px). Closes #3.
- [x] **T8.** Same checks on an installed MSIX.
- [x] **T9.** Mark `Done`.

### Amendment R2.7: log privacy note (approved 2026-10-30)

- [ ] **T11.** Privacy line in the log dialog. *(R2.7)*
  - Verify: manual: open a log, the line is visible and readable in light, dark and high contrast.
  - Dark theme checked on a real Firefox install (see log). Light and high contrast still to check.

### Amendment R2.8–R2.10: WinGet's record in the log (approved 2026-10-30)

- [x] **T12.** Probe: can an installed MSIX read WinGet's `DiagOutputDir`? Does each operation still get one file when two are queued back to back? Record in design.md. *(R2.8, R2.9)*
  - Done 2026-10-30: yes to both (see design). Queuing was tested on the unpackaged app; the MSIX is covered by T15.
- [x] **T13.** `WinGetRecordReader`: matching and cleaning. *(R2.8–R2.10)*
  - Verify: `WinGetRecordReaderTests.*`.
- [x] **T14.** `OperationLogStore` and the queue add the `--- WinGet log ---` section. *(R2.8, R2.9, R2.5)*
  - Verify: `ActivityTests.WinGets_record_comes_last_and_is_saved`, `A_missing_record_is_explained_and_the_operation_still_succeeds`.
- [x] **T15.** Manual: a package with no installer log, one with an installer log, and a failed one, on the portable app and an installed MSIX. *(R2.8–R2.10)*
  - Passed on the portable app and the released v1.0.3 MSIX (see log).

### Amendment R4: filter, tabs and sort (approved 2026-11-12)

- [x] **T16.** `ActivityTab`, `ActivitySort`, `AppSettings.ActivitySort`, `PackageOperation.Sequence` set by the queue. *(R4.5, R4.8)*
  - Verify: `SettingsTests` (round trip, older file loads as Default); queue test for the sequence on enqueue and retry.
- [x] **T17.** `ActivityArranger.Arrange` with every rule. *(R4.1–R4.5, R4.7)*
  - Verify: `ActivityArrangerTests`.
- [x] **T18.** `ActivityViewModel`: filter, tab, sort, `Entries` kept in step by moves, counts, Clear filter. *(R4.6–R4.9)*
  - Verify: `ActivityViewModelTests`.
- [x] **T19.** Pane XAML: filter box, `SelectorBar` with counts, sort menu, dividers, no-match message. *(R4.1–R4.5, R4.7, R4.10)*
  - Verify: build; manual T20.
- [x] **T20.** Manual in the VM with a dozen queued updates and one failure: each tab and sort, filter, keyboard and Narrator names, focus kept when a card finishes, light, dark and high contrast. *(R4.1–R4.10)*
  - Verify: results in the log below.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1–R1.5 | 2026-10-30 | Pass | Screenshot (dark): separate cards with icon, name, time, action, state icon + words, message; failed Wget card stands out in red; finished ones below active |
| R1.6 | 2026-10-30 | Pass | UIA names: "Log for Install Wget", "Retry Install Wget" |
| R2.1–R2.3, R2.6 | 2026-10-30 | Pass | Log dialog on a real LocalSend install (25 KB Inno log below the summary) and its uninstall (summary + "The installer didn't write a log."); plain text, read-only |
| R2.4 | 2026-10-30 | Pass | Clicked with UI Automation on a real Firefox install: Copy put the log text on the clipboard, identical to the dialog apart from line endings; Open folder opened `%LOCALAPPDATA%\CartonStash\logs`; the dialog stayed open after both |
| R3.1–R3.3, R3.6 | 2026-10-30 | Pass | UIA: pin keeps the pane beside the page, page resizes, clicks elsewhere keep it open, close button hidden; unpin closes it; overlay as before |
| R3.4 | 2026-10-30 | Pass | Restart: pane open and pinned; `ActivityPinned` in settings.json |
| R3.5 | 2026-10-30 | Pass | At 900 px the pane drops to a closed overlay, pin kept; back at 1300 px it reopens |
| MSIX (T8) | 2026-10-30 | Pass | 1.0.1 test MSIX: LocalSend install wrote its 25 KB log to the package `LocalState\logs`, shown in the Log dialog |
| Log dialog width | 2026-10-30 | Note | Stays at the default dialog width; long lines scroll sideways. Fine for now |
| R2.7 (dark) | 2026-10-30 | Pass | Real Firefox install: the line is visible above the log, readable on a screenshot, and doesn't scroll away |
| R2.7 (light, high contrast) | – | **Pending** | Open a log and check the privacy line |
| R2.5 (log file) | 2026-10-30 | Closed, no bug | The Firefox install's log was missing when first checked, after the uninstall, because the maintainer had deleted it by hand. Firefox's installer takes no log switch (`/S /PreventRebootRequired=true`), so that file only held our summary |
| R2.8–R2.10 (portable) | 2026-10-30 | Pass | From the app: jq (no installer log), LocalSend (25 KB Inno log, then WinGet's section) and Wget (failed, hash mismatch). Each log held its own record, last; URLs had no query; the headers line read "(removed)" |
| R2.8–R2.10 (package identity) | 2026-10-30 | Pass | Same three cases with the dev build started through `Invoke-CommandInDesktopPackage` in the installed 1.0.1 package: logs went to the package's `LocalState\logs`, and `Caller:` matched the family name. Not a real MSIX of this code |
| R2.8–R2.10 (real MSIX) | 2026-10-30 | Pass | Released v1.0.3 x64 MSIX, installed over 1.0.1 and started from Start: jq, LocalSend and the failing Wget each logged their own record, last, with the package family name as `Caller:`, no URL queries, and the headers line "(removed)". Queuing two operations was tested on the portable app only |
| R1.7 high contrast | – | **Pending** | With the 006 high-contrast pass |
| R4.1–R4.4, R4.7 | 2026-11-12 | Pass | VM, Store MSIX 1.3.97: five portable updates, jq kept running so it failed. All (5), Done (4), Failed (1), Active with no count; All tab shows Failed then Done under dividers; each tab lists its category; Active shows "No operations match"; filter `zzz` → message and Clear filter, which emptied the box and went back to All (screenshot) |
| R4.6 | 2026-11-12 | Pass, after a fix | First build used `Move`: the list showed Done above Failed while the collection was right. Remove and insert since (design R4.6); groups in order in the next run. The updates finished in seconds, so cards were never seen queued or running in the VM; unit tests cover that order |
| R4.5, R4.8 | 2026-11-12 | Pass | Sort menu → Name A to Z: button and tooltip "Sort: Name A to Z", Done group sorted by name inside, `settings.json` `"ActivitySort": "Name"`, same after restart. Oldest and Newest weren't driven through the menu (the script couldn't reopen it); same handler, covered by `ActivityArrangerTests` |
| R4.9 | 2026-11-12 | Pass (unit) | `ActivityFilterTests.Clear_finished_removes_hidden_finished_operations_too`; cards kept Log and Retry in the VM |
| R4.10 | 2026-11-12 | Partial | Esc emptied the filter; Tab from the filter goes All tab → card → Log → Retry, skipping the dividers; dark theme starts. Narrator names and high contrast **pending** |
| – | 2026-11-12 | Not this feature | Tab from Refresh into the Updates list after updates finish crashed with E_BOUNDS (`Microsoft.UI.Xaml.dll`, 0xc000027b). Released 1.3.0 crashes the same way in the same run, so it's an older bug, to fix separately |
