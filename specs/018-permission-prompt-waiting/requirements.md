# 018 – Say when Windows' permission prompt is waiting: Requirements

**Status:** In progress. Approved 2026-11-13: Q1 and Q3 as proposed, Q2 as answered by the maintainer (a notification after a minute, R1.4)
**Depends on:** 003, 004, 009, 010, 016, 017

## Problem

Many installers ask Windows for administrator rights by themselves (an `exe` installed for all users, for example).
WinGet starts them from its own background process, so Windows doesn't show the permission (UAC) prompt over the
desktop: it only flashes a shield button in the taskbar. CartonStash keeps reading "Updating…" and the queue waits
behind it with no end. During **Update all** this is easy to miss: the user sees a list that stopped moving and nothing
saying that Windows is waiting for them.

003 R8.3 already says "Windows may ask for permission" on the card, but only for all-users MSI packages it runs in
Default mode, and only as a possibility, before anything happens. Every other installer that asks gets no hint at all.

## User stories

- **US1.** As a user updating several packages, I want CartonStash to tell me clearly when Windows is waiting for my
  permission, and where to find the prompt, so that the queue doesn't sit there while I think it's still working.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | While the queue is running an operation (the one it took off its waiting list, whatever its phase, including 017's elevated run) and Windows' permission prompt is open (R1.6), the app shall show a banner at the top of the window, on every page: "Windows is waiting for your permission to continue. Select the flashing shield in the taskbar to answer it." |
| R1.2 | US1   | In the same state, the running operation's Activity card and its row shall read "Waiting for your permission…" instead of "Updating…" / "Installing…" / "Uninstalling…", with a shield icon. Queued operations keep "Queued". |
| R1.3 | US1   | When the prompt closes (accepted or declined), the banner and the waiting text shall go away within about a second, and the operation shall show its normal progress or result again. |
| R1.4 | US1   | If the prompt has stayed open for one minute without a break, the app shall show one Windows notification: "Windows is waiting for your permission" / "An operation in CartonStash can't continue until you answer the prompt. Select the flashing shield in the taskbar." It shall show at most one per prompt (a prompt that closes and opens again starts a new minute), and shall remove it from the notification center when the prompt closes. Selecting it brings CartonStash to the front. No notification before the minute, and the app doesn't flash its own taskbar button. *(Q2.)* |
| R1.5 | US1   | The taskbar updates badge (016) shall be unchanged: the waiting state isn't an update count. |
| R1.6 | –     | "The prompt is open" means that Windows' consent process (`consent.exe`) is running. The app only looks for it while the queue is running an operation (R1.1), at most once a second, and stops when it isn't. It never touches that process (P1: read only). |
| R1.7 | –     | The wording shall not claim that the prompt belongs to the package: another program may have opened it. The banner says that Windows is waiting, not which installer asked. |
| R1.8 | –     | The banner and the card text shall be read by Narrator when they appear (a live region), and shall follow light, dark and high contrast themes. |
| R1.9 | –     | 003 R8.3's line ("Windows may ask for permission…") stays as it is for the operations it applies to; R1.2 replaces it only while the prompt is actually open. |

## Out of scope

- Bringing the prompt to the front or answering it. Windows doesn't let an unelevated app reach the prompt (UIPI and
  the secure desktop), and the app shouldn't try.
- Running operations with administrator rights from the start to avoid the prompt (017 covers the retry; A5 stays).
- A timeout that cancels an operation whose prompt is never answered.

## Open questions

- **Q1. How to tell that the prompt is open?** *Proposed:* look for a process named `consent` in the process list
  (R1.6). An unelevated app can see its name, though not open it. It can't tell whose prompt it is, hence R1.7. To
  measure in the VM first (T1): that the name is visible unelevated, from the portable build and the Store MSIX, and
  that it appears both when the prompt is deferred to the taskbar and when it shows on the secure desktop. The
  alternative, "Running for more than N seconds with no progress", would also fire for long silent installers, so it's
  only a fallback if T1 fails. **Answer, 2026-11-13:** yes, as proposed.
- **Q2. Flash CartonStash's own taskbar button (R1.4)?** *Proposed:* yes, once, only when the window isn't in front.
  Alternative: a Windows notification instead. **Maintainer's answer, 2026-11-13:** a notification, only once the
  prompt has been open for at least a minute; no flashing.
- **Q3. Also during background checks (010)?** They don't install anything, so no. Proposed: R1 applies to the queue's
  operations only. **Answer, 2026-11-13:** no, as proposed.
