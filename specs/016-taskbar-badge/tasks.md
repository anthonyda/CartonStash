# 016 – Updates count on the taskbar icon: Tasks

**Status:** In progress
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit.

- [x] **T1.** Add `ITaskbarBadge` to Core/Services. *(R1.1)*
  - Verify: `dotnet build`.
- [x] **T2.** `ShellViewModel` sends the Updates count to the badge: nothing while null, first count always, only on change. *(R1.1–R1.6)*
  - Verify: `ShellViewModelTests.TaskbarBadge_*` (null sends nothing; first count sends; change sends; same value not repeated; 0 sends 0; error after a count sends nothing).
- [x] **T3.** `BackgroundCheckRunner` sets the badge after a finished check, not after a failure, skip or not-due run. *(R1.2, R2.1, R2.2)*
  - Verify: `BackgroundCheckRunnerTests.TaskbarBadge_*` (updates → n; none → 0; excluded left out; failed / busy / metered / not due → no call).
- [x] **T4.** `WindowsTaskbarBadge` in App/Services over `BadgeNotificationManager`, catching every failure; wire into DI and `BackgroundCheckTask`. *(R3.1, R3.2)*
  - Verify: `dotnet build`, `dotnet test` (894 passed, 2026-11-12). The portable zip start (R3.1) moved to T5: no test builds on the host.
- [ ] **T5.** Manual check in the CartonStash-Test VM with a packaged build. *(R1.1–R1.4, R2.1, R3.1, R3.2)*
  - Verify: badge shows the count; follows an update and an exclusion; clears at 0; stays after closing; a scheduled check while closed sets it; nothing when taskbar badges are off; the portable zip starts with no error (R3.1).

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
