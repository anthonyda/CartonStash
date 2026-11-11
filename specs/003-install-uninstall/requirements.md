# 003 – Install / uninstall with progress: Requirements

**Status:** Done (2026-10-30). Amendment R7 (say when a program is running): done 2026-11-06. **Amendment R8 (all-users MSI packages): Done** 2026-11-11, approved 2026-11-10 with the proposed answers to Q8 and Q9
**Depends on:** 001, 002

## Problem

Users can find and list packages but still need a terminal to install or remove them, with no clear progress or outcome.

## User stories

- **US1.** As a user, I want to install a package from search results with one click.
- **US2.** As a user, I want to uninstall a package from the Installed list, with a confirmation first.
- **US3.** As a user, I want to see the progress of every operation and be able to cancel it.
- **US4.** As a user, I want to start several operations and have them run one after another.
- **US5.** As a user, I want a clear result: success, failure with a reason, or "restart required".

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When a search result is not installed, its row shall offer an **Install** button. |
| R1.2 | US1   | When the user presses Install, the app shall install the package's default version from the row's source, with silent install mode and the scope from Settings (default: WinGet's default). |
| R1.3 | US1   | If the package requires accepting a license agreement, then the app shall show the agreement text and install only after the user accepts. |
| R2.1 | US2   | When a package on the Installed page is managed by WinGet, its row shall offer an **Uninstall** button. |
| R2.2 | US2   | When the user presses Uninstall, the app shall ask for confirmation naming the package, and uninstall only if confirmed (constitution P2). |
| R3.1 | US3   | While an operation runs, its row and the Activity panel shall show its phase (Queued, Downloading %, Installing / Uninstalling, Finishing). |
| R3.2 | US3   | When the user presses Cancel on a queued or downloading operation, the app shall cancel it. Once the installer itself is running, Cancel shall be unavailable (WinGet can't stop it safely). |
| R4.1 | US4   | The app shall run operations one at a time, in the order requested. Others wait as Queued. |
| R4.2 | US4   | The app shall not queue the same package twice. Its button shows the in-progress state instead. |
| R5.1 | US5   | When an operation succeeds, the app shall show a success notification and refresh the Installed list (002 R1.3). |
| R5.2 | US5   | If an operation fails, then the app shall show a readable reason (download failed, installer error *code*, blocked by policy, no applicable installer…) and keep the entry in Activity with a Retry action. |
| R5.3 | US5   | When WinGet reports that a restart is required, the app shall say so clearly. The app shall never restart the PC itself. |
| R6.1 | –     | The app shall keep running unelevated. When an installer needs admin rights, Windows' own UAC prompt shall appear for that installer only (constitution A5). |
| R6.2 | –     | If the user closes the app while operations are running or queued, then the app shall warn and offer to wait or quit anyway. |

## Amendment R7: say when a program is running (approved 2026-11-06, done 2026-11-06)

**Why.** R5.2 isn't met when the package being changed is running. On 2026-11-06, Claude Code (`Anthropic.ClaudeCode`, a
portable package) failed to update from 2.1.302 to 2.1.317. The app showed only "The installer failed with error code
0x8A150003." The download and hash check passed. WinGet's own log, copied into the operation log (009 R2.8), shows why:
the old `claude.exe` was in use, so WinGet couldn't delete it (`remove: Access is denied.`). The result carried
`0x8A150003` (`APPINSTALLER_CLI_ERROR_COMMAND_FAILED`) and extended error `0x8A150052`
(`APPINSTALLER_CLI_ERROR_PORTABLE_INSTALL_FAILED`). Neither code says "in use": a portable install can fail for other
reasons. WinGet does have codes that do say it, for installers whose manifest maps their exit codes:
`0x8A150101` (`INSTALL_PACKAGE_IN_USE`), `0x8A150103` (`INSTALL_FILE_IN_USE`) and `0x8A150111`
(`INSTALL_PACKAGE_IN_USE_BY_APPLICATION`). The app turns those into "error code …" too.

- **US6.** As a user, when an operation fails because the program is running, I want to be told to close it and retry,
  so I don't have to read WinGet's log to find out.

| ID   | Story | Criterion |
|------|-------|-----------|
| R7.1 | US6   | If an install, update or uninstall fails and WinGet reports that the program or one of its files is in use (`0x8A150101`, `0x8A150103`, `0x8A150111`), then the result shall say that the package is running, ask the user to close it, and keep **Retry** (R5.2). |
| R7.2 | US6   | If an update or uninstall of a portable package fails (`0x8A150052`, `0x8A150057`) and a program is running from that package's install folder, then the result shall say so as in R7.1 and name the running file (for example `claude.exe`). |
| R7.3 | US6   | If no running program is found for R7.2, then the result shall stay as it is today: the app doesn't guess at the cause (constitution P1). |
| R7.4 | –     | The app shall never close or end the running program itself (constitution P2). The user closes it. |
| R7.5 | –     | The operation log shall keep WinGet's status, exit code and extended error unchanged (009 R2.2). Only the message shown changes. |

### Open questions (amendment R7), decided 2026-11-06 as recommended

- **Look for the running program after the failure only, or before every update?** Recommended: after the failure only.
  Many installers close their own program, so a check beforehand would hold back updates that would have worked. It
  would also add a step to every operation.
- **Read "Access is denied" from WinGet's log instead?** Recommended: no. The log is text, not an API. Its wording can change
  between WinGet versions and languages.
- **Message wording?** Recommended, for an update: "Claude Code is running (claude.exe), so it couldn't be updated. Close it,
  then press Retry." For an uninstall, "couldn't be uninstalled". Without a file name (R7.1), the brackets are left out.
- **To check in design:** whether WinGet reports a portable package's install folder (`InstalledLocation` metadata), and
  whether the packaged app can see the paths of the user's other processes.

## Amendment R8: all-users MSI packages (approved 2026-11-10)

**Why.** R6.1 isn't met for MSI packages installed for all users. In the test VM (2026-11-10, WinGet 1.29.380, tester
is an administrator with UAC on, the app at Medium integrity), with Pandoc 3.12 installed for all users:

| Operation | Mode | msiexec arguments | UAC | Result |
|-----------|------|-------------------|-----|--------|
| Update to 3.13 from the Updates page (the app) | Silent | `/x{671ADF5A…} /quiet /norestart` | none | `InstallError`, 1603, `0x8A150030`; the app said "The installer failed with error code 1603." |
| `winget upgrade` (CLI, `--silent`) | Silent | same | none | same: "Uninstall failed with exit code: 1603" |
| Uninstall (COM, as the app does it) | Silent | `/x{…} /quiet /norestart` | none | `UninstallError`, 1603, `0x8A150030` |
| Uninstall | Interactive | `/x{…}` | after Windows Installer's own dialog | Ok |
| Update to 3.13 | Default | `/x{…} /passive /norestart`, then the MSI with `/passive … ALLUSERS=1` | one prompt | Ok: 3.12 removed, 3.13 installed, one entry |
| Uninstall | Default | `/x{…} /passive /norestart` | one prompt | Ok |

Windows Installer's event log says why: "Error 1730. You must be an Administrator to remove this application." With
`/quiet`, Windows Installer can't ask for consent, so removing a product installed for all users fails. Pandoc's
manifest has `UpgradeBehavior: uninstallPrevious`, so its updates start with that removal, and the app's
**Uninstall** button hits it directly. `0x8A150030` is `APPINSTALLER_CLI_ERROR_EXEC_UNINSTALL_COMMAND_FAILED`:
WinGet's uninstall command failed, not the new version's installer. In Default mode WinGet passes `/passive`: a
small progress window, and Windows' UAC prompt. That's also what `winget` does without `--silent`.

- **US7.** As a user, I want to update and uninstall a package that's installed for all users, with Windows asking
  for my consent as for any installer, instead of a failure.

| ID   | Story | Criterion |
|------|-------|-----------|
| R8.1 | US7   | When the app updates, uninstalls or goes back (014) on a package whose installed version is an MSI product that Windows Installer has installed for all users (014 R3.8's check), it shall run WinGet in its Default mode instead of Silent, so that Windows shows its UAC prompt and the installer's progress window. *(Q8.)* |
| R8.2 | US7   | Every other operation shall stay Silent, as today. |
| R8.3 | US7   | Before an operation that R8.1 applies to, nothing shall be asked beyond what the app asks today. The Activity card shall say while it runs that Windows may ask for permission ("Waiting for Windows to ask for permission…" or similar), since the UAC prompt can open behind the app. *(Q9.)* |
| R8.4 | US7   | If an update or uninstall fails with `0x8A150030`, then the result shall say that WinGet couldn't uninstall the installed version ({version}), name the uninstaller's exit code, and, when that code is 1603 or 1730 and R8.1 didn't apply, say that the package may be installed for all users and need administrator rights. It shall not say "the installer failed". **Retry** stays. |
| R8.5 | –     | The operation log shall record the mode the operation ran in (`Mode: silent` or `Mode: default, for an all-users MSI`), next to 014's "Scope" line. WinGet's status, exit code and extended error stay unchanged (R7.5). |

### Open questions (amendment R8)

- **Q8. Which operations get Default mode (R8.1)?** *Proposed: only those on an all-users MSI,* which the app can
  tell from Windows Installer (014's `MsiInstallContext`). Everything else stays silent, so nothing changes for
  most packages. The alternatives: Default mode for everything, as the `winget` CLI does (every installer would
  show its own progress window, and some, such as Inno and NSIS, may show more than a progress bar); or Silent
  first, then a **Retry with permission** button after a `0x8A150030` failure (one failure every time, before it
  works).
  **Decided 2026-11-10: only all-users MSIs.**
- **Q9. Saying that a prompt may come (R8.3).** *Proposed: on the Activity card while it runs,* since the prompt is
  Windows' own and can open behind the app. The alternative is a line in Update all's confirmation, which only
  covers one of the three ways in.
  **Decided 2026-11-10: on the Activity card.**
- **To check in design:** whether Default mode for an MSI ever shows more than a progress bar (some MSIs have their
  own dialogs in `/passive`); whether the app can tell an update starts with an uninstall (`UpgradeBehavior`) or
  should apply R8.1 to every update of an all-users MSI. The measurements above say the latter works for Pandoc.

## Out of scope

- Choosing a specific version, architecture or install location (possible later "advanced install" spec).
- Interactive installer mode (always silent in v1). *Amendment R8 uses WinGet's Default mode for all-users MSIs only;
  Interactive mode stays out.*
- Batch install from a list or file (possible later "import/export" spec).

## Open questions

- Windows toast when minimized? **Decided: in-app only for v1.** *Operations still are. Spec 010 adds a notification for background update checks (010 R2.1).*
