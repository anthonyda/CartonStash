# 010 – Background update checks: Tasks

**Status:** Done (2026-11-03), including amendment R3.14
**Design:** [design.md](design.md)
**Issues:** #14 (close with the commit that finishes the spec)

Tick a task only when its verification passes. Keep tasks small enough for one commit.

- [x] **T1.** Probe on a throwaway build, on both MSIX types: the six questions in the design's T1 results. Record the answers in design.md. *(all)*
  - Verify: each answer recorded with what was measured. If WinGet or the time limit fails, stop and return the spec for review. The probe code is not kept.
  - Done 2026-10-31 on x64, with the shared-runtime build, which both MSIX types become under 007 amendment R8. Probe code removed. Sign-in after a shutdown and a one-time timer across sleep weren't tested, and moved to T11.
- [x] **T1a.** 007 amendment R8 is implemented: T7 can't work on the GitHub MSIX before it.
  - Verify: 007 T34 and T35.
- [x] **T2.** `AppSettings`: check mode, interval, time, exclusions. *(R3.1, R3.5, R5.6)*
  - Verify: `SettingsTests.*`.
- [x] **T3.** `CheckSchedule`. *(R3.9, R3.10)*
  - Verify: `CheckScheduleTests.*`.
- [x] **T4.** `CheckHistoryStore`. *(R3.11–R3.13, R5.8)*
  - Verify: `CheckHistoryStoreTests.*`.
- [x] **T5.** `BackgroundCheckRunner` and its interfaces. *(R1.2–R1.6, R1.9, R1.10, R2.1, R2.3–R2.5, R5.2)*
  - Verify: `BackgroundCheckRunnerTests.*`.
- [x] **T6.** Exclusions on the Updates page. *(R5.1, R5.3, R5.5, R5.7, R5.9, R5.10)*
  - Verify: Updates view model tests; manual with keyboard and in high contrast.
- [x] **T7.** `Program`, manifest, `BackgroundCheckScheduler`, `AppActivityMarker`, `WindowsNotifier`. *(R1.1, R1.2, R1.5, R2.2, R3.2, R3.4, R3.7)*
  - Verify: manual on an installed MSIX; the portable app still starts (007 R1.6).
- [x] **T8.** Settings sections: Update checks, Excluded packages, Check history. *(R3.1–R3.4, R3.8, R3.11, R4.1, R5.4)*
  - Verify: `SettingsTests.*`; manual with keyboard, light, dark and high contrast.
- [x] **T9.** Badge refresh while the app is open. *(R1.7)*
  - Verify: manual. Done 2026-11-02 in the test VM (log below).
- [x] **T10.** Update specs 003, 004, 006 and 007, and `PRIVACY.md`.
  - Verify: review against the requirements' last section.
- [x] **T11.** Manual end to end on the Store version (007 R9 ended the GitHub MSIX): each mode, a missed check after sleep and after a shutdown, metered, Windows-blocked, uninstall, and an app update. Includes T1's two gaps: `UserPresent` at sign-in after a shutdown, and a daily timer across sleep. *(all)*
  - If sign-in after a shutdown starts nothing, use the startup-task fallback in the design's risks, and update the spec first.
  - 2026-11-02: all passed in the test VM and Sandbox except sign-in after a shutdown, which started nothing. Amendment R3.14 below. Only the Store build is in scope now (007 R9).
  - 2026-11-03: sign-in after a shutdown passes with the startup entry (T16).
- [x] **T12.** Mark `Done`; close #14.

### Amendment R3.14: startup entry (approved 2026-11-02)

- [x] **T13.** Probe in the test VM, on a throwaway Store-identity build: the manifest's startup entry, turned on with `RequestEnableAsync`, starts the exe after a shutdown and sign-in, with activation kind `StartupTask`; no prompt; the name shows in Startup apps; `DisabledByUser` after turning it off there. *(R3.14, R3.15)*
  - Verify: each point recorded in design.md, for an automatic sign-in and a typed one (the maintainer types it in the VM window). If the entry doesn't start the exe, stop and return the spec for review. The probe code is not kept.
  - Done 2026-11-02: every point passed, on automatic and typed sign-in. Turning the entry on also starts the exe right away; recorded in design.md, and T16 covers it.
- [x] **T14.** `ICheckScheduler.StartupBlocked` and the Settings note. *(R3.16)*
  - Verify: `CheckSettingsViewModelTests.*`.
  - Done 2026-11-03: 358 unit tests pass. `BackgroundCheckScheduler` reports false until T15 reads the entry.
- [x] **T15.** Manifest startup entry; `BackgroundCheckScheduler` turns it on and off; `Program` runs the check for `StartupTask` activation through the shared check method. *(R3.14, R3.15, R3.17)*
  - Verify: unit tests pass; the portable app still starts (007 R1.6).
  - Done 2026-11-03: 358 unit tests pass; the 1.1.6 x64 portable build opened its window in 3 s in the test VM, no crash. The app also applies the entry each time it opens, for users updating with checks on (design updated).
- [x] **T16.** Manual in the test VM, Store build: R3.10 after a shutdown and sign-in; nothing at sign-in when no check is missed; turning checks on shows nothing (the entry's immediate start finds no check due); checks off turns the entry off; the user's "off" in Startup apps is kept and Settings shows the note; one check when unlock and sign-in both fire; uninstall leaves nothing. *(R3.10, R3.14–R3.17, R3.6)*
  - Verify: logged below. Then finish T11 and T12.
  - Done 2026-11-03. The run also found that two schedule changes in a row could leave two "check on return" tasks; `BackgroundCheckScheduler.ApplyAsync` now runs one change at a time, and removes and registers with no await between them.

## Amendment R3.18: two triggers in one process (2026-11-12, bug review BR-06)

- [x] **T17.** `ActiveTasks` counts the tasks the COM server runs; only the last to end signals the process to stop. *(R3.18)*
  - Verify: `ActiveTasksTests`.
- [ ] **T18.** Manual in the test VM: daily time, sleep past it, wake and unlock: one Check history entry, and the
  next daily timer set. *(R3.18)*

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1, R1.2, R2.1, R2.2, R3.2, R3.3, R3.11 (T7) | 2026-11-01 | Pass | x64 test install beside the Store app. Daily mode set for 9:20 PM at 8:56 PM: the check ran at 9:05:51 PM, inside the 15 minutes allowed, found 2 updates, and set its next timer, with no crash and no process left. The maintainer saw the notification; clicking it brought the open window to Updates with no second window; Settings showed the history and next check |
| R3.8 (first use) | 2026-11-01 | Pass | A daily time set 5 minutes ahead showed the next check as tomorrow, as designed |
| 007 R1.6 | 2026-11-01 | Pass | x64 portable build with the custom `Main` opens its window, no crash |
| R5.1, R5.4, R5.5, R5.7, R5.9, R5.10, Q5 (T6) | 2026-11-02 | Pass | Merged build in Windows Sandbox, driven by UI Automation: "Exclude WindowsAppRuntime.1.8 from update checks" moved the row under "Excluded", the title went from "Updates (1)" to "Updates", the ID was saved, and "Include … again" was offered. Tab reaches Update, Include again, Refresh and the rows. Screenshots in dark and High Contrast Black are readable, with the focus visible |
| R3.1, R3.4, R3.8, R3.11, R4.1, R5.4 (T8) | 2026-11-02 | Pass | Same run: every section, label and empty state found by name; Remove cleared the exclusion and showed "No packages are excluded."; Tab reaches the theme, start page, scope and mode controls, and the disabled controls are skipped. With background access turned off for the app in the registry, choosing Every showed "Windows isn't allowing background checks" and registered no task |
| R3.10 after sleep, merged build | 2026-11-02 | Pass | The maintainer's PC slept 11:23 PM to 11:13 AM; the merged build ran a missed check at 11:13:51 on wake and found 2 updates, with no crash |
| Background checks in Windows Sandbox | 2026-11-02 | Explained, not an app bug | Sandbox build 26100.9757. A real daily timer started `CartonStash.exe -RegisterForBGTaskServer` at 11:19:44 and the class was registered, but Windows never called `Run`; the process was ended 12 minutes later. `Start-AppBackgroundTask` did the same, for a signed install and for loose files in developer mode. The maintainer's PC (26220) runs checks normally. Cause found later: Windows Sandbox ships with UAC off (`EnableLUA=0`), so the server ran at High integrity and backgroundTaskHost (Low, AppContainer) never reached it. With UAC turned on in the Sandbox, Windows called `Run` and checks were recorded |
| T11 app update, metered, operations running (Sandbox, UAC on) | 2026-11-02 | Pass | GitHub identity, built from main. 1.1.4 with checks on updated to 1.1.5: both tasks kept, the next check ran and found 1 update (R3.6). Ethernet set to metered: "SkippedMetered" recorded (R1.4). Operations marker held: "SkippedBusy" recorded (R1.5). No server process left after any check |
| R1.1, R1.2, R2.1, R3.9, R5.2 (T11, Store build) | 2026-11-02 | Pass | Hyper-V VM, Windows 11 26H2 (26300.9457), a standard admin user with UAC on, only the Store build 1.1.5 installed. A real daily timer with the app closed ran at 10:48 PM, the start of its 15-minute window, and found 4 updates with 1 excluded; the next timer was set and no process was left |
| R1.7, R2.5 (T9) | 2026-11-02 | Pass | Same VM, fresh install, open on Settings. The real timer's check at 11:06 PM updated the page live: "Last check: Today 11:06 PM · Schedule · 4 updates, 1 excluded · …", next check moved to tomorrow; the Updates page then showed "Updates (4)" and the Excluded section. No crash |
| R3.10 at sign-in after a shutdown (T11, T1 gap) | 2026-11-02 | **Fail** | Same VM, check due, app closed. Clean shutdown at 11:07:10 PM, start at 11:07:19, tester signed in (automatic sign-in) at 11:07:25. Nothing ran in the next 5 minutes: no process started for the package, no history. `UserPresent` doesn't fire at sign-in after a shutdown. T11 says to use the startup-task fallback, spec first |
| R3.6 uninstall (T11, Store build) | 2026-11-02 | Pass | Same VM after the sign-in test: no package, no tasks and no data folder left |
| GitHub and Store builds side by side | 2026-11-02 | Not supported | Same VM, both 1.1.5 identities installed: every trigger of the GitHub package crashed backgroundTaskHost (`0xc000027b` in `biwinrt.dll`, stowed `0x8007001F`), and Windows started the Store package's server for it. Both packages register the same CLSID. Out of scope: only the Store build is published (007 R9) |
| R3.15, R3.2 (T16, checks on) | 2026-11-03 | Pass | Test VM, Store build 1.1.6, fresh install. The entry stays off with checks off. Choosing Every turned it on, with no window, no check and no note; one timer and one "check on return" task. The first 1.1.6 build left two "check on return" tasks, fixed before this run |
| R3.10, R3.14, R3.17 (T16, missed check at sign-in) | 2026-11-03 | Pass | Same VM, check due, app closed. Shutdown and start: tester signed in at 6:21:14 PM, Windows started the app through the entry at 6:21:23, and the missed check ran at 6:21:27 and found 5 updates. One history entry, no window, no process left |
| R3.14 (T16, nothing missed) | 2026-11-03 | Pass | Shutdown and start again: the entry started the app at 6:23:27 PM, which ran no check, showed no window and exited |
| R3.15, R3.16 (T16) | 2026-11-03 | Pass | Checks off turned the entry off, and on turned it back on. Turned off in Startup apps: the app opened with the note "Windows won't start CartonStash at sign-in", kept it after changing the schedule, hid it with checks off and showed it again with checks on. The entry stayed off throughout |
| R3.6 (T16, uninstall) | 2026-11-03 | Pass | No package, startup entry, task or data folder left; Startup apps no longer lists it |
| App Certification Kit, Store build 1.1.1 x64 | 2026-11-02 | Partial | Kit copied from the host SDK into Windows Sandbox, since the admin prompt couldn't be approved over a remote session. 14 tests pass, including app manifest, resources, background-task capabilities and run level. The six metadata tests (ExclusiveTo, Type location, the two type-name tests, metadata correctness and Properties) couldn't start because the copied Kit found no platform metadata folder, so they weren't run. The optional Blocked executables test flags the usual launch-process APIs in the runtime and .NET files, and the optional Platform appropriate files test fails with no detail. DPI awareness is only a warning, because WinUI sets it at run time. Store certification on submission still decides |
