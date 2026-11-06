# 003 – Install / uninstall with progress: Requirements

**Status:** Done (2026-10-30). Amendment R7 (say when a program is running): done 2026-11-06
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

## Out of scope

- Choosing a specific version, architecture or install location (possible later "advanced install" spec).
- Interactive installer mode (always silent in v1).
- Batch install from a list or file (possible later "import/export" spec).

## Open questions

- Windows toast when minimized? **Decided: in-app only for v1.** *Operations still are. Spec 010 adds a notification for background update checks (010 R2.1).*
