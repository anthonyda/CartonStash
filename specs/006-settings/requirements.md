# 006 – Settings: Requirements

**Status:** Approved (2026-10-29). Amendment R5 (start page) approved 2026-10-30. Amendment R4.3 (safe diagnostics) approved 2026-10-30. Amendment R3.4 (startup check and source agreements) approved 2026-11-01. Amendment R6 (window layout) approved 2026-11-12. Amendment R3.14 to R3.17 (explicit sources, bug review BR-01): **Draft** 2026-11-12
**Depends on:** 002, 003

## Problem

Install behaviour (user vs machine scope), the app's look, and which WinGet sources are in use are all hidden
today. Users need one place to see and change them.

## User stories

- **US1.** As a user, I want to choose the app theme (Light, Dark, Use system setting).
- **US2.** As a user, I want to choose the default install scope (WinGet default, Current user, All users / machine).
- **US3.** As a user, I want to see WinGet's sources and refresh them when results look stale.
- **US4.** As a user, I want to see the app and WinGet versions to report problems.
- **US5.** As a user, I want to choose which page the app opens on, so I land where I usually start (e.g. Updates).

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When the user changes the theme, the app shall apply it immediately without restarting. |
| R1.2 | –     | The app shall save settings per Windows user and restore them on start. |
| R2.1 | US2   | The install scope setting (default "Let WinGet decide") shall be used for every install and upgrade (003 / 004). |
| R2.2 | US2   | When "All users" is selected, the app shall explain that installers may show a UAC prompt. |
| R3.1 | US3   | The Settings page shall list WinGet's configured sources with name, type, URL, trust level, whether they are explicit (excluded from default searches), and last update time. |
| R3.2 | US3   | When the user presses **Refresh sources**, the app shall refresh each source, show progress, and then clear the cached catalogs so the next search uses fresh data (002 `CatalogConnection.Invalidate`). |
| R3.3 | US3   | If a source requires accepting an agreement, then the app shall show the agreement text with Accept / Decline before using that source. |
| R4.1 | US4   | The Settings page shall show the app version and the WinGet (App Installer) version. |
| R4.2 | US4   | The Settings page shall show a "Copy diagnostic info" button that copies versions, OS build and source list as plain text. |
| R4.3 | US4   | The copied diagnostic text shall not carry secrets or personal paths from a source's address: web URLs shall keep only scheme, host, port and path, and anything else shall appear as `(local source)`. *(Security review, 2026-10-30: the README invites public bug reports, and a private source's URL can hold a token.)* |
| R5.1 | US5   | The Settings page shall offer a **Start page** setting with the choices Search, Installed and Updates. The default is Search. |
| R5.2 | US5   | When the app starts, it shall open the chosen start page and show it as selected in the navigation. |
| R5.3 | US5   | Changing the start page shall take effect the next time the app starts; it shall not navigate away from Settings. |
| R5.4 | –     | If the saved start page is missing (settings from an older version), the app shall use Search without reporting a reset. |

Spec 010 adds the Update checks, Excluded packages and Check history sections, with the last check line (010 R3.1, R3.3, R3.11, R5.4).

## Amendment R3.4: startup check and source agreements (approved 2026-11-01)

R3.3 is not met today. `PackageCatalogReference.AcceptSourceAgreements` defaults to true, and `CatalogConnection` only ever
sets it to true, so the app uses `msstore` whether the user accepts, declines or never opens the dialog. The R3.3 manual check
tested the dialog, not that the source was held back.

Measured on WinGet 1.29.380 in Windows Sandbox, region US, after `winget source reset --force`:

| Step | Result |
|------|--------|
| COM, default reference, connect and search on `msstore` | Ok, no prompt |
| `winget search` after that | Still prompts: COM doesn't record acceptance |
| `winget search --accept-source-agreements`, then `winget search` | No prompt: the CLI records it |
| COM with `AcceptSourceAgreements = false`, after the CLI recorded acceptance | `SourceAgreementsNotAccepted`: COM ignores WinGet's record, so the app can't read it |
| Composite with one reference set to false | The whole connect fails, so a declined source must be left out |
| `SourceAgreements` on `msstore` | 1 entry. The CLI's region notice isn't one, so the app's dialog doesn't show it |

With region World, the search got HTTP 400 for market `ZZ` (spec 011 findings), so the region does reach the service with
agreements reset.

| ID   | Story | Criterion |
|------|-------|-----------|
**Maintainer's direction, 2026-11-01:** don't quietly hide a source. Check the prerequisites when the app starts, and if one is
missing, offer to set it up, showing the licence so the user can read and accept it.

| ID   | Story | Criterion |
|------|-------|-----------|
| R3.4 | US6   | When the app starts, it shall check that WinGet is installed and that the agreements of every source that has them are accepted, before it connects to any source. |
| R3.5 | US6   | If a prerequisite is missing, then the app shall list what's missing and offer to set it up. Nothing shall be installed or accepted until the user chooses to. |
| R3.6 | US6   | When the user chooses to set up a source's agreements, the app shall show the agreement text or link and what the source receives, with Accept and Decline. For `msstore`, it receives the PC's 2-letter region. |
| R3.7 | US6   | If WinGet is missing and the user chooses to set it up, then the app shall install App Installer from Microsoft, show progress and the outcome, and carry on without a restart where it can. If that fails, it shall offer the link from 011 R3.2. |
| R3.8 | US6   | Until the user has accepted a source's agreements in the app, the app shall contact that source only to fetch the agreements it shows, and shall not search it. After Decline, it shall not contact the source until the user chooses to read the agreements again. If the agreements can't be read, the source shall stay out and the other sources shall keep working. *(Changed 2026-11-01 during T19: the first wording, "shall not connect", can't be met, because the text comes from the source. See the note below.)* |
| R3.9 | US6   | The app shall save the user's answer per Windows user and keep it across restarts. It shall ask again at start if the user hasn't answered, or if the agreement text has changed, and shall not ask again after Decline. |
| R3.10 | US6  | While a source is not accepted, Search, Installed, Updates and Settings shall keep working with the other sources, and Search and Settings shall show a visible **Read and accept** button naming the source. |
| R3.11 | US6  | The user shall be able to withdraw an acceptance in Settings. |
| R3.12 | –    | Accepting in the app shall not change WinGet's own record: the `winget` command keeps asking for itself. The dialog shall say so. |
| R3.13 | –    | `PRIVACY.md` shall say that the Microsoft Store source receives the PC's region once the user accepts its agreements (007 R6.6). |

**US6.** As a user, I want the app to check what it needs when it starts and offer to set it up, so that I can read and accept a
licence myself and nothing is hidden from me.

**Reading the agreements contacts the source (T19, 2026-11-01).** With the Store's address sent to a local listener, reading
`SourceAgreements` for `msstore` made a connection attempt, and threw a WinGet error when it failed. After one successful read WinGet keeps a
copy. `winget` has to fetch them too before it can prompt. 011 saw the market in the `manifestSearch` request; the encrypted agreement
request itself wasn't inspected, so what it carries beyond the IP address isn't confirmed.

Links to other specs:

- **011 (in progress):** its `WinGetAvailability` check and `WinGetSetupWindow` are the WinGet half of R3.4 and R3.5. R3.7 adds
  a button to that window. It reverses two 011 decisions, "the app never installs it itself" and installing App Installer being
  out of scope. P2 allows it, because the user asks for it. 011 can ship first with the link alone.
- **010:** must not run before this ships, or background checks would reach `msstore` without acceptance.
- **008:** `--accept-source-agreements` records acceptance in WinGet for the user, so its decision of 2026-10-30 needs another
  look before 008 resumes.

Effects to review: a user who declines gets no `msstore` results and no update information for Store packages. Until this ships,
`PRIVACY.md` is incomplete: it names the IP address only.

Open questions for this amendment:

- **Where does acceptance live?** **Decided 2026-11-01: in the app's settings.** Everyone already on an earlier version is asked once, even if
  they accepted in `winget`, because COM can't read that record. No `winget.exe` calls, so exception A6 isn't needed.
- **How does the app install App Installer?** To settle in the design with a Sandbox test, since 011 found WinGet can be absent on real
  installs. Candidates: the Windows package API with Microsoft's `aka.ms/getwinget` address, or the Store page. The design must also
  check that the Store edition of the app is allowed to do this.
- **Should the amendment stay general or name `msstore`?** Recommended: general, since any source can have agreements. R3.6 needs
  fixed text per known source, because the COM API exposes only the agreement list.

## Amendment R3.14 to R3.17: explicit sources (2026-11-12, Draft)

**Found in the bug review of 2026-11-10 (BR-01).** R3.1 lists a source as "explicit (excluded from default searches)",
and `winget` leaves such a source out of `search`, `list` and `upgrade` unless it is named with `--source`. The app
doesn't: `CatalogConnection` puts every source into its catalogs, so search results, the Installed list's matches, the
Updates list and the background check all include explicit sources. `Info.Explicit` is read for the Settings text only.
`winget-font` is such a source on the maintainer's PC (R3.1's manual check), so font packages show up in searches that
`winget search` doesn't return. The text on the Settings page is therefore wrong today.

**Maintainer's choice, 2026-11-12:** follow `winget` by default, with a checkbox for each explicit source on the
Settings page to search it too, and a paragraph that says what an explicit source is.

**US8.** As a user who added an explicit source (fonts, for example), I want to choose, source by source, whether the
app searches it, so my everyday results match `winget` unless I ask for more.

| ID    | Story | Criterion |
|-------|-------|-----------|
| R3.14 | US3   | By default, the app shall leave a source WinGet marks as explicit out of everything it reads from sources: search, the Installed and Updates lists' matches, the details pane and the background check (010), as `winget` does when no source is named. It shall not read such a source's agreements (R3.8) or test it after a failure (011 R1.3). |
| R3.15 | US8   | In the source list, each explicit source shall have its own checkbox, **Use in searches**, off by default. When it's on, Search shall use that source too, and the details pane and the operations of a result from it shall find the package in it. The Installed and Updates lists and the background check shall still leave it out (R3.14). A change shall apply to the next search without a restart. Sources that aren't explicit have no checkbox. |
| R3.16 | US3   | When at least one source is explicit, the Settings page shall show, above the source list, a short paragraph saying what an explicit source is: a source WinGet uses only when it is named, so that a large or specialised catalog (fonts, for example) doesn't fill everyday results; CartonStash, like `winget`, leaves it out unless its **Use in searches** box is checked; and only Search uses it, not the Installed and Updates lists or the background checks. |
| R3.17 | US3   | The Settings page shall keep listing explicit sources (R3.1). **Refresh sources** shall refresh an explicit source only while its box is checked. The choice shall be saved per source ID, and kept when the source is removed and added again under the same ID. |

With a box off, a program installed from that source has no source match, so it lists as not from a WinGet source
(002 R6), as `winget list` shows it. With the box on, that stays true: its updates aren't checked, as `winget upgrade`
doesn't check them without `--source`. If the setting is missing (settings from an older version), every box is off,
without reporting a reset (as R5.4). Adding or removing sources stays out of scope.

Proposed paragraph (R3.16), to settle in review:

> **Explicit sources.** WinGet uses an explicit source only when you name it, so a large or specialised catalog, such as
> fonts, doesn't fill your everyday results. CartonStash does the same: it leaves an explicit source out unless you
> check **Use in searches** for it. Even then, only Search uses it. The Installed and Updates lists and the background
> checks leave it out, as `winget list` and `winget upgrade` do.

### Open question (amendment R3.14)

- **Q1. Leave explicit sources out, or use them and change the text?** **Decided 2026-11-12:** out by default, with a
  **Use in searches** checkbox per explicit source (R3.15) and a paragraph explaining them (R3.16). Installed, Updates
  and background checks follow `winget`.

## Out of scope

- **Adding / removing / editing sources.** The COM API has `AddPackageCatalogAsync`, `RemovePackageCatalogAsync` and
  `EditPackageCatalog`, but they require admin rights (A5 conflict). Possible later spec with a separate elevated helper.
- Proxy, download directory, telemetry and other WinGet `settings.json` options. Users can still edit WinGet's own settings file.
- Language / localization (English only in v1).

## Open questions

- **Start page (R5): also offer "Last used page"?** **Decided 2026-10-30: no for now.** 002 R4.1 deliberately doesn't remember the last page in v1;
  a fixed choice is predictable. Can be added later as a fourth option without changing the stored format much.
- **Start page: offer Settings as a choice?** **Decided 2026-10-30: no.** Nobody needs to land on Settings every day.

- Settings storage? **Decided: JSON in `%LOCALAPPDATA%\CartonStash\settings.json` behind `ISettingsStore`; `ApplicationData` comes with 007.**

## Amendment R6: window layout (approved 2026-11-12)

**Maintainer's request, 2026-11-12:** the app layout and window size should stay the same after closing and reopening the app.

**US7.** As a user, I want the window to reopen the way I left it, so I don't resize it every time.

| ID   | Story | Criterion |
|------|-------|-----------|
| R6.1 | US7   | When the main window closes, the app shall save its size, position, whether it is maximized, and whether the navigation pane is expanded. |
| R6.2 | US7   | When the app starts, it shall restore the saved size, position, maximized state and navigation pane. A maximized window shall restore to its last normal size when un-maximized. |
| R6.3 | –     | If the saved position is no longer on any display (a monitor was removed), then the app shall keep the saved size and let Windows place the window. |
| R6.5 | US7   | The pinned Activity pane shall come back with the window (009 R3.4 already saves the pin): pinned and wide enough (009 R3.5), it shall open beside the page at start. |
| R6.4 | –     | If nothing is saved (first start, or settings from an older version), the app shall open at 1200 × 780 with the navigation pane as before, without reporting a reset. |
