# 009 – Activity pane: Requirements

**Status:** Done (2026-10-30). Amendment R2.7 (log privacy note) approved 2026-10-30. Amendment R2.8–R2.10 (WinGet's record in the log) approved 2026-10-30
**Depends on:** 003 (operation queue, Activity pane), 006 (settings)
**Issues:** [#2](https://github.com/anthonyda/CartonStash/issues/2) (operation logs), [#3](https://github.com/anthonyda/CartonStash/issues/3) (pin the pane), [#4](https://github.com/anthonyda/CartonStash/issues/4) (redesign)

## Problem

The Activity pane is where the user follows and understands installs, updates and uninstalls, but it falls short:
entries run together and are hard to tell apart, a failure only gives a one-line reason, and the pane closes as soon as the
user clicks elsewhere, so it can't be watched while using the app.

## User stories

- **US1.** As a user, I want to tell entries apart and see at a glance what succeeded, failed or is still running. (#4)
- **US2.** As a user, I want to read the log of an operation, especially a failed one, and share it in a bug report. (#2)
- **US3.** As a user, I want to pin the pane so it stays open beside the page while I work. (#3)

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | Each operation shall be shown as a separate card, clearly distinct from the next. |
| R1.2 | US1   | Each card shall show, in order of prominence: the package icon (005 R5) and name, the action (Install, Update, Uninstall), a state icon with the state in words, then the result message. The state shall never rely on color alone (constitution Q5). |
| R1.3 | US1   | The states shall be: Queued, Running (with phase and progress), Succeeded, Succeeded with restart required, Failed, Cancelled. Failed cards shall stand out from the others. |
| R1.4 | US1   | Each card shall show the time the operation started, as a short local time (the date too when it's not today). |
| R1.5 | US1   | Active operations shall be listed first, then finished ones, newest first. |
| R1.6 | US1   | Cancel and Retry (003 R3.2, R5.2) shall stay on the card, keyboard reachable with accessible names. |
| R1.7 | –     | The pane shall work in light, dark and high-contrast themes. |
| R2.1 | US2   | Each finished card shall offer a **Log** button with an accessible name naming the operation ("Log for Update Git"). |
| R2.2 | US2   | When the user presses Log, the app shall show the operation's log in a dialog: a summary (package, ID, version, action, start and end time, result, message, WinGet status, installer exit code, extended error code) followed by the installer's own log when one exists. |
| R2.3 | US2   | If the installer wrote no log, then the dialog shall say so and still show the summary. |
| R2.4 | US2   | The log dialog shall offer **Copy** (the whole log as text) and **Open folder** (the app's log folder). |
| R2.5 | US2   | The app shall keep logs across restarts for at most the 50 newest operations, deleting older ones at startup. Log failures shall never fail an operation. |
| R2.6 | –     | Log text shall be shown as plain text, never interpreted. |
| R2.7 | US2   | The log dialog shall say that logs can contain the user's name, PC name and file paths, and should be checked before sharing. *(Security review, 2026-10-30: Copy exists for bug reports, and those are public.)* |
| R2.8 | US2   | When an operation finishes, the app shall add WinGet's own record of that operation to its log, after the installer's log, and label it as WinGet's. *(2026-10-30: Firefox's installer has no log option, so the log held only the summary.)* |
| R2.9 | US2   | If WinGet's record can't be found, can't be read, or can't be matched to exactly one operation, then the log shall say so, and shall never show a record that may belong to another operation. This shall never fail the operation (R2.5). |
| R2.10 | US2  | Before WinGet's record is added, the app shall remove URL credentials, URL query strings and download header values, so that a private source's secrets don't reach a shared log. |
| R3.1 | US3   | The pane header shall offer a **Pin** toggle with an accessible name and tooltip. |
| R3.2 | US3   | While pinned, the pane shall stay open beside the page, the page shall resize next to it, and clicking elsewhere shall not close it. |
| R3.3 | US3   | While unpinned, the pane shall behave as today (overlay, closes on click outside). |
| R3.4 | US3   | The pinned state shall be saved in Settings (006) and restored at start, with the pane open. |
| R3.5 | US3   | If the window is too narrow to fit the pane beside the page (below 1000 px), then the pane shall act as an overlay until the window is wide enough, keeping its pinned state. |
| R3.6 | US3   | While pinned, the pane's close button shall be hidden; unpinning is the way to close it. |

## Out of scope

- Live log streaming while an operation runs (the log is shown once it has finished).
- ~~WinGet's internal diagnostic logs in the dialog. They can't be matched reliably to one operation (a file may cover several). The log dialog can link to their folder instead.~~ *Replaced by amendment R2.8–R2.10: 13 of 13 operations checked on 2026-10-30 had their own file.*
- Resizing the pane by dragging (fixed 400 px).
- Keeping the operation list itself across restarts: the list stays per session, as in 003. Log files stay on disk for support.

## Open questions

- **Close while pinned (R3.6):** **Decided 2026-10-30:** the close button is hidden while pinned, and pressing the Activity navigation item
  does nothing special; unpin to close. Simple, and "pinned" means it stays.
- **Log dialog or just open the file?** **Decided 2026-10-30:** an in-app dialog. It works the same on every package, and a summary is always there, even
  when the installer wrote nothing.
- **Show Log on running operations?** **Decided 2026-10-30:** no, finished only (no live streaming).
- **Keep 50 logs?** **Decided 2026-10-30:** yes, about 50 files, usually a few MB at most in total.
- **R2.8: always add WinGet's record, or only when the installer wrote nothing?** **Decided 2026-10-30: always.** It's the same shape on every package, and it shows the
  arguments the installer got, which also helps when an installer log exists.
- **R2.8: where in the log?** **Decided 2026-10-30: last.** The dialog keeps the end of a very long log, so a large installer log can't push it out.
