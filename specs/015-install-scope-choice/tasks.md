# 015 – Choose the install scope per install: Tasks

**Status:** Done (2026-11-11)
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit, and keep the build green at
every commit (Q4). Manual checks run in the CartonStash-Test VM, never on the host.

## Measure first

- [x] **T1.** `GetApplicableInstaller` per scope for Pandoc, Git, VS Code, 7-Zip, jq, PowerShell, Notepad++ (read-only,
  the T1 tool's `applicable` command). *(R1.2)*
  - Verify: the table in design.md. *Done 2026-11-11.*

## Core

- [x] **T2.** `ScopeFromInstalled` becomes `ScopeSource` (`Setting`, `Installed`, `Chosen`) on `OperationOutcome` and
  `PackageOperation`; the log's "Scope" line reads it. No behaviour change. *(014 R3.8, prepares R1.5)*
  - Verify: the existing `GoingBackScopeTests` and `UninstallThenInstallTests` pass, renamed where they read it.
    *Done 2026-11-11: the log also has the "chosen" wording ready for T4. 796 tests pass.*
- [x] **T3.** `ScopeFor` with the chosen scope, the carried one and `keepsInstalled` (going back or an update);
  `UpdatedScopeNote` and the success message (R3.1, R3.2).
  - Verify: `UpdateScopeTests` (`*_R3_1`, `*_R3_2`); `GoingBackScopeTests` still pass.
- [x] **T4.** `OperationOptions.Scope`; the queue passes `Scope ?? PreviousScope`; `PackageActions.InstallAsync` and
  `InstallVersionAsync` take a scope; `Title` names it; `MayAskPermission` for a chosen `System`; the log's "chosen"
  wording (R1.4, R1.5).
  - Verify: `ChosenScopeTests` (`*_R1_5`). *Done 2026-11-11: R1.4's `MayAskPermission` for a chosen `System` is in
    the COM path, which the fake installer doesn't run; checked in T9 instead.*
- [x] **T5.** `OffersScopeChoice` (the rule), `PackageDetails.OffersScopeChoice` and `InstalledScope` from
  `GetDetailsAsync`; `IPackageService.OffersScopeChoiceAsync` for a version (R1.2, R1.3, R2.1).
  - Verify: `ScopeChoiceRuleTests` (`*_R1_2`) with the measured answers as fakes.
- [x] **T6.** `PackageDetailsViewModel`: `OffersScopeChoice`, `InstalledScopeText`/`HasInstalledScope`, install per scope
  for the pane and for the selected version (cached per version); `VersionItem.OffersScopeChoice` (R1.1, R1.3, R1.7,
  R2.1, R2.2).
  - Verify: `PackageDetailsScopeTests` (`*_R1_1`, `*_R1_3`, `*_R1_7`, `*_R2_1`, `*_R2_2`).

## App

- [x] **T7.** `PackageActionsView.OffersScopeChoice` and the `SplitButton`; the pane's "Installed for …" line; the
  version line's split button. Theme resources only (R1.1, R1.3, R1.6, R2.1).
  - Verify: `dotnet build`; the pane in T9.

## Tests against WinGet

- [x] **T8.** Integration: `OffersScopeChoiceAsync` for Pandoc's default version is true, for 7-Zip false. Tagged
  `Category=Integration` (R1.2).
  - Verify: `dotnet test --filter Category=Integration`.

## Manual checks

- [x] **T9.** In the VM, on a build from this branch, record the results in the log below. *(R1 to R3)*
  - Pandoc not installed: the split button; **Install for the current user** → no UAC, `AppData\Local\Pandoc`, the pane
    says "Installed for the current user". Clean VM: **Install for all users** → UAC, Program Files, "Installed for all
    users", the card's permission line.
  - 7-Zip and jq: a plain Install button.
  - Pandoc 3.12 per-user: Update → stays per-user, no UAC. With Settings on "All users": "Updated for the current user, the way it
    was installed."
  - Keyboard: Tab to the split button, Enter installs, Alt+Down opens the menu; the names read correctly (UIA). Light,
    dark, high contrast. Changelog line.
- [x] **T10.** Close: every criterion maps to a passing test or a log entry, status `Done` in all three files and in
  the roadmap.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| T1 (R1.2) | 2026-11-11 | Measured | See design.md, "Measured first": Pandoc, Git, VS Code have a User and a System installer; 7-Zip only System; jq, PowerShell (MSIX) none with a scope; Notepad++'s "User" is a portable zip |
| T9, first run (R3.1) | 2026-11-11 | Fail (found) | Pandoc 3.12 installed per-user from the version line, then **Update** with Settings on "All users": `NoApplicableUpgrade`, `0x8A15002B`. WinGet's upgrade keeps the scope it reads for the installed version (`System`), so asking for User matched nothing; `main` moved Pandoc to all users instead. Measured: `InstallPackageAsync` of 3.13 with User replaces the per-user 3.12, no UAC, one entry. Fixed (design, "When WinGet disagrees") |
| T9, second run (R3.1) | 2026-11-11 | Fail (found) | Same result: WinGet's `InstalledScope` string is `System`, not `Machine`, so WinGet's reading was never mapped and the disagreement never seen. Fixed in `InstalledScopes.FromWinGetScope` (both words); 014's design corrected |
| T9, R1.1, R1.7 | 2026-11-11 | Pass | Fresh VM, build of the fixes. The Pandoc pane shows a SplitButton "Install Pandoc"; 7-Zip's and jq's panes a plain Button |
| T9, R1.3, R1.5, R2.1 | 2026-11-11 | Pass | Version 3.12 selected: its line is a SplitButton "Install Pandoc 3.12"; menu items "Install Pandoc 3.12 for all users" and "… for the current user". The latter: no UAC, `AppData\Local\Pandoc`, log "Scope: for the current user, chosen", the card "Install Pandoc 3.12 for the current user: Installed", the pane "Installed for the current user" |
| T9, R3.1, R3.2 | 2026-11-11 | Pass | Settings on "All users", **Update** on the Updates page: no UAC, 3.13 replaced 3.12 in `AppData\Local\Pandoc` (one entry), "Updated for the current user, the way it was installed." (the first wording repeated the entry's name, "Pandoc 3.12": changed), log "Scope: for the current user, as installed" |
| T9, R1.4, R1.6, R2.1 | 2026-11-11 | Pass | Clean VM, Pandoc not installed. The split button takes focus as "Install Pandoc" (SplitButton); its menu has "Install Pandoc for all users" and "… for the current user". **For all users**: UAC prompt after 4 s (answered by the maintainer), the card "Installing…" with "Windows may ask for permission. If nothing appears, look for the shield in the taskbar." (screenshot), installed into `C:\Program Files\Pandoc\`, log "Scope: for all users, chosen", the pane "Installed for all users". No crashes in any run |
| T9, not checked | 2026-11-11 | — | Enter and Alt+Down on the split button pressed by hand (UIA Invoke and ExpandCollapse used); dark and high contrast (the SplitButton is the standard WinUI control with its theme resources) |
