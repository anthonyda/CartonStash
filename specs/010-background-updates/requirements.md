# 010 – Background update checks: Requirements

**Status:** Approved (2026-10-31), including the R3.8 change from T1's results. Amendment R3.14 (missed check at sign-in through a startup entry): approved 2026-11-02. Amendment R3.18 (two triggers in one process, bug review BR-06): **Draft** 2026-11-12
**Depends on:** 002, 003, 004, 006, 007
**Issues:** [#14](https://github.com/anthonyda/CartonStash/issues/14)

## Problem

CartonStash only finds out about updates while its window is open, so users have to start the app and look at the Updates
page to know that something is waiting. Users who leave it closed miss updates, including security fixes.

**Approach, decided 2026-10-31:** a Windows background task. The MSIX registers a timer, Windows starts a short check at about
the chosen interval or daily time, and the check exits. Nothing stays running between checks, and the app has no tray icon. Closing the
window still quits. Options considered: a tray app, a check at sign-in, a scheduled task, a separate helper program, and no
background work.

## User stories

- **US1.** As a user, I want the app to check for updates on a schedule, even while it's closed, so I don't have to remember.
- **US2.** As a user, I want a notification when updates are available, and to reach them in one click.
- **US3.** As a user, I want to choose how often it checks or the time of day it checks, or turn it off.
- **US4.** As a portable app user, I want to know this isn't available to me, and why.
- **US5.** As a user, I want to exclude a package from update checks, so I can easily stop hearing about one I've decided not to update.
- **US6.** As a user, I want to see the history and results of scheduled checks inside the app.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | While **Background update checks** is on, the app shall have Windows run an update check at about the chosen interval or daily time, whether or not the app is open. |
| R1.2 | US1   | A check shall start, finish and exit by itself, with no window, tray icon or process left running. |
| R1.3 | US1   | A check shall get its result from WinGet (constitution P1), with the same rules as the Updates page (004 R1.1). |
| R1.4 | US1   | A check shall never install, update or uninstall anything (constitution P2). |
| R1.5 | US1   | If a check is due while the app is running operations, then that check shall be skipped. |
| R1.6 | US1   | If a check fails (no network, or Windows ends it early), then it shall show no notification, record the failure in Check history (R3.11), and try again at the next scheduled time. The app shall never crash or show an error dialog from a check. |
| R1.7 | US1   | While the app is open, it shall update the Updates count badge when a check finishes, or the next time it refreshes. |
| R1.8 | US1   | A check shall run without elevation (constitution A5). |
| R1.9 | US1   | A check shall first refresh WinGet's sources, so it doesn't report stale data. If the refresh fails or takes too long, then the check shall use the data WinGet already has. |
| R1.10 | US1  | While the PC is on a metered connection, checks shall be skipped. |
| R2.1 | US2   | When a check finds available updates, the app shall show a Windows notification with the number of updates and up to three package names. |
| R2.2 | US2   | When the user activates the notification, the app shall open on the Updates page, or show that page if the app is already open. |
| R2.3 | US2   | The app shall notify at every check that finds updates, including updates it has already shown. Each new notification shall replace the previous one, so they don't pile up. |
| R2.4 | US2   | If a check finds no updates, then it shall show no notification. |
| R2.5 | US2   | While the app's window is in the foreground, a check shall show no notification, and the badge shall show the updates instead. |
| R3.1 | US3   | Settings shall offer **Background update checks** with exactly one of three modes: **Off** (the default), **Every** (1 hour, 6 hours, 12 hours, 24 hours or 7 days), or **Every day at** (a time the user picks, in local time, default 10:00 PM). Only the chosen mode's control shall be available. |
| R3.2 | US3   | When the user changes the mode, interval or time, the app shall replace the schedule at once, without a restart, and the previous schedule shall stop. |
| R3.8 | US3   | Settings shall say that Windows decides the exact moment, so a check can start up to 15 minutes before the chosen time, or later than it. |
| R3.9 | US3   | The daily time shall follow the PC's local time, including daylight saving changes. |
| R3.10 | US3  | If the PC was off or asleep when a check was due, in either mode, then the missed check shall run at the next sign-in or unlock, once only, and the following check shall still aim for the chosen schedule. If no check was missed, then signing in shall not run one. |
| R3.3 | US3   | Settings shall show when the last check finished, and whether it found updates. |
| R3.4 | US3   | If Windows doesn't allow the app to run in the background, then Settings shall say so and say where to change it, and the app shall keep working normally. |
| R3.5 | –     | Settings from an older version shall load with checks off, without reporting a reset (006 R5.4). |
| R3.6 | –     | Uninstalling the app shall remove the schedule, leaving nothing behind. |
| R3.7 | –     | When the app is updated, the chosen schedule shall keep working. |
| R3.11 | US6  | Settings shall offer **Check history**, listing each background check of the last 7 days, newest first, with its date and time, what started it (schedule, or a missed check at sign-in), and its result: updates found with their number and names, no updates, skipped (operations running, or metered connection), or failed with a readable reason. |
| R3.12 | US6  | The app shall delete history entries older than 7 days, whether or not checks are still running. Turning checks off shall keep the history until it ages out. |
| R3.13 | US6  | The history shall stay on the PC, hold no user name or file paths, and be plain text in the UI. A failure to write it shall never fail a check (009 R2.5). |
| R4.1 | US4   | In the portable app, the setting shall be unavailable, with a short note that it needs the Microsoft Store version. *(Reworded by 007 R9.)* |
| R5.1 | US5  | Each row on the Updates page shall offer **Exclude from update checks**, keyboard reachable with an accessible name (constitution Q5). |
| R5.2 | US5  | A background check shall leave excluded packages out of the notification's count and names (R2.1), and the message shall state how many were ignored ("3 updates available, 1 ignored"). If only excluded packages have updates, then it shall show no notification. |
| R5.3 | US5  | Excluding a package shall apply to it by package ID, for every future version, until the user removes it. |
| R5.4 | US5  | Settings shall list excluded packages, each with a keyboard-reachable **Remove**, and removing one shall apply from the next check. |
| R5.5 | US5  | The app shall show an excluded package's row as excluded on the Updates page. The user shall still be able to update it there. |
| R5.7 | US5  | The Updates count badge (004 R4.1) shall not count excluded packages. |
| R5.8 | US5  | Check history shall state how many packages were excluded from each check's result ("3 updates, 1 excluded"). |
| R5.9 | US5  | **Update all** shall leave out excluded packages, and its confirmation count shall match (004 R2.3). |
| R5.10 | US5 | The Updates page title shall count packages with an available update that aren't excluded, matching the badge. |
| R5.6 | US5  | Exclusions shall be saved with the app's settings, shall not change anything in WinGet, and shall survive app updates. |

## Out of scope

- Installing updates automatically, even as an option. It needs a constitution change (P2), so it would be its own spec.
- Background checks in the portable app. Windows needs a packaged app for this, and a scheduled task was rejected.
- A tray icon, or keeping the app running after its window closes.
- Exact timing. Windows may delay or skip a check, for example on battery saver. A check missed while the PC was off or asleep is caught up at the next sign-in or unlock (R3.10).
- Checking for updates to CartonStash itself: the Store does this, and it's unchanged for the GitHub files.
- Snoozing or quiet hours. Windows' own Do Not Disturb still applies.
- Excluding a package from WinGet itself. Pins are a separate feature (spec 008), and an exclusion here never stops the user or WinGet from updating it.

## Decisions (2026-10-31)

- **On by default?** Off. Background activity and notifications are the user's choice (P2).
- **Remind about updates already shown?** Yes, at every check that finds updates (R2.3). To stop hearing about a package the user skips on purpose, they can exclude it (R5.1).
- **Refresh WinGet's sources first?** Yes (R1.9). The design has to fit this within Windows' time limit for a background task.
- **Skip checks on metered connections?** Yes (R1.10).
- **Interval or daily time?** One or the other, never both (R3.1).
- **Where the check history lives.** In Settings, beside the schedule, because the Activity pane lists one session's operations and checks mostly happen while the app is closed.
- **Daily time, not weekly or several times a day.** Keeps the setting simple.

- **Do excluded packages count?** Not in the badge (R5.7). The notification and Check history state how many were ignored (R5.2, R5.8).
- **Catch up after sleep too, and in both modes?** Yes (R3.10).
- **After a check is skipped because operations are running, retry soon?** No (R1.5). It waits for the next scheduled time.

- **Does Update all include excluded packages?** No (R5.9). They can still be updated one at a time.
- **What does the Updates page title count?** Packages with an available update that aren't excluded (R5.10).

## Risks, to prove first

The design's first task is a probe, because these decide whether this approach works:

- **WinGet from a background task.** Checks must reach WinGet's COM server from a process Windows starts. Unproven.
- **Time and memory limits.** Windows limits a background task's run time, and the app bundles .NET, so a search through WinGet may take too long. Unproven.
- **Runtime support.** Microsoft documents `BackgroundTaskBuilder` for packaged WinUI apps, and time triggers are the reported working case. It needs testing with the Store and GitHub MSIX.
- **Daily time.** Windows has no "at 10 PM" timer: a one-time timer starts 0 to 15 minutes after its delay, so each check must set the next one (R3.1). If that chain breaks, opening the app has to restore it.
- **Missed checks at sign-in (R3.10).** This needs a second trigger for sign-in or unlock. One [report against an older SDK](https://github.com/microsoft/WindowsAppSDK/issues/3840) says system triggers failed to register while time triggers worked, so it must be tested. If it fails, the fallback is a hidden check at Windows startup through the MSIX's startup task, which the user could see in Startup apps.
- **History can't show what never ran (R3.11).** A check that Windows delays or drops leaves no entry, because no code ran. To record metered skips, the check must start and decide for itself, not use a Windows condition that stops it from starting.
- **Store review.** The manifest gains a background task and a COM server entry.

If the probe fails, this spec returns for review before any more work.

## Amendment R3.14: missed check at sign-in through a startup entry (approved 2026-11-02)

**Why.** T11 tested R3.10 after a real shutdown, in a Windows 11 26H2 VM with the Store build: the check was due, the user
signed in, and nothing ran. Windows' "user present" trigger doesn't fire at sign-in after a shutdown. It still works on
unlock and after sleep (T1, and T11 on the maintainer's PC). This is the fallback named in the risks below: a startup
entry in the MSIX, which Windows starts at sign-in.

| ID   | Story | Criterion |
|------|-------|-----------|
| R3.14 | US3  | While **Background update checks** is on, the MSIX shall have Windows start it at sign-in through a startup entry named **CartonStash update check**. Started this way, the app shall run the missed check of R3.10 if one is due, with no window, and exit (R1.2). If none is due, then it shall exit at once without a check. |
| R3.15 | US3  | Turning checks on shall turn the startup entry on, and turning them off shall turn it off, without asking the user. The app shall never turn it back on after the user turned it off in Windows' Startup apps. |
| R3.16 | US3  | If the user or a policy turned the startup entry off while checks are on, then Settings shall say that a check missed while the PC was off waits for the next unlock, and where to turn the entry back on (Settings > Apps > Startup). |
| R3.17 | –    | A sign-in that fires both the startup entry and the "user present" trigger shall run at most one check (R3.10, "once only"). |

R3.10 doesn't change: a check missed while the PC was off or asleep still runs at the next sign-in or unlock. R3.14 is how
sign-in is covered; unlock and sleep keep the existing trigger. R3.6 covers the entry too: uninstalling the MSIX removes it.
The portable app has no startup entry (R4.1).

### Open questions (amendment R3.14)

- **Turn the entry on by default when checks are on?** **Decided 2026-11-02: yes**, as R3.15 says. Without it, R3.10 fails after every
  shutdown. Users see it in Startup apps and Task Manager, and can turn it off there (R3.16).
- **What does the user see at sign-in?** **Decided 2026-11-02:** nothing but a notification when a missed check finds updates (R2.1). No
  window, no splash screen, no tray icon.
- **Run a missed check when the user opens the app, too?** **Decided 2026-11-02: no.** The app's own Updates page already refreshes when it
  opens, so a background check then would only duplicate it.

## Amendment R3.18: two triggers in one process (2026-11-12, Draft, bug review BR-06)

R3.17 was checked with the startup entry and "user present", which are two processes. The timer task and the
"check on return" task are both served by the background COM server, one process. When the second fires while the
first check runs, its `Run` finds the mutex, returns, completes its deferral and signals the server to stop. `Main`
returns and the process ends with the first check still running: no history entry, no notification, and in daily mode
the next timer isn't set until an unlock or the app's start repairs it. Waking the PC after a missed daily time is the
likely case: the late timer and "user present" fire seconds apart, and a check takes longer than that.

| ID    | Story | Criterion |
|-------|-------|-----------|
| R3.18 | –     | When a second trigger arrives in the same process while a check is running, the running check shall finish and be recorded as if the second hadn't come (R3.17's "at most one check" unchanged). The background process shall end only when every trigger it served is done, or at its time limit. |

Plausible, not yet seen: the check in the VM comes first (daily time, sleep past it, wake and unlock, read Check
history).

## Changes to other specs, if approved

- **004:** the "Scheduled / automatic background updates" line narrows to automatic installs, and R1.1, R2.3 and R4.1 gain the exclusions (R5.5, R5.7, R5.9, R5.10).
- **003:** the "Windows toast when minimized?" decision is replaced by R2.1.
- **006:** gains the setting and the last check line.
- **006 amendment R3.4:** background checks use `CatalogConnection`, so a source that isn't accepted stays out of them too. The check at start doesn't run in the background task, so an unanswered source stays out until the user opens the app.
- **007 (amendment R3.14):** the manifest also gains a startup task. The Store listing's notes for certification should say why the app has one.
- **007:** the manifest gains the background task entries, and `PRIVACY.md` must mention the checks, which WinGet carries out, and the 7-day history kept on the PC.
