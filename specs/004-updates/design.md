# 004 – Updates: Design

**Status:** Approved (2026-10-29, R3 deferred). Amendment R6 (updating App Installer): approved 2026-11-04. Amendment R7 (license agreements on update): approved 2026-11-05
**Requirements:** [requirements.md](requirements.md)

## Overview

The Updates page is a filtered view over the installed data loaded in 002 (`HasUpdate == true`). Upgrades are a
third operation kind in the 003 queue. The main unknown is R3 (explicit targeting), which needs an
investigation task before the design is final.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `InstalledPackagesStore` | Owns the loaded installed list, `ReloadAsync`, change notifications. Shared by Installed and Updates (R1.2). *Amended:* built directly in 002 | from 002 |
| Core/Services | `IPackageInstaller.UpgradeAsync` | `UpgradePackageAsync` with `InstallOptions` (silent, scope from Settings) | new method |
| Core/Services | `PackageOperationQueue` | `OperationKind.Upgrade`; `EnqueueMany` | changed |
| Core/ViewModels | `UpdatesViewModel` | Filtered list, selection, Update / Update selected / Update all, count, empty state | new |
| Core/ViewModels | `ShellViewModel` | `UpdatesCount` for the nav badge (R4.1) | changed |
| App | `UpdatesPage` | Checkbox list (`SelectionMode="Multiple"`), command bar, confirmation dialog | new |

## WinGet API usage

- Data: `CatalogPackage.IsUpdateAvailable`, `InstalledVersion.Version`, `DefaultInstallVersion.Version`.
- Upgrade: `PackageManager.UpgradePackageAsync(pkg, InstallOptions)` → same progress / result mapping as install.
  Extra status `NoApplicableUpgrade` → "No applicable update for this PC".
- **Investigation for R3 (T1), done 2026-10-29:** reflected over the ComInterop 1.29.380 projection.
  There are **no pin-related types or members**, and no `RequireExplicitUpgrade`. The only `Explicit` members are on
  *catalogs* (`IPackageCatalogInfo2.Explicit`, `I{Add,Edit}PackageCatalogOptions.Explicit`), meaning "source
  excluded from default searches". That's relevant to spec 006, not here.
  **R3 cannot be met through the COM API.** Decision 2026-10-29: **R3 deferred, question left open** (see requirements).
  v1 behaves as option A: no "Manual update only" marking, and "Update all" queues every package with `IsUpdateAvailable`.

## Data flow

`InstalledPackagesStore.Changed` → `UpdatesViewModel` rebuilds `Updates = all.Where(HasUpdate)` (sorted by name)
→ `ShellViewModel.UpdatesCount`. Update all → confirm (n packages) → `queue.EnqueueMany`
→ each completion triggers a store reload (debounced to once per queue drain, so updating 20 packages doesn't reload 20 times).

## UI

```
Updates (3)                    [Update selected] [Update all]
☐ Git           2.56.0.3 → 2.57.0     winget     [Update]
☐ Miniconda3    py312… → py314…       winget     [Update]
☐ 7-Zip         24.08 → 25.01         winget     [Update]
```

## Error handling and edge cases

- Upgrade where the old version can't be found or the installer type changed → the WinGet result maps to the 003 messages.
- A package updated outside the app while the page is open → stale until Refresh. Acceptable (P1 says we re-read, not guess).
- Reload debounce: one reload after the queue is empty, plus the per-package UI state from the queue in the meantime.

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.1, R2.2–R2.4, R4.1, R5.1 | Unit tests on `UpdatesViewModel` + `InstalledPackagesStore` with fakes |
| R2.1 | Manual: update one outdated package |

## Risks and alternatives considered

- Packages WinGet refuses to update in bulk (pins, explicit-only) fail individually in "Update all", with WinGet's reason shown (003 R5.2). Acceptable until R3 is revisited.

## Amendment R6: updating App Installer (approved 2026-11-04)

Against requirements amendment R6 (approved 2026-11-04).

### Probe results (2026-11-04, test VM, 1.1.0, App Installer 1.29.379.0 to 1.29.380.0)

The requirements' first risk, measured: the app updated App Installer (WinGet `Ok` after 12 s), then its window was closed
normally. The app exited in 1 s, and WinGet's server stopped **11 s** later. Reopened, the app ran on WinGet from
1.29.380.0, and App Installer had left the Updates list. So a clean exit is enough, but a restart that starts the new copy
at once would connect to the old server during those 11 s. The new copy has to wait for it.

A forced end of the process left the server running for more than 3 minutes (requirements table), so the restart must
end the old copy the normal way.

### Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `WinGetHost` | Knows App Installer: `IsAppInstaller(PackageInfo)` from the installed entry `MSIX\Microsoft.DesktopAppInstaller_<version>_<arch>__8wekyb3d8bbwe`. Holds the version waiting for a restart this session (`PendingVersion`), set by R6.1 or by the restart arguments (R6.4) | new |
| Core/Services | `WinGetPackageInstaller` | After `Ok` for an App Installer upgrade, asks WinGet's installed catalog for the row's installed entry. Still there means the old version is still registered: outcome `SucceededAppRestartRequired` (R6.1) | changed |
| Core/Models | `OperationResult.SucceededAppRestartRequired` | Card state `RestartRequired` (the warning icon), status "Updated · restart CartonStash", message "Updated. Restart CartonStash to finish: WinGet runs from App Installer, so the new version starts with it." The log takes the message as usual (009) | changed |
| Core/ViewModels | `PackageActions.UpdateManyAsync` | Moves App Installer to the end of the list it queues (R6.5) | changed |
| Core/ViewModels | `PackageRowViewModel` | While `WinGetHost` has App Installer's installed version pending: `ShowUpdate` false, `ShowRestart` true, result text "Restart CartonStash to finish" (R6.2). **Update all** already takes only rows with `ShowUpdate` | changed |
| Core/ViewModels | `ShellViewModel` | `RestartCommand`: if operations are running or queued, the same dialog as closing (003 R6.2), then `IAppRestarter.Restart(currentPage)` (R6.3). The notification for that outcome gets a **Restart CartonStash** button | changed |
| Core/Services | `IAppRestarter` | Interface, so view models stay free of WinUI (A1) | new |
| App | `AppRestarter` | Notes the process IDs of WinGet's servers in this Windows session, restarts with `AppInstance.Restart("--finish-winget-update <app pid> <server pids> --pending <version> --page <key>")` (T8) | new |
| App | `Program` | With `--finish-winget-update`: waits for the old copy to exit (so it takes the "main" instance key, 010 R2.2), then starts the app | changed |
| App | `App` / `FinishUpdateWindow` | Before anything touches WinGet: a small window, "Finishing the App Installer update…", while it waits for the old servers to exit, 60 s at most, then 2 s more for Windows to mark the switch (T15 finding below). Then the normal start, on the given page | new |
| App | `OperationCard`, `PackageActionsView` | **Restart CartonStash** button for that outcome and that row state | changed |

### Data flow

1. **Update all** → App Installer queued last (R6.5) → WinGet `Ok` → installed-catalog lookup still finds
   `MSIX\Microsoft.DesktopAppInstaller_1.29.379.0_…` → `SucceededAppRestartRequired`, `WinGetHost.PendingVersion = 1.29.379.0`.
2. The card, the notification and the log show the warning (R6.1). After the queue drains, the reload still lists App Installer
   with an update; its row shows **Restart CartonStash to finish** and is left out of **Update all** (R6.2). The badge and the
   title still count it (decided).
3. **Restart CartonStash** → close guard if needed → `AppInstance.Restart` with the arguments, which ends the old copy
   → the new copy waits for the old copy, then for the servers, then starts on the same page (R6.3).
4. The reload shows 1.29.380.0: nothing more to do (R6.4). The arguments carry the old version (`--pending 1.29.379.0`). If the
   reload still shows that version, for example because the servers didn't stop within 60 s, the new copy sets `PendingVersion`
   to it, keeps R6.2's row, and the Updates page shows: "App Installer finishes updating once every program using WinGet is
   closed, such as the winget command or another app." (R6.4)

### How the new copy is started (decided by T8: option A)

- **Option A, `AppInstance.Restart(args)`** (Windows App SDK). Works packaged and unpackaged; it ends the calling process
  itself.
- **Option B, start our own exe** (`Environment.ProcessPath`) with the arguments, then close normally.

**T8 results (2026-11-04, test VM, throwaway Store-identity build 1.1.8, App Installer 1.29.379.0):**

| Trial | Old copy gone | WinGet's old server gone | New copy |
|-------|---------------|--------------------------|----------|
| A, no operation run | 1.1 s | with the app | Store identity, arguments received, activation kind `Launch`, old copy already gone, took the "main" key |
| B, no operation run | 1.1 s | with the app | Same as A |
| A, right after updating App Installer in the app | 0.36 s | 10.4 s | Started 0.16 s after the call, while the old server still ran. Reopened later: App Installer 1.29.380.0, WinGet's server from 1.29.380.0 |

`AppInstance.Restart` doesn't leave the server behind: after the update it stopped in the same time as after a normal close
(11 s in the earlier probe). **Option A is chosen**: one call, the same for the Store and portable builds, and the old copy is
gone before the new one checks, so the "main" key and notification clicks (010 R2.2) keep working. The new copy must still
wait for the old servers, as designed: it started while one was running. `AppInstance.Restart` skips the window's closing
event, so `RestartCommand` runs the close guard first.

App Installer's installed entry, as the app's installed list reports it:
`MSIX\Microsoft.DesktopAppInstaller_1.29.379.0_x64__8wekyb3d8bbwe` (WinGet ID `Microsoft.AppInstaller`), as assumed.

**T15 finding, 2026-11-05: connecting the moment the old server stops can start the old version again.** In one of two runs,
the new copy connected right after WinGet's old server stopped, got a new server from App Installer 1.29.379.0, and loading
the installed list failed with `0x80073D02` ("resources it modifies are currently in use"). Probes, with nothing starting
WinGet in between:

| Step | Result |
|------|--------|
| The old server stops | About 0.1 s later, Windows changes the old App Installer's status: the staged version takes over at the next start |
| Nothing starts WinGet | Windows doesn't switch on its own. Here it switched 28 s later, only while processing an unrelated Outlook update |
| `winget --version` started as soon as the server stopped, and 2 s after it | Both ran 1.29.380.0, and the switch was done |

The `winget` command takes long enough to start to miss that 0.1 s; a COM connection made within milliseconds doesn't. So
after the old servers stop, the new copy waits **2 s more** before it connects. If App Installer still shows the old version
after all, R6.4's message applies, so a miss is never silent.

A second miss, in one of three runs with that wait: WinGet's old server didn't stop at all. The restart had been pressed while
the reload after the update was still talking to WinGet, and ending a copy in the middle of a call leaves the server waiting
minutes, as a forced end did. So `RestartCommand` first waits for the installed list to finish loading, 10 s at most. With
both waits, three of three runs switched to the new App Installer within 14 s.

### Error handling and edge cases

- **The lookup fails** after `Ok`: report plain success, as today. The reload then shows the row as it is, and the row
  offers **Update** again; that's the current behaviour, never worse.
- **The user doesn't restart**: closing and reopening later finishes the update too (probe). That session has no pending
  version, so App Installer shows normally: the new version, or still the update if something kept WinGet running.
- **Several servers** (another app, the `winget` command, a background check): the new copy waits for all of this session's, up
  to 60 s, then carries on (R6.4).
- **Operations queued after App Installer**: none in **Update all** (R6.5). A single **Update** started afterwards still runs
  on the old WinGet, which worked for four packages in the 2026-11-04 run.
- **Restart fails to start the new copy**: the old copy stays open and says so, with "Close CartonStash and open it again".

### Testing strategy

| Criteria | How |
|----------|-----|
| R6.1 | Unit: the outcome for `Ok` + still installed, `Ok` + gone, `Ok` + lookup failed; the card's state and status text |
| R6.2 | Unit: `PackageRowViewModel` with a pending version (`ShowUpdate`, `ShowRestart`, text); `UpdatesViewModel` Update all leaves it out |
| R6.3 | Unit: `RestartCommand` asks first while the queue has work and restarts only on "Quit anyway"; argument parsing |
| R6.4 | Manual in the test VM: restore `clean`, **Update all**, Restart CartonStash: App Installer 1.29.380.0, gone from Updates, same page. Then the same with the `winget` command holding WinGet open |
| R6.5 | Unit: `UpdateManyAsync` queues App Installer last, others in order |
| `WinGetHost.IsAppInstaller` | Unit on installed IDs, including other `Microsoft.*` MSIX entries |

### Risks and alternatives considered

- **Release WinGet's objects and wait in the same copy**, then restart. Rejected: every service would have to drop its COM
  objects reliably, and the 11 s are WinGet's own idle time, not ours.
- **Keep the pending version in settings across sessions.** Rejected: R6.4 covers the restart, and a later start reads the
  truth from WinGet (P1).
- **App Installer's installed entry ID could change format** in a later WinGet. `IsAppInstaller` is the only place that knows
  it, and a miss means today's behaviour.

## Amendment R7: license agreements on update (approved 2026-11-05)

Against requirements amendment R7 (approved 2026-11-05).

### Findings

- `IPackageInstaller.GetAgreementsAsync` already reads the agreements of the package's default install version in its source
  (`FindByIdAsync` with `localFirst: false`). For an installed package, that's the version an update installs, so it serves
  updates unchanged.
- The queue already stores `AcceptAgreements` per operation and **Retry** runs the same operation again, so R7.3 needs no
  code, only a test. Updates enqueue with `false` today; `InstallOptions.AcceptPackageAgreements` takes it as given.
- The only caller of a single update is `PackageRowViewModel.Update`, on the Updates page rows and the details pane header.

### Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/ViewModels | `PackageActions` | `UpdateAsync(package)`: agreements as in `InstallAsync` (read, show, queue with acceptance only if accepted; nothing on Decline; no acceptance if they can't be read, R7.4). The shared part moves to one private `AskAgreementsAsync`, so install and update can't drift | changed |
| Core/ViewModels | `PackageActions.UpdateManyAsync` | After the confirmation (unchanged, R2.3), for each package in queue order: ask its agreements; then enqueue the accepted and agreement-free packages, each with its own acceptance, App Installer still last (R6.5). Nothing is queued until every dialog is answered (R7.2) | changed |
| Core/ViewModels | `PackageRowViewModel.UpdateCommand` | Async, calling `UpdateAsync` | changed |

The dialog is 003's: "`<name>`: license agreement", with the agreement text or link and Accept / Decline.

### Data flow

Update → `GetAgreementsAsync` (one source lookup) → none: queue → some: dialog → Accept: queue with acceptance → WinGet
installs; Decline: nothing. Update all → confirm *n* → for each package: lookup, dialog if needed → queue the kept ones.

### Error handling and edge cases

- **The lookup is slow or offline**: each lookup is one search by ID in the source, as for an install. Update all reads them
  one after another before anything starts; with 20 packages that's a few seconds, and the rows stay as they are until then.
- **Every package declined**: nothing is queued and no confirmation repeats.
- **A package is already queued** (R4.2): skipped before its agreements are read, as `InstallAsync` does.

### Testing strategy

| Criteria | How |
|----------|-----|
| R7.1 | Unit: `PackageActions.UpdateAsync` with agreements, accepted and declined; without agreements no dialog |
| R7.2 | Unit: `UpdateManyAsync` asks only for the packages with agreements, queues none before the last answer, leaves out declined ones, keeps App Installer last |
| R7.3 | Unit: Retry of a failed accepted update keeps `AcceptAgreements` |
| R7.4 | Unit: agreements that throw → queued without acceptance |
| R7.1, R7.2 live | Manual in the test VM: Power BI Desktop 2.158 → 2.159 with Update, then with Update all; Decline once, then Accept |

## Amendment R8: rows stay in place when the list reloads (approved 2026-11-12)

### Finding

The stowed exception's managed half is two `ArgumentOutOfRangeException` from CsWinRT's
`IList.ToAbiHelper.GetAt`, called by `Microsoft.UI.Xaml` while handling the Tab key. In WinUI's source (`Selector`),
Tab into a list asks for the container at `m_lastFocusedIndex` (`GetFirstFocusableElementOverride`). An `ItemRemoved`
change sets that index back to 0 when it falls past the end; a `Reset` doesn't. `UpdatesViewModel.Rebuild` did
`Updates.Clear()` then added every row, a reset, so the index stayed where it was in the longer list.

### Components

- `UpdatesViewModel.Rebuild` builds the wanted rows, reusing the existing row for a package with the same `Key`
  (`PackageRowViewModel.Refresh` with the new package, then `NotifyStateChanged`, since App Installer's restart state and
  the exclusion aren't part of the package), and making rows only for new packages.
- `RowListSync.Sync` (shared with 001 R4 and 002 R9) turns `Updates` and `Excluded` into the wanted lists with `RemoveAt` and `Insert` only: never
  `Clear`, never `Move`. A row that changes place is removed and inserted.
- The selection keeps the checked rows still in `Updates` (R8.2). The ListView already keeps them checked, and drops the
  removed ones with a `SelectionChanged`.

### Alternatives considered

- Clamping or resetting the ListView's focus from the page after a reload: the remembered index is internal to WinUI,
  and there is no public way to set it without moving focus.
- Setting `ItemsSource` again (WinUI resets the index then): it also loses the scroll position and the checks.

### Testing strategy

- Unit: `UpdatesViewModelTests.Finished_updates_leave_by_removal_never_by_reset`, `New_updates_are_inserted_in_name_order`,
  `A_reload_keeps_the_checked_rows_that_stay`, `Sync_handles_a_row_whose_place_changed`.
- Manual in the test VM: the crash's repro (T19).

The Installed page (`InstalledViewModel.ApplyFilter`) and Search results also refill their lists with `Clear`; not changed
here.

