# 002 – Installed packages: Requirements

**Status:** Done (2026-10-29). Amendment R6 (one row per installed program) done 2026-10-30. Amendment R7 (version badge colors) done 2026-10-30
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
| R2.4 | US2, US3 | The page shall offer a "Show apps not managed by WinGet" option, **off by default**. While it is off, packages not matched to a WinGet source are hidden. The count (R2.2) counts shown rows against all installed packages. *(Added 2026-10-29 at the user's request.)* |
| R3.1 | US3   | When a package is matched to a WinGet source, its row shall show the source. Otherwise it shall show "Not managed by WinGet". |
| R3.2 | US3   | When an installed package has an update, its row shall show the "Update available" badge from spec 001. |
| R4.1 | US4   | The app shall provide left-side navigation with Search and Installed entries (later: Updates, Settings). The last-used page is not remembered in v1. |
| R4.2 | US4   | Switching pages shall keep each page's state (query, filter, scroll) for the session. |
| R5.1 | –     | If loading fails, then the app shall show a readable error with a Retry action. |
| R6.1 | US1   | The Installed page shall show one row per installed program in WinGet's installed catalog, the same entries `winget list` shows, even when several programs share one WinGet package ID. |
| R6.2 | US1   | Each row shall show the program's own installed name, not the source package's name. The version shall be WinGet's installed version for WinGet's own match (as `winget list` shows it, e.g. `> 1.8.10`), and the version Windows records for every other row. *(Amended 2026-10-30 during R7: raw MSIX package versions like `8000.994.2142.0` don't follow the product's numbering.)* |
| R6.3 | US3   | A row shall count as managed by WinGet (source, available version, R3.1) when WinGet matched that program to a source package, or when the program's product code or package family name is listed in a source package's manifest. |
| R6.4 | US3   | When several rows match the same package ID, only the row WinGet itself evaluated shall show "Update available" (R3.2); the others shall not claim an update. |
| R6.5 | –     | Uninstall on a row (003) shall remove exactly that row's program, never another program sharing its package ID. |
| R7.1 | US3   | The "Installed *version*" badge shall be colored by how the installed version compares with the source's available version, as decided by WinGet's own version comparer (never by comparing strings in the app): **same** → green; **older, update offered** → amber; **newer than the source** → blue; **different but WinGet offers no update or can't compare** (e.g. `Unknown`, ranges like `> 1.8.10`, explicit-upgrade packages) → neutral gray. |
| R7.2 | US3   | Unmanaged rows (no available version) and rows linked only through manifest codes (R6.3) shall keep a neutral badge: their recorded version may use a different numbering than the source. *(Manifest part amended 2026-10-30 during implementation.)* |
| R7.3 | –     | Each color shall come with a tooltip and an accessible name that says the state in words ("Installed 2.5.6, newer than winget's 2.5.5"), so the meaning never depends on color alone (constitution Q5). |
| R6.6 | US2   | The count (R2.2) shall count programs (rows), e.g. "90 of 362 packages". |

## Amendment R6: why (2026-10-30)

Seen live: WSL 3.0.4.0 and the old "Windows Subsystem for Linux Update" 5.10.102.1 are both installed and both match
`Microsoft.WSL`. WinGet's composite catalog (COM API 1.29) merges them into one package and reports only the old
version, so the app showed "Windows Subsystem for Linux 5.10.102.1", which matches neither program. `winget list` shows
both. WinGet's installed-only catalog returns both separately (362 entries vs. 330 merged packages on the dev machine).

## Out of scope

- Uninstall / upgrade actions on rows (specs 003 / 004 add them).
- Grouping, multi-column sorting, export of the list.

## Open questions

- **R7 colors:** **Decided 2026-10-30:** green / amber / blue / gray as in R7.1, using the WinUI theme brushes (Success, Caution, Informational,
  neutral) so they work in light, dark and high contrast. The existing "Update available" badge stays as is.
- **R7 scope:** **Decided 2026-10-30:** rows everywhere (Search, Installed, Updates) and the details pane's "Installed version" line.

- **R6: rename rows to the installed name (R6.2)?** **Decided 2026-10-30: yes, the full installed name**, to match `winget list`. Most names barely change;
  a few get more detail (e.g. "Mozilla Firefox (x64 en-US)"). Search results keep the source package name.
- **R6: rows matched only through the manifest (R6.3 second case), e.g. WSL 3.0.4.0?** **Decided 2026-10-30: build it as proposed, then review the result.** Proposal: show them as managed
  (source + available version) but never with Update / Uninstall by package ID; Uninstall goes through the program's own
  installed entry (R6.5). If the manifest doesn't list the program, the row shows "Not managed by WinGet" and is hidden by
  default (R2.4). Whether WSL 3.0.4.0 is matched this way depends on the Microsoft.WSL manifest; checked during T9.
- **R6: also report the merge upstream** (microsoft/winget-cli)? **Decided 2026-10-30: no.**

- Show Microsoft Store / MSIX system components? **Decided: show all, as `winget list` does.** Amended 2026-10-29: apps not managed by WinGet are hidden by default and shown on request (R2.4).
