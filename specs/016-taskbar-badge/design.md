# 016 – Updates count on the taskbar icon: Design

**Status:** Approved (2026-11-12). Done 2026-11-12
**Requirements:** [requirements.md](requirements.md)

## Overview

Use the Windows App SDK's `Microsoft.Windows.BadgeNotifications.BadgeNotificationManager` (in
`Microsoft.WindowsAppSDK.Foundation` 2.6.4, already referenced): `Current.SetBadgeAsCount(uint)` and
`Current.ClearBadge()`. The badge belongs to the app's package identity, not to a window, so it stays on the taskbar
pin and Start after the window closes (Q2), and the background check process can set it too (Q1). Core gets a
one-method interface; the App wraps the Windows call and swallows every failure (R3.1).

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `ITaskbarBadge` | `void Show(int count)`: sets the number, or clears the badge for 0. | New |
| Core/Services | `BackgroundCheckRunner` | Takes an `ITaskbarBadge`. After a check that finished (`Updates` or `None`), calls `Show(names.Count)`. Not on `Failed`, `SkippedBusy`, `SkippedMetered`, or no check due (R2.1, R2.2). | Changed |
| Core/ViewModels | `ShellViewModel` | Takes an optional `ITaskbarBadge`. When `UpdatesViewModel.Count` changes and is not null, calls `Show(count)` if it differs from the last value it sent (R1.1–R1.6). | Changed |
| App/Services | `WindowsTaskbarBadge` | Calls `BadgeNotificationManager.Current`. Catches every exception, logs it at debug level, carries on (R3.1). | New, in `BackgroundCheckPlatform.cs` next to `WindowsNotifier` |
| App | `App.xaml.cs`, `BackgroundCheckTask.cs` | Register `WindowsTaskbarBadge` in DI; pass it to the runner. | Changed |

## WinGet API usage

None. The count is the one the app and the check already compute from WinGet (004 R4.1, 010 R1.3).

## Data flow

- **App open:** `InstalledPackagesStore` loads or reloads → `UpdatesViewModel.Count` (null while loading or after an
  error, else the count without excluded packages) → `ShellViewModel.OnUpdatesChanged` → `ITaskbarBadge.Show(count)`.
  A null count sends nothing, so the badge keeps its last value (R1.5, R1.6). The first non-null count always sends,
  which corrects a stale badge left by an earlier session or check.
- **Background check:** `BackgroundCheckRunner.Finish` → `Show(names.Count)`, with the same rule as the Updates page
  (`HasUpdate && IsManaged`, not excluded), so the two numbers match (R1.2). A check while the app is open sets it
  too; the app's own reload (010 R1.7) then sends the same number.

## UI

Nothing inside the window changes. Windows draws the badge, shows "99+" above 99 (R3.2), and hides it when the user
turns off "Show badges on taskbar apps". Narrator reads taskbar badges as part of the taskbar button; no accessible
name to add.

## Error handling and edge cases

- No package identity (portable zip, `dotnet run`): the call throws or does nothing; caught, no message (R3.1).
- The count can't be negative; `Show` clamps to 0 before the `uint` cast.
- Badge stale after updating outside CartonStash with the app closed: accepted (Q1); the next check or start fixes it.
- The app uninstalled: Windows removes the badge with the package.

## Testing strategy

- Unit (fake `ITaskbarBadge` recording calls):
  - `ShellViewModel`: no call while `Count` is null; call on first count; call on change; no repeat for the same
    value; 0 sends 0; error after a count sends nothing (R1.1–R1.6).
  - `BackgroundCheckRunner`: `Show(n)` after a check with updates, `Show(0)` after none, excluded packages left out,
    no call on failure, skip or not-due (R1.2, R2.1, R2.2).
- Manual, in the CartonStash-Test VM with a packaged build (never on the host): badge appears with the count, follows
  an update and an exclusion, clears at 0, stays after closing, set by a scheduled check while closed, nothing when
  badges are off in taskbar settings; portable zip starts and runs with no error (R3.1).

## Risks and alternatives considered

- **`ITaskbarList3::SetOverlayIcon`:** works unpackaged, but only on a running window's button, and needs a drawn
  icon per number. Rejected: lost on close (Q2).
- **UWP `Windows.UI.Notifications.BadgeUpdateManager`:** same behaviour, but the Windows App SDK API is the supported
  path for desktop apps and is already in the dependency set.
- **Risk:** the badge shows on the taskbar only for a pinned or running app; on Start only when pinned. That's
  Windows' rule, not ours.
