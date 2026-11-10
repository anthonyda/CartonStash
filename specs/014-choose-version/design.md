# 014 – Install a chosen version: Design

**Status:** Done (2026-11-09). Approved 2026-11-08. **Amendment R1.6, R1.7 (2026-11-09): Done**, approved with the proposed answer to Q6. **Amendment R2.6, R2.7, R3.8, R3.9 (2026-11-10): Done**, approved 2026-11-10, with Q7's notice added. R3.8's scope source changed after T18 (Windows Installer first), approved 2026-11-10
**Requirements:** [requirements.md](requirements.md)

## Overview

The chosen version travels as one extra value, `string? Version`, from the details pane to WinGet. The pane asks
`PackageActions` for it, `PackageActions` asks the questions (agreements, the going-back confirmation) and queues the
operation, the queue keeps the version on the `PackageOperation` (so Retry reuses it), and `WinGetPackageInstaller`
turns it into `InstallOptions.PackageVersionId`. A null version means WinGet's default, which is every operation today,
so rows, **Update selected** and **Update all** don't change (R2.4).

Whether a version is newer or older is decided in `WinGetPackageService` with WinGet's comparer and travels as an enum
on each version (R1.2). The pane only reads it.

Going back is the uncertain part: WinGet's behaviour for an older version over an installed one isn't documented for
the COM API. T1 measured it in the test VM before anything else was built; the "Going back" section has the results.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `PackageVersion`, `VersionDirection` | One available version and how it compares with the installed one (R1.1, R1.2) | New |
| Core/Models | `PackageDetails` | `Versions` becomes `IReadOnlyList<PackageVersion>` | Changed |
| Core/Models | `PackageOperation` | `Version` (null = default), `ExcludeOnSuccess`, `GoingBack`, `PreviousVersion`, `ThenInstall`, `AfterUninstall`; `Title` names the version (R2.1); `CanUninstallThenInstall`, `CanInstallPrevious` and their commands (R3.5, R3.7) | Changed |
| Core/Services | `WinGetPackageService` | `ReadVersions` adds the direction from WinGet's comparer | Changed |
| Core/Services | `IPackageInstaller`, `WinGetPackageInstaller` | `version` parameter on `GetAgreementsAsync` and `RunAsync`; `PackageVersionId`; 013's check after going back; R3.3 message | Changed |
| Core/Services | `PackageOperationQueue` | `Enqueue(..., version, excludeOnSuccess)`; passes the version to the installer | Changed |
| Core/Services | `OperationLogStore` | A "Chosen version" line in the log (R2.1) | Changed |
| Core/Services | `IDialogService` | `ConfirmWithOptionAsync` for the going-back confirmation and its box (R3.1, R3.2) | Changed |
| Core/ViewModels | `PackageActions` | `InstallVersionAsync(package, version)`; adds the exclusion after success (R3.2); `UninstallThenInstallAsync` and the chained install (R3.5, R3.6); `InstallPrevious` (R3.7); takes `ISettings` | Changed |
| App/Controls | `OperationCard.xaml` | The two new buttons, as Restart CartonStash is shown today (R3.5, R3.7) | Changed |
| Core/ViewModels | `PackageDetailsViewModel` | The version list, the selected version, its action and `Show all versions` (R1). The action goes through the row (`PackageRowViewModel.InstallVersionAsync`), which already holds `PackageActions` | Changed |
| App/Services | `DialogService` | `ConfirmWithOptionAsync`: a ContentDialog with a CheckBox | Changed |
| App/Controls | `PackageDetailsPane.xaml` | "Available versions" becomes a list with one action button (R1.1, R1.3, R1.4) | Changed |

## Models

```csharp
public enum VersionDirection
{
    NotInstalled,   // package not installed: "Install"
    Installed,      // the installed version: no action, marked "(installed)"
    Newer,          // "Update to this version"
    Older,          // "Go back to this version"
    Unknown,        // WinGet can't compare: "Install this version", confirmed as Older (R1.2)
}

public sealed record PackageVersion(string Version, VersionDirection Direction);
```

`VersionsText` in the view model is kept for the case where actions aren't offered (R1.5): the list then reads as
today.

## WinGet API usage

| Need | Call | Notes |
|------|------|-------|
| Versions (R1.1) | `CatalogPackage.AvailableVersions` | Already read by `ReadVersions`, by index (W2) |
| Direction (R1.2) | `CatalogPackage.InstalledVersion.CompareToVersion(v)` | `Greater` → the version is `Older`; `Lesser` → `Newer`; `Equal` → `Installed`; `Unknown` → `Unknown`. No installed version → `NotInstalled`. Only for a primary match (002 R6.4); other rows get `Unknown` |
| One version (R2.2) | `CatalogPackage.GetPackageVersionInfo(PackageVersionId)` | Its `GetCatalogPackageMetadata().Agreements` for the chosen version's agreements |
| Install it (R2.1) | `InstallOptions.PackageVersionId` | The `PackageVersionId` found in `AvailableVersions` by `Version` (ordinal). If it isn't there any more (source updated since the pane opened), the operation fails with "This version is no longer available from {source}." and no Retry |
| Newer (R1.1) | `UpgradePackageAsync` | As today, with the version set |
| Older (R3) | `InstallPackageAsync`, no `Force` | Measured in T1, see below |

The COM objects never leave the services (A2): the version crosses the boundary as a string and is looked up again
when the operation runs, the same way the package itself is found again today.

## Going back (R3)

**Measured in T1 (2026-11-08, test VM, WinGet 1.29.380).** A console tool on the COM API installed each package's
latest version, then tried the previous one with each call, without and with `Force`, then uninstalled and installed
the previous one. A clean checkpoint was restored for each package. The logs are in `C:\VMs\CartonStash-Test\logs\t014-*`.

| Type | Package | `InstallPackageAsync` with the older version | Uninstall, then install it |
|------|---------|----------------------------------------------|----------------------------|
| Portable | `jqlang.jq` 1.8.2 → 1.8.1 | **Goes back** | (not needed) |
| NSIS | `Notepad++.Notepad++` (nullsoft) 8.9.9 → 8.9.8.1 | **Goes back** | Works |
| MSI (WiX) | `Notepad++.Notepad++` (wix), `Microsoft.PowerShell` (wix) | `InstallError`, exit 1603, `0x8A150049`. The MSI log says "A newer version of … is already installed." | Works |
| Inno | `Git.Git` 2.56.0.4 → 2.56.0.3 | `InstallError`, exit 1, `0x8A150006`. The installer asks "Do you really want to downgrade?" and silent mode answers No. | Works |
| MSIX | `Microsoft.PowerShell` (msix) 7.6.9.0 → 7.6.8.0 | `InstallError`, `0x80070002`: Windows stages the older package and refuses to register it | Works |
| MSIX, ships with Windows | `Microsoft.WindowsTerminal` 1.25 → 1.24 | `InstallError`, `0x80070002` | **Fails**: the uninstall works, then the older version still won't register, so nothing is left installed |

Also measured:

- `UpgradePackageAsync` with an older version refuses every time, before any download: `NoApplicableUpgrade`,
  `0x8A15004F`.
- `Force` changed nothing in any case.
- Updating to a version newer than the installed one but not the latest (`UpgradePackageAsync` with that version)
  works for portable, NSIS and MSI. So does installing an older version when the package isn't installed.
- Every refusal left the installed version as it was: trying costs a download, nothing more.

**Decisions:**

- **The call:** `InstallPackageAsync` with `PackageVersionId` for going back and for `Unknown`; `UpgradePackageAsync`
  with `PackageVersionId` for `Newer`; `InstallPackageAsync` for `NotInstalled`.
- **`Force` (Q3):** never set. It changed nothing, and it would let other warnings through.
- **R3.3 can't rely on a code.** The refusals are an installer's generic failure (MSI 1603, Inno exit 1) or a generic
  Windows error (`0x80070002`). They're the same codes as other failures. So the R3.3 message applies to any
  `InstallError` while going back, after the in-use arm (003 R7 still wins). It says "may", because the cause isn't
  certain:

> {name} couldn't go back to {version}. Its installer may not install over a newer version. To go back, uninstall
> {name}, then install {version}. Uninstall, then install does both.

  It stays retryable: unlike 013 R3.1, the cause isn't known.

**Open, for the maintainer (from T1):** going back directly works only for portable and NSIS packages. MSI, Inno and
MSIX packages, which are most of the catalog, need an uninstall first. **Decided 2026-11-08 (Q5):** the app offers
it, below.

## Uninstall, then install (R3.4 to R3.7)

**Where it's offered.** A failed going-back operation (`GoingBack`, `Kind == Install`, `Result == Failed`, outcome from
the R3.3 arm, so not 003 R7's in-use case) has `CanUninstallThenInstall`. Its Activity card shows
**Uninstall, then install {version}** next to Retry. The command raises the queue's `UninstallThenInstallRequested`,
which `PackageActions` handles, the way Restart CartonStash goes through `RestartRequested` today.

**The confirmation (R3.5).** `ConfirmAsync`:

- Title: "Uninstall {name}, then install {version}?"
- Message: "{name} {installed} will be uninstalled, then {version} installed. Settings and data that {name} keeps may
  be lost. Apps that come as an MSIX package always lose them."
- Buttons: "Uninstall, then install", "Cancel".

**The chain.** On confirm, `PackageActions` queues an `Uninstall` of the row's installed entry (the same one as 003
R2.2) with `ThenInstall = version`, `PreviousVersion = installed` and the failed operation's `ExcludeOnSuccess` and
agreement acceptance. The queue's one-operation-per-package rule (003 R4.2) would refuse a second operation queued
next to it, so the install is queued from the `Completed` handler instead:

- Uninstall `IsSuccess` → queue `Install` with the version, `AfterUninstall = true`, `PreviousVersion` and the same flags.
- Uninstall failed or cancelled → nothing more (R3.6). Its result is an ordinary uninstall result.

**After the install (R3.7).** `PackageOperationQueue.Finish` passes an `AfterUninstall` failure through a pure
`AfterUninstallOutcome(outcome, name, version)`, which puts first: "{name} is no longer installed: it was uninstalled
to make way for {version}, and then that install failed." The installer's reason follows. Such an operation has
`CanInstallPrevious`: **Install {previous version}**, which queues an ordinary install of `PreviousVersion`, with no
going-back flags and no exclusion. If the source no longer has that version, the "no longer available" result
applies. Retry stays, and installs the chosen version again.

**What isn't chained.** The app never uninstalls without this confirmation, never chains more than one uninstall and
one install, and never installs the previous version on its own: each step after a failure is a button.

## The confirmation (R3.1, R3.2)

`IDialogService.ConfirmWithOptionAsync(title, message, confirmText, optionText, optionChecked)` returns
`(bool Confirmed, bool OptionChecked)`.

- Title: "Go back to {name} {version}?"
- Message: "{name} {installed} is installed. Going back to {version} runs that version's installer over it. Some
  installers can't go back, and {name}'s settings may not carry over to an older version."
- Option: "Leave {name} out of update checks", checked. If the package is already excluded, the box isn't shown.
- Buttons: "Go back to {version}", "Cancel".

An `Unknown` direction gets the same dialog, with "Install {name} {version}?" and "Install {version}".

If the box is checked, the operation gets `ExcludeOnSuccess`. `PackageActions` already handles the queue's `Completed`
event for App Installer (004 R6.1). The same handler adds the exclusion with `settings.Update(s => s.WithExcluded(id))`
when the result is `Succeeded` or `SucceededRestartRequired`. It doesn't add it on `SucceededUnconfirmed` (nothing
changed), on a failure, or on a cancel. Retry keeps the flag.

## Data flow

```
Pane: select version → action button
  → PackageDetailsViewModel.RunVersionCommand
  → Row.InstallVersionAsync(version) → PackageActions.InstallVersionAsync(row.Package, version)
       ├─ queue.Find(package) active → nothing (003 R4.2)
       ├─ Older / Unknown → ConfirmWithOptionAsync; Cancel → nothing (R3.1)
       ├─ AskAgreementsAsync(package, version.Version) (R2.2)
       └─ queue.Enqueue(kind, package, accepted, version.Version, excludeOnSuccess)
            kind: NotInstalled → Install; Newer → Upgrade; Older / Unknown → Install (T1)
  → WinGetPackageInstaller.RunAsync(..., version)
       ├─ find PackageVersionId (missing → "no longer available")
       ├─ Install/UpgradePackageAsync with PackageVersionId
       ├─ MapInstallResult (+ R3.3 arm when going back)
       └─ Succeeded and installed before → InstalledAfterUpdateAsync → AfterUpdate (013, R2.5)
  → Completed → ExcludeOnSuccess? settings.WithExcluded (R3.2)
  → installed data refresh (002 R1.3), pane and rows update
```

R2.5 reuses 013's `AfterUpdate` unchanged: it warns when WinGet still lists the version from before and still reports
an update. After going back an update is available again, so the check fits. For an upgrade it already runs. The only
change is that it now also runs for a version-chosen install over an installed package.

## Operation wording (R2.1)

`PackageOperation.Title` becomes `"{Verb} {Name} {Version}"` when a version is chosen, for example "Install PowerToys
0.101.0" and "Update PowerToys 0.102.1". `StatusText` doesn't change. The notification uses `Title` already. The log adds
`Chosen version: {Version}` after "Available version" (or `default` when null).

## UI (PackageDetailsPane)

The "Available versions" section becomes:

- A `ListView` of the shown versions, one tab stop and arrow keys between items (Q5). Each item reads
  "0.101.0" or "0.102.3101.0 (installed)". It has no scrolling of its own: the pane scrolls.
- Under it, one button for the selected version, its text from the direction ("Install", "Update to this version",
  "Go back to this version", "Install this version"), its accessible name with the package and version (R1.4), for
  example "Go back to PowerToys 0.101.0". It's hidden until a version is selected, and for the installed one.
- **Show all versions** (a HyperlinkButton) when there are more than 10 (R1.3). It's replaced by the full list and
  stays expanded until another package opens.
- When actions aren't offered (R1.5), the section is today's text (`VersionsText`), so nothing becomes focusable for
  no reason.

One list and one button, rather than a button per version, so a package with 80 versions isn't 80 tab stops.

`CanRunVersion` (a plain bool, see the x:Bind gotcha) is false while the row is busy, so the button follows 003 R4.2.

## Amendment: action on the selected version, excluded rows open details (R1.6, R1.7)

**R1.6, the action in the item.** `VersionItem` gains what its line needs, so the template binds plain values (the
x:Bind gotcha):

| Member | Meaning |
|--------|---------|
| `IsSelected` (bool, observable) | Set by `PackageDetailsViewModel` when `SelectedVersion` changes; only one item has it |
| `ShowAction` (bool, observable) | `IsSelected` and the pane's `ShowVersionAction` |
| `ActionText`, `ActionName` | Today's `VersionActionText` and `VersionActionName` for this item (R1.1, R1.4) |

The item template becomes a two-column `Grid`: the label, then the `Button` (right-aligned, `Visibility` from
`ShowAction`, `Command` the pane's `RunVersionCommand`, so `CanRunVersion` and 003 R4.2 still disable it). The button
under the list goes. **Show all versions** stays under the list, since it belongs at the end.

- The button is compact (`Padding="8,2"`) so a selected line doesn't grow taller than the others by much.
- Keyboard: arrow keys move in the list as today; Tab from the selected item reaches its button, Shift+Tab goes back.
  Narrator reads the item ("1.8.1"), then the button ("Update jq to 1.8.1").
- `ShowVersionAction` and `CanRunVersion` stay on the view model, used by the items and the tests.

**R1.7, excluded rows open details.** `DetailsBehavior.Attach(page, list, details, pane)` does two things today: the
page's layout and Esc, and one list's click and focus return. It splits into `Attach(page, details, pane)` and
`AttachList(list, details)`, so `UpdatesPage` attaches both `UpdatesList` and the new `ExcludedList`. Focus goes back
to whichever row opened the pane. The Excluded list keeps `SelectionMode="None"` (it has no checkboxes: Update
selected and Update all leave it out, 010 R5.9) and gets item click from `AttachList`, as the other lists do.

No Core change for R1.7: excluded rows are already `PackageRowViewModel`s with their `PackageActions`.

## Amendment: installed scope, second copies, fresh installed state (R2.6, R2.7, R3.8, R3.9)

**What happened (VM, 2026-11-10, WinGet 1.29, Pandoc).** The upgrade from 3.10 ran the `Scope: machine` installer:
UAC, `C:\Program Files\Pandoc`, and WinGet uninstalled 3.10 first (`UpgradeBehavior: uninstallPrevious`). Going
back to 3.12 with `InstallPackageAsync` and `PackageInstallScope.Any` ran the `Scope: user` one. WinGet's log has
`Installer args: /quiet /norestart /log … ALLUSERS=2` and "2 matches were found for the package". The MSI log
installed into `C:\Users\tester\AppData\Local\Pandoc\`. Windows Installer's upgrade table only finds related
products in the same context (per-user or per-machine), so 3.12 didn't see 3.13. Uninstall entries afterwards:
`{34C3EAA3-…}` Pandoc 3.13 and `{671ADF5A-…}` Pandoc 3.12.

### R3.8, the installed scope

**Measured in T18 (2026-11-10, VM, WinGet 1.29.380, the T1 tool).** Pandoc 3.13 installed with `--scope machine`,
then with `--scope user`, a clean checkpoint each time:

| | All users | Current user |
|---|---|---|
| Location | `C:\Program Files\Pandoc\` | `C:\Users\tester\AppData\Local\Pandoc\` |
| WinGet `InstalledScope` | `System` | **`System`** (wrong) |
| Uninstall entry | HKLM | HKLM |
| Windows Installer, `MsiGetProductInfoEx` | (not read, see T24) | `UserUnmanaged` only (1605 for the others) |
| Going back to 3.12 with the matching scope | Ok, exit 0: MSI sets `PREVIOUSVERSIONSINSTALLED` to 3.13's code and replaces it. One entry: 3.12 | Same, per-user |

So WinGet's scope can't be trusted for this MSI: it seems to come from the uninstall entry's hive, and this
installer writes its entry under HKLM either way. Read as a per-user install, the first finding also explains itself:
WinGet took 3.10 for an all-users install, so the update ran the all-users installer, and uninstallPrevious removed
the per-user 3.10. With the right scope, Pandoc goes back directly; it doesn't refuse as T1's MSIs did.

**Where the scope comes from.** `WinGetPackageInstaller.InstallAsync` already has the `CatalogPackage`, found with
`localFirst: true` for anything but a first install. When going back (`GoesBack(kind, info, version)`):

1. **MSI products: Windows Installer.** For each product code of the installed version
   (`InstalledVersion.ProductCodes`, by index, W2), a new `IInstallContext` (`Core/Services`, P/Invoke to
   `msi.dll`) calls `MsiGetProductInfoEx(code, null, context, "InstallLocation")` for the machine context, then the
   per-user ones. The first context Windows Installer has the product in wins: machine → `System`, user (managed or
   unmanaged) → `User`. This is Windows' own record, not a guess (P1).
2. **Anything else, or no MSI answer: WinGet's `InstalledScope`** (`User` → `User`, `Machine` → `System`).
3. **Neither:** the setting (006 R2.1).

```csharp
// 014 R3.8: an explicit scope (the R3.5 chain), else going back asks for the installed scope; else the setting (006 R2.1).
internal static (InstallScope Scope, bool FromInstalled) ScopeFor(InstallScope? explicitScope, bool goingBack,
    InstallScope? msiContext, string? winGetScope, InstallScope setting) =>
    explicitScope is InstallScope.User or InstallScope.System ? (explicitScope.Value, true)
    : !goingBack ? (setting, false)
    : msiContext is InstallScope.User or InstallScope.System ? (msiContext.Value, true)
    : FromWinGetScope(winGetScope) is { } w ? (w, true) // "User" → User, "Machine" → System, anything else → null
    : (setting, false);
```

`InstallAsync` then sets `PackageInstallScope = ToWinGet(scope)`.

A read that throws, at either step, counts as unknown. The same read gives `PackageInfo.InstalledScope` for the
notice below. `IInstallContext` has a fake in the tests; the pure part is `ScopeFor` and the context mapping.

The uninstall then install chain (R3.5) keeps the same scope for its install, because after the uninstall nothing
knows it. *(Changed 2026-11-10 during T19: the scope can't be read before the uninstall from `PackageActions`, which
has no COM objects, A2.)* So the installer reports the scope it asked for:

- `OperationOutcome` gains `Scope` (`InstallScope`) and `ScopeFromInstalled` (bool: true when it came from Windows
  Installer or WinGet, false when it's the setting). `Finish` copies both onto the `PackageOperation`.
- When the failed going-back operation offers **Uninstall, then install**, `PackageActions` puts its `Scope` in the
  uninstall's options as `PreviousScope` when `ScopeFromInstalled`, and the chained install and **Install {previous
  version}** carry it on.
- `IPackageInstaller.RunAsync` gains `InstallScope? scope = null`: an explicit scope, which `ScopeFor` takes before
  anything else. The queue passes `operation.Options.PreviousScope` for an `AfterUninstall` install and for
  **Install {previous version}**, null otherwise.

The log adds `Scope: …` after "Chosen version": "for all users, as installed", "for the current user, as
installed", or "from Settings ({setting})". A run says which installer was asked for.

**The notice (Q7).** The confirmation is shown by `PackageActions` before anything is queued, so the installed scope
has to reach it as a plain value (A2). `PackageInfo` gains `InstalledScope` (`InstallScope?`: `User`, `System`, or
null when WinGet doesn't say), set by `ReadInstalledAsync` (R2.7's read, below) through the same steps as `ScopeFor`
(Windows Installer, then WinGet). A pure function adds one sentence to R3.1's message:

```csharp
// 014 R3.8, Q7: only when Settings names a scope and the installed one is the other.
internal static string? ScopeNotice(string name, string version, InstallScope setting, InstallScope? installed) =>
    setting == InstallScope.Any || installed is null || installed == setting ? null
    : installed == InstallScope.System
        ? $"{name} is installed for all users, so {version} will be installed for all users too, not for the current user as set in Settings."
        : $"{name} is installed for the current user, so {version} will be installed for the current user too, not for all users as set in Settings.";
```

It goes after the existing message, before the box. With "Let WinGet decide" nothing is added: no choice of the
user's is being overridden. If R2.7's read fails, there's no notice, and the installer still reads the scope itself
when it runs (it may then use it without having said so; the log's "Scope" line records it).

### R3.9, a second copy

Before the install, when going back, the installer counts the installed entries for the package ID:
`CatalogConnection.CountInstalledAsync(id)` searches `GetInstalledOnlyAsync()` with an exact ID filter and returns
the matches' `Count` (by index, W2). It counts again after a success. A pure function decides:

```csharp
internal static OperationOutcome AfterGoingBack(OperationOutcome outcome, int? before, int? after, bool previousStillInstalled,
    string name, string version, string? installed)
```

**Changed 2026-11-10 after T24's first run:** with Pandoc 3.12 per-user and 3.13 all-users both installed (two
uninstall entries, two folders, `winget list` shows both), WinGet's search by ID on the local-first catalog still
returned one match, so the count alone misses exactly this case. The installed-only catalog doesn't link them by
code either: 3.12's product code isn't in 3.13's manifest. Windows Installer knows, though. So for MSI products,
before the install, the installer keeps the installed version's product codes that Windows Installer has
(`IInstallContext`, as for R3.8). After a success, it drops the codes the chosen version's manifest also lists (a
product code that stays the same across versions is replaced in place), and asks Windows Installer about the rest:
any still installed means the old version wasn't replaced (`previousStillInstalled`). Non-MSI packages have no
codes and keep the count.

- `previousStillInstalled`, or `after > before` → `SucceededUnconfirmed` (no exclusion, R3.2 already skips it) with: "{name} {version} was
  installed next to {installed}, not over it. Both are now installed. To keep only {version}, uninstall {name}
  {installed} from the Installed page."
- Either count null (couldn't read), or not more → the outcome unchanged; 013's `AfterUpdate` runs as today (R2.5).

`SucceededUnconfirmed` is the closest existing result: the card is amber and says what WinGet did, and the
notification follows it. No new result kind.

### R2.6, rows and the pane after an operation

`PackageRowViewModel.Package` becomes settable from inside the class, with `Refresh(PackageInfo)` raising every
property that reads it (badge, texts, `Show*`). The row already watches its operation's `Result`; when it becomes a
success (`IsSuccess`), the row asks `PackageActions.ReadInstalledAsync(Package)` and refreshes itself with what comes
back. The call is a new `IPackageService.ReadInstalledAsync(PackageInfo)`: `FindByIdAsync(id, source, localFirst:
true)` after `Invalidate`, mapped as a search row is, or null when it fails (the row stays as it is: a failed read is
never an error of its own, 013 R2.3).

- The Search page's rows are the ones this fixes. Installed and Updates rows refresh too, and are then replaced by
  the store's reload (002 R1.3); the extra read is one `FindByIdAsync`.
- `PackageDetailsViewModel` watches `Row.Package`. When it changes, it reloads its details (`LoadAsync(Row)`), so
  the version actions are recomputed against the new installed version. The selection and **Show all versions**
  reset, as when another package opens.
- An open pane on the Installed or Updates page whose row was replaced by the store's reload keeps the old row
  object, which refreshed itself first, so it shows the same state.

### R2.7, check before going on

`PackageActions.InstallVersionAsync` starts, for an installed package, with `ReadInstalledAsync(package)`. If the
installed version it returns differs (ordinal) from `package.InstalledVersion`, it calls the row's `Refresh` (through
the row, which owns it; the pane passes the row) and returns without asking or queuing. A null read goes on as
today. With R2.6 in place this is rare: it covers changes made outside the app since the pane loaded.

### Components (amendment)

| Layer | Type | Change |
|-------|------|--------|
| Core/Services | `WinGetPackageInstaller` | `ScopeFor`; counts before and after when going back; `AfterGoingBack` |
| Core/Services | `IInstallContext`, `MsiInstallContext` | Windows Installer's context for a product code (`msi.dll`, R3.8) |
| Core/Services | `CatalogConnection` | `CountInstalledAsync(id)` |
| Core/Services | `IPackageService`, `WinGetPackageService` | `ReadInstalledAsync(PackageInfo)` |
| Core/Models | `OperationOptions` | `PreviousScope` for the R3.5 chain and R3.7 |
| Core/Models | `PackageInfo` | `InstalledScope`, set by `ReadInstalledAsync` (Q7's notice) |
| Core/ViewModels | `PackageActions` | `ScopeNotice` in the going-back confirmation |
| Core/Services | `OperationLogStore` | The "Scope" line |
| Core/ViewModels | `PackageRowViewModel` | `Refresh(PackageInfo)`, refresh after a success |
| Core/ViewModels | `PackageDetailsViewModel` | Reload when `Row.Package` changes |
| Core/ViewModels | `PackageActions` | `ReadInstalledAsync`; R2.7's check in `InstallVersionAsync` |

`IPackageInstaller.RunAsync` gains an optional explicit scope, only for the R3.5 chain and R3.7 (see above). `OperationOutcome` and `PackageOperation` gain `Scope` and `ScopeFromInstalled`.

## Testing

| Requirement | Test |
|-------------|------|
| R1.1, R1.2 | `WinGetPackageService` direction mapping as a pure function over `CompareResult` (unit) |
| R1.3, R1.5, R1.4 | `PackageDetailsViewModel`: shown count, Show all, action text and name per direction, no actions for one version, unmanaged, busy (unit) |
| R2.1 | `PackageOperation.Title` with and without a version; log line (unit) |
| R2.2 | `PackageActions` asks agreements with the chosen version (unit, fake installer records it) |
| R2.3 | Queue Retry passes the same version and acceptance (unit) |
| R2.4 | Existing row and Update all tests pass a null version (unit) |
| R2.5 | `AfterUpdate` cases for a chosen version over an installed one (unit) |
| R3.1, R3.2 | `PackageActions`: dialog shown for Older and Unknown, not for Newer; cancel queues nothing; exclusion added on success only, not on unconfirmed or failure (unit) |
| R3.3 | `MapInstallResult` arm: any `InstallError` while going back, after the in-use arm (unit) |
| R3.4, R3.5 | `PackageActions`: an uninstall is queued only after the R3.5 confirmation; Cancel queues nothing; the install is queued only after the uninstall succeeds, with the version and flags (unit) |
| R3.6 | A failed or cancelled uninstall queues no install (unit) |
| R3.7 | `AfterUninstallOutcome` wording; `CanInstallPrevious` only after an `AfterUninstall` failure; it queues the previous version without flags (unit) |
| All | VM: the T1 packages, through the app: install an old version, update to a middle one, go back, Retry after a refusal, exclusion appears in Settings; keyboard and Narrator names; light, dark, high contrast |
| R1.6 | `PackageDetailsViewModel`: only the selected item has `ShowAction`; it follows the selection, busy, and the installed version; `ActionText`/`ActionName` per direction (unit). VM: the button shows on the selected line in a 10-item list and after Show all |
| R1.7 | VM: click and Enter on an Excluded row open its details; Esc returns focus to that row; going back from there works |
| Live | Integration test: `GetDetailsAsync` on an installed package returns directions consistent with `IsUpdateAvailable` |
| R3.8 | `ScopeFor`: an MSI context wins over WinGet's scope; WinGet's `User`, `Machine`, `Unknown`, empty, null without one; not going back → the setting, for each setting (unit). `MsiInstallContext` against a real product: integration. `ScopeNotice`: each setting against each installed scope and null; the confirmation carries it (unit). VM: Pandoc 3.13 for all users, then for the current user: go back to 3.12 → the matching installer runs, one entry left (T18 measured this with the tool) |
| R3.9 | `AfterGoingBack`: more entries after → warning text and `SucceededUnconfirmed`; same count, fewer, or a null count → unchanged (unit). `PackageActions`: no exclusion after the warning (unit) |
| R2.6 | `PackageRowViewModel`: after a success, `Refresh` with the read package updates the badge and texts; a null read leaves the row; a failure reads nothing (unit, fake service). `PackageDetailsViewModel` reloads when `Row.Package` changes (unit). VM: update Pandoc from Search, the row and the pane show 3.13 |
| R2.7 | `PackageActions`: installed version read differs → no dialog, nothing queued, the row refreshed; same → as today; null → as today (unit) |

## Risks

- **Installers that go back badly.** An installer may report success and leave a mix of both versions. The app can't
  detect that beyond 013's check, and R3.1's warning is the mitigation.
- **Version strings with channels.** `PackageVersionId` also has a channel. The lookup matches on `Version` only. A
  source with two channels for one version would pick the first. No package in winget-pkgs uses channels today.
- **Big version lists.** Some packages have over 100 versions. The list shows 10 until asked (R1.3), and reading them is
  the same call as today.
- **The exclusion surprises later.** An excluded package keeps showing on the Updates page as excluded (010 R5.5) and can
  be included again there or in Settings, so the user can find it.
