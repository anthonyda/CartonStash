# 003 – Install / uninstall with progress: Requirements

**Status:** Draft
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

## Out of scope

- Choosing a specific version, architecture or install location (possible later "advanced install" spec).
- Interactive installer mode (always silent in v1).
- Batch install from a list or file (possible later "import/export" spec).

## Open questions

- Show a Windows toast notification when the app is minimized? *Proposed: in-app only for v1.*
