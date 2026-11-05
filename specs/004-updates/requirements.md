# 004 – Updates: Requirements

**Status:** Approved (2026-10-29, R3 deferred). Amendment R6 (updating App Installer): approved 2026-11-04. Amendment R7 (license agreements on update): approved 2026-11-05
**Depends on:** 002, 003

## Problem

Keeping software current with `winget upgrade` means reading a table, then running commands one by one or
`--all` blindly. Users want to see what's outdated and update with a click, selectively or all at once.

## User stories

- **US1.** As a user, I want a page listing every installed package that has an update.
- **US2.** As a user, I want to update one package, a selection, or everything.
- **US3.** As a user, I want packages that WinGet won't update automatically to be clearly marked and left out of "Update all".
- **US4.** As a user, I want to know how many updates are waiting without opening the page.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | The Updates page shall list installed packages for which WinGet reports an available update (`IsUpdateAvailable`), showing name, ID, installed version → available version, source. Packages excluded from update checks appear below the rest (010 R5.5). |
| R1.2 | US1   | The list shall come from the same cached data as the Installed page (002), and refresh with it. |
| R2.1 | US2   | Each row shall offer **Update**, which upgrades that package through the operation queue (003). |
| R2.2 | US2   | The user shall be able to select rows with checkboxes and press **Update selected**. |
| R2.3 | US2   | When the user presses **Update all**, the app shall show a confirmation listing the number of packages, then queue them (constitution P2). Excluded packages are left out (010 R5.9). |
| R2.4 | US2   | When an update finishes successfully, the package shall leave the Updates list after the refresh. |
| R3.1 | US3   | **Deferred, see Open questions.** ~~When WinGet requires explicit targeting for a package (pinned, or a version scheme it can't compare), its row shall show "Manual update only" with the reason.~~ |
| R3.2 | US3   | **Deferred.** ~~"Update all" shall exclude "Manual update only" packages. They can still be updated individually.~~ |
| R4.1 | US4   | The Updates navigation item shall show a count badge of available updates once the installed data is loaded, leaving out excluded packages (010 R5.7). |
| R5.1 | –     | If nothing needs updating, then the page shall show "Everything is up to date". |

## Amendment R6: updating App Installer (approved 2026-11-04)

**Why.** R2.4 isn't met for App Installer. In the test VM on 2026-11-04, with 1.1.0, **Update all** reported "Updated
successfully" for App Installer, but its row stayed on the Updates page with the old version, marked "Updated", and still
offered the update. App Installer is the package WinGet runs from, and the app talks to WinGet through it, so the app is
always using the version it asks WinGet to replace.

Measured in the test VM: Windows 11 26H2, WinGet 1.29.380, App Installer 1.29.379.0 to 1.29.380.0.

| Step | Result |
|------|--------|
| `UpgradePackageAsync` for `Microsoft.AppInstaller` | `Ok`, installer exit code 0, no restart flag |
| Windows' deployment log | 1.29.380.0 staged. Registering it failed with `0x80073D02`, because `WinGetComServer` was running. WinGet didn't ask Windows to finish it later |
| The rest of the same **Update all** (Outlook, Windows Terminal, two Windows App Runtimes) | Updated normally, through the old WinGet |
| App closed by force, then reopened | WinGet's server stayed alive for more than 3 minutes, and the app connected to it again: still 1.29.379.0 |
| WinGet's server stopped, then WinGet started again | 1.29.380.0 took over at once, and App Installer left the Updates list |
| App closed normally, with no operation run | The app exited in 1 s, and WinGet's server stopped within 1 s |

- **US5.** As a user who updates App Installer, I want to be told that the update finishes when CartonStash restarts, and to
  finish it in one click, so the app never says "Updated" while the old version stays.

| ID   | Story | Criterion |
|------|-------|-----------|
| R6.1 | US5   | If WinGet reports success for an update of App Installer, and the reloaded list still shows the previous version, then the app shall report a warning, as for a PC restart (003 R5.3), saying that the update finishes when CartonStash restarts. The Activity card, the notification and the operation's log shall not say "Updated successfully". |
| R6.2 | US5   | While that update waits for the restart, App Installer's row shall say **Restart CartonStash to finish** wherever it would offer **Update**, and **Update all** shall leave it out. |
| R6.3 | US5   | The warning and the row shall offer **Restart CartonStash**, which closes the app and opens it again on the same page. If operations are running or queued, then the app shall first warn, as when closing (003 R6.2). |
| R6.4 | US5   | After that restart, App Installer shall show the new version, or leave the Updates list. If it still shows the previous version, then the app shall keep R6.2's row and say that the update finishes once every program using WinGet is closed. |
| R6.5 | US5   | **Update all** and **Update selected** shall queue App Installer after the other packages, so they run on the WinGet that started them and the restart comes last. |

### Open questions (amendment R6), decided 2026-11-04 as recommended

- **App Installer only, or any update whose version doesn't change?** Recommended: App Installer only. It's the one case with a
  known cause and a fix in the app. Other packages can still show an update after a successful one, when WinGet can't match the
  installed version to the manifest. Calling those unfinished would be wrong, and their fix belongs in the package's manifest.
- **Count it in the badge and the title while it waits?** Recommended: yes. WinGet still reports the update (P1), and the
  restart clears it.
- **Restart, or only close?** Recommended: restart, so one click finishes the job. The design checks that it works for the Store
  build and the portable build.
- **Keep updating App Installer through WinGet?** The alternative is to leave it out and link to its Store page. Recommended:
  keep it. WinGet stages the new version correctly, only the finish is missing, and a second way to update would need explaining.

### Risks, to prove first

- **A normal close right after the update.** Does WinGet's server stop, and does the reopened app get the new version? It was
  only measured with no operation run (1 s) and after a forced close (more than 3 minutes).
- **Other WinGet users.** The `winget` command, another app or a background check (010) may keep WinGet's server running. Then
  R6.4's message applies, and the design has to find out how the app can tell.

Links to other specs: 003 R5.3 gains a second kind of restart, and 009's Activity card shows it. Neither changes otherwise.

## Amendment R7: license agreements on update (approved 2026-11-05)

**Why.** R2.1 and R2.3 aren't met for packages with a license agreement. In the test VM on 2026-11-05, with the current
build, Microsoft PowerBI Desktop 2.158.1304.0 (installed with the `winget` command) failed to update to 2.159.1231.0 within
a second: "The package's license agreement wasn't accepted.", WinGet status `PackageAgreementsNotAccepted`, `0x8A150041`.
Its manifest has one agreement, "Microsoft Software License Terms". The app shows a package's agreements before
**Install** (003 R1.3), but **Update**, **Update selected** and **Update all** never do and always tell WinGet they weren't
accepted, so WinGet refuses every update of such a package. The `winget` command asks, or takes
`--accept-package-agreements`.

- **US6.** As a user, I want to read and accept a package's license agreement when I update it, as when I install it, so
  the update runs instead of failing.

| ID   | Story | Criterion |
|------|-------|-----------|
| R7.1 | US6   | When the user updates a package whose new version has license agreements, the app shall show them, as for an install (003 R1.3), and update only if the user accepts. Declining shall queue nothing for that package. |
| R7.2 | US6   | When the user presses **Update selected** or **Update all**, the app shall ask for every package with agreements before it queues anything, then queue the accepted packages and the packages without agreements, in their usual order (R6.5). A declined package shall be left out, and the Activity pane shall not list it. |
| R7.3 | US6   | **Retry** of an update shall reuse the acceptance the user gave for that operation, as it does for an install. |
| R7.4 | –     | If the agreements can't be read, then the app shall queue the update without acceptance, as an install does today: WinGet then refuses, and the result says why (003 R5.2). |

### Open questions (amendment R7), decided 2026-11-05 as recommended

- **One dialog per package, or one for the whole Update all?** Recommended: one per package, the dialog 003 already has. A
  combined dialog would need a new layout, and agreements are rare: one in the six updates seen in the VM.
- **Ask again for a package accepted earlier in the session?** Recommended: yes, at each update. WinGet ties acceptance to
  the version, and a new version can bring new terms.
- **Check the Update all confirmation count?** Recommended: the confirmation comes first and counts every package (R2.3), then
  the agreements. A declined package doesn't get queued, and the Activity pane shows what really runs.

## Out of scope

- Automatic background updates, meaning installs. Background *checks* are spec 010, and installing without the user is ruled out by constitution P2.
- Pin management (pin, unpin, pin to version). Possible later spec.
- Updating unmanaged (non-WinGet) apps.
- Installer failures that WinGet reports correctly. On 2026-11-04, Microsoft 365 Copilot's own installer failed with
  `0x88A30033`, in the app and in the `winget` command alike.

## Open questions

- **R3 (explicit targeting / pins) is deferred and stays open (decision 2026-10-29), tracked in [issue #1](https://github.com/anthonyda/CartonStash/issues/1); a full proposal is drafted in [spec 008](../008-pin-management/requirements.md).** 004 ships without it:
  "Update all" queues every package WinGet reports. WinGet itself refuses pinned / explicit-only packages, and the
  per-package result says why (003 R5.2).
  - Why it's blocked: the COM API 1.29 doesn't expose pins or `RequireExplicitUpgrade` (see design.md, T1).
  - Option on the table: read-only CLI parsing (would need a constitution A2 exception).
  - Finding from 2026-10-29: `winget pin list` alone doesn't cover `Anaconda.Miniconda3` (it has no pin). It shows up only
    in the "require explicit targeting" section of `winget upgrade`. Any CLI approach would need to read both.
