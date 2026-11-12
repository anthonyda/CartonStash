# 010 – Background update checks: Design

**Status:** Approved (2026-10-31), including the changes from T1's results. Depends on 007 amendment R8. Amendment R3.14 (startup entry): approved 2026-11-02
**Requirements:** [requirements.md](requirements.md)

## Overview

The MSIX registers background tasks with Windows. When one fires, Windows starts `CartonStash.exe` as a COM server with no window. It runs one
check through the existing `IPackageService`, writes one history entry, shows a notification if needed, and exits. All decisions (is a check
missed, what's the next time, what does the message say) live in plain Core classes, so they can be unit tested. Windows-only code stays in thin
App services, and Task 1 is a probe that proves those Windows parts before anything else is built.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `AppSettings` | `CheckMode` (Off, Interval, DailyTime), `CheckIntervalHours`, `CheckTime`, `CheckScheduleSetAt`, `ExcludedPackageIds` | changed |
| Core/Models | `CheckResult`, `CheckRecord` | Trigger, time, outcome (Updates, None, SkippedBusy, SkippedMetered, Failed), counts, names, reason | new |
| Core/Services | `CheckSchedule` | Pure: next due time for each mode, delay for a one-time timer, `IsDue` | new |
| Core/Services | `BackgroundCheckRunner` | One check: skip rules, refresh, read installed, filter exclusions, record, notify (R1.x, R2.x, R5.2) | new |
| Core/Services | `CheckHistoryStore` | `check-history.json` in the data folder: append, read, prune to 7 days (R3.11–R3.13) | new |
| Core/Services | `INotifier`, `IAppActivity`, `INetworkState` | Interfaces that keep the runner free of Windows APIs (A1) | new |
| Core/ViewModels | `UpdatesViewModel` | Excluded rows below the rest; `Count` and Update all ignore them (R5.5, R5.7, R5.9, R5.10) | changed |
| Core/ViewModels | `CheckSettingsViewModel` | Mode, interval, time, exclusions, last check, history, availability notes, beside `SettingsViewModel` | new |
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
- What counts as missed starts from the later of the last check and `CheckScheduleSetAt`, the time the schedule was last changed. Without it, nothing could be measured before the first check, and a sign-in right after turning checks on would run one (R3.10).
- **An early daily timer:** a run that isn't due registers a new one-time timer for the time still left. The 15-minute window in `IsDue` means that time is never below Windows' minimum.
- A timer can't be under 15 minutes. If the daily time is closer than that, the check waits for tomorrow's, and Settings says when the next one is.
- Opening the app re-registers the schedule if it's missing, which repairs a broken daily chain and covers app updates (R3.7).
- Changing the mode removes every task and registers the new one (R3.2). Off removes them all.
- MSIX uninstall removes registrations and the data folder (R3.6).

## WinGet API usage

No new COM use. The runner calls existing `IPackageService` methods: `GetSourcesAsync`, `RefreshSourceAsync` for each source that isn't explicit and has no agreements or accepted ones (006 R3.8), then `GetInstalledAsync` with
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

**Updates page:** each row has an **Exclude** button, beside **Update** ("Exclude Git from update checks" as its accessible name and tooltip), because rows have no overflow menu. Excluded rows appear under an "Excluded" heading, with **Update** and **Include again**. All of it works by keyboard and in high contrast (Q5).

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

## Amendment R3.14: startup entry (approved 2026-11-02)

**Manifest.** One more application extension, beside the background task:

```xml
<uap5:Extension Category="windows.startupTask" Executable="CartonStash.exe" EntryPoint="Windows.FullTrustApplication">
  <uap5:StartupTask TaskId="CartonStashCheck" Enabled="false" DisplayName="CartonStash update check" />
</uap5:Extension>
```

`Enabled="false"`, so a new install with checks off (the default) has nothing at startup. The name shows in Settings > Apps > Startup and Task Manager.

**Turning it on and off (R3.15).** `BackgroundCheckScheduler.ApplyAsync` also sets the startup entry through `Windows.ApplicationModel.StartupTask.GetAsync("CartonStashCheck")`:

| Checks | Entry state | Action |
|--------|-------------|--------|
| On | `Disabled` | `RequestEnableAsync()`. A packaged desktop app gets no prompt |
| On | `DisabledByUser`, `DisabledByPolicy` | Nothing. Report it, for R3.16 |
| On | `Enabled`, `EnabledByPolicy` | Nothing |
| Off | `Enabled` | `Disable()` |

The app also applies this table each time it opens, packaged only, without touching the background tasks. So an update from a version without the entry turns it on for users who already have checks on, since no schedule change would. (Changed during T15: the design first had it only in `ApplyAsync`.)

`ICheckScheduler` gains `bool StartupBlocked`, set by both and read when Settings opens. `CheckSettingsViewModel` shows it as a second note under the Windows-blocked one: "Windows won't start CartonStash at sign-in, so a check missed while the PC was off waits until you unlock it. Turn on **CartonStash update check** in Settings > Apps > Startup." Like R3.4, it's a note, not an error: the schedule still works.

**At sign-in (R3.14).** Windows starts `CartonStash.exe` with no arguments. `Program.Main`, packaged only, reads `AppInstance.GetCurrent().GetActivatedEventArgs()`. For `ExtendedActivationKind.StartupTask`, it runs the check and returns, before `RedirectedToOpenApp` (so it never takes the "main" instance key) and before any XAML:

- The check body in `BackgroundCheckTask.CheckAsync` moves to a static method that both entry points call, with trigger `MissedCheck`. Same runner, mutex, history, notification and next daily timer.
- No deferral or Windows time limit applies, so the startup path keeps the same 12-minute cap as the COM server (R1.2).
- `IsDue` decides as for any trigger. With nothing missed, the process exits within a second or two, which keeps Task Manager's startup impact low.

**Once only (R3.17).** The startup entry and `UserPresent` can both fire at sign-in. The existing `Local\CartonStash.CheckRunning` mutex lets only one run at a time, and the second one finds the first's history entry, so `IsDue` is false and it exits.

**Unchanged.** Interval and daily timers, and `UserPresent` for unlock and sleep. The startup entry isn't a `BackgroundTaskRegistration`, so `RemoveAll` doesn't touch it, and switching modes keeps it on.

**Testing.** `CheckSettingsViewModelTests` for the note (a fake scheduler reports `StartupBlocked`). In the test VM: a probe first (T13), then R3.10 after a real shutdown, the entry turned off by the user, checks off, and uninstall.

**Risks.**
- Windows delays startup apps for a few seconds after sign-in, and less on fast boots. That only shifts the check.
- ~~The probe must confirm the activation kind and that the entry starts the exe after a shutdown and sign-in, on a typed sign-in as well as an automatic one.~~ Confirmed by T13, below.
- Store certification sees a new startup task. The certification notes say what it does and that it's off until the user turns checks on.

**T13 results (2026-11-02, test VM, Windows 11 26H2 build 26300.9457, x64).** Throwaway 1.1.6 build with the Store identity and the manifest entry above. Its `Main` logged the activation kind and the entry's state, called `RequestEnableAsync` on a normal launch when the state was `Disabled`, and exited on a `StartupTask` launch. The probe code is not kept.

| Point | Result |
|-------|--------|
| `RequestEnableAsync` from a normal launch | **Pass**: `Disabled` → `Enabled`, with no prompt |
| Name in Settings > Apps > Startup | **Pass**: "CartonStash update check" |
| Activation kind | **Pass**: `StartupTask`, no arguments |
| Shutdown, then automatic sign-in | **Pass**: boot at 2:39:22 PM, the app started through the entry at 2:39:41 PM |
| Shutdown, then a typed sign-in | **Pass**: the maintainer signed in at 5:53:29 PM, and the app started through the entry at 5:53:47 PM |
| User turns it off in Startup apps | **Pass**: the app then reads `DisabledByUser`, and `RequestEnableAsync` isn't called, so the entry stays off |

One behavior the design didn't expect: when `RequestEnableAsync` turns the entry on, Windows starts the exe through it right away, with kind `StartupTask` (5 seconds later in the probe). Turning it on from Startup apps didn't do this. The check path handles it unchanged: `IsDue` decides, and right after the user turns checks on, no check is due, so the process exits. T16 confirms that turning checks on shows nothing.

## Amendment R3.18: two triggers in one process (approved 2026-11-12)

- A small Core class, `ActiveTasks`, counts the tasks the COM server runs: `Begin()`, and `End()` that returns true
  for the last one. `BackgroundCheckTask.Run` calls `Begin()` before it starts and `End()` after `deferral.Complete()`;
  only the last `End()` calls `Program.SignalBackgroundDone()`. The 12-minute limit stays the backstop.
- A second `Run` still finds the mutex and records nothing (R3.17).
- Tests: `ActiveTasks` (unit): two begins and one end don't signal, the second end does. Manual check in the VM as in
  the requirements: one history entry, and the next daily timer set.

## Risks and alternatives considered

- **If sign-in after a shutdown fails (T11):** a hidden check through the MSIX's startup task, as the requirements say. It covers sign-in only, and unlock already works. **It failed on 2026-11-02**, so this is amendment R3.14 above.
- **Two CartonStash packages side by side** (found in T11): both register the same COM class ID, so a trigger of one package can get the other package's server, and backgroundTaskHost crashes (`0xc000027b`). Only the Store build is published (007 R9), so users can't get there; a developer with a local test package beside the Store app can.
- **Runtime:** `UniversalBGTask` needs the shared runtime, which the Store installs with the app.
- **Startup cost:** a custom `Main` runs for every launch, so it must stay small, and the portable path must not change. R1.6 from spec 007 still applies.
- **WinGet log noise:** each check adds a `WinGetCOM-*.log` file in WinGet's folder. The exact-match rule in 009 R2.9 stays safe, because the queue and a check can't run at once (R1.5).
- **Alternative: a separate small exe for checks.** Rejected: it would bundle its own .NET runtime or share the app's folder, and Core's WinGet code needs W1's files next to it anyway.
- **Alternative: `Windows.ApplicationModel.Background` directly.** Rejected after T1: on either SDK setup, it doesn't crash, but system triggers are refused (`COMException`) and timers never ran in over 2 hours.
