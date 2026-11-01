# 010 – Background update checks: Tasks

**Status:** In progress
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
- [ ] **T6.** Exclusions on the Updates page. *(R5.1, R5.3, R5.5, R5.7, R5.9, R5.10)*
  - Verify: Updates view model tests; manual with keyboard and in high contrast.
- [x] **T7.** `Program`, manifest, `BackgroundCheckScheduler`, `AppActivityMarker`, `WindowsNotifier`. *(R1.1, R1.2, R1.5, R2.2, R3.2, R3.4, R3.7)*
  - Verify: manual on an installed MSIX; the portable app still starts (007 R1.6).
- [ ] **T8.** Settings sections: Update checks, Excluded packages, Check history. *(R3.1–R3.4, R3.8, R3.11, R4.1, R5.4)*
  - Verify: `SettingsTests.*`; manual with keyboard, light, dark and high contrast.
- [ ] **T9.** Badge refresh while the app is open. *(R1.7)*
  - Verify: manual.
- [x] **T10.** Update specs 003, 004, 006 and 007, and `PRIVACY.md`.
  - Verify: review against the requirements' last section.
- [ ] **T11.** Manual end to end on the Store version (007 R9 ended the GitHub MSIX): each mode, a missed check after sleep and after a shutdown, metered, Windows-blocked, uninstall, and an app update. Includes T1's two gaps: `UserPresent` at sign-in after a shutdown, and a daily timer across sleep. *(all)*
  - If sign-in after a shutdown starts nothing, use the startup-task fallback in the design's risks, and update the spec first.
- [ ] **T12.** Mark `Done`; close #14.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1, R1.2, R2.1, R2.2, R3.2, R3.3, R3.11 (T7) | 2026-11-01 | Pass | x64 test install beside the Store app. Daily mode set for 9:20 PM at 8:56 PM: the check ran at 9:05:51 PM, inside the 15 minutes allowed, found 2 updates, and set its next timer, with no crash and no process left. The maintainer saw the notification; clicking it brought the open window to Updates with no second window; Settings showed the history and next check |
| R3.8 (first use) | 2026-11-01 | Pass | A daily time set 5 minutes ahead showed the next check as tomorrow, as designed |
| 007 R1.6 | 2026-11-01 | Pass | x64 portable build with the custom `Main` opens its window, no crash |
