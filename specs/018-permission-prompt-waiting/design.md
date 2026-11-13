# 018 – Say when Windows' permission prompt is waiting: Design

**Status:** Approved 2026-11-13
**Requirements:** [requirements.md](requirements.md)

## Overview

While the queue runs an operation, a small monitor in Core asks once a second whether a process named `consent` exists
(Q1). The App drives it with a `DispatcherQueueTimer`, so every change happens on the UI thread like the rest of the
queue. When the prompt is open, the monitor marks the running operation as waiting (card and row text, R1.2) and the
shell shows a banner (R1.1). After 60 seconds without a break it asks the App to show one Windows notification (R1.4),
which it removes when the prompt closes. Nothing touches WinGet.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `IPermissionPromptProbe` | `bool IsOpen()`: is Windows' consent process running | New |
| Core/Services | `ConsentProcessProbe` | `Process.GetProcessesByName("consent")`, each one disposed; any exception → false (R1.6: read only) | New |
| Core/Services | `IPermissionNotifier` | `Show()`, `Remove()` for R1.4's notification | New |
| Core/Services | `PermissionPromptMonitor` | State machine below; `NeedsTicks`, `IsWaiting`, `Tick(DateTimeOffset now)` | New |
| Core/Services | `PackageOperationQueue` | Exposes `Running` (the operation it took, or null) and raises `RunningChanged` when it changes. Between two operations it is null for a moment while work is still pending: the monitor keeps the prompt's state then (found by `The_next_operation_takes_the_mark_while_the_prompt_stays_open`) | Changed |
| Core/Models | `PackageOperation` | `IsWaitingForPermission` (observable, internal set); `StatusText` → "Waiting for your permission…" while set (R1.2); `ShowPermission` false while set (R1.9) | Changed |
| Core/ViewModels | `ShellViewModel` | `IsPermissionBannerOpen`, a plain bool from the monitor's `IsWaiting` | Changed |
| App/Services | `WindowsPermissionNotifier` | `AppNotificationBuilder`, tag `permission`, argument `page=activity`; `RemoveByTagAsync("permission")` | New |
| App | `MainWindow.xaml` | Second `InfoBar` above the frame, Severity Warning, `IsClosable=False`, live region | Changed |
| App | `OperationCard.xaml`, `PackageActionsView.xaml` | Shield glyph (`&#xEA18;`) next to the status while `IsWaitingForPermission` | Changed |
| App | `App.xaml.cs` | Wires the monitor, its timer (1 s, started and stopped by `NeedsTicks`) and the notifier | Changed |

## WinGet API usage

None. The probe only lists process names; WinGet's progress keeps reporting "Installing" underneath, which the
waiting text covers (R1.2) and restores when the prompt closes (R1.3).

## Data flow

```
queue.RunningChanged ──► monitor.NeedsTicks = queue.Running is not null
                              │ (App starts / stops the DispatcherQueueTimer, 1 s)
timer tick ──► monitor.Tick(now) ──► probe.IsOpen()
   open,   was closed  → openedAt = now; Running.IsWaitingForPermission = true; IsWaiting = true
   open,   still open  → if now - openedAt >= 60 s and not notified → notifier.Show(); notified = true
   closed, was open    → Running.IsWaitingForPermission = false; IsWaiting = false; if notified → notifier.Remove()
RunningChanged to another operation or null → clear the old operation's flag; if still open, flag the new one
   (the minute keeps counting: it's the same prompt); if null with work still pending → keep the state, the next
   operation follows; if null and the queue is empty → as "closed" (banner, flag and notification go)
```

The monitor takes the time as an argument, so tests drive it with plain values; no clock inside.

## UI

- **Banner (R1.1):** under the title bar, above 003's outcome InfoBar, on every page. Title "Windows is waiting for your
  permission", message "Select the flashing shield in the taskbar to answer it." No close button: it goes when the
  prompt does. `AutomationProperties.LiveSetting="Assertive"` (R1.8). Theme brushes come from InfoBar's own Warning
  style, so light, dark and high contrast follow.
- **Card and row (R1.2):** status "Waiting for your permission…", shield glyph before it, progress bar stays
  indeterminate. 003 R8.3's line is hidden while this shows (R1.9). The row's status `AutomationProperties.Name`
  already follows `StatusText`; add `LiveSetting="Polite"` there.
- **Notification (R1.4):** title "Windows is waiting for your permission", text "An operation in CartonStash can't
  continue until you answer the prompt. Select the flashing shield in the taskbar." Selecting it goes through the
  existing activation path (010) with `page=activity`: bring the window forward and open Activity. It's removed when
  the prompt closes, so a stale one isn't left in the notification center.

## Error handling and edge cases

- **Another program's prompt (R1.7):** shown the same way; the wording doesn't name the package.
- **Prompt opens between two operations:** the monitor isn't ticking (no running operation), so nothing shows.
  Accepted: the queue isn't waiting then.
- **Several consent processes:** any count above zero is "open".
- **Probe throws** (access, process gone mid-enumeration): treated as closed for that tick; next tick tries again.
- **User cancels nothing:** Cancel is already disabled in phase Running (003 R3.2); unchanged.
- **App closes while waiting:** the close guard (003) already asks about running operations; the notification is
  removed on shutdown if shown.
- **Notifications turned off in Windows, or no identity in the portable build:** `Show` fails quietly; banner and card
  still do the job.

## Testing strategy

- Unit (`PermissionPromptMonitorTests`, fake probe and notifier): open → flag and banner (R1.1, R1.2); close → both
  cleared (R1.3); open 59 s → no notification, 60 s → one, 120 s → still one (R1.4); close and reopen → a new minute;
  close after notifying → `Remove` (R1.4); running operation changes while open → flag moves (data flow); no running
  operation → `NeedsTicks` false and no probe calls (R1.6).
- Unit (`PackageOperationTests`): `StatusText` and `ShowPermission` while waiting (R1.2, R1.9).
- Manual, test VM (T1 first, then the end check): `consent.exe` visible unelevated from the portable build and the
  Store MSIX, deferred to the taskbar and on the secure desktop (Q1); Update all with two all-users installers, prompt
  left alone over a minute, then accepted (R1.1 to R1.4); declined (R1.3); Narrator and high contrast (R1.8); taskbar
  badge unchanged (R1.5).

## Amendment R2: a declined prompt

`MapInstallResult` and `MapUninstallResult` mark a failed result whose exit code is 1625, or whose extended error is
WinGet's "cancelled by user" (`0x8A15010C`), with `OperationOutcome.MayBeDeclinedPrompt`; the message is unchanged. The
monitor sets `PackageOperation.SawPermissionPrompt` whenever it flags the operation, and `Start` clears it for each run.
After the installer returns, the queue's `DeclinedPrompt` turns a marked failure into **Cancelled** with 017's
`ElevatedProtocol.DeclinedMessage` only if the prompt was seen, and drops Retry as administrator. `Details` (the log's
status and codes) is kept. 017's elevated run already reports a declined prompt itself and is unchanged.

## Risks and alternatives considered

- **`consent.exe` hidden from an unelevated or packaged process:** T1 measures it before any code. Fallback (would need
  a requirements change): "Running with no progress for N seconds" wording, which is weaker.
- **Polling cost:** one process-list read per second, only while an operation runs. `GetProcessesByName` enumerates
  all processes; at ~1 ms per call it's negligible.
- **Flash the window instead:** declined by the maintainer (Q2).
