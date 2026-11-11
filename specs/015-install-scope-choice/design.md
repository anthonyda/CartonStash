# 015 – Choose the install scope per install: Design

**Status:** Approved (2026-11-11)
**Requirements:** [requirements.md](requirements.md), approved 2026-11-10 with Q1 to Q3 as proposed

## Overview

Three pieces, all on what 014's amendment and 003's R8 already built:

- **R1, the choice.** WinGet says, per version, which installer it would pick for each scope. When it has a real
  per-user installer *and* a real all-users one, the details pane's Install becomes a split button with the two
  scopes in its menu. The chosen scope travels on the operation as an explicit scope, the way 014 carries the
  installed scope across "Uninstall, then install".
- **R2, the label.** The details pane reads how the package is installed with 014's `InstalledScopes.Read`
  (Windows Installer first, then WinGet) and shows it under the installed version.
- **R3, updates.** `WinGetPackageInstaller` already asks for the installed scope when going back (014 R3.8). Updates
  get the same rule, and a result that overrode a scope chosen in Settings says so.

## Measured first (2026-11-11, on the host, read-only)

The T1 tool, extended with `applicable <id>`: `PackageVersionInfo.GetApplicableInstaller(InstallOptions)` with each
`PackageInstallScope`, for the default version. It only reads the catalog; nothing is installed.

| Package | Asked `User` → gets | Asked `System` → gets | Two real scopes? |
|---------|---------------------|-----------------------|------------------|
| `JohnMacFarlane.Pandoc` 3.13 | Wix, scope User | Wix, scope System | **yes** |
| `Git.Git` 2.56.0.4 | Inno, User | Inno, System | **yes** |
| `Microsoft.VisualStudioCode` 1.145.2 | Inno, User | Inno, System | **yes** |
| `7zip.7zip` 26.06 | none (`HasApplicableInstaller` false) | Wix, System | no |
| `jqlang.jq` 1.8.2 | Portable, scope Unknown | the same | no |
| `Microsoft.PowerShell` 7.7.0.0 | Msix, scope Unknown | the same | no |
| `Notepad++.Notepad++` 8.9.9 | **Zip/portable**, scope Unknown | Wix, System | no |

- Asking for `User` also accepts an installer with no scope (jq, PowerShell's MSIX, Notepad++'s zip). So "WinGet
  returned something for User" isn't enough: the returned installer's own `Scope` must be `User`.
- Notepad++ shows why: its "current user" answer is a different kind of install (a portable zip), not the same
  program for one user. Offering it as "Install for the current user" would mislead.
- `Any` picks the per-user installer for Pandoc, Git and VS Code: what made 014's second copy possible.

**The rule (R1.2):** the choice is offered when `GetApplicableInstaller(User).Scope == User` and
`GetApplicableInstaller(System).Scope == System`. Anything else, including a read that throws, is "one scope".

## Components

| Layer | Type | Change |
|-------|------|--------|
| Core/Services | `WinGetPackageService` | `OffersScopeChoice(PackageVersionInfo)` (the rule); `GetDetailsAsync` sets `PackageDetails.OffersScopeChoice` for the default version and `InstalledScope` (R2); `OffersScopeChoiceAsync(package, version)` for a version from the list |
| Core/Services | `IPackageService` | `OffersScopeChoiceAsync(PackageInfo, string version)` |
| Core/Models | `PackageDetails` | `OffersScopeChoice` (bool), `InstalledScope` (`InstallScope?`) |
| Core/Models | `OperationOptions` | `Scope` (`InstallScope?`): the scope the user chose (R1.5) |
| Core/Models | `OperationOutcome`, `PackageOperation` | `ScopeFromInstalled` (bool) becomes `ScopeSource` (`Setting`, `Installed`, `Chosen`) |
| Core/Models | `PackageOperation` | `Title` names a chosen scope (R1.5) |
| Core/Services | `PackageOperationQueue` | passes `Options.Scope ?? Options.PreviousScope` as the explicit scope |
| Core/Services | `WinGetPackageInstaller` | `ScopeFor` gains the chosen scope and updates (R3.1); `UpdatedScopeNote` (R3.2) |
| Core/Services | `OperationLogStore` | the "Scope" line reads `ScopeSource` |
| Core/ViewModels | `PackageActions` | `InstallAsync(package, InstallScope? scope)`; `InstallVersionAsync(..., scope)` |
| Core/ViewModels | `PackageDetailsViewModel` | `OffersScopeChoice`, `InstalledScopeText`, `HasInstalledScope`; the selected version's choice; install commands per scope |
| Core/ViewModels | `VersionItem` | `OffersScopeChoice` for its line (R1.3) |
| App/Controls | `PackageActionsView` | `OffersScopeChoice` dependency property: when true, Install is a `SplitButton` (R1.1, Q3) |
| App/Controls | `PackageDetailsPane.xaml` | sets it; the "Installed for …" line (R2.1); the version line's split button (R1.3) |

## R1: choosing the scope

### Which scopes (R1.2)

```csharp
// 015 R1.2: two real scopes, as WinGet itself would pick them. User also accepts scope-less installers (portable,
// MSIX), so the returned installer's own scope is what counts.
internal static bool OffersScopeChoice(Func<PackageInstallScope, PackageInstallerScope?> pick) =>
    pick(PackageInstallScope.User) == PackageInstallerScope.User
    && pick(PackageInstallScope.System) == PackageInstallerScope.System;
```

`pick` wraps `info.GetApplicableInstaller(new InstallOptions { PackageInstallScope = s })?.Scope`, null when it
throws. For the default version it runs inside `GetDetailsAsync`, already off the UI thread. For a version selected
in the list (R1.3), the pane asks `OffersScopeChoiceAsync(package, version)` when the selection changes, only for
`NotInstalled` items; the answer is cached per version while the pane shows that package.

### The control (R1.1, R1.3, R1.6, Q3)

`PackageActionsView` gets `OffersScopeChoice` (a plain bool DP; the x:Bind gotcha). Rows leave it false (R1.7). The
details pane binds it to `ViewModel.OffersScopeChoice`, true only when the package isn't installed and the default
version offers both scopes. When it's true and Install would show, the view shows a `SplitButton` instead:

- Primary part: **Install**, as today (the setting, R1.1). Accessible name "Install {name}".
- Flyout: **Install for all users** and **Install for the current user**, accessible names "Install {name} for all
  users" and "Install {name} for the current user" (R1.6). "For all users" has a second line in the menu item: "Windows
  may ask for permission" (R1.4).
- The version line (014 R1.6) does the same for an `Install` action whose version offers both scopes.

Keyboard: the split button's own behaviour (Enter is Install, Alt+Down opens the menu).

### Running it (R1.4, R1.5)

- `PackageActions.InstallAsync(package, scope)` and `InstallVersionAsync(package, version, refresh, scope)` put the
  scope in `OperationOptions.Scope`. Agreements are asked as today.
- R1.4 is met by the menu item's second line and by 003 R8.3's line on the card, which the installer already shows
  for an all-users MSI. For other installer types an all-users install elevates itself (Inno: `ElevatesSelf` above),
  and the card's line is shown too: the installer reports `MayAskPermission` whenever the chosen scope is `System`.
- The queue passes `Options.Scope ?? Options.PreviousScope` to `RunAsync` (014's explicit scope). `ScopeFor` puts an
  explicit scope first, so the chosen one wins; `ScopeSource` is `Chosen` for it.
- `Title`: "Install Pandoc for all users", "Install Pandoc 3.12 for the current user". Retry keeps the options, so
  the scope (R1.5). The setting isn't touched.
- Log: `Scope: for all users, chosen` / `for the current user, chosen`.

## R2: how it's installed

`GetDetailsAsync` sets `PackageDetails.InstalledScope` with `InstalledScopes.Read(found.InstalledVersion, context)`
when the package is installed. The pane shows, under the installed version badge:

- `System` → "Installed for all users"; `User` → "Installed for the current user"; null → nothing (R2.2).

A refreshed row (014 R2.6) reloads the details, so the line follows an update or a go-back.

## R3: updates keep the installed scope

`ScopeFor` today: explicit scope → it; going back → the installed scope; else the setting. It becomes:

```csharp
internal static (InstallScope Scope, ScopeSource Source) ScopeFor(InstallScope? chosen, InstallScope? carried,
    bool keepsInstalled, InstallScope? msiContext, string? winGetScope, InstallScope setting)
// chosen (R1.5) → Chosen; carried (014 R3.5) → Installed; keepsInstalled (going back, or an update, R3.1)
// → Windows Installer, then WinGet → Installed; else the setting → Setting.
```

`keepsInstalled` is `GoesBack(...) || kind == OperationKind.Upgrade`.

*Added 2026-11-11 during T3:* an update only keeps the installed scope when the version it installs has an
installer for it (`GetApplicableInstaller` with that scope returns one whose `Scope` matches, as in R1.2, or a
scope-less one). Otherwise it falls back to the setting, as today. Without this, a package whose new version
dropped, say, its per-user installer would fail with "No installer for this PC with the selected install scope"
where it updates today. For Pandoc's per-user 3.10 that WinGet reads as
all users (014 T18), Windows Installer's `UserUnmanaged` now keeps the update per-user: no UAC prompt, no move to
Program Files.

### Saying so (R3.2, Q2)

```csharp
// 015 R3.2: an update that used the installed scope against a scope chosen in Settings says so. Nothing for "Let
// WinGet decide", or when they match.
internal static string? UpdatedScopeNote(string name, InstallScope setting, InstallScope used, ScopeSource source)
```

When it returns text, a successful update's message becomes "Updated for the current user, as Pandoc is installed."
(or "…for all users…"); a failure's message is unchanged, and the log's "Scope" line says "as installed" either way.

## Testing

| Requirement | Test |
|-------------|------|
| R1.2 | `OffersScopeChoice` against the measured answers: Pandoc, Git → true; 7-Zip, jq, PowerShell MSIX, Notepad++ → false; a throwing pick → false (unit) |
| R1.1, R1.6, R1.7 | `PackageDetailsViewModel.OffersScopeChoice` only when not installed and the details say so; rows never (unit). App: the split button's names (VM, UIA) |
| R1.3 | A `NotInstalled` version's line asks `OffersScopeChoiceAsync` once and caches it (unit) |
| R1.4 | The installer reports `MayAskPermission` for a chosen `System` (unit) |
| R1.5 | `PackageActions` puts the scope in the options; the queue passes it; `Title`; Retry keeps it; the setting unchanged (unit) |
| R2.1, R2.2 | `InstalledScopeText` for `System`, `User`, null (unit); VM: Pandoc per-user and all users |
| R3.1 | `ScopeFor`: an update keeps the MSI context / WinGet scope; chosen and carried come first; not going back nor updating → the setting (unit) |
| R3.2 | `UpdatedScopeNote` per setting × used scope (unit); the success message carries it |
| Live | Integration: `OffersScopeChoiceAsync` for Pandoc true, 7-Zip false |
| All | VM: Pandoc not installed → split button → **Install for the current user** → `AppData`, no UAC, "Installed for the current user"; **Install for all users** on a clean VM → UAC, Program Files, "Installed for all users"; per-user Pandoc 3.12 updated → stays per-user, no UAC; with Settings on "All users", the result says "Updated for the current user, as Pandoc is installed." |

## Risks and alternatives

- **Two catalog reads per pane open.** `GetApplicableInstaller` twice for the default version. They're local reads of
  the manifest already fetched for the details; no network.
- **`ScopeFromInstalled` → `ScopeSource`.** A rename across 014's code and tests; mechanical, in its own task.
- **R3.1 changes WinGet's own choice for updates.** It's what WinGet's CLI would do with `--scope` set to the
  installed scope. Where nothing knows the scope, behaviour is today's.
- **Alternative for R1.2:** reading the manifest's installers list. Rejected: the COM API doesn't expose it, and
  `GetApplicableInstaller` also applies the PC's architecture and OS, which the list wouldn't.
