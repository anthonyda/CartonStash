# 016 – Updates count on the taskbar icon: Requirements

**Status:** Approved (2026-11-12), with the proposed answers to Q1 and Q2. **Done** 2026-11-12
**Depends on:** 004 (R4.1 Updates count badge), 010 (background checks; R5.7 excluded packages don't count)

## Problem

The number of available updates shows only inside the window, on the Updates navigation item (004 R4.1). With the
window minimized, behind other windows, or closed, the user can't see that updates are waiting unless a background
check shows a notification (010 R2), and notifications are easy to miss or dismiss. Windows can show a number badge on
an app's taskbar icon (and its Start pin), like Mail or Teams do.

## User stories

- **US1.** As a user, I want the taskbar icon to show how many updates are available, so I can see it at a glance
  without opening the window.
- **US2.** As a user, I want that number to stay correct, so it never claims updates I've already installed.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | While the app is open and the Updates count (004 R4.1) is above 0, the app shall show that number as a badge on its taskbar icon. |
| R1.2 | US1   | The taskbar badge shall show the same number as the Updates navigation badge, leaving out excluded packages (010 R5.7). |
| R1.3 | US2   | When the Updates count changes (an update finishes, a refresh, an exclusion, a background check while open, 010 R1.7), the app shall update the taskbar badge to match. |
| R1.4 | US2   | When the Updates count becomes 0, the app shall clear the taskbar badge. |
| R1.5 | US2   | Until the installed data has loaded, the app shall not show a number it hasn't read from WinGet in this session: it keeps the badge as it is until the first count, then sets or clears it (P1). |
| R1.6 | US2   | If the installed data fails to load, then the app shall leave the badge as it is. |
| R2.1 | US1   | When a background check finishes (010) and the app is closed, it shall set the taskbar badge to the count it found, or clear it for 0. |
| R2.2 | US2   | If a background check fails (no network, source failure), then it shall leave the badge as it is. |
| R3.1 | US1   | If Windows can't show badges (badges turned off in taskbar settings, or the build has no package identity), then the app shall carry on with no error and no message. |
| R3.2 | US1   | The badge shall carry no text other than the number; Windows shows "99+" above 99. |

## Out of scope

- A setting in CartonStash to turn the badge off. Windows has its own ("Show badges on taskbar apps").
- Glyph badges (alert, attention) and badges for running operations or errors.
- Overlay icons on the taskbar button (`ITaskbarList3`): they disappear when the window closes.
- The portable zip and `dotnet run` builds: they have no package identity, so R3.1 applies.

## Open questions

- **Q1 (answered 2026-11-12: yes). Should the background check set the badge while the app is closed (R2.1)?** Proposed: yes. Without it, the
  badge goes away whenever the app closes and isn't much use beyond the in-window badge. With it, a badge can be
  stale until the next check or the next start if the user updates from outside CartonStash (P1); the next check or
  start corrects it (R1.5, R2.1).
- **Q2 (answered 2026-11-12: keep it). Clear the badge when the app closes?** Proposed: no, keep it, for the same reason as Q1.
