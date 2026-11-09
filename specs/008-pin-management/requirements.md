# 008 – Pin management: Requirements

**Status:** Draft, updated 2026-11-09 against the current code (after 009 to 014), **needs review**
**Issue:** [#1](https://github.com/anthonyda/CartonStash/issues/1)
**Depends on:** 002 (Installed list, including R6 one row per program and R8 status chips), 004 (Updates page, "Update all"),
010 (R5 exclusions from update checks, background checks), 014 (choose a version)
**Replaces:** 004 R3 ("Manual update only"), which this spec delivers

## Problem

WinGet can pin a package so that `upgrade --all` skips it, block its upgrades entirely, or limit them to a
version range. Today that needs `winget pin` commands and reading a table, and the frontend can't show which
packages are pinned, so "Update all" (004) would try to update packages the user deliberately held back.
Some packages also refuse a plain upgrade because their publisher requires it to be explicit
(`RequireExplicitUpgrade`, e.g. `Anaconda.Miniconda3`); "Update all" fails on them today.
The WinGet COM API (1.29) exposes neither, so this feature reads the `winget` command line
(constitution amendment, see design.md).

## What the app does today (checked 2026-11-09, ComInterop 1.29.380)

- **No pin awareness anywhere.** Core has no pin model or service. Nothing on Installed, Updates, Details or Settings
  shows a pin.
- **Update all** queues every package COM reports as updatable, minus the app's own exclusions (010 R5.9). The Updates
  count badge, the page title, background notifications and Check history count the same set (010 R5.2, R5.7, R5.8, R5.10).
  Pinned, blocked and manual-only packages are all counted and offered.
- **Explicit-only packages (Miniconda):** COM reports `IsUpdateAvailable`, so the row offers Update and Update all
  includes it. The update fails, and the message explains why (013 R3.1, `0x8A150114`). Nothing marks it in advance.
- **Pinned packages: not known.** Whether COM `UpgradePackageAsync` honours Pin, Block or Gate has not been checked
  (T2). If it doesn't, Update all and the row's Update button override the user's pins today. Issue #1's "WinGet refuses
  pinned packages itself" is an assumption until T2.
- **Choose version (014)** installs a chosen version through COM, including over a newer one. Whether that respects a
  Block or a Gate is not known either (T2).
- **Workaround in place:** excluding a package from update checks (010 R5) keeps it out of Update all, the badge and
  notifications. It lives in the app's settings and changes nothing in WinGet. 014 R3.2 uses it instead of pins.

### What's missing

1. Reading pins and the explicit-only list (R1.1, R6.1), with a safe failure (R1.4, R6.3).
2. Showing them on rows (R1.2, R6.1) and a Pinned filter (R1.3).
3. Leaving them out of Update all (R4.1) and out of every count that matches it: badge, title, background
   notifications, Check history (R4.4, R4.5).
4. Defining how a pin and a 010 exclusion sit together on the Updates page (R4.6).
5. Not bypassing a Block or Gate through a row's Update or through 014's version choice (R4.2, R4.3, R4.7).
6. Changing pins from the app (R2, R3), if kept in scope.

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
| R4.4 | US4, US5 | The Updates count badge (004 R4.1, 010 R5.7) and the Updates page title (010 R5.10) shall not count packages with a Pin or Block, or marked "Manual update only", so that they match what Update all runs. *(Added 2026-11-09.)* |
| R4.5 | US4, US5 | A background check (010) shall leave the same packages out of the notification's count and names and state them with the ignored ones (010 R5.2), and Check history shall count them as excluded (010 R5.8). If the pin or explicit list can't be read during a background check, then it shall count as today (nothing left out). *(Added 2026-11-09.)* |
| R4.6 | US4, US5 | On the Updates page, a package with a Pin or Block, or marked "Manual update only", shall be listed with the excluded packages (010 R5.5), with its badge giving the reason. A package that is both excluded and pinned shall be listed once, showing both, and counted once in Update all's skipped count. *(Added 2026-11-09.)* |
| R4.7 | US4   | Installing a chosen version (014) of a Blocked package, or of a Gated package outside its range, shall not bypass the pin: WinGet enforces it, or the app disables the action with the reason (decided after T2, as R4.3). *(Added 2026-11-09.)* |
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
- **Pins and exclusions (R4.6, added 2026-11-09).** 010 added app-side exclusions after this draft. *Proposed:* one
  "not in Update all" section on the Updates page for both, each row saying why. Alternative: keep pins as a badge on
  the regular list.
- **Background checks (R4.5, added 2026-11-09).** Reading the CLI adds ~1 to 3 s to each background check, which runs
  in its own process at sign-in or on a schedule. *Proposed: yes,* so the notification never announces updates that
  Update all won't run. Alternative: background checks ignore pins, and only the app's pages use them.
- **Source agreements (design, added 2026-11-09).** `--accept-source-agreements` records acceptance in WinGet, which
  006 R3.4 leaves to the user for `msstore`. *Proposed:* drop the flag and pass `--source` for each source that has no
  agreements or that the user accepted in the app (006 R3.8); a source left out has its pins ignored and
  counted in R1.4's message.

Verification items (exit codes, elevation, COM upgrade and 014 version install vs pins, CLI from the packaged background check, `--installed` + `--version`, output in another display
language) are tasks T1/T2, done on the dev PC before implementation starts.
