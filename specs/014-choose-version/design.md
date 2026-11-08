# 014 – Install a chosen version: Design

**Status:** Approved (2026-11-08)
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
| Core/ViewModels | `PackageDetailsViewModel` | The version list, the selected version, its action and `Show all versions` (R1) | Changed |
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
  → PackageActions.InstallVersionAsync(row.Package, version)
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
| Live | Integration test: `GetDetailsAsync` on an installed package returns directions consistent with `IsUpdateAvailable` |

## Risks

- **Installers that go back badly.** An installer may report success and leave a mix of both versions. The app can't
  detect that beyond 013's check, and R3.1's warning is the mitigation.
- **Version strings with channels.** `PackageVersionId` also has a channel. The lookup matches on `Version` only. A
  source with two channels for one version would pick the first. No package in winget-pkgs uses channels today.
- **Big version lists.** Some packages have over 100 versions. The list shows 10 until asked (R1.3), and reading them is
  the same call as today.
- **The exclusion surprises later.** An excluded package keeps showing on the Updates page as excluded (010 R5.5) and can
  be included again there or in Settings, so the user can find it.
