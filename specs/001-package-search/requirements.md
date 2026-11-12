# 001 – Package search: Requirements

**Status:** Done (2026-10-29). Amendment R4 (Tab into new results never closes the app): done 2026-11-12
**Depends on:** none

## Problem

Finding the right package with `winget search` means reading a wide text table in a terminal and
copying exact IDs. Users want to type a few letters and see clear, readable results.

## User stories

- **US1.** As a user, I want to search packages by name, ID or keyword so I can find software to install.
- **US2.** As a user, I want to see whether a result is already installed and which version is available,
  so I don't reinstall something I have.
- **US3.** As a user, I want clear feedback while searching and when something goes wrong.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When the user submits a non-blank query (Enter key or Search button), the app shall search all configured WinGet sources by name, ID and moniker, case-insensitively. |
| R1.2 | US1   | While the query is blank or whitespace, the app shall disable the Search action. |
| R1.3 | US1   | The app shall show at most 50 results per search. |
| R1.4 | US1   | When results are shown, each row shall display the package name, ID, available version and source. |
| R2.1 | US2   | When a result is installed, its row shall show the installed version and an "Installed" indicator. |
| R2.2 | US2   | When an installed result has a newer available version, its row shall show an "Update available" indicator. |
| R3.1 | US3   | While a search is running, the app shall show an indeterminate progress indicator. |
| R3.2 | US3   | When a new search starts while one is running, the app shall cancel the previous search and show only the new results. |
| R3.3 | US3   | If a search returns no matches, then the app shall show an empty state "No packages found for '*query*'". |
| R3.4 | US3   | If a search fails, then the app shall show an error message and keep the query so the user can retry. |
| R3.5 | US3   | The search box and results list shall be keyboard reachable and have accessible names. |

## Out of scope

- Installing from results (spec 003).
- Package details pane (spec 005).
- Filtering by source, or search-as-you-type (possible later spec).

## Amendment R4: Tab into new results never closes the app (approved 2026-11-12)

**Why.** In the test VM on 2026-11-12, Store test build 1.3.96, 2 runs of 2: search `microsoft` (42 to 50 results), focus
the 13th result, search `jqlang.jq` (1 result), then Tab from the search box: the app closed. WinUI's ListView remembers the index of the row it last focused, and Tab into the list goes back to it. Removing rows brings that index back in range; emptying the list in one go (a reset) doesn't, so Tab asks for a row past the end and the app closes: stowed `E_BOUNDS`, WER `0xc000027b` in `Microsoft.UI.Xaml.dll` at offset `0x3d1699`. Found on the Updates page (004 amendment R8, PR #42), where the full dump shows it. Each search
empties `Results` with `Clear()` before filling it.

| ID   | Story | Criterion |
|------|-------|-----------|
| R4.1 | –     | When a new search replaces the results (R1.1, R3.2), Tab and the arrow keys into the results shall never close the app, whatever row was focused before. Focus goes to a row of the new results. |

## Open questions

- Should results be sorted by relevance as WinGet returns them, or by name? *Proposed: keep WinGet's order.*
