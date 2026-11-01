# 010 – Background update checks: Design

**Status:** Approved (2026-10-31), including the changes from T1's results. Depends on 007 amendment R8.
**Requirements:** [requirements.md](requirements.md)

## Overview

The MSIX registers background tasks with Windows. When one fires, Windows starts `CartonStash.exe` as a COM server with no window. It runs one
check through the existing `IPackageService`, writes one history entry, shows a notification if needed, and exits. All decisions (is a check
missed, what's the next time, what does the message say) live in plain Core classes, so they can be unit tested. Windows-only code stays in thin
App services, and Task 1 is a probe that proves those Windows parts before anything else is built.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `AppSettings` | `CheckMode` (Off, Interval, DailyTime), `CheckIntervalHours`, `CheckTime`, `ExcludedPackageIds` | changed |
| Core/Models | `CheckResult`, `CheckRecord` | Trigger, time, outcome (Updates, None, SkippedBusy, SkippedMetered, Failed), counts, names, reason | new |
| Core/Services | `CheckSchedule` | Pure: next due time for each mode, delay for a one-time timer, `IsDue` | new |
| Core/Services | `BackgroundCheckRunner` | One check: skip rules, refresh, read installed, filter exclusions, record, notify (R1.x, R2.x, R5.2) | new |
| Core/Services | `CheckHistoryStore` | `check-history.json` in the data folder: append, read, prune to 7 days (R3.11–R3.13) | new |
| Core/Services | `INotifier`, `IAppActivity`, `INetworkState` | Interfaces that keep the runner free of Windows APIs (A1) | new |
| Core/ViewModels | `UpdatesViewModel` | Excluded rows below the rest; `Count` and Update all ignore them (R5.5, R5.7, R5.9, R5.10) | changed |
| Core/ViewModels | `SettingsViewModel` | Mode, interval, time, exclusions, last check, history, availability notes | changed |
| Core/ViewModels | `PackageRowViewModel` | Exclude command and excluded state (R5.1, R5.5) | changed |
| App/Services | `BackgroundCheckScheduler` | Registers and replaces tasks with the Windows App SDK's `BackgroundTaskBuilder`; `RequestAccessAsync` (R3.2, R3.4) | new |
| App/Services | `WindowsNotifier` | `AppNotificationManager`, one fixed tag so a new notification replaces the old (R2.3) | new |
| App/Services | `AppActivityMarker` | Named mutexes for "operations running" and "window in foreground", per package (R1.5, R2.5) | new |
| App | `Program` | Custom `Main`: `-RegisterForBGTaskServer` registers the COM class and runs a check with no XAML; otherwise the normal app (R1.2). It must not touch `Package.Current` on the portable path, where that throws | new |
| App | `Package.appxmanifest` | `UniversalBGTask` background task and `inProcessServer` entries, COM `ExeServer` entry | changed |
| App | `CartonStash.App.csproj` | `DISABLE_XAML_GENERATED_MAIN`, `WindowsAppSDKBackgroundTask` | changed |
| App/Views | `SettingsPage`, `UpdatesPage`, `MainWindow` | New sections, row action, open on Updates from a notification | changed |

The portable app has none of this active: `AppPackage.IsPackaged` is false, so the setting is disabled with its note (R4.1), and nothing is registered. It
keeps working with exclusions, which also affect its Updates page.

## Scheduling

| Mode | Windows trigger | Next run |
|------|-----------------|----------|
| Interval | Repeating `TimeTrigger(minutes, oneShot: false)` | Windows repeats it; the first run comes after n to n + 15 minutes |
| Daily time | One-time `TimeTrigger(delay, oneShot: true)` | Each run registers the next one from `CheckSchedule`, so daylight saving changes are handled (R3.9) |
| Catch-up, both modes | `SystemTrigger(UserPresent)` | Windows runs it when the user returns (R3.10) |

- **Every trigger goes through `IsDue`.** Windows can fire early or late, and T1 saw triggers run close together, so no trigger is trusted to mean a check is due. A run that isn't due writes no history entry and exits at once.
- `IsDue` is true from 15 minutes before the scheduled time onwards, as long as no check has run since the previous scheduled time, so several missed checks count as one.
- **An early daily timer:** a run that isn't due registers a new one-time timer for the time still left. The 15-minute window in `IsDue` means that time is never below Windows' minimum.
- A timer can't be under 15 minutes. If the daily time is closer than that, the check waits for tomorrow's, and Settings says when the next one is.
- Opening the app re-registers the schedule if it's missing, which repairs a broken daily chain and covers app updates (R3.7).
- Changing the mode removes every task and registers the new one (R3.2). Off removes them all.
- MSIX uninstall removes registrations and the data folder (R3.6).

## WinGet API usage

No new COM use. The runner calls existing `IPackageService` methods: `GetSourcesAsync`, `RefreshSourceAsync` for each source that isn't explicit, then `GetInstalledAsync` with
the same filter as the Updates page. A 30-second budget limits the refresh: when it runs out, the check goes on with WinGet's current data (R1.9). T1's slowest refresh took 10 s, just after waking from sleep. A task cancelled by Windows
records `Failed`, reason "Windows stopped the check". Other failures record `WinGetException`'s message, with paths removed (R3.13).

## Data flow

Timer or user present → Windows starts the exe → `Program` activates the task → `BackgroundCheckRunner`: due? busy? metered? → refresh → installed → exclusions → history entry → notification → next timer → exit.

If the app is open, a `FileSystemWatcher` on the history file reloads the installed data, which updates the badge and Settings (R1.7). Clicking a notification reaches `App` through `AppNotificationManager`. If the app is already running, `AppInstance` redirects
it to that copy, which shows the Updates page (R2.2).

## UI

**Settings → Update checks:** Off / Every [interval] / Every day at [time picker], radio buttons with only the chosen control enabled. Under them: the note that Windows decides the exact moment, last check, next check, and any Windows-blocked warning (R3.3, R3.4, R3.8).

**Settings → Excluded packages:** name, ID, Remove. Empty state: "No packages are excluded."

**Settings → Check history:** a list, newest first: "Today 10:03 PM · Schedule · 3 updates, 1 excluded · Git, Node.js, Python". A failed entry shows its reason in words and an icon, never color alone. Empty state: "No checks in the last 7 days."

**Updates page:** each row's overflow menu has **Exclude from update checks** ("Exclude Git from update checks" as its accessible name). Excluded rows appear under an "Excluded" heading, with **Update** and **Include again**. All of it works by keyboard and in high contrast (Q5).

**Notification:** "3 updates available, 1 ignored", then "Git, Node.js, Python". With more than three: "and 2 more".

## Error handling and edge cases

- Settings can't be read during a check: record `Failed`, no notification.
- History can't be written: the check carries on (R3.13). The file is written whole through a temp file, so a crash can't corrupt it; a corrupt file is replaced.
- Two checks at once: a named mutex lets the second exit, recording nothing. T1 saw runs 10 s apart, and lost log lines when two wrote at once.
- App closes while a check runs: nothing depends on it.
- An excluded package that's been uninstalled stays in the list until removed, shown by its ID.
- Background access denied: the setting stays as chosen, and Settings shows where to change it.
- The queue's busy mutex dies with the app, so a crash can't block checks forever.

## Testing strategy

| Criteria | How |
|----------|-----|
| Next time, missed, daylight saving, 15-minute rule | `CheckScheduleTests`, with a fixed clock |
| Skip rules, notify rules, exclusions, message text, failure paths (R1.5, R1.6, R1.9, R1.10, R2.x, R5.2, R5.8) | `BackgroundCheckRunnerTests`, with fakes |
| History, pruning, corrupt file, no paths (R3.11–R3.13) | `CheckHistoryStoreTests`, temp folder |
| Settings load, old files, exclusions (R3.5, R5.6) | `SettingsTests` |
| Counts, Update all, row actions (R5.1, R5.5, R5.7, R5.9, R5.10) | `UpdatesViewModel` tests |
| Real checks, notification click, sign-in catch-up, uninstall, Store update | Manual on the installed MSIX, logged in `tasks.md` |

## T1 results (2026-10-31, x64)

| Question | Result |
|----------|--------|
| 1. A time trigger starts the packaged exe with no window | **Yes**, with the Windows App SDK's `BackgroundTaskBuilder` and `UniversalBGTask` on the shared runtime. Windows runs `CartonStash.exe -RegisterForBGTaskServer -Embedding`, and the process exits after the check |
| 2. WinGet COM works from it | **Yes**: sources, refresh and the installed list (370 packages) |
| 3. Time against Windows' limit | 2.5 to 5.1 s in normal runs, 13 s just after waking from sleep, 72 MB at most. Never cancelled. Windows' limit wasn't reached, so it wasn't measured |
| 4. A sign-in or unlock trigger | **`UserPresent` works**, after a lock long enough to turn the screen off (about 2 minutes) and after sleep. Locks under a minute with the screen on started nothing. `SessionConnected` registered but never logged a run under its name, so it isn't used. **Sign-in after a shutdown is untested**: T11 covers it |
| 5. A one-time timer across sleep | **Not tested across sleep**: T11 covers it. Without sleep, a 20-minute timer fired after about 7 minutes. A 15-minute repeating timer was seen up to 1 h 49 min late while the PC was in use. Hence `IsDue` |
| 6. Survives an update, gone on uninstall | **Yes, both.** Timer and `UserPresent` tasks ran on a new version without registering again. After uninstall, no tasks and no data folder remain |

Notifications also work from the background process, with no extra setup. Clicking one starts the app with activation kind `ToastNotification` and the notification's arguments (`page=updates`), which R2.2 needs.

**Bundled Windows App SDK: doesn't work.** With the SDK bundled in the package, `backgroundTaskHost.exe` crashes with `0xc000027b` in `biwinrt.dll`. The cause wasn't found: the DLL is identical and its dependencies are present. So every MSIX uses the shared runtime (007 amendment R8), and the GitHub MSIX isn't an exception. T1 ran on the shared-runtime build only, because that's what both MSIX types become. The portable app is unaffected: it has no background checks.

## Risks and alternatives considered

- **If sign-in after a shutdown fails (T11):** a hidden check through the MSIX's startup task, as the requirements say. It covers sign-in only, and unlock already works.
- **Runtime:** `UniversalBGTask` needs the shared runtime, so GitHub MSIX users must install it themselves (007 R8.2).
- **Startup cost:** a custom `Main` runs for every launch, so it must stay small, and the portable path must not change. R1.6 from spec 007 still applies.
- **WinGet log noise:** each check adds a `WinGetCOM-*.log` file in WinGet's folder. The exact-match rule in 009 R2.9 stays safe, because the queue and a check can't run at once (R1.5).
- **Alternative: a separate small exe for checks.** Rejected: it would bundle its own .NET runtime or share the app's folder, and Core's WinGet code needs W1's files next to it anyway.
- **Alternative: `Windows.ApplicationModel.Background` directly.** Rejected after T1: on either SDK setup, it doesn't crash, but system triggers are refused (`COMException`) and timers never ran in over 2 hours.
