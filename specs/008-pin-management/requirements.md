# 008 – Pin management: Requirements

**Status:** Draft, updated 2026-10-30 against the current code, **needs review**
**Issue:** [#1](https://github.com/anthonyda/CartonStash/issues/1)
**Depends on:** 002 (Installed list, including R6 one row per program and R8 status chips), 004 (Updates page, "Update all")
**Replaces:** 004 R3 ("Manual update only"), which this spec delivers

## Problem

WinGet can pin a package so that `upgrade --all` skips it, block its upgrades entirely, or limit them to a
version range. Today that needs `winget pin` commands and reading a table, and the frontend can't show which
packages are pinned, so "Update all" (004) would try to update packages the user deliberately held back.
Some packages also refuse a plain upgrade because their publisher requires it to be explicit
(`RequireExplicitUpgrade`, e.g. `Anaconda.Miniconda3`); "Update all" fails on them today.
The WinGet COM API (1.29) exposes neither, so this feature reads the `winget` command line
(constitution amendment, see design.md).

## User stories

- **US1.** As a user, I want to see which installed packages are pinned, and how.
- **US2.** As a user, I want to pin, block, or gate a package to a version range without typing commands.
- **US3.** As a user, I want to remove a pin, or all pins at once.
- **US4.** As a user, I want "Update all" to respect my pins.
- **US5.** As a user, I want packages that can only be updated explicitly to be marked, with the reason, and left out of "Update all".

## Pin types (WinGet's definitions)

| Type | CLI | Effect |
|------|-----|--------|
| **Pin** | `winget pin add --id X` | Skipped by `upgrade --all`. An explicit upgrade of that package is still allowed. |
| **Block** | `winget pin add --id X --blocking` | No upgrade at all until unpinned. |
| **Gate** | `winget pin add --id X --version 1.2.*` | Upgrades only to versions matching the range (trailing wildcard). |

`--installed` pins the currently installed version instead of the available package (applies to all three types).

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When the installed data (002) loads or refreshes, the app shall also read the pin list from WinGet and attach each pin to its installed package by ID and source. |
| R1.2 | US1   | A pinned package's row (Installed, Updates) shall show a badge with the pin type: "Pinned", "Blocked", or "Gated *range*". The badge has an accessible name including the type and range. |
| R1.3 | US1   | The Installed page shall offer a **Pinned** chip next to the status chips (002 R8) with the count of pinned rows. It combines with the status chip, the text filter and the unmanaged option. |
| R1.4 | US1   | If the pin list can't be read or parsed, then the app shall show "Pin status unavailable" on the Installed and Updates pages, treat no package as pinned, and keep the rest of the page working. It shall never guess a pin state. |
| R2.1 | US2   | Each WinGet-managed installed package shall offer a **Pin** menu with: Pin, Block updates, Pin to version range…, Unpin (only the applicable entries are enabled). |
| R2.2 | US2   | When the user chooses Pin or Block updates, the app shall add that pin for the package and source. |
| R2.3 | US2   | When the user chooses Pin to version range…, the app shall ask for a range, prefilled from the installed version with the last part replaced by `*` (e.g. `1.2.*`), and add a gating pin with it. |
| R2.4 | US2   | Each of the three pin dialogs / actions shall offer "Pin the installed version" (`--installed`), off by default. |
| R2.5 | US2   | When the package already has a pin, choosing another type shall replace it (WinGet `--force`), after a confirmation that names the old and new pin. |
| R2.6 | US2   | If the range entered isn't accepted by WinGet, then the app shall show WinGet's reason and keep the dialog open with the text. |
| R3.1 | US3   | When the user chooses Unpin, the app shall remove the package's pin. No confirmation (easy to redo). |
| R3.2 | US3   | Settings (006) shall offer **Remove all pins**, which asks for confirmation naming the number of pins, then resets them (`winget pin reset --force`). |
| R4.1 | US4, US5 | "Update all" (004 R2.3) shall exclude packages with a Pin or Block and packages marked "Manual update only" (R6.1), and the confirmation shall say how many were skipped and why. |
| R4.2 | US4   | A Blocked package's individual **Update** button shall be disabled, with the tooltip "Blocked by a pin. Unpin to update." |
| R4.3 | US4   | A Pinned package can still be updated individually from its row. A Gated package can be updated individually; the update shall respect the gate (see design: enforced by WinGet or blocked by the app, never bypassed). |
| R6.1 | US5   | When WinGet lists a package as requiring explicit targeting (the matching section of `winget upgrade`), its row on the Updates page shall show a **Manual update only** badge with a tooltip giving the reason. |
| R6.2 | US5   | A "Manual update only" package can still be updated individually from its row (WinGet allows an explicit upgrade). |
| R6.3 | US5   | If that list can't be read, then no package is marked, Update all behaves as it does today, and the rest of the page keeps working. |
| R7.1 | –     | Pins and "Manual update only" attach only to the row WinGet itself evaluated (002 `IsPrimaryMatch`). Other programs sharing the package ID (002 R6) show neither. |
| R7.2 | –     | Everything here shall work from the MSIX (007) as well as the portable zip. |
| R5.1 | –     | After any pin change, the app shall re-read the pin list (not assume the result) and update the badges. |
| R5.2 | –     | Every pin change shall show progress while running and end with a success or error message (constitution P3). |
| R5.3 | –     | Pin changes shall run without elevating the app (constitution A5). |
| R5.4 | –     | If `winget.exe` can't be found or started, then pin actions shall be disabled with the tooltip "WinGet command line not found", and R1.4 applies. |

## Out of scope

- Pins for packages that aren't installed (WinGet allows it; the UI only works on installed rows in v1).
- Editing a pin in place (replace = remove + add, R2.5).
- Exporting / importing pins.

## Open questions (needs your decision)

- **Constitution exception (A6, see design.md).** Pins can only be read and changed through `winget.exe`. Approving this
  spec approves that exception to "only the COM API touches WinGet". *Proposed: yes.* Declined earlier for 004 (when pins
  were deferred); it's the only way to close issue #1.
- **Scope.** Issue #1 only asks to mark packages and skip them. This spec also lets you pin, block, gate and unpin from the
  app. *Proposed: all of it.* A smaller alternative is read-only: R1.x, R4.x, R6.x, R7.x, without R2.x, R3.x.
- **Remove all pins in Settings (R3.2).** *Proposed: keep,* with confirmation.

Verification items (exit codes, elevation, COM upgrade vs pins, `--installed` + `--version`, output in another display
language) are tasks T1/T2, done on the dev PC before implementation starts.
