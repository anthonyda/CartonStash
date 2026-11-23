# 020 – Uninstall, then update: Requirements

**Status:** Done (2026-11-23). Approved (2026-11-23), with the UI proposal; Q1, Q3 and Q6 answered by the maintainer, Q2, Q4 and Q5 as proposed. Q7 is measured in T1
**Depends on:** 003 (queue, uninstall, results), 004 (updates), 009 (Activity card, logs), 013 (R1.2, R3.1), 014
(R3.5 to R3.8: the uninstall-then-install chain this reuses), 017 (a follow-up button on a failed result)
**Amends:** 013's out-of-scope lines "An **Uninstall and reinstall** action for R3.1" (deferred then as "a later
amendment can add it") and "Repairing a damaged install, such as the EA app's missing uninstaller" (R5 offers a
reinstall, not a repair)

## Problem

Some updates can't finish, and the result tells the user to uninstall or reinstall the package by hand. The
maintainer's PC shows two cases every time Update all runs (operation logs, 2026-11-12 to 2026-11-19):

| Package | WinGet's result | What the app says today (013) |
|---------|-----------------|-------------------------------|
| **Miniconda3** (`Anaconda.Miniconda3`, py312_24.11.1-0 → py314_26.9.4-1), 6 times | `CatalogError`, exit code 0, extended error `0x8A150114` (`INSTALL_UPGRADE_NOT_SUPPORTED`): the manifest says the update can't run over the installed version | R3.1: "… can't be updated in place: its publisher asks for it to be uninstalled first. To get the new version, uninstall it, then install it again." No Retry |
| **EA app** (`ElectronicArts.EADesktop`, 13.813.1.6327 → 13.832.0.6345), 7 times | `InstallError`, exit code 0, extended error `0x800401F5` (`CO_E_APPNOTFOUND`): WinGet runs the installed version's uninstaller before the new installer, and `EAUninstall.exe` is gone (`ShellExecuteInstallerHandler.cpp(353)`) | R1.2: "WinGet couldn't start a program needed to update EA app: it wasn't found (0x800401F5). Repairing or reinstalling EA app from its publisher may fix this." Retry, which fails the same way |

Both leave the user to do it outside the app, and the row comes back in Updates after every check. They need different
fixes: Miniconda needs an uninstall first, while EA app *can't* be uninstalled (its uninstaller is the missing file),
so for it only installing the new version over the old one can help.

The app already chains an uninstall and an install for going back to an older version (014 R3.5): one confirmation,
the install only if the uninstall succeeded, and **Install {previous version}** if the install fails. R1 to R4 reuse it.

## User stories

- **US1.** As a user whose update can't run over the installed version, I want one action that uninstalls the package
  and installs the new version, so that I get the update without three steps by hand.
- **US2.** As a user, I want to be told what an uninstall may cost before anything is removed, and to be able to get
  back to the version I had if the new install fails.
- **US3.** As a user whose update fails because the installed copy is damaged, I want to install the new version over
  it, so that the package works and updates again.

## Acceptance criteria (EARS)

### Uninstall, then update (Miniconda)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | If an update fails with a result that asks for an uninstall first (R1.2), then the result shall offer **Uninstall, then update** on its Activity card and on the package's row in Updates, in place of Retry (which stays hidden, 013 R3.1). Only the package's latest card offers it, and R5.1's button: once another operation for the package is queued, an older card's offer would describe a package that has changed. *(Added 2026-11-23 after T11: after the chain, the failed update's card still offered to uninstall.)* |
| R1.2 | –     | A result "asks for an uninstall first" when WinGet's extended error is `0x8A150114`, whatever WinGet's status (013 R3.1; measured: Miniconda3). Further codes are added only from measured cases, as 017 R1.2 does. Nothing else is guessed (P1). |
| R1.3 | US1   | 013 R3.1's message shall end by naming the action: "… uninstall it, then install it again. Uninstall, then update does both." |
| R2.1 | US2   | When the user presses it, the app shall ask for confirmation naming the package, the installed version and the version the update would install, and saying that settings and data the program keeps may be lost (apps that come as an MSIX package always lose them), as 014 R3.5 does. It shall queue nothing unless the user confirms (P2). |
| R2.2 | US1   | If the user confirms, the app shall uninstall the package (the row's own entry, 002 R6.5) and then, only if the uninstall succeeds, install the version the update would have installed, not whatever is newest by then. Both go through the queue, one at a time, never the same package twice (003 R4.1, R4.2). |
| R2.3 | US1   | The install shall use the scope the package was installed with (for the current user or for all users), as 014 R3.8 does, so the package comes back as it was. Only if WinGet didn't know that scope shall the install scope setting apply (006 R2.1). |
| R2.4 | US1   | The chain shall run with the user's rights, as the failed update did. It shall not ask for administrator rights on its own; if a step needs them, 017 applies to that step as to any other. *(Q3, maintainer: no.)* |
| R2.5 | US1   | After the uninstall reports success, the app shall install only once WinGet no longer lists the package as installed, checking every 2 seconds for up to 5 minutes, then a 5-second pause; meanwhile the uninstall's card shows "Finishing…". If it is still listed after 5 minutes, then the app shall install nothing, and the uninstall's result shall say: "The uninstaller finished, but Windows still lists {name} 5 minutes later.", followed by R3.1's sentence. The same wait applies to 014 R3.5's chain, which has the same uninstall step. *(Added 2026-11-23 during T2: Miniconda3's uninstaller returns `Ok` in 3 s and a copy of it (`Un_A.exe`) keeps removing files; WinGet stops listing the package 15 s later and the copy exits 2 s after that. An install started at once failed with exit code 2, `0x8A150006`; started after the wait, it succeeded.)* |
| R3.1 | US2   | If the uninstall fails or is cancelled, then the app shall install nothing, and the result shall say why, as for any uninstall (003 R5.2, 003 R7). The package is still installed at its old version. |
| R3.2 | US2   | If the install fails or is cancelled after the uninstall succeeded, then the result shall say that {name} is no longer installed, and offer **Install {previous version}**, the version that was installed before (014 R3.7). |
| R3.3 | US1   | When the install succeeds, the result shall read as an update, "Updated to {version}.", and the row shall leave Updates. *(Amended 2026-11-23 during T7: was "with 013 R2's check of the installed version". That check looks for the old version still listed after an update; after the uninstall nothing was listed before the install, so it can't apply. Install over it (R5) is an update and keeps it.)* |

### Install over it (EA app)

| ID   | Story | Criterion |
|------|-------|-----------|
| R5.1 | US3   | If an update fails because WinGet couldn't start the installed version's uninstaller (extended error `0x800401F5` or `0x80070002` during an update, 013 R1.2; measured: EA app), then the result shall offer **Install over it** on its Activity card and on the row, next to Retry. |
| R5.2 | US3   | 013 R1.2's message for an update shall then read: "{name}'s update couldn't start: the uninstaller of the installed version is missing. Install over it installs {version} without removing the installed one first." |
| R5.3 | US3   | When the user presses it, the app shall ask for confirmation naming the package and the version, and saying that nothing is uninstalled first. If the user confirms, the app shall install that version with the installed scope (R2.3), without running the installed version's uninstaller. |
| R5.4 | –     | R5 is offered only if the design's T1 shows that WinGet's COM API can run an install that skips the old uninstaller for EA app. If it can't, R5 is dropped and 013 R1.2 stays as it is. Nothing else may run the publisher's installer outside WinGet (P1). |

### Both

| ID   | Story | Criterion |
|------|-------|-----------|
| R4.1 | –     | **Update all**, the background checks and the notification's actions shall never uninstall or install over on their own: only the user's press and confirmation do (P2). |
| R4.2 | –     | Each operation keeps its own log (009 R2). The install that follows an uninstall says so in its log, and the Activity card shows both steps as 014's chain does. |
| R4.3 | –     | The new buttons follow the card's other buttons: reachable by Tab, accessible names "Uninstall {name}, then update to {version}" and "Install {name} {version} over the installed one" (Q5). Light, dark and high contrast. |

## Out of scope

- Offering either action after any failed update. Most installer failures aren't fixed by an uninstall, and an
  uninstall can remove user data (013's Miniconda environments).
- Uninstalling first on its own, before the update is tried. WinGet's `UpgradeBehavior: uninstallPrevious` in a
  manifest already does that without the app.
- Keeping or restoring the program's settings and data across the uninstall.
- WinGet's `repair` command.
- Fixing the manifests (Miniconda's upgrade behaviour, EA app's uninstaller). For `microsoft/winget-pkgs`.

## Open questions

- **Q1. Which failures offer it?** *Answered 2026-11-23:* the codes the app catches today and the maintainer sees:
  `0x8A150114` (Miniconda, R1) and `0x800401F5` / `0x80070002` on an update (EA app, R5). Each needs its own action;
  see the Problem.
- **Q2. Which version does the install take?** *Proposed:* the one the failed update targeted (R2.2, R5.3), so the
  confirmation is true. If WinGet no longer has it when the install runs, the install fails and R3.2 applies.
- **Q3. Elevated chain.** *Answered 2026-11-23:* no; the chain keeps the user's rights and the installed scope (R2.3,
  R2.4).
- **Q4. Exclusions.** A package left out of update checks (010 R5) isn't in Updates, so nothing to carry. *Proposed:*
  no box in the confirmation.
- **Q5. Button text and look.** See the UI proposal, https://claude.ai/artifact/ETzZwt1k6qDum2Y8AJsggq (2026-11-23). *Proposed:* **Uninstall, then update** and
  **Install over it**, accent style on the card as Retry as administrator is.
- **Q6. Test cases.** *Answered 2026-11-23:* the two measured cases, in the VM with 013's recipes: Miniconda3
  `py312_24.11.1-0` then update (R1 to R3), and EA app with its ARP version set back and `EAUninstall.exe` deleted
  (R5). The maintainer's PC isn't used for tests.
- **Q7. R5 in WinGet (T1).** Does `InstallPackageAsync` on an installed package with `InstallOptions.Force` (or an
  install of the exact version) skip the old uninstaller for EA app? *Measured 2026-11-23 (T1):* yes, without `Force`:
  WinGet runs EA's new installer directly. R5 is kept. Not measured: what that installer does over a really old EA app
  bundle, which the VM can't reproduce (tasks T1, T13).
