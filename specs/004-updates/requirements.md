# 004 – Updates: Requirements

**Status:** Approved (2026-10-29, R3 deferred)
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
| R1.1 | US1   | The Updates page shall list installed packages for which WinGet reports an available update (`IsUpdateAvailable`), showing name, ID, installed version → available version, source. |
| R1.2 | US1   | The list shall come from the same cached data as the Installed page (002), and refresh with it. |
| R2.1 | US2   | Each row shall offer **Update**, which upgrades that package through the operation queue (003). |
| R2.2 | US2   | The user shall be able to select rows with checkboxes and press **Update selected**. |
| R2.3 | US2   | When the user presses **Update all**, the app shall show a confirmation listing the number of packages, then queue them (constitution P2). |
| R2.4 | US2   | When an update finishes successfully, the package shall leave the Updates list after the refresh. |
| R3.1 | US3   | **Deferred, see Open questions.** ~~When WinGet requires explicit targeting for a package (pinned, or a version scheme it can't compare), its row shall show "Manual update only" with the reason.~~ |
| R3.2 | US3   | **Deferred.** ~~"Update all" shall exclude "Manual update only" packages. They can still be updated individually.~~ |
| R4.1 | US4   | The Updates navigation item shall show a count badge of available updates once the installed data is loaded. |
| R5.1 | –     | If nothing needs updating, then the page shall show "Everything is up to date". |

## Out of scope

- Scheduled / automatic background updates.
- Pin management (pin, unpin, pin to version). Possible later spec.
- Updating unmanaged (non-WinGet) apps.

## Open questions

- **R3 (explicit targeting / pins) is deferred and stays open (decision 2026-10-29), tracked in [issue #1](https://github.com/anthonyda/CartonStash/issues/1); a full proposal is drafted in [spec 008](../008-pin-management/requirements.md).** 004 ships without it:
  "Update all" queues every package WinGet reports. WinGet itself refuses pinned / explicit-only packages, and the
  per-package result says why (003 R5.2).
  - Why it's blocked: the COM API 1.29 doesn't expose pins or `RequireExplicitUpgrade` (see design.md, T1).
  - Option on the table: read-only CLI parsing (would need a constitution A2 exception).
  - Finding from 2026-10-29: `winget pin list` alone doesn't cover `Anaconda.Miniconda3` (it has no pin). It shows up only
    in the "require explicit targeting" section of `winget upgrade`. Any CLI approach would need to read both.
