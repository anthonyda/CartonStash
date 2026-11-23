# 020 – Uninstall, then update: Design

**Status:** Approved (2026-11-23)
**Requirements:** [requirements.md](requirements.md)
**UI proposal:** https://claude.ai/artifact/ETzZwt1k6qDum2Y8AJsggq (approved with the requirements, 2026-11-23)

## Overview

Both actions are follow-ups on a failed update card, like 014's **Uninstall, then install** and 017's **Retry as
administrator**: the result's mapping sets a flag, the card and the row show a button, the press goes through
`PackageActions.FollowUp`, which asks for confirmation and queues ordinary operations.

- **Uninstall, then update (R1 to R3)** is 014's chain with an update's target. The uninstall carries `ThenInstall`; on
  its success `PackageActions` queues the install with `AfterUninstall` and a new `AsUpdate` flag, so the card, the
  message and 013 R2's check read as an update.
- **Install over it (R5)** queues the same update again with a new `InstallOver` flag, which makes
  `WinGetPackageInstaller` call `InstallPackageAsync` with the target version instead of `UpgradePackageAsync`, the
  path 015 R3.1 already uses (`byInstalling`). Whether that skips the old uninstaller is T1's measurement (R5.4, Q7).

Nothing new talks to WinGet: both reuse `IPackageInstaller.RunAsync`.

## Measured so far (maintainer's PC, logs only, 2026-11-23)

| Package | Manifest | Result |
|---------|----------|--------|
| `Anaconda.Miniconda3` | Refuses the update in place | `CatalogError`, exit code 0, `0x8A150114` |
| `ElectronicArts.EADesktop` 13.832.0.6345 | `InstallerType: burn`, `Scope: machine`, `UpgradeBehavior: uninstallPrevious` (winget-pkgs, read 2026-11-23) | `InstallError`, exit code 0, `0x800401F5` from `ShellExecuteInstallerHandler.cpp(353)`: WinGet's `uninstallPrevious` step can't start `EAUninstall.exe` |

So EA app's failure is WinGet's `uninstallPrevious`, which runs in WinGet's *upgrade* flow.

**T1 (VM, 2026-11-23):** an *install* of the target version on the installed package (`InstallPackageAsync` with
`PackageVersionId`, scope System, no `Force`) runs EA's new installer directly, with no uninstall step; the update
(`UpgradePackageAsync`) fails with `0x800401F5` as on the host. `InstallOverSupported` is true. The VM's EA app is
the newest client underneath (EA's thin installer), so there the installer had nothing to do, and the result was `Ok`
with WinGet still listing 13.813.1.6327: the app shows 013 R2.2's "not confirmed" warning in that case, which is
honest. On the host the old bundle is real (`BundleVersion` 13.813.1.6327, its cached installer present); Burn
upgrades a related bundle through that cached copy, so Install over it should update it there. Not measured.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `OperationOutcome` | `OffersUninstallFirst` (exists, 014) now also set for R1.2; new `OffersInstallOver` (R5.1) | Changed |
| Core/Models | `OperationOptions` | `AsUpdate`: an install that stands for an update (R3.3); `InstallOver`: an update run as an install of the target version (R5.3) | Changed |
| Core/Models | `OperationFollowUp` | `UninstallThenUpdate`, `InstallOver` | Changed |
| Core/Models | `PackageOperation` | `CanUninstallThenUpdate`, `CanInstallOver` and their commands, texts and accessible names (R1.1, R5.1, R4.3); `KindText` "Update" for `AsUpdate`; `ThenText` under the progress of a chained uninstall (UI 3) | Changed |
| Core/Services | `WinGetPackageInstaller.MapInstallResult` | R1.2/R1.3: the 0x8A150114 arm sets `OffersUninstallFirst` and ends with "Uninstall, then update does both."; R5.1/R5.2: on an update, `AppNotFound`/`FileNotFound` with exit code 0 sets `OffersInstallOver` and the R5.2 message, only while `InstallOverSupported` is true (R5.4) | Changed |
| Core/Services | `WinGetPackageInstaller.InstallAsync` | `InstallOver`: `byInstalling = true` with the target version's ID. `AsUpdate`: maps the result with `OperationKind.Upgrade` ("Updated to {version}.", 013 R2.1 check) | Changed |
| Core/Services | `PackageOperationQueue` | Copies `OffersInstallOver`; `AfterUninstallOutcome` unchanged (R3.2 wording already fits); a failed chained uninstall adds R3.1's sentence | Changed |
| Core/ViewModels | `PackageActions.FollowUp` | Two new cases: confirmation, then queue (R2.1, R2.2, R5.3). `Completed` handler: `ThenInstall` on an uninstall of an update chain queues the install with `AsUpdate` | Changed |
| Core/ViewModels | `PackageRowViewModel` | `ShowUninstallThenUpdate` (replaces Update, UI 1), `ShowInstallOver` (next to Update, UI 4) | Changed |
| App/Controls | `OperationCard.xaml`, `PackageActionsView.xaml` | The two buttons, accent style, as Retry as administrator; `ThenText` caption | Changed |

## Data flow

### Uninstall, then update

```
Update fails: 0x8A150114 → Failed, not retryable, OffersUninstallFirst, message R1.3
  → card / row: Uninstall, then update (CanUninstallThenUpdate: Kind Upgrade, Failed, OffersUninstallFirst, !GoingBack)
  → FollowUp(UninstallThenUpdate): R2.1 confirmation (UninstallThenUpdateQuestion)
       Cancel → nothing (P2)
       Confirm → Enqueue(Uninstall, package, options {
           ThenInstall = target, PreviousVersion = InstalledVersion,
           PreviousScope = ScopeToKeep(operation),            // R2.3, 014 R3.8
           AsUpdate = true })
  → uninstall card: "Uninstalling…", ThenText "Then installs {target} {for the current user / for all users}."
       failed / cancelled → R3.1: uninstall result + "{name} {installed} is still installed; nothing was updated."
       succeeded → Completed handler: Enqueue(Install, package with InstalledVersion = null, options {
           Version = target, AfterUninstall = true, PreviousVersion, PreviousScope, AsUpdate = true })
  → install card, KindText "Update"
       succeeded → "Updated to {target}." + 013 R2 check (R3.3); Updates reloads, the row leaves
       failed / cancelled → AfterUninstallOutcome (014 R3.7): "no longer installed…", Install {previous} (R3.2)
```

**Target version (Q2).** `operation.Version ?? operation.Package.AvailableVersion` of the failed update, fixed when the
user confirms. If WinGet no longer has it, the install fails with 014's "version unavailable" result and R3.2 applies.

**Rights (R2.4).** The chain's operations have `Elevated = false`. If the failed update was itself an elevated retry
(017), the chain still starts unelevated; any step that then fails for rights offers 017's button on its own card.

### Install over it

```
Update fails: InstallError, exit 0, 0x800401F5 or 0x80070002 → Failed, retryable, OffersInstallOver, message R5.2
  → card: Log, Retry, Install over it; row: Install over it, Update, Exclude
  → FollowUp(InstallOver): R5.3 confirmation (InstallOverQuestion)
       Confirm → Enqueue(Upgrade, package, options { Version = target, InstallOver = true })
  → InstallAsync: scope from the installed one (keepsInstalled, as any update), byInstalling = true,
    PackageVersionId = target → InstallPackageAsync
  → result mapped as an update (013): success "Updated to {target}.", or the installer's failure
```

`CanInstallOver` is false on a run that already had `InstallOver`, so a failed install over it offers Retry only.

### Never automatic (R4.1)

Update all and the background runner only call `Enqueue(Upgrade, …)` with default options; the follow-ups are reached
only from `FollowUpRequested`, raised by the card and row commands. A unit test checks that a failed Update all leaves
nothing queued.

## WinGet API usage

No new calls. `UninstallPackageAsync` (003), `InstallPackageAsync` with `PackageVersionId` and `PackageInstallScope`
(014, 015 R3.1), `UpgradePackageAsync` (004). Collections by index (W2), statuses checked (W3).

`Force` stays off (014 T1: it never helped and lets other warnings through; 020 T1: not needed).

## UI

As the approved proposal, with one change found while writing this design: the proposal's "Waiting for the uninstall"
update card doesn't exist, because 014's chain queues the install only once the uninstall succeeded (so a failed
uninstall leaves no orphan card). The uninstall card says what comes next instead, with `ThenText` under its progress
bar: "Then installs py314_26.9.4-1 for the current user." The proposal is updated to match.

| Element | Text | Accessible name |
|---------|------|-----------------|
| Card / row button (R1.1) | Uninstall, then update | Uninstall {name}, then update to {version} |
| Confirmation (R2.1) | Title "Uninstall {name}, then update to {version}?"; "{name} {installed} will be uninstalled, then {version} installed {scope}, as it is now." / "Settings and data that {name} keeps may be lost. Apps that come as an MSIX package always lose them."; buttons "Uninstall, then update", "Cancel" | dialog title |
| Card / row button (R5.1) | Install over it | Install {name} {version} over the installed one |
| Confirmation (R5.3) | Title "Install {name} {version} over the installed one?"; "{version} will be installed {scope}, as {name} is now. Nothing is uninstalled first: the installed version's uninstaller is missing." / "If {name} is running, close it first."; buttons "Install over it", "Cancel" | dialog title |

On the row, **Uninstall, then update** takes the Update button's place (Update would fail the same way, 013 R3.1);
**Install over it** goes before Update, which stays as Retry. Both are accent buttons, Tab order left to right (Q5 A).
Theme brushes only, so dark and high contrast follow the card's other buttons.

## Error handling and edge cases

- **Package busy.** `FollowUp` returns when the package has an active operation (003 R4.2), as today.
- **Row is not WinGet's own match for a shared ID (002 R6.3).** The uninstall takes the row's own entry (002 R6.5), as
  014's chain does.
- **User cancels the uninstaller's own prompt** (018 R2): the uninstall is Cancelled, R3.1 applies, nothing installed.
- **Uninstall succeeds, install's agreements.** The chain carries the failed update's `AcceptAgreements`; the update
  already had them accepted (004 R7).
- **MSIX packages.** The confirmation's MSIX sentence stays, as in 014.
- **App restarts mid-chain.** The queue isn't persisted (003): after an uninstall the install isn't queued again. Same
  as 014; the Installed page then shows the package missing and Search can install it.

## Testing strategy

| Requirement | Verification |
|-------------|--------------|
| R1.1, R1.2, R1.3 | `MapInstallResult`: 0x8A150114 on an update (CatalogError, InstallError) → message ends "Uninstall, then update does both.", `OffersUninstallFirst`, not retryable; on an install → not offered (unit). `CanUninstallThenUpdate` (unit) |
| R2.1, R2.2, R2.3 | `PackageActions`: confirmation text; Cancel queues nothing; Confirm queues one uninstall with `ThenInstall` = target, `PreviousScope` from the installed scope, `AsUpdate`; the install only after it succeeded (fake queue, unit) |
| R2.4 | The chain's options have `Elevated = false`, also after an elevated failed update (unit) |
| R3.1 | Failed and cancelled chained uninstall: no install queued, R3.1 sentence (unit) |
| R3.2 | `AfterUninstallOutcome` with `AsUpdate`; `CanInstallPrevious` (unit, reuses 014's tests) |
| R3.3 | `AsUpdate` install: KindText "Update", "Updated to {version}.", 013 R2 check runs (unit) |
| R4.1 | Update all with a 0x8A150114 failure queues nothing more (unit) |
| R4.2, R4.3 | Logs of both operations; buttons by Tab, Narrator names, dark and high contrast (VM, manual) |
| R5.1, R5.2 | `MapInstallResult`: 0x800401F5 / 0x80070002 with exit 0 on an update → R5.2 message, `OffersInstallOver`; on an install or uninstall → 013 R1.2 unchanged (unit) |
| R5.3 | `InstallOver` reaches `InstallPackageAsync` with the target's ID and the installed scope (fake connection, unit) |
| R5.4 | T1 (VM) |
| Whole chain | VM: Miniconda3 `py312_24.11.1-0`, update → fails, Uninstall, then update → updated, row leaves Updates. EA app (013 recipe: ARP version set back, `EAUninstall.exe` deleted), update → fails, Install over it → updated |

## Risks and alternatives considered

- **T1 may show WinGet's install also runs `uninstallPrevious`** for EA app. Then R5 is dropped (R5.4), 013 R1.2 stays,
  and `InstallOverSupported` stays false. Running EA's installer outside WinGet was rejected (P1: WinGet is the only
  gateway, A2).
- **One "Uninstall and reinstall" for both cases** was rejected: for EA app the uninstall is what fails.
- **Offering the chain after any failed update** was rejected in the requirements (out of scope): an uninstall can
  remove user data where it can't help.
- **Persisting the chain across restarts**: not done, consistent with 014; the risk is a package left uninstalled if the
  app closes between the two steps. The window's close confirmation for pending work (`Shell.HasPendingWork`) covers the running uninstall,
  and the install is queued the moment the uninstall ends.
