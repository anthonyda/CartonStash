# 011 – Source failures and missing WinGet: Tasks

**Status:** In progress (code done; high-contrast check and R3.5 pending)
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit.

- [x] **T1.** `SourceFailure`, `SourceFailureMessages`, `WindowsRegion`. *(R2.2, R2.3, R2.4, R2.7)*
  - Verify: `SourceFailureMessagesTests`.
- [x] **T2.** `SourceIsolation` and `CatalogConnection`: every composite find goes through it; `NotFound` names a failed source. *(R1.1 to R1.6, R2.5, R2.6)*
  - Verify: `SourceIsolationTests`; `WinGetPackageServiceIntegrationTests.A_source_left_out_is_skipped_until_the_catalogs_reset`; review: `_failedSources` lives in memory, and no code changes a source (R1.6).
- [x] **T3.** `IPackageService.SourceFailures`, `SearchViewModel.SourceNotice`, the `InfoBar` on `SearchPage`. *(R2.1)*
  - Verify: `SearchViewModelTests`.
- [x] **T4.** `WinGetAvailability`, `WinGetSetupViewModel`, `WinGetSetupWindow`, the check in `App.OnLaunched`. *(R3.1 to R3.6)*
  - Verify: `WinGetSetupViewModelTests`.
- [x] **T5.** Manual: scenarios S1 to S4 in Windows Sandbox, portable app in all four, MSIX in S3. *(all)*
  - Verify: results recorded in the log below. High contrast (R3.6) and R3.5 are T7.
- [x] **T6.** After merging with the 007 R8 work, update the note on 007's T36 and the roadmap.
- [ ] **T7.** Check the setup window and the notice in high contrast (R3.6). Check R3.5 if a catchable WinGet start failure can be produced. Then mark `Done`.

- [x] **T8.** Bind plain bools instead of x:Bind functions for anything that can become null, on every page. Found in 006 T19; it reaches the `SourceNotice` InfoBar (R2.1) and the setup window's status (R3.4), and the same pattern in the pages of specs 001, 002, 004, 005, 006 and 009. See "Messages that clear" in the design.
  - Verify: `MessageFlagTests`; before and after builds in Windows Sandbox, in the log below.

## Manual verification log

All runs 2026-11-01, Windows Sandbox, WinGet 1.29.380, version 1.0.99 built from this branch. S3 ran first in each clean Sandbox, so WinGet was really absent.

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R3.1, R3.2 | 2026-11-01 | Pass | S3, portable and signed MSIX: the window "CartonStash needs WinGet" opens, with no crash in the event log. Screenshots checked |
| R3.3 | 2026-11-01 | Pass | S3: the setup window replaces the pages, and the process stays alive with nothing activated |
| R3.4 | 2026-11-01 | Pass | S3, portable: Check again while missing says "WinGet still wasn't found." Installing App Installer with the app open, then Check again, opens the main window in the same process, and Search works |
| R3.6 | 2026-11-01 | Partial | Focus starts on Get App Installer; Check again takes focus after use; UI Automation sees both controls by name. Light theme seen. Dark and high contrast not checked (T7) |
| R3.5 | | Not run | No way found yet to produce a catchable WinGet start failure (T7) |
| R1.1, R2.1, R2.4 | 2026-11-01 | Pass | S1, region World: `ripgrep` shows 7 `winget` results and the notice "'msstore' needs a country…" |
| R1.1, R2.1, R2.2 | 2026-11-01 | Pass | S2, region US, Store blocked in the hosts file: `starship` shows results and the notice "'msstore' can't be reached…". The Store was confirmed unreachable in the same setup during the investigation |
| R1.2, R1.3, R2.6 | 2026-11-01 | Pass | S2, real services through a test harness: details, `jqlang.jq` install and uninstall all succeed, with status `InternalError` handled. An `msstore` package gives "Couldn't look up… 'msstore' can't be reached", with `0x80072EFD` on the exception. Update wasn't run: it uses the same local-first lookup as uninstall |
| R2.3 | | Unit test only | `SourceFailureMessagesTests`. No live source reporting an error with a real region was available |
| R2.5 | | Unit test only | `SourceIsolationTests`, `SearchViewModelTests` |
| R1.4 | 2026-11-01 | Pass | S4, region US, nothing blocked: `gitui` shows results and no notice. No extra calls: `SourceIsolationTests.Healthy_sources_get_no_extra_calls` |
| R1.5 | 2026-11-01 | Pass | S2: the left-out list empties after each install and uninstall, and refills on the next read while the Store stays blocked |
| R1.6 | 2026-11-01 | Pass | Review, plus the integration test comparing the source list before and after |
| Test setup | 2026-11-01 | Note | Writing `HKCU\Control Panel\International\Geo` doesn't change the region in a clean Sandbox, so S2 and S4 were repeated after `Set-WinHomeLocation -GeoId 244`. The app's notice exposed this |
| T8, details pane | 2026-11-01 | Pass | Sandbox, region US, UI Automation and screenshots: PowerToys, then `jq`, which has no privacy URL. Before the fix, `jq` kept a "Privacy policy" link; after it, the link is gone |
| T8, source notice | 2026-11-01 | Pass | Sandbox, region World then US, with Refresh sources between two searches. Before the fix, "Some sources are missing" stayed open with no message (screenshot); after it, the bar opens under World and closes under US. The fixed build needed its own fresh Sandbox, because a second pass in the same one never opened the notice |
| T8, other messages | | Unit test only | `MessageFlagTests`. The same generated `if (obj != null)` applies to each. A search failing on every source wasn't produced: blocking hosts in Sandbox had no effect |
