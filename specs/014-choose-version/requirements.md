# 014 – Install a chosen version: Requirements

**Status:** Draft
**Depends on:** 003 (operation queue, agreements, results), 004 (updates), 005 (details pane, R2.1 version list),
010 (R5 exclusions from update checks)
**Replaces:** 005's out-of-scope line "Installing a specific (older) version. The version list is information only in v1."

## Problem

The details pane lists every version WinGet has for a package (005 R2.1), but nothing in the app can install one of
them: Install, Update and Update all always take WinGet's default, the latest. The list can't be acted on. The common
reason to look at it is a bad update: the user wants the version that worked, and today that needs
`winget install --id X --version 1.2.3` in a terminal.

## What WinGet offers (checked 2026-11-07 in the COM API 1.29 metadata)

- `CatalogPackage.AvailableVersions` (the list the pane already shows) and `GetPackageVersionInfo` for one version.
- `InstallOptions.PackageVersionId` installs or upgrades to that version instead of the default.
- `InstallOptions.Force` exists. **Not yet checked:** what `InstallPackageAsync` and `UpgradePackageAsync` do with a
  version older than the installed one (refuse, install over it, or uninstall first), with and without `Force`, for
  the common installer types (MSI, Inno, NSIS, MSIX, portable). That is the design's first task, run in the test VM.

## User stories

- **US1.** As a user, I want to install a specific version of a package from its details, so I don't need the command line.
- **US2.** As a user, after a bad update, I want to go back to the version I had, and not have the app bring the bad one back.
- **US3.** As a user, I want to update to a version that isn't the latest, for example to skip a broken release.

## Acceptance criteria (EARS)

### R1. Choosing a version

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1, US2, US3 | In the details pane, each version in the "Available versions" list other than the installed one shall offer an action: **Install** when the package isn't installed, **Update to this version** when it is newer than the installed one, **Go back to this version** when it is older. |
| R1.2 | US1   | Whether a version is newer or older shall come from WinGet's version comparer, never from comparing the strings (constitution P1). If WinGet can't compare it with the installed version, then the action shall be **Install this version** and R3.1 shall apply, as for an older one. |
| R1.3 | US1   | The list shall show the newest 10 versions, with **Show all versions** to see the rest. The installed version stays marked (005 R2.1). |
| R1.4 | –     | Every version action shall be keyboard reachable and have an accessible name with the package and the version, for example "Go back to PowerToys 0.101.0" (constitution Q5). |
| R1.5 | US1   | The actions shall not be offered for a package whose source lists only one version (for example the Microsoft Store source), for an installed package WinGet doesn't manage, or while the package is queued or being changed (003 R4.2). The list itself stays. |

### R2. Running it

| ID   | Story | Criterion |
|------|-------|-----------|
| R2.1 | US1, US3 | A chosen version shall install or update through the same queue as any operation, with the same progress, Cancel, result, notification and Retry (003, 004). The Activity card, the notification and the operation log shall name the version, for example "Installing PowerToys 0.101.0". |
| R2.2 | US1   | When the chosen version has license agreements, the app shall show that version's agreements, not the latest version's, and go ahead only if the user accepts (003 R1.3, 004 R7.1). |
| R2.3 | US1   | **Retry** shall install the same chosen version again, reusing the acceptance the user gave (004 R7.3). |
| R2.4 | –     | The row's Install and Update buttons, **Update selected** and **Update all** shall keep installing WinGet's default version, unchanged. |
| R2.5 | US1   | When the operation succeeds, the app shall refresh the installed data (002 R1.3) and show the version WinGet reports afterwards. If that isn't the chosen version, then the result shall say so and shall not say the operation succeeded. |

### R3. Going back to an older version

| ID   | Story | Criterion |
|------|-------|-----------|
| R3.1 | US2   | When the user chooses **Go back to this version**, the app shall ask for confirmation naming the package, the installed version and the chosen one, and saying that some installers can't go back and that the program's settings may not carry over. It shall queue nothing unless the user confirms (constitution P2). |
| R3.2 | US2   | That confirmation shall offer **Leave this package out of update checks** (010 R5), checked by default, so that Update all and the background checks don't bring the newer version back. The exclusion shall be added only when the operation succeeds. |
| R3.3 | US2   | If WinGet or the installer refuses to go back, then the result shall say that this package's installer can't go back to an older version from here, and suggest uninstalling it and then installing the chosen version. WinGet's status and codes stay in the operation log (009 R2.2). |
| R3.4 | US2   | The app shall never uninstall the package on its own as part of going back. If going back needs an uninstall first, the user does it (constitution P2). |

## Out of scope

- Choosing a version from a row, from the Updates page or for **Update all**. They keep using the latest (R2.4).
- Several versions of one package side by side.
- Release notes for the chosen version. The pane keeps showing the latest version's notes (012).
- Pins (008). R3.2 uses the existing exclusions instead, which work today and need no WinGet command line.
- Typing a version that isn't in the list, or a version range.

## Open questions

- **Q1. Where the actions live.** *Proposed: on the version list in the details pane (R1.1),* because that's the list
  the user is already reading. The alternative is a **Choose version…** item on the Install and Update buttons, which
  opens a picker. It would work from rows too, but adds a second place to show versions.
- **Q2. Exclusion checked by default (R3.2).** *Proposed: yes.* Without it, the next Update all reinstalls the version
  the user just left, which is the surprise P2 rules out. The box is visible in the dialog, so it can be unchecked.
- **Q3. `Force` (see "What WinGet offers").** If WinGet refuses an older version unless `Force` is set, should the app
  set it? *Proposed: decide after the VM check.* `Force` also lets WinGet go on past other non-security warnings, so
  the design has to say exactly what it skips.
- **Q4. Overlap with 013 (honest results).** R2.5 checks that WinGet afterwards reports the chosen version. 013, now
  done, already warns when an update reported Ok but the old version is still listed ("Update not confirmed").
  *Proposed:* R2.5 reuses 013's check and its warning, compared against the chosen version instead of the latest.
