# 018 – Say when Windows' permission prompt is waiting: Tasks

**Status:** In progress
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit.

- [x] **T1.** Probe in the test VM: `Process.GetProcessesByName("consent")` from an unelevated portable build and from
  the Store MSIX, with the prompt deferred to the taskbar and on the secure desktop. Record the result; if it fails,
  stop and update the requirements (Q1). *(R1.6)*
  - Verify: manual, recorded below.
- [x] **T2.** `IPermissionPromptProbe`, `ConsentProcessProbe`. *(R1.6)*
  - Verify: T1 in the VM (the probe is the same call); unit tests use a fake probe.
- [x] **T3.** `PackageOperation.IsWaitingForPermission`, `StatusText`, `ShowPermission`. *(R1.2, R1.9)*
  - Verify: `PermissionPromptTests.An_open_prompt_marks_the_running_operation_and_opens_the_banner_R1_1_R1_2`, `The_may_ask_line_hides_while_the_waiting_text_shows_R1_9`.
- [x] **T4.** `PackageOperationQueue.Running` and `RunningChanged`. *(R1.6)*
  - Verify: `PermissionPromptTests.The_queue_says_which_operation_it_runs_R1_1`.
- [x] **T5.** `PermissionPromptMonitor` with `IPermissionNotifier`. *(R1.1–R1.4, R1.6)*
  - Verify: `PermissionPromptTests` (11 tests, the cases listed in the design).
- [x] **T6.** App: banner InfoBar, `ShellViewModel.IsPermissionBannerOpen`, timer wiring in `App.xaml.cs`. *(R1.1, R1.3, R1.8)*
  - Verify: build; manual in T9.
- [x] **T7.** App: shield glyph and live region on the card and the row. *(R1.2, R1.8)*
  - Verify: build; manual in T9.
- [x] **T8.** App: `WindowsPermissionNotifier`, activation with `page=activity`, removal on shutdown. *(R1.4)*
  - Verify: build; manual in T9.
- [ ] **T9.** End check in the test VM: Update all with two all-users installers; prompt left over a minute, then
  accepted; again declined; Narrator; dark and high contrast; taskbar badge unchanged. *(R1.1–R1.5, R1.8)*
  - Verify: manual, recorded below.
- [ ] **T10.** CHANGELOG entry; roadmap status. 

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| Q1 (T1) | 2026-11-13 | Pass | Test VM, Store test build 1.4.3 (`builds8`), tester unelevated (`t018p-run.ps1`). VLC 3.0.23 and 7-Zip 26.05 installed for all users; Update all. `consent.exe` was in tester's process list 3 s later; the prompt was deferred to the taskbar (flashing shield), as the user reported. The Store build's own probe found it (banner shown). Portable build not run separately: an unelevated PowerShell (same integrity as the portable exe) saw it |
| R1.1, R1.2 (T9) | 2026-11-13 | Pass | 3 s after the prompt opened: banner "Windows is waiting for your permission to continue / Select the flashing shield in the taskbar to answer it." on Updates; 7-Zip's row and card read "Waiting for your permission…" with the shield; VLC stayed "Queued" (`p1-banner.png`, `p2-activity.png`) |
| R1.4 (T9) | 2026-11-13 | Pass | Notification "Windows is waiting for your permission" at 61 s, not before; one only (`p3-notification.png`). Selecting it (opens Activity) not checked |
| R1.3 (T9) | 2026-11-13 | Pass | Prompt declined (host ended `consent.exe` as admin): banner and waiting text gone within 2 s; 7-Zip's card shows its result; VLC went on with 003 R8.3's line (`p4-closed.png`). Accepting the prompt not checked (no click on the secure prompt from a script). Finding, outside 018: a declined MSI prompt ends as "Failed, error code 1625" instead of Cancelled |
| R1.5, R1.8 | – | Not checked | Taskbar badge stayed "2" in the shots; Narrator, dark and high contrast not checked |
