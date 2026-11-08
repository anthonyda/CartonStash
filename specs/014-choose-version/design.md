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
the COM API. T1 measures it in the test VM before anything else is built, and the "Going back" section below lists
what T1 can change.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `PackageVersion`, `VersionDirection` | One available version and how it compares with the installed one (R1.1, R1.2) | New |
| Core/Models | `PackageDetails` | `Versions` becomes `IReadOnlyList<PackageVersion>` | Changed |
| Core/Models | `PackageOperation` | `Version` (null = default), `ExcludeOnSuccess`; `Title` names the version (R2.1) | Changed |
| Core/Services | `WinGetPackageService` | `ReadVersions` adds the direction from WinGet's comparer | Changed |
| Core/Services | `IPackageInstaller`, `WinGetPackageInstaller` | `version` parameter on `GetAgreementsAsync` and `RunAsync`; `PackageVersionId`; 013's check after going back; R3.3 message | Changed |
| Core/Services | `PackageOperationQueue` | `Enqueue(..., version, excludeOnSuccess)`; passes the version to the installer | Changed |
| Core/Services | `OperationLogStore` | A "Chosen version" line in the log (R2.1) | Changed |
| Core/Services | `IDialogService` | `ConfirmWithOptionAsync` for the going-back confirmation and its box (R3.1, R3.2) | Changed |
| Core/ViewModels | `PackageActions` | `InstallVersionAsync(package, version)`; adds the exclusion after success (R3.2); takes `ISettings` | Changed |
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
| Older (R3) | `InstallPackageAsync` or `UpgradePackageAsync`, `Force`? | Decided by T1, see below |

The COM objects never leave the services (A2): the version crosses the boundary as a string and is looked up again
when the operation runs, the same way the package itself is found again today.

## Going back (R3)

**T1 (test VM, before T2).** For each installer type below, install the latest version, then try an older one through a
small console tool on the COM API: `InstallPackageAsync` and `UpgradePackageAsync`, each without and with `Force`.
Record WinGet's status, extended error, installer exit code, and what Windows lists afterwards.

| Type | Candidate package | Why |
|------|-------------------|-----|
| MSI | `7zip.7zip` (MSI installer) | MSI refuses a downgrade by default unless the product allows it |
| NSIS | `Notepad++.Notepad++` | Usually installs over anything |
| Inno | `Git.Git` | Has its own downgrade prompt, silent mode skips it |
| MSIX | `Microsoft.WindowsTerminal` | Windows refuses an older MSIX unless it's removed first |
| Portable | `jqlang.jq` | WinGet manages the files itself |

**What T1 decides, written into this design before T2 starts:**

- **The call.** The working assumption is `InstallPackageAsync` with the version: the CLI's
  `winget install --version` takes this path, and `UpgradePackageAsync` refuses anything that isn't newer.
- **`Force` (Q3).** Set it only if T1 shows some packages go back with it and not without it, and only after this
  design names what else `Force` lets through. Otherwise it stays off.
- **The refusal codes for R3.3.** The extended errors and statuses WinGet returns when an installer won't go back.
  WinGet's own names suggest `APPINSTALLER_CLI_ERROR_INSTALL_DOWNGRADE` and
  `APPINSTALLER_CLI_ERROR_INSTALL_ALREADY_INSTALLED`, but the values are taken from T1, not assumed.

The R3.3 message is a new arm in `MapInstallResult`, ahead of the generic error arm and after the in-use arm (003 R7
still wins), only for an operation going back:

> {name} couldn't go back to {version}: its installer doesn't install over a newer version. To go back, uninstall
> {name}, then install {version} from its details.

It's not retryable (013 R3.1), because Retry would fail the same way.

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
            kind: NotInstalled → Install; Newer → Upgrade; Older / Unknown → per T1
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
| R3.3 | `MapInstallResult` arm with T1's codes (unit) |
| R3.4 | No uninstall is queued anywhere in the flow (unit); review |
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
