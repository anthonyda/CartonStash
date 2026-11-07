# 013 – Honest operation results: Requirements

**Status:** Approved (2026-11-07)
**Depends on:** 003 (R5.2 readable reasons, R7), 004 (R2.4, R6.1), 009 (R2.2 operation log)

## Problem

Three updates on 2026-11-04 to 2026-11-07 ended with a result that didn't match what happened. The operation logs
(009 R2.8) show the real cause each time, but the message the app shows is wrong or useless:

- **EA app** (`ElectronicArts.EADesktop`, 13.813.1.6327 → 13.822.0.6335). Since 2026-11-05 every attempt shows "The
  installer failed with error code 0." WinGet's status is `InstallError`, its extended error is `0x800401F5`
  (`CO_E_APPNOTFOUND`, "Application not found"), and its log shows the failure in `ShellExecuteInstallerHandler` with no
  `Starting:` line: no installer ran, so the exit code is just the default 0. The likely cause is on the PC, not in the
  app. On 2026-11-04 WinGet ran the old version's uninstaller first (the manifest's upgrade behaviour), which deleted
  `EAUninstall.exe` but left its entry in Windows' installed programs. Each later attempt points WinGet at that missing
  file.
- **Autodesk Fusion** (`Autodesk.Fusion`, 2706.1.14 → 2706.1.17). Five attempts, each "Updated successfully". WinGet's
  status is `Ok` and the installer exited 0 after about 4 seconds, but WinGet logged "0 changes to ARP were observed",
  Windows still lists 2706.1.14, and the update is offered again on the next check. 004 R2.4 ("the package shall leave
  the Updates list") isn't met, and the app claims a success it can't show.
- **Miniconda3** (`Anaconda.Miniconda3`, py312_24.11.1-0 → py314_26.9.4-0). Five attempts, each "Couldn't reach the
  package source. Check your internet connection." WinGet's status is `CatalogError`, but its extended error is
  `0x8A150114` (`APPINSTALLER_CLI_ERROR_INSTALL_UPGRADE_NOT_SUPPORTED`) and its log says "Manifest specifies update
  behavior is denied". The network was fine: the new manifest doesn't allow an in-place update.

3 of 3 cases break constitution P3 (a clear outcome), and the Fusion case breaks P1 (the app reports more than WinGet
can show).

## User stories

- **US1.** As a user, when an operation fails before any installer has run, I want to see the real error instead of
  "error code 0", so I know the installer wasn't the problem.
- **US2.** As a user, when an update reports success but the package still has its old version, I want to be told, so I
  don't think it's done and I understand why it's offered again.
- **US3.** As a user, when a package can't be updated in place, I want to be told that and what to do instead, not
  sent to check my internet connection.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | If an install, update or uninstall fails with `InstallError` or `UninstallError`, the installer's exit code is 0 and WinGet reports an extended error, then the result shall show that extended error as hex, with Windows' description of it when Windows has one (for example "Application not found (0x800401F5)"), instead of "error code 0". |
| R1.2 | US1   | If the extended error for R1.1 is `0x800401F5` (`CO_E_APPNOTFOUND`) or `0x80070002` (`ERROR_FILE_NOT_FOUND`), then the result shall say that WinGet couldn't start a program the operation needed because it wasn't found, and that repairing or reinstalling the package from its publisher may help. |
| R1.3 | US1   | If the exit code isn't 0, then the result shall stay as it is today: "The installer failed with error code …" (003 R5.2). The in-use messages (003 R7) still come first. |
| R2.1 | US2   | When WinGet reports success for an update, the app shall read the package's installed version again. |
| R2.2 | US2   | If the installed version read in R2.1 is still the version from before the update, and WinGet still reports an update for it, then the result shall be a warning, not a success: it shall say that WinGet finished without an error but Windows still lists the old version, and that the installer may still be finishing or the program may update itself when it next starts. The Activity card, the notification and the operation log shall not say "Updated successfully". |
| R2.3 | US2   | If the installed version can't be read for R2.1, or it changed, or WinGet no longer reports an update, then the result shall stay a success. The app doesn't guess (constitution P1). |
| R2.4 | US2   | App Installer keeps its own check and message (004 R6.1). R2.1 to R2.3 shall not apply to it. |
| R2.5 | US2   | R2.1 to R2.3 shall not apply to installs or uninstalls. |
| R3.1 | US3   | If an update fails and WinGet's extended error is `0x8A150114` (`INSTALL_UPGRADE_NOT_SUPPORTED`), then the result shall say, whatever WinGet's status, that the package can't be updated in place and that it can be uninstalled and then installed again to get the new version. |
| R3.2 | US3   | The app shall not uninstall or install anything for R3.1 by itself (constitution P2). |
| R4.1 | –     | The operation log shall keep WinGet's status, exit code and extended error unchanged (009 R2.2). Only the message shown changes, and for R2.2 the result. |
| R4.2 | –     | Every other result shall read as it does today. |

## Out of scope

- Repairing a damaged install, such as the EA app's missing uninstaller. That's on the PC, not something the app should
  touch.
- An **Uninstall and reinstall** action for R3.1. See open questions.
- Marking packages on the Updates page that can't be updated in place, or that keep coming back (004 R3.1, deferred).
- Hiding or skipping one version of a package (pins, spec 008).
- Fixing the WinGet manifests (Fusion's `--globalinstall` on a per-user install, Miniconda's upgrade behaviour). That's
  for winget-pkgs; we may report them there.
- Other `CatalogError` results with a different extended error: they keep today's internet message.

## Open questions, decided 2026-11-07 as recommended

- **R2.2 wait before reading the version again?** Some installers hand off to a background process and update Windows'
  list a few seconds later (EA's installer exited 0 in a second on 2026-11-04). Recommended: no wait, read once. A
  fixed delay slows every update and still misses slow ones. The warning already says the installer may still be
  finishing, and the next refresh corrects the row.
- **R2.2 keep Retry?** Recommended: no Retry, as for any warning. Retrying runs the same installer, which did nothing
  five times for Fusion.
- **R3.1 offer an "Uninstall and reinstall" button?** Recommended: not now. It's two operations, the uninstall needs its
  own confirmation (P2), and an uninstall can remove user data (Miniconda environments). Saying what to do is enough for
  a first step. A later amendment can add it.
- **R3.1 Retry?** Recommended: hide it. WinGet will refuse the same update every time.
- **R1.2 wording?** Recommended, for an update: "WinGet couldn't start a program needed to update EA app: it wasn't found
  (0x800401F5). Repairing or reinstalling EA app from its publisher may fix this." For an uninstall, "to uninstall".
- **To check in design:**
  - Whether Windows has a description for the extended error (for example `Win32Exception(hresult).Message`) and
    that it returns nothing useful for WinGet's own `0x8A15…` codes, so those show as hex only.
  - How to read the installed version again after an update. 004 R6.1 reloads the installed entry by its ID, but for
    an ARP entry the ID doesn't hold the version.
  - Packages installed side by side, where WinGet may keep listing the old version after a good update.
  - Whether WinGet still reports an update for Miniconda on the Updates page, or hides it as the CLI may do.
