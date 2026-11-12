# Bug review, 2026-11-10

A read-through of the whole codebase at commit `b3c98a8` (main), looking for bugs: every file under `src/`, the
scripts in `tools/`, both workflows and the package manifest. The tests were read only where they show what a piece of
code is meant to do.

**Nothing was built, run or tested for this review, and nothing is fixed.** Each finding says how sure it is:

- **Confirmed**: it follows from the code alone.
- **Plausible**: it depends on how WinGet, WinUI or Windows behaves at run time. Do the check listed with it first.

A fix that changes specified behaviour needs its spec amended first (`specs/README.md`).

Tiers: **1** = a wrong result in an ordinary flow, or a requirement not met. **2** = real, but needs unusual
conditions. **3** = small, or hardening.

One thing applies to many findings: the App has no handler for unhandled exceptions. An exception that leaves an
`async void` method or a toolkit command ends the app, even while an installer is running.

## Summary

| ID | Tier | Sure? | Finding |
|----|------|-------|---------|
| BR-01 | 1 | Confirmed | Explicit sources are searched and matched like any other |
| BR-02 | 1 | Confirmed | Going back from the newest version: an installer that does nothing is reported as a success |
| BR-03 | 1 | Confirmed | The details pane keeps a row its list has thrown away |
| BR-04 | 1 | Confirmed | Rows built after an operation ended still take its result: Install or Update button missing |
| BR-05 | 1 | Confirmed | Search rows lose their icons after any reload of the installed list |
| BR-06 | 1 | Plausible | Two background triggers in one process: the second ends the process while the first check runs |
| BR-07 | 2 | Confirmed | The same package can be queued twice from different pages |
| BR-08 | 2 | Confirmed | A reset settings file is never rewritten |
| BR-09 | 2 | Confirmed | Check history can keep part of a file path |
| BR-10 | 2 | Confirmed | The queue stops for good if anything around an operation throws |
| BR-11 | 2 | Plausible | A failed App Installer setup may end the app instead of showing the reason |
| BR-12 | 2 | Plausible | "WinGet couldn't start" only handles one exception type |
| BR-13 | 2 | Plausible | Copy can end the app when the clipboard is busy |
| BR-14 | 2 | Confirmed | A superseded load that fails for a real reason escapes the catch filters |
| BR-15 | 2 | Plausible | Dropping the cached catalogs can be undone by a connection already under way |
| BR-16 | 2 | Confirmed | Search maps its results on the UI thread |
| BR-17 | 2 | Plausible | The update check mode has no guard: a bad index would reset all settings |
| BR-18 | 2 | Confirmed | Background check: three gaps in "use old data" and "never throws" |
| BR-19 to BR-33 | 3 | Mixed | Small ones, listed at the end |

---

## Tier 1

### BR-01 Explicit sources are searched and matched like any other

- [ ] Reviewed. *2026-11-12, spec change approved (006 R3.14 to R3.17); code waits until the whole review is read. Decided: explicit sources are left out by default, as in `winget`. In Settings, each explicit source gets its own **Use in searches** checkbox, which adds it to Search only, and a paragraph explains what an explicit source is.*

**Where:** [CatalogConnection.cs:150-188](../src/CartonStash.Core/Services/CatalogConnection.cs#L150-L188) (every
catalog goes into the composite), [CatalogConnection.cs:224-237](../src/CartonStash.Core/Services/CatalogConnection.cs#L224-L237),
[AppSettings.cs:121-123](../src/CartonStash.Core/Models/AppSettings.cs#L121-L123). Spec 006 R3.1.

**What happens:** Settings shows such a source as "explicit (excluded from default searches)", and the `winget` command
leaves it out unless it is named. But `ConnectAsync` only filters on failed sources and agreements, so search results,
the Installed list's matches and the Updates list all include it. `Info.Explicit` is read for display only
([WinGetPackageService.cs:232](../src/CartonStash.Core/Services/WinGetPackageService.cs#L232)). The background check
skips explicit sources when it refreshes ([BackgroundCheckRunner.cs:123](../src/CartonStash.Core/Services/BackgroundCheckRunner.cs#L123))
but then reads the same composite. `winget-font` is such a source on the dev PC (006 tasks.md, R3.1 check).

**Check:** search a font that only `winget-font` has, and compare with `winget search`.

**Fix idea:** leave explicit sources out in `ConnectAsync` and `SourcesInUse`, or keep them and change the text. The
spec has to say which (constitution P1).

### BR-02 Going back from the newest version: an installer that does nothing is reported as a success

- [ ] Reviewed. *2026-11-12, spec change approved (014 R2.8); code waits until the whole review is read. Proposed: after going back, compare the version alone; if Windows still lists the old one, warn "Not confirmed" and add no exclusion.*

**Where:** [WinGetPackageInstaller.cs:249-263](../src/CartonStash.Core/Services/WinGetPackageInstaller.cs#L249-L263)
(`AfterUpdate`), [WinGetPackageInstaller.cs:185-189](../src/CartonStash.Core/Services/WinGetPackageInstaller.cs#L185-L189),
[PackageActions.cs:41-54](../src/CartonStash.Core/ViewModels/PackageActions.cs#L41-L54). Spec 014 R2.5.

**What happens:** VLC 3.0.21 is installed, the newest version WinGet has, so the Updates page doesn't list it. In its
details you choose **Go back to this version** for 3.0.20. When the installer ends, the app checks whether it worked.
That check, `AfterUpdate`, was written for updates (013) and warns only when both are true:

1. Windows still lists the version from before, **and**
2. WinGet still offers an update.

- **Going back worked:** Windows lists 3.0.20 and WinGet offers 3.0.21. Condition 1 is false, so no warning. Correct.
- **The installer did nothing:** Windows still lists 3.0.21, so condition 1 is true. But VLC is still on the newest
  version, so WinGet offers no update and condition 2 is false. No warning: the app says "Installed successfully", and
  with **Leave this package out of update checks** ticked (the default) it also excludes VLC. Wrong.

So when going back starts from the newest version, the usual case, the check can never warn. R2.5 says it should: if
WinGet still lists the version from before, the result shall say so and not claim success.

And when the warning does fire for a go-back (going back from a version that isn't the newest), its words are about
updating: "Update not confirmed" and "may update itself the next time it starts".

The design note (014 design, "R2.5 reuses 013's `AfterUpdate` unchanged … After going back an update is available
again, so the check fits") only holds when going back worked.

**Check:** unit test: a go-back with `AfterUpdate(ok, "2.0", new InstalledAfterUpdate("2.0", UpdateAvailable: false))`
should warn.

**Fix idea:** for a go-back, compare the version alone, with its own message.

### BR-03 The details pane keeps a row its list has thrown away

- [x] Reviewed. *2026-11-12, fixed (PR #45). Installed and Updates were already fixed by 004 R8 and 014 R2.6, which keep the same rows on reload. Search still built new rows on every search, so the open pane stayed tied to a row no longer in the list: Install from the pane showed no progress. Now, when a search finishes, the pane switches to the new row for the same package, or closes if the package isn't in the results. Tests: `SearchViewModelTests.The_open_pane_*`.*

**Where:** [PackageDetailsViewModel.cs:67-68](../src/CartonStash.Core/ViewModels/PackageDetailsViewModel.cs#L67-L68),
[InstalledViewModel.cs:138-147](../src/CartonStash.Core/ViewModels/InstalledViewModel.cs#L138-L147),
[UpdatesViewModel.cs:110-125](../src/CartonStash.Core/ViewModels/UpdatesViewModel.cs#L110-L125),
[SearchViewModel.cs:96](../src/CartonStash.Core/ViewModels/SearchViewModel.cs#L96),
[PackageRowViewModel.cs:167-182](../src/CartonStash.Core/ViewModels/PackageRowViewModel.cs#L167-L182).

**What happens:** every reload builds new row objects: after each successful operation, on Refresh, when an exclusion
changes, on a new search. Nothing re-points the open pane, so it keeps the old row. From then on:

- the installed version, the badge and the "(installed)" mark in the version list are the old ones;
- an operation started from the pane never reaches the old row (the tracker only walks the rows in the lists), so the
  pane shows no progress and its buttons stay enabled. The queue still refuses a second active operation;
- the go-back question names the old installed version, and "Install {previous version}" would offer it too
  ([PackageActions.cs:138](../src/CartonStash.Core/ViewModels/PackageActions.cs#L138),
  [PackageActions.cs:166](../src/CartonStash.Core/ViewModels/PackageActions.cs#L166)).

**Check:** on Updates, open a package's pane, update it from the pane, wait for the list to reload: the pane still shows
the old version. On Search, open details for a package, search again, press Install in the pane: the pane doesn't change.

**Fix idea:** after a rebuild, point the pane at the new row with the same key and load its details again, or close it
when that row is gone.

### BR-04 Rows built after an operation ended still take its result: Install or Update button missing

- [x] Reviewed. *2026-11-12, fixed (PR #45). Only the row that watched the operation run hides its button, and only until it shows WinGet's data again. New rows, and rows refreshed by a reload, show buttons from the package's real state, so Install comes back on Search after an uninstall, and Update after "Update not confirmed". Tests: `RowFinishedOperationTests`.*

**Where:** [PackageRowViewModel.cs:37](../src/CartonStash.Core/ViewModels/PackageRowViewModel.cs#L37) (a new row takes
the newest operation with its key, finished or not),
[PackageRowViewModel.cs:89-92](../src/CartonStash.Core/ViewModels/PackageRowViewModel.cs#L89-L92),
[PackageRowViewModel.cs:137](../src/CartonStash.Core/ViewModels/PackageRowViewModel.cs#L137),
[UpdatesViewModel.cs:86-88](../src/CartonStash.Core/ViewModels/UpdatesViewModel.cs#L86-L88).

**What happens:** the "hide the button after success" rule is meant for the moment between an operation ending and the
list reloading. But rows built later find the same finished operation and apply the rule again. Two cases:

- **Search:** install X from Search, uninstall X on the Installed page, search X again. The new row finds the old
  successful install (the uninstall has another key, see BR-07), so it says "Installed" and has no Install button.
- **Updates:** an update ends as "Update not confirmed" (013 R2.2), or succeeds while WinGet still offers an update.
  After the reload the row is in Updates again with no Update button. The badge counts it, **Update all** is enabled,
  and pressing it does nothing: no question, no message. **Update selected** still queues it (BR-21). This only
  happens when the installed entry ID stays the same across versions (most EXE installers). MSI and MSIX entries change
  ID, so their rows get the button back.

Both last until the app restarts, or until Clear finished and a new search or refresh.

**Check:** unit tests with the fake queue: a finished successful Install for key K, then a new Search row for K with no
installed version; a finished Upgrade, then `Rebuild` with the package still `HasUpdate`.

**Fix idea:** a row built after an operation finished shouldn't hide buttons because of it.

### BR-05 Search rows lose their icons after any reload of the installed list

- [x] Reviewed. *2026-11-12, fixed (PR #45). Search entries are kept apart from the installed list's: a reload clears only its own, and only the next search replaces the search's, so search rows keep their icons. Tests: `VersionCacheTests`.*

**Where:** [WinGetPackageService.cs:196-200](../src/CartonStash.Core/Services/WinGetPackageService.cs#L196-L200),
[WinGetPackageService.cs:172-181](../src/CartonStash.Core/Services/WinGetPackageService.cs#L172-L181),
[PackageRowViewModel.cs:82-84](../src/CartonStash.Core/ViewModels/PackageRowViewModel.cs#L82-L84),
[InstalledPackagesStore.cs:48-53](../src/CartonStash.Core/Services/InstalledPackagesStore.cs#L48-L53).

**What happens:** search results and installed packages share one dictionary of version objects, and every reload of
the installed list clears it. The comment says "the next load fills it again", which is only true for installed
packages. A reload follows every successful operation, Refresh, a source refresh and a finished background check.
After it, rows of the current search that haven't been on screen yet get no icon URL, and each row only asks once, so
they keep the generic glyph until a new search. The Activity card of a package installed from Search asks the same way.

**Check:** search something with 30 or more results, install the first, wait for "Installed", scroll down.

**Fix idea:** don't clear the entries on invalidate (the next load overwrites them), or keep search entries apart.

### BR-06 Two background triggers in one process: the second ends the process while the first check runs

- [ ] Reviewed. *2026-11-12, spec change approved (010 R3.18); code waits until the whole review is read. Proposed: the background process counts the triggers it serves and exits only when the last is done. Check in the VM first.*

**Where:** [BackgroundCheckTask.cs:31-50](../src/CartonStash.App/Services/BackgroundCheckTask.cs#L31-L50),
[BackgroundCheckTask.cs:53-60](../src/CartonStash.App/Services/BackgroundCheckTask.cs#L53-L60),
[Program.cs:68-78](../src/CartonStash.App/Program.cs#L68-L78). Spec 010 R3.17.

**What happens:** the timer task and the "check on return" task are served by the same COM server process. If the
second fires while the first check is running, its `Run` finds the mutex, returns at once, completes its deferral and
calls `SignalBackgroundDone`. `Main` then returns and the process exits with the first check still under way: no
history entry, no notification. In daily mode the one-shot timer isn't set again
([BackgroundCheckTask.cs:74-77](../src/CartonStash.App/Services/BackgroundCheckTask.cs#L74-L77) never runs) until the
next unlock check or app start repairs it.

Waking the PC after a missed timer is the likely case: the late timer and "user present" fire seconds apart, and a
check takes longer than that. R3.17 was tested for the startup entry plus "user present", which are two processes.

**Check:** in the VM, set a daily time, sleep past it, wake and unlock, then look at Check history.

**Fix idea:** count the tasks running in the process and signal only when the last one ends.

---

## Tier 2

### BR-07 The same package can be queued twice from different pages

- [ ] Reviewed. *2026-11-12, fix approved, no spec change (restores 003 R4.2): when either side has no installed entry ID (a Search row), the queue also matches on WinGet ID plus source. Separate installed copies with the same WinGet ID (002 R6.5) stay separate. Code waits until the whole review is read.*

**Where:** [PackageInfo.cs:42](../src/CartonStash.Core/Models/PackageInfo.cs#L42),
[PackageOperationQueue.cs:51-69](../src/CartonStash.Core/Services/PackageOperationQueue.cs#L51-L69),
[WinGetPackageService.cs:336-352](../src/CartonStash.Core/Services/WinGetPackageService.cs#L336-L352). Spec 003 R4.2.

**What happens:** the queue knows a package by `Key`: the installed entry ID on Installed and Updates rows, the WinGet
ID on Search rows, which have no installed entry ID. So an operation started on one side isn't seen from the other.
With an update of X queued from Updates, the details pane for X on Search still offers "Go back to this version" and
queues a second operation for X. Rows on the other page don't show the running operation either. On Search, two
sources with the same package ID share one key.

**Fix idea:** when either side has no installed entry ID, also match on WinGet ID and source.

### BR-08 A reset settings file is never rewritten

- [ ] Reviewed. *2026-11-12, fix approved as proposed: save the defaults right after a reset, and at start remove leftover background tasks when the mode is Off. Code waits until the whole review is read.*

**Where:** [Settings.cs:102-113](../src/CartonStash.Core/Services/Settings.cs#L102-L113),
[Settings.cs:130-134](../src/CartonStash.Core/Services/Settings.cs#L130-L134),
[ShellViewModel.cs:45-48](../src/CartonStash.Core/ViewModels/ShellViewModel.cs#L45-L48),
[BackgroundCheckRunner.cs:66-69](../src/CartonStash.Core/Services/BackgroundCheckRunner.cs#L66-L69),
[App.xaml.cs:126-134](../src/CartonStash.App/App.xaml.cs#L126-L134).

**What happens:** after a corrupt `settings.json`, defaults are used in memory but the file stays as it is until some
setting changes. Until then:

- the "Settings were reset" notice shows at every start (the interface calls it one-time);
- every background trigger, each unlock included, adds "Failed: The app's settings couldn't be read." to Check
  history, because that test comes before the "is a check due" test;
- the tasks registered before the reset stay registered although the mode now reads Off. With the mode Off, start-up
  only handles the startup entry.

**Fix idea:** save the defaults right after a reset (the `.bak` keeps the old file), and remove leftover tasks at
start when the mode is Off.

### BR-09 Check history can keep part of a file path

- [x] Reviewed. *2026-11-12, decided otherwise than proposed: keep full paths in failure reasons and say so in `PRIVACY.md`, as for operation logs. Spec change approved (010 R3.13a, R3.13b). Code waits until the whole review is read.*

**Where:** [CheckHistoryStore.cs:45-48](../src/CartonStash.Core/Services/CheckHistoryStore.cs#L45-L48),
[BackgroundCheckRunner.cs:95](../src/CartonStash.Core/Services/BackgroundCheckRunner.cs#L95). Spec 010 R3.13, and
`PRIVACY.md` ("It holds no user name or file paths").

**What happens:** the pattern stops at the first space, so `C:\Users\Jean Dupont\AppData\…` becomes
`[path] Dupont\AppData\…`. Paths written with forward slashes aren't matched at all. The text comes from exception
messages.

**Fix idea:** don't keep exception text. Map failures to a fixed set of reasons.

### BR-10 The queue stops for good if anything around an operation throws

- [ ] Reviewed. *2026-11-12, fix approved as proposed: `try`/`finally` around the worker's loop body so the queue always moves on, and a catch-all around writing the log and around each `Completed` handler. Code waits until the whole review is read.*

**Where:** [PackageOperationQueue.cs:159-202](../src/CartonStash.Core/Services/PackageOperationQueue.cs#L159-L202),
[PackageOperationQueue.cs:204-225](../src/CartonStash.Core/Services/PackageOperationQueue.cs#L204-L225),
[OperationLogStore.cs:54-92](../src/CartonStash.Core/Services/OperationLogStore.cs#L54-L92). Spec 009 R2.5.

**What happens:** `_isWorking` is set at the top of the worker and cleared only at its last line. The log path is made
outside the `try`, and `Finish` writes the log and calls every `Completed` handler with no protection. An exception
there faults the worker task, which nobody looks at. After that nothing runs: new operations stay "Queued", `Drained`
never fires, the installed list never reloads, and the "operations running" marker stays on, so background checks keep
being skipped. The log store only catches `IOException` and `UnauthorizedAccessException`.

**Fix idea:** `try`/`finally` around the loop body, and a catch-all around logging and each handler.

### BR-11 A failed App Installer setup may end the app instead of showing the reason

- [ ] Reviewed. *2026-11-12, fix approved as proposed: catch any error from Windows' package install in App Installer setup and show its message ("Windows couldn't install App Installer: …") instead of ending the app. Unit test with an `addPackage` that throws. Code waits until the whole review is read.*

**Where:** [AppInstallerSetup.cs:122-131](../src/CartonStash.Core/Services/AppInstallerSetup.cs#L122-L131),
[AppInstallerSetup.cs:58](../src/CartonStash.Core/Services/AppInstallerSetup.cs#L58),
[WinGetSetupViewModel.cs:44-56](../src/CartonStash.Core/ViewModels/WinGetSetupViewModel.cs#L44-L56).

**What happens:** when Windows can't deploy a package, awaiting `AddPackageByUriAsync` normally throws. The
`result.ErrorText` branch is rarely reached. The exception isn't one of the types `RunAsync` catches and the command
has no catch, so "Windows couldn't install App Installer: …" would never show.

**Check:** make `addPackage` throw in a unit test, or run Set up for me in the VM with deployment blocked.

**Fix idea:** catch everything around `addPackage` and return its message.

### BR-12 "WinGet couldn't start" only handles one exception type

- [ ] Reviewed. *2026-11-12, fix approved as proposed: at start, any error from WinGet (not only `COMException`) opens the "WinGet couldn't start" window instead of ending the app. Code waits until the whole review is read.*

**Where:** [App.xaml.cs:27-59](../src/CartonStash.App/App.xaml.cs#L27-L59). Spec 011 R3.5 (still pending in the roadmap).

**What happens:** the projection turns WinGet's error codes into several .NET exception types: access denied becomes
`UnauthorizedAccessException`, a missing interface `InvalidCastException`, and so on. Only `COMException` opens the
setup window. Anything else leaves `OnLaunched`, which is `async void`, and the app ends with no window. The same
narrow filter is in [SourceAgreementCheck.cs:24](../src/CartonStash.Core/Services/SourceAgreementCheck.cs#L24) and
[WinGetPackageService.cs:128](../src/CartonStash.Core/Services/WinGetPackageService.cs#L128).

**Fix idea:** catch `Exception` at this one place.

### BR-13 Copy can end the app when the clipboard is busy

- [ ] Reviewed. *2026-11-12, fix approved as proposed: when Windows refuses the clipboard (another program holds it), wait a moment and retry once; if it still fails, say "Couldn't copy: another program is using the clipboard. Try again." instead of ending the app. Covers the Log dialog's Copy and Settings' Copy diagnostic info. Code waits until the whole review is read.*

**Where:** [PlatformServices.cs:188-197](../src/CartonStash.App/Services/PlatformServices.cs#L188-L197), called from
[PlatformServices.cs:157-161](../src/CartonStash.App/Services/PlatformServices.cs#L157-L161) and
[SettingsViewModel.cs:148-153](../src/CartonStash.Core/ViewModels/SettingsViewModel.cs#L148-L153).

**What happens:** `Clipboard.SetContent` and `Flush` throw when another program holds the clipboard. Clipboard managers
and remote sessions do that, the Hyper-V enhanced session included. Neither caller catches it.

**Fix idea:** catch it in `SetText`, retry once, and tell the user if it still fails.

### BR-14 A superseded load that fails for a real reason escapes the catch filters

- [ ] Reviewed. *2026-11-12, fix approved as proposed: any error from a load that a newer one replaced is ignored, since the newer load shows its own result. In the installed list's store, Search and the details pane, with a unit test where an old load fails with a real error. Code waits until the whole review is read.*

**Where:** [InstalledPackagesStore.cs:72-78](../src/CartonStash.Core/Services/InstalledPackagesStore.cs#L72-L78),
[SearchViewModel.cs:117-124](../src/CartonStash.Core/ViewModels/SearchViewModel.cs#L117-L124),
[PackageDetailsViewModel.cs:284-292](../src/CartonStash.Core/ViewModels/PackageDetailsViewModel.cs#L284-L292).

**What happens:** the two filters cover "a cancellation, on a superseded load" and "any error, on the current load".
An error that isn't a cancellation, on a load that was superseded, matches neither. Example: `Task.WhenAll` in
`GetInstalledAsync` ([WinGetPackageService.cs:58](../src/CartonStash.Core/Services/WinGetPackageService.cs#L58))
rethrows the first task's `WinGetException` even when the second ended by cancellation. For the store, that exception
reaches `async void` code ([InstalledPage.xaml.cs:20-24](../src/CartonStash.App/Views/InstalledPage.xaml.cs#L20-L24),
the `Drained` handler) or a command (Refresh, Search), and ends the app. For the details pane it's harmless.

It needs a failure and a new load at the same moment, for example Refresh pressed twice while a source is failing.

**Fix idea:** add `catch (Exception) when (load.IsCancellationRequested) { }`.

### BR-15 Dropping the cached catalogs can be undone by a connection already under way

- [ ] Reviewed. *2026-11-12, fix approved as proposed: a counter that each `Invalidate` raises; a connection notes it when it starts and is only cached if it hasn't changed when it finishes. Covers the remote-first, local-first and installed-only catalogs. Unit test on that rule (a connection that started before an invalidate isn't kept). Code waits until the whole review is read.*

**Where:** [CatalogConnection.cs:31-38](../src/CartonStash.Core/Services/CatalogConnection.cs#L31-L38),
[CatalogConnection.cs:103-135](../src/CartonStash.Core/Services/CatalogConnection.cs#L103-L135),
[CatalogConnection.cs:81-96](../src/CartonStash.Core/Services/CatalogConnection.cs#L81-L96).

**What happens:** `Invalidate` only clears the fields. A connection that started before it and finishes after it stores
its older catalog as the current one, and a reader waiting on the gate takes it. Example: an install is running; the
details pane of an installed package starts a local-first connection; the install ends and invalidates; the reload
waits on the gate and gets the catalog from before the install. The new package is missing until the next Refresh.
`GetInstalledOnlyAsync` has no gate at all.

**Fix idea:** a generation number: store a catalog only if no invalidate happened since its connection began.

### BR-16 Search maps its results on the UI thread

- [ ] Reviewed. *2026-11-12, fix approved as proposed: move search's result mapping off the UI thread (`Task.Run`, as the installed list does), and the same for the version loop in `GetAgreementsAsync` and `GetPackageCatalogByName` in `RefreshSourceAsync` (constitution A4). Measure the stall on a 50-result search before and after. Code waits until the whole review is read.*

**Where:** [WinGetPackageService.cs:45](../src/CartonStash.Core/Services/WinGetPackageService.cs#L45),
[WinGetPackageService.cs:281-295](../src/CartonStash.Core/Services/WinGetPackageService.cs#L281-L295),
[WinGetPackageService.cs:336-352](../src/CartonStash.Core/Services/WinGetPackageService.cs#L336-L352). Constitution A4.

**What happens:** after `await connection.FindAsync` the code is back on the UI thread, and `ToPackages` makes about
eight to ten calls into WinGet's process per result, for up to 50 results. `GetInstalledAsync` moves the same work to
the thread pool on purpose; search doesn't. Also on the UI thread: the loop over every available version in
`GetAgreementsAsync` ([WinGetPackageInstaller.cs:15-18](../src/CartonStash.Core/Services/WinGetPackageInstaller.cs#L15-L18))
and `GetPackageCatalogByName` in `RefreshSourceAsync`
([WinGetPackageService.cs:242](../src/CartonStash.Core/Services/WinGetPackageService.cs#L242)).

**Check:** measure how long the window stalls when a 50-result search returns.

**Fix idea:** `Task.Run` around the mapping, as the installed list does.

### BR-17 The update check mode has no guard: a bad index would reset all settings

- [ ] Reviewed. *2026-11-12, fix approved as a guard (not known to happen): ignore a mode index outside the list, as the three combo boxes and the interval already do, so an invalid mode is never saved. Unit test with index -1. Code waits until the whole review is read.*

**Where:** [CheckSettingsViewModel.cs:139](../src/CartonStash.Core/ViewModels/CheckSettingsViewModel.cs#L139) against
[CheckSettingsViewModel.cs:141-147](../src/CartonStash.Core/ViewModels/CheckSettingsViewModel.cs#L141-L147) and
[SettingsViewModel.cs:178-200](../src/CartonStash.Core/ViewModels/SettingsViewModel.cs#L178-L200);
[Settings.cs:93-99](../src/CartonStash.Core/Services/Settings.cs#L93-L99).

**What happens:** the three combo boxes and the interval check their index before saving. The mode doesn't. If the
RadioButtons control ever reports -1 (nothing selected), `(CheckMode)(-1)` is saved, and at the next start the loader
finds an undefined value and resets every setting, source agreement answers and exclusions included. Not known to
happen today.

**Check:** log `ModeIndex` changes while moving to and from Settings and switching theme.

**Fix idea:** the same guard as the others.

### BR-18 Background check: three gaps in "use old data" and "never throws"

- [ ] Reviewed. *2026-11-12, fix approved, restores 010 R1.6 and R1.9: (1) any refresh failure falls back to the data WinGet already has, not only `WinGetException` and the timeout; (2) the due test and the two skip tests move inside the `try`, so an error there is recorded and a daily timer is still set again; (3) showing the notification gets its own catch, so it can't add a second, Failed entry. Unit tests for each. Code waits until the whole review is read.*

**Where:** [BackgroundCheckRunner.cs:63-97](../src/CartonStash.Core/Services/BackgroundCheckRunner.cs#L63-L97),
[BackgroundCheckRunner.cs:128](../src/CartonStash.Core/Services/BackgroundCheckRunner.cs#L128),
[BackgroundCheckRunner.cs:152-155](../src/CartonStash.Core/Services/BackgroundCheckRunner.cs#L152-L155). Spec 010 R1.6, R1.9.

**What happens:**

1. R1.9: only `WinGetException` and the refresh budget's own timeout fall back to the data WinGet already has. Any
   other exception from the refresh ends the whole check as Failed.
2. The due test and the two skip tests run before the `try`. An exception there (the network cost lookup, for example)
   leaves `RunAsync`, which says it never throws. The callers catch it, but nothing is recorded and a daily timer isn't
   set again.
3. The notification is shown inside the `try`, after the result was recorded. If showing it throws, a second entry,
   Failed, is added for the same check.

---

## Tier 3

- [ ] **BR-19 A failed icon download leaves an empty square.**
  *2026-11-12: reproduced in the test VM (1.4.0 Store build, the app's downloads blocked by a firewall rule): rows with an icon address show nothing. Fix approved: on `ImageFailed`, show the generic package glyph again. Code waits until the whole review is read.*
  [PackageIcon.xaml.cs:83-90](../src/CartonStash.App/Controls/PackageIcon.xaml.cs#L83-L90) hides the glyph as soon as
  it has an address, and nothing handles `ImageFailed`. A dead link or an unsupported format shows nothing. Confirmed.

- [ ] **BR-20 Operation logs are read and written on the UI thread, whole file.**
  [PackageOperationQueue.cs:215](../src/CartonStash.Core/Services/PackageOperationQueue.cs#L215) calls
  [OperationLogStore.cs:85-86](../src/CartonStash.Core/Services/OperationLogStore.cs#L85-L86), which reads the whole
  installer log and writes it back, and
  [WinGetRecordReader.cs:42-46](../src/CartonStash.Core/Services/WinGetRecordReader.cs#L42-L46), which reads every
  WinGet log made during the operation. The Log button reads the whole file again to show its last million characters
  in one TextBox. A verbose MSI log can be tens of MB: the window freezes at the end of the operation. Confirmed; cost
  unmeasured.

- [ ] **BR-21 Update selected doesn't apply the row's own rules.**
  [UpdatesViewModel.cs:81-83](../src/CartonStash.Core/ViewModels/UpdatesViewModel.cs#L81-L83) filters on busy only;
  Update all and the row button also need `ShowUpdate`. A selected row whose update just succeeded, or App Installer
  waiting for a restart (004 R6.2), is queued again. Confirmed.

- [ ] **BR-22 The version list has no guard around WinGet's comparer.**
  [WinGetPackageService.cs:367](../src/CartonStash.Core/Services/WinGetPackageService.cs#L367) and
  [:376](../src/CartonStash.Core/Services/WinGetPackageService.cs#L376) call `CompareToVersion` without the `try` that
  `Compare` has ([:124-131](../src/CartonStash.Core/Services/WinGetPackageService.cs#L124-L131)). One version string
  WinGet refuses, an empty one for example, fails the whole details load. Plausible.

- [ ] **BR-23 "No applicable update" doesn't mention the scope setting.**
  The scope setting applies to updates too
  ([WinGetPackageInstaller.cs:81](../src/CartonStash.Core/Services/WinGetPackageInstaller.cs#L81), 006 R2.1). A
  mismatch ends as "No applicable update for this PC."
  ([:433](../src/CartonStash.Core/Services/WinGetPackageInstaller.cs#L433)), while installs get the hint to change the
  setting ([:430-432](../src/CartonStash.Core/Services/WinGetPackageInstaller.cs#L430-L432)). Confirmed.

- [ ] **BR-24 Source agreement check: what happens when reading the sources fails.**
  [SourceAgreementCheck.cs:19-27](../src/CartonStash.Core/Services/SourceAgreementCheck.cs#L19-L27): at start it
  returns without loading the installed list, so the Updates badge stays empty until a page is opened. The later reads
  ([:34](../src/CartonStash.Core/Services/SourceAgreementCheck.cs#L34),
  [:55](../src/CartonStash.Core/Services/SourceAgreementCheck.cs#L55),
  [:68](../src/CartonStash.Core/Services/SourceAgreementCheck.cs#L68)) have no catch: from Settings and from Search's
  Read and accept, an exception there ends the app. Confirmed.

- [ ] **BR-25 No recovery when WinGet's server goes away.**
  [CatalogConnection.cs:22](../src/CartonStash.Core/Services/CatalogConnection.cs#L22) makes one `PackageManager` for
  the session. If the server process ends, every call fails with "RPC server unavailable" until CartonStash restarts.
  Nothing makes a new one or says what to do. Plausible.

- [ ] **BR-26 The WinGet presence check doesn't prove WinGet can start, and background checks skip it.**
  [WinGetAvailability.cs:13-18](../src/CartonStash.Core/Services/WinGetAvailability.cs#L13-L18) only checks that the
  App Installer package is registered. One too old to have the COM API still passes, and then `new PackageManager()`
  ends the process. [BackgroundCheckTask.cs:62-71](../src/CartonStash.App/Services/BackgroundCheckTask.cs#L62-L71)
  makes the connection with no check at all, and before the runner knows whether a check is due: every "user present"
  trigger starts WinGet's server, even to do nothing. Plausible.

- [ ] **BR-27 Interval mode: a timer tick can arrive "not yet due".**
  The timer repeats from when it was registered
  ([BackgroundCheckScheduler.cs:58](../src/CartonStash.App/Services/BackgroundCheckScheduler.cs#L58)), a check counts
  as due from 15 minutes early ([CheckSchedule.cs:33-34](../src/CartonStash.Core/Services/CheckSchedule.cs#L33-L34)),
  and the last check's time is taken when it ends
  ([BackgroundCheckRunner.cs:144-145](../src/CartonStash.Core/Services/BackgroundCheckRunner.cs#L144-L145)). A late,
  slow check followed by a tick on time can make that tick more than 15 minutes early. It is dropped with no entry, and
  the next chance is the following tick or an unlock. Plausible.

- [ ] **BR-28 GitHub notes that can't be parsed break the pane for that package.**
  The body is cached before it is parsed
  ([GitHubReleaseNotesService.cs:72-75](../src/CartonStash.Core/Services/GitHubReleaseNotesService.cs#L72-L75)). The
  pane parses cached notes inside its own load
  ([PackageDetailsViewModel.cs:279-282](../src/CartonStash.Core/ViewModels/PackageDetailsViewModel.cs#L279-L282)), and
  any exception there becomes "Couldn't load details". If the Markdown parser throws on a body (it does for very deep
  nesting), that package's pane shows an error for the rest of the session. Plausible.

- [ ] **BR-29 Icon and version caches use the package ID alone.**
  [WinGetPackageService.cs:21-22](../src/CartonStash.Core/Services/WinGetPackageService.cs#L21-L22). Two sources with
  the same ID share one entry: the wrong icon, and the installed badge can be compared against the other source's
  version. Confirmed; only with such sources.

- [ ] **BR-30 The first installed load and an operation finishing.**
  [InstalledPackagesStore.cs:21-28](../src/CartonStash.Core/Services/InstalledPackagesStore.cs#L21-L28) reloads on
  `Drained` only when a load has already finished. If the first load is running when an operation ends, no reload
  follows, and that load may have read the list before the change. Confirmed; narrow.

- [ ] **BR-31 `Publish-Release.ps1` empties any folder it is given.**
  [Publish-Release.ps1:27-30](../tools/Publish-Release.ps1#L27-L30) deletes every file in `-Output` and
  `-StoreOutput`. Right for the two default folders; a mistyped path is emptied with no question. Confirmed.

- [ ] **BR-32 A restart on the Settings page may open it with nothing selected in the navigation.**
  [MainWindow.xaml.cs:82-84](../src/CartonStash.App/MainWindow.xaml.cs#L82-L84) runs before the window is shown, when
  `Nav.SettingsItem` may still be null. Plausible; cosmetic.

- [ ] **BR-33 Small gaps.**
  - [Settings.cs:64-78](../src/CartonStash.Core/Services/Settings.cs#L64-L78) and
    [:102-111](../src/CartonStash.Core/Services/Settings.cs#L102-L111) don't catch `UnauthorizedAccessException`: a
    settings file the user can't read ends the app at start.
  - [CheckHistoryStore.cs:75-77](../src/CartonStash.Core/Services/CheckHistoryStore.cs#L75-L77): the app and a
    background check write through the same `.tmp` name. If they collide, one record is lost silently.
  - [BackgroundCheckTask.cs:27](../src/CartonStash.App/Services/BackgroundCheckTask.cs#L27): the result of
    `CoRegisterClassObject` is ignored. If it fails, the process waits its 12 minutes for nothing.
  - [PlatformServices.cs:162-167](../src/CartonStash.App/Services/PlatformServices.cs#L162-L167): Open folder creates
    the folder inside an `async void` handler with no catch.
  - [PackageOperation.cs:206](../src/CartonStash.Core/Models/PackageOperation.cs#L206): the card's time is worked out
    when the operation starts, so yesterday's card keeps showing the time without the date.

---

## Looked at, nothing found

So the next review can skip them:

- **`is A or B && c` conditions.** All nine read as intended: `PackageActions.cs:54`, `PackageOperation.cs:240`,
  `PackageOperationQueue.cs:232`, `OperationCard.xaml.cs:46`, `BackgroundCheckScheduler.cs:88`,
  `BackgroundCheckRunner.cs:123`, `WinGetNotesParser.cs:67` and `:292`, `GitHubReleaseNotesService.cs:82`.
- **WinGet collections (W2).** Every one is read by index. The `foreach` loops are over .NET lists.
- **Dialogs.** `DialogService` shows one at a time, so two can't collide.
- **Page lifetime.** All four pages are cached, so the handlers they add to the session-long view models are added once.
- **Links and icons.** Only http and https links can be opened, icons are https only, HTML in release notes is reduced
  to text, images are never loaded.
- **GitHub requests.** Host check on redirects, size limit, timeout, no cookies.
- **App Installer setup download.** SHA-256 check before anything is installed; zip entries can't leave the folder.
- **Schedule arithmetic.** Daylight saving gaps and repeats, and the 15-minute early window in daily mode.
- **Settings equality.** The hand-written `Equals` covers all ten properties.
- **Release workflow.** Tag format check, changelog check before the build, pinned actions, no secrets.
- **Skipped and failed checks wait a full interval.** That's what 010 R1.5 and R1.6 ask for.

## Not covered

- The tests themselves weren't reviewed for bugs.
- XAML was checked for bindings and event wiring, not for layout, styles or high contrast.
- `tools/Render-Logos.ps1` was read; it's a developer tool and nothing in it affects the app.
