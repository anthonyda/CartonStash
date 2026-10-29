# 002 – Installed packages: Requirements

**Status:** Approved (2026-10-29)
**Depends on:** 001

## Problem

Users can find packages but can't see what is already on their PC. `winget list` shows everything
Windows knows about (including apps WinGet didn't install) in a hard-to-scan table.

## User stories

- **US1.** As a user, I want a list of everything installed on my PC so I know what I have.
- **US2.** As a user, I want to filter that list quickly so I can find one app among hundreds.
- **US3.** As a user, I want to see which installed apps WinGet can manage, so I know what I can update or uninstall from here.
- **US4.** As a user, I want to move between Search and Installed (and later screens) easily.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When the user opens the Installed page, the app shall list all packages from WinGet's local installed catalog. |
| R1.2 | US1   | Each row shall show name, ID, installed version and, when known, the source it is matched to. |
| R1.3 | US1   | The app shall load the list at most once per app session, and refresh it only when the user presses Refresh or after an install / uninstall / upgrade completes. |
| R1.4 | US1   | While the list is loading, the app shall show a progress indicator. |
| R2.1 | US2   | When the user types in the filter box, the app shall show only rows whose name or ID contains the text (case-insensitive), without querying WinGet again. |
| R2.2 | US2   | The app shall show the count "*shown* of *total* packages". |
| R2.3 | US2   | The list shall be sorted by name by default. |
| R3.1 | US3   | When a package is matched to a WinGet source, its row shall show the source. Otherwise it shall show "Not managed by WinGet". |
| R3.2 | US3   | When an installed package has an update, its row shall show the "Update available" badge from spec 001. |
| R4.1 | US4   | The app shall provide left-side navigation with Search and Installed entries (later: Updates, Settings). The last-used page is not remembered in v1. |
| R4.2 | US4   | Switching pages shall keep each page's state (query, filter, scroll) for the session. |
| R5.1 | –     | If loading fails, then the app shall show a readable error with a Retry action. |

## Out of scope

- Uninstall / upgrade actions on rows (specs 003 / 004 add them).
- Grouping, multi-column sorting, export of the list.

## Open questions

- Show Microsoft Store / MSIX system components? **Decided: show all, as `winget list` does.**
