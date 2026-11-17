# 014 – Install a chosen version: Requirements

**Status:** Done (2026-11-09). Approved 2026-11-08, with the proposed answers to Q1, Q2 and Q4; Q3 and Q5 decided after T1 (amendments R2.5, R3.3 to R3.7). **Amendment R1.6, R1.7 (2026-11-09): Done**, approved with the proposed answer to Q6. **Amendment R2.6, R2.7, R3.8, R3.9 (2026-11-10): Done**, approved 2026-11-10, Q7 with a notice to the user, Q8 as proposed. **Amendment R2.8 (going back from the newest version, bug review BR-02): approved 2026-11-12**. **Amendment R2.9 (the fresh read is of the row's own program): Done (2026-11-17)**, approved 2026-11-17, Q9: the row's own entry. **Amendment R3.10, R3.11 (a version chosen on a row that shares its ID): Proposed 2026-11-17**, Q10 and Q11 open
**Depends on:** 003 (operation queue, agreements, results), 004 (updates), 005 (details pane, R2.1 version list),
010 (R5 exclusions from update checks)
**Replaces:** 005's out-of-scope line "Installing a specific (older) version. The version list is information only in v1."

## Problem

The details pane lists every version WinGet has for a package (005 R2.1), but nothing in the app can install one of
them: Install, Update and Update all always take WinGet's default, the latest. The list can't be acted on. The common
reason to look at it is a bad update: the user wants the version that worked, and today that needs
`winget install --id X --version 1.2.3` in a terminal.

## What WinGet offers (checked 2026-11-07 in the COM API 1.29 metadata)

- `CatalogPackage.AvailableVersions` (the list the pane already shows) and `GetPackageVersionInfo` for one version.
- `InstallOptions.PackageVersionId` installs or upgrades to that version instead of the default.
- `InstallOptions.Force` exists.
- **Measured in T1 (2026-11-08, test VM):** an install of an older version over the installed one goes back for
  portable and NSIS packages. MSI, Inno and MSIX installers refuse it, and the refusal leaves the installed version
  as it was. All of them go back after an uninstall, except Windows Terminal, which ships with Windows. `Force`
  changes nothing. See design.md, "Going back".

## User stories

- **US1.** As a user, I want to install a specific version of a package from its details, so I don't need the command line.
- **US2.** As a user, after a bad update, I want to go back to the version I had, and not have the app bring the bad one back.
- **US3.** As a user, I want to update to a version that isn't the latest, for example to skip a broken release.

## Acceptance criteria (EARS)

### R1. Choosing a version

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1, US2, US3 | In the details pane, each version in the "Available versions" list other than the installed one shall offer an action: **Install** when the package isn't installed, **Update to this version** when it is newer than the installed one, **Go back to this version** when it is older. |
| R1.2 | US1   | Whether a version is newer or older shall come from WinGet's version comparer, never from comparing the strings (constitution P1). If WinGet can't compare it with the installed version, then the action shall be **Install this version** and R3.1 shall apply, as for an older one. |
| R1.3 | US1   | The list shall show the newest 10 versions, with **Show all versions** to see the rest. The installed version stays marked (005 R2.1). |
| R1.4 | –     | Every version action shall be keyboard reachable and have an accessible name with the package and the version, for example "Go back to PowerToys 0.101.0" (constitution Q5). |
| R1.5 | US1   | The actions shall not be offered for a package whose source lists only one version (for example the Microsoft Store source), for an installed package WinGet doesn't manage, or while the package is queued or being changed (003 R4.2). The list itself stays. |
| R1.6 | US1, US2 | The version action shall be visible next to the selected version, wherever that version is in the list, without scrolling past the rest of the list. *(Added 2026-11-09, amendment: the button sat under the list, out of view below 10 versions or after **Show all versions**.)* |
| R1.7 | US2   | A package in the Updates page's **Excluded** list (010 R5.5) shall open the details pane when clicked or on Enter, like any row there (005 R1.1), so its versions can be acted on. *(Added 2026-11-09, amendment: going back with R3.2's box checked moves the package to that list, which didn't open details.)* |

### R2. Running it

| ID   | Story | Criterion |
|------|-------|-----------|
| R2.1 | US1, US3 | A chosen version shall install or update through the same queue as any operation, with the same progress, Cancel, result, notification and Retry (003, 004). The Activity card, the notification and the operation log shall name the version, for example "Installing PowerToys 0.101.0". |
| R2.2 | US1   | When the chosen version has license agreements, the app shall show that version's agreements, not the latest version's, and go ahead only if the user accepts (003 R1.3, 004 R7.1). |
| R2.3 | US1   | **Retry** shall install the same chosen version again, reusing the acceptance the user gave (004 R7.3). |
| R2.4 | –     | The row's Install and Update buttons, **Update selected** and **Update all** shall keep installing WinGet's default version, unchanged. |
| R2.5 | US1   | When the operation succeeds, the app shall refresh the installed data (002 R1.3) and show the version WinGet reports afterwards. If WinGet still lists the version that was installed before, then the result shall say so and shall not say the operation succeeded. *(Amended 2026-11-08 during design: was "if that isn't the chosen version". 013 found that installers often list a different version than the manifest's, so that test would warn after good installs.)* |
| R2.6 | US1, US2 | When an operation on a package succeeds, the Search page's row for it and an open details pane for it shall show the installed state WinGet reports afterwards: the installed version, the update badge, and each version's action (R1.1). *(Added 2026-11-10, amendment: after updating Pandoc 3.10 to 3.13 from Search, the row and the pane still said "Installed 3.10".)* |
| R2.7 | US2   | When the user chooses a version action on an installed package, the app shall read the installed version from WinGet again before asking anything. If it differs from the version the pane shows, then the app shall queue nothing and refresh the row and the pane as in R2.6, so the user chooses again from the right list. If it can't be read, then the app shall go on with the version it shows. *(Added 2026-11-10, amendment: the going-back log said "Installed version: 3.10" while 3.13 was installed. Whether a version is older or newer depends on the installed one, so the app doesn't go on with a list made for another version.)* |

### R3. Going back to an older version

| ID   | Story | Criterion |
|------|-------|-----------|
| R3.1 | US2   | When the user chooses **Go back to this version**, the app shall ask for confirmation naming the package, the installed version and the chosen one, and saying that some installers can't go back and that the program's settings may not carry over. It shall queue nothing unless the user confirms (constitution P2). |
| R3.2 | US2   | That confirmation shall offer **Leave this package out of update checks** (010 R5), checked by default, so that Update all and the background checks don't bring the newer version back. The exclusion shall be added only when the operation succeeds. |
| R3.3 | US2   | If going back fails with an installer error, then the result shall say that the installer may not install over a newer version, and suggest uninstalling the package and then installing the chosen version. WinGet's status and codes stay in the operation log (009 R2.2). *(Amended 2026-11-08 after T1: was "if WinGet or the installer refuses". The refusals use the installers' generic failure codes, so the app can't tell them from other failures.)* |
| R3.4 | US2   | The app shall uninstall the package as part of going back only when the user chooses R3.5's action and confirms it (constitution P2). *(Amended 2026-11-08, Q5: was "never … the user does it".)* |
| R3.5 | US2   | When going back fails (R3.3), the result shall offer **Uninstall, then install {version}**. When the user presses it, the app shall ask for confirmation naming the package, the installed version and the chosen one, and saying that the program's settings and data may be lost. If the user confirms, the app shall uninstall the package and then, only if the uninstall succeeds, install the chosen version. The R3.2 choice carries over. *(Added 2026-11-08, Q5.)* |
| R3.6 | US2   | If that uninstall fails, then the app shall install nothing, and the result shall say why, as for any uninstall (003 R5.2). *(Added 2026-11-08, Q5.)* |
| R3.7 | US2   | If the install fails after the uninstall succeeded, then the result shall say that the package is no longer installed, and offer **Install {previous version}**, the version that was installed before. *(Added 2026-11-08, Q5. T1: Windows Terminal ends this way.)* |
| R3.8 | US2   | When going back (or installing a version WinGet can't compare, R1.2) over an installed package, the app shall ask WinGet for the installer with the scope the package is installed with (for the current user or for all users), whatever the install scope setting (006 R2.1). Only if WinGet doesn't know that scope shall the setting apply. When the setting names a scope ("Current user" or "All users") and the package is installed with the other one, R3.1's confirmation shall say that {version} will be installed {for all users / for the current user}, as {name} is now, not as the setting says. *(Added 2026-11-10, amendment, Q7: going back to Pandoc 3.12 picked the per-user MSI while 3.13 was installed for all users, so Windows Installer added 3.12 next to it.)* |
| R3.9 | US2   | If going back succeeds and WinGet then lists more installed entries for the package than before, then the result shall be a warning, not a success. It shall say that {version} was installed next to {installed version}, not over it, that both are now installed, and that uninstalling {installed version} from the Installed page leaves only {version}. R3.2's exclusion shall not be added. *(Added 2026-11-10, amendment, Q8.)* |

## Amendment R1.6, R1.7 (2026-11-09, from use)

Two problems found after release, in the details pane and on the Updates page:

1. **The action is out of sight.** The one button sits under the list (design, UI). With 10 versions it's usually
   below the fold of the pane, and after **Show all versions** it's under 100 or more. The user selects a version and
   sees nothing happen. R1.6.
2. **Excluded packages can't be opened.** The Updates page's **Excluded** list has no selection and no click, so its
   packages never open the details pane. A package lands there after going back with R3.2's box checked, which is
   exactly when the user may want another version (go back further, or update to a middle one). R1.7.

The Installed page already opens excluded packages, so R1.7 only changes the Updates page. Settings' excluded list
(010 R5.4) stays a list of IDs with **Remove**: it has no rows to open.

- **Q6. Where the action goes (R1.6).** *Proposed: inside the selected item, right-aligned on its line* ("1.8.1 ·
  [Update to this version]"). It's where the user just clicked, and stays one tab stop (Tab from the selected item
  goes to its button), so R1.4 and the "80 tab stops" reason in the design still hold. The alternative is the button
  above the list, under the heading: simpler, but off screen again once the user scrolls into a long list.

## Amendment R2.6, R2.7, R3.8, R3.9 (2026-11-10, from use)

Found in the test VM with Pandoc (`JohnMacFarlane.Pandoc`), a WiX MSI whose manifests list two installers per
version: one for the current user (`Scope: user`, `ALLUSERS=2`) and one for all users (`Scope: machine`,
`ALLUSERS=1`).

1. **Going back installed a second copy.** Pandoc 3.10 updated to 3.13 from the Search page: WinGet ran the
   all-users installer (UAC prompt, `C:\Program Files\Pandoc`). Going back to 3.12 then called `InstallPackageAsync`
   with the scope setting, "Let WinGet decide". WinGet picked the per-user installer (`ALLUSERS=2`, no UAC,
   `%LOCALAPPDATA%\Pandoc`). Windows Installer keeps per-user and all-users products apart, so the 3.12 MSI didn't
   see 3.13: no "newer version is installed" refusal (T1's MSI result), just a second product. WinGet said Ok, the
   app said "Installed successfully" and added the exclusion. The Installed page then shows Pandoc 3.12 and Pandoc
   3.13; Search and Updates show one row, since they show one row per WinGet ID. R3.8 asks for the installed scope.
   *T18 (2026-11-10): with the matching scope, Pandoc's MSI finds 3.13 and replaces it, so going back works directly,
   one entry left. T18 also found that WinGet reports this MSI as installed for all users even when it's installed for
   the current user, so the design asks Windows Installer instead.* R3.9 says so when a second copy still happens,
   for installers R3.8 can't steer.
2. **The app went on with the old version.** After the update, the Search row and its details pane still showed
   "Installed 3.10", and the going-back operation carried 3.10 as the installed version: in the confirmation, in the
   log, and in R2.5's check, which compares with the version from before. Search rows keep the package as the search
   found it; only the Installed and Updates pages reload after an operation (002 R1.3). R2.6 and R2.7.

- **Q7. The scope when the setting names one (R3.8).** If the user chose "Current user" or "All users" in Settings
  and the package is installed with the other scope, which wins for going back? *Proposed: the installed scope.*
  Going back replaces what is installed; with the other scope it can only add a second copy, which is what R3.8 is
  there to stop. The setting keeps its meaning for new installs and updates (006 R2.1). The alternative is the
  setting, and R3.9's warning when it goes wrong.
  **Decided 2026-11-10: the installed scope, and the user is told.** R3.8's last sentence: the confirmation says so
  when it overrides a scope chosen in Settings.
- **Q8. After a second copy (R3.9).** *Proposed: a warning with what to do, and no button.* Which entry to remove is
  the user's choice, and the Installed page already uninstalls one entry (002 R6.5). The alternative is an
  **Uninstall {installed version}** button on the card, which would need its own confirmation (P2).
  **Decided 2026-11-10: as proposed.**

Not in this amendment, seen in the same runs: an update whose UAC prompt nobody answers shows "Updating…" with no
hint about the prompt, and WinGet has by then uninstalled the old version (Pandoc's `UpgradeBehavior:
uninstallPrevious`). That's WinGet's behaviour for any update, not 014's.

## Amendment R2.8 (2026-11-12, bug review BR-02, approved 2026-11-12)

R2.5 is not met when going back starts from the newest version.

**Example:** VLC 3.0.21 is installed, the newest version WinGet has, so the Updates page doesn't list it. In its
details you choose **Go back to this version** for 3.0.20. When the installer ends, the app checks whether it worked.
That check, `AfterUpdate`, was written for updates (013) and warns only when both are true:

1. Windows still lists the version from before, **and**
2. WinGet still offers an update.

- **Going back worked:** Windows lists 3.0.20 and WinGet offers 3.0.21. Condition 1 is false, so no warning. Correct.
- **The installer did nothing:** Windows still lists 3.0.21, so condition 1 is true. But VLC is still on the newest
  version, so WinGet offers no update and condition 2 is false. No warning: the app says "Installed successfully", and
  with **Leave this package out of update checks** ticked (the default) it also excludes VLC. Wrong.

So when going back starts from the newest version, the usual case, the check can never warn. R2.5 says it should: if
WinGet still lists the version from before, the result shall say so and not claim success.

And when the warning does fire for a go-back (going back from a version that isn't the newest), its words are about
updating: "Update not confirmed" and "may update itself the next time it starts".

| ID   | Story | Criterion |
|------|-------|-----------|
| R2.8 | US1   | After going back (an install of a chosen version over an installed package), R2.5's check shall compare the installed version alone: if WinGet still lists the version from before, the result shall be a warning, whether or not WinGet offers an update. Its card status shall read **Not confirmed**, and its message shall say that Windows still lists {name} {version before} and that going back to {chosen version} may not have worked. R3.2's exclusion isn't added, as for any warning. |

Updates keep 013's rule unchanged: there, an unchanged version with no update offered can be a good update whose
installer lists another version.

## Amendment R2.9 (2026-11-17, from use, approved 2026-11-17)

R2.6 and R2.7 read "the installed state WinGet reports" by **WinGet ID**. When two installed programs share one ID
(002 R6.5), that read returns WinGet's own match, which can be the *other* program. The row then takes on the other
program's name, version and badge.

**Example (screen recording 2026-11-17, reproduced in the VM).** FireDaemon publishes OpenSSL 3 and OpenSSL 4 under one
ID, `FireDaemon.OpenSSL`, as two MSI products (different UpgradeCodes, `UpgradeBehavior: install`), so they install side
by side; plain `winget upgrade` from 3.6.2 to 4.1.0 leaves both. The Installed page rightly shows two rows: "FireDaemon
OpenSSL 3", Installed 3.6.2, and "FireDaemon OpenSSL 4", Installed 4.1.0. In the OpenSSL 3 row's details, the user
chooses **Install this version** on 4.1.0:

1. R2.7 reads the installed state by ID. WinGet's match is OpenSSL 4, so the read says "Installed 4.1.0".
2. 4.1.0 differs from the 3.6.2 the pane shows, so the app queues nothing and refreshes the row with that read.
3. The OpenSSL 3 row now reads "FireDaemon OpenSSL, Installed 4.1.0, same as winget", still under the **Other** chip;
   its pane says the same. Nothing appears in Activity. OpenSSL 3 is still installed, 3.6.2. Only a reload of the list
   (Refresh) brings the right row back.

R2.6's read after a successful operation has the same fault for any row that shares its ID with another program.

| ID   | Story    | Criterion |
|------|----------|-----------|
| R2.9 | US1, US2 | The fresh read of R2.6 and R2.7 shall be of the row's own installed program (002 R6.5). When WinGet's match for the package's ID is another installed program, the app shall read the row's own program as Windows lists it (its name and version), and R2.6 and R2.7 shall use that; the row keeps its source and what it shows about the available version. If the row's own program can't be found, the read shall count as one that couldn't be made. The read shall never give a row another program's name, version or state. |

Not in this amendment: OpenSSL 3 and 4 installing side by side. That's how the package is published (the VM run
reproduced it with `winget upgrade` alone), and 014 leaves several versions side by side out of scope.

- **Q9. A read that can't be made, or the program's own version?** When WinGet's match is another program, the app
  can either count the read as failed, or read the row's own entry from the installed-only catalog and use its
  version. Counting it as failed skips R2.7's check for every row that shares an ID, so a change made outside the app
  goes unnoticed, and R2.6 leaves the row stale until the next reload. Reading the own entry compares like with like:
  the rows that reach this case are manifest matches, which already show Windows' version (002 R6.2); a primary-match
  row finds itself in WinGet's answer. Mockups of both: https://claude.ai/artifact/8H1jJxziDK1LWjTL7NLPaU.
  **Decided 2026-11-17: read the row's own entry.**

## Amendment R3.10, R3.11 (2026-11-17, from use, proposed)

After R2.9, in the same OpenSSL case: the OpenSSL 3 row keeps its own data and 4.1.0 is queued, but what the app says
around that install is about the wrong program.

**Example (the maintainer, 1.6.0, 2026-11-17).** OpenSSL 3.6.2 and 4.1.0 installed side by side. In the OpenSSL 3 row's
details, **Install this version** on 4.1.0. The row is a manifest match (002 R6.3), so its direction is `Unknown`
(design, "WinGet API usage": only a primary match gets one), and `Unknown` gets the going-back confirmation (R1.2,
R3.1):

> **Install FireDaemon OpenSSL 3 4.1.0?** FireDaemon OpenSSL 3 3.6.2 is installed. Installing 4.1.0 runs that
> version's installer over it. Some installers can't go back, and FireDaemon OpenSSL 3's settings may not carry over to
> an older version.

with **Leave FireDaemon OpenSSL 3 out of update checks**, checked. After the install, OpenSSL 3 is still there.

1. **It promises a replacement that the package doesn't do.** OpenSSL 3 and 4 are different MSI products (T26), so
   4.1.0's installer goes in next to 3.6.2, as plain `winget upgrade` does. For a row WinGet doesn't evaluate itself,
   the app can't know whether the chosen version replaces the row's program, and must not say it will.
2. **It talks about an older version.** 4.1.0 is newer than 3.6.2. The app gave no direction on purpose (002 R6.4),
   but the going-back text then implies one.
3. **The box excludes the other program.** Exclusions are by WinGet ID (010 R5). `FireDaemon.OpenSSL`'s updates are
   OpenSSL 4's, WinGet's own match, so the box ticked by default would stop update checks for OpenSSL 4, a program the
   user didn't touch.
4. **The installer checks the other program.** `WinGetPackageInstaller.RunAsync` finds the package by ID, so for this
   row the installed version it works from is WinGet's match, OpenSSL 4: R3.8's scope, R3.9's "was the previous one
   replaced?" products and R2.8's after-check (`InstalledAfterUpdateAsync`) are all OpenSSL 4's, the same fault R2.9
   fixed for the fresh read. Here the scope is right by chance (both are machine-wide), and the warnings after the
   install can come out wrong either way.

| ID    | Story    | Criterion |
|-------|----------|-----------|
| R3.10 | US1      | When the user chooses a version on a row that isn't WinGet's own match for its ID (002 R6.3, R6.4), R3.1's confirmation shall name the package, its installed version and the chosen one, and say that {name} shares its WinGet ID with another program, so the app can't tell whether {version} replaces {name} {installed} or installs next to it, and that if both stay installed, {name} {installed} can be uninstalled from the Installed page afterward. It shall not say the version is older, nor that it runs over the installed one, and shall not offer R3.2's box. R3.8's notice still applies. Rows that are WinGet's own match keep R3.1 and R3.2 unchanged, including a version WinGet can't compare (R1.2). |
| R3.11 | US1, US2 | When a chosen version is installed over an installed package, the installed program the app works from (R3.8's scope, R3.9's products, and R2.5 and R2.8's check after the install) shall be the row's own program (002 R6.5), as for R2.9's read. If it can't be found, the app shall go on as when the scope or the check can't be read (R3.8: the setting; R2.5, R3.9: no warning). |

After the install, R3.9 then says what happened when both stay: "{version} was installed next to {installed}, not over
it… To keep only {version}, uninstall {name} {installed} from the Installed page." That text is unchanged.

- **Q10. Predict it?** WinGet's manifest for the chosen version lists its installers' product and upgrade codes
  (`AppsAndFeaturesEntries`), and Windows Installer knows the installed program's, so for MSI packages the app could
  sometimes say "will install next to" instead of "can't tell". *Proposed: no, R3.10's "can't tell" only.* Many
  manifests leave the codes out, the COM API exposes product codes but not upgrade codes, and a wrong prediction is
  worse than an honest "can't tell" that R3.9 settles after the install.
- **Q11. The box (R3.10).** *Proposed: not offered on these rows.* The ID's updates belong to WinGet's own match, so
  excluding it hides another program's updates. The alternative is to show it unchecked, with that said in its text.

## Out of scope

- Choosing a version from a row, from the Updates page or for **Update all**. They keep using the latest (R2.4).
- Several versions of one package side by side. *(R3.8 and R3.9 are about not creating them by accident.)*
- Release notes for the chosen version. The pane keeps showing the latest version's notes (012).
- Pins (008). R3.2 uses the existing exclusions instead, which work today and need no WinGet command line.
- Typing a version that isn't in the list, or a version range.

## Open questions

- **Q1. Where the actions live.** *Proposed: on the version list in the details pane (R1.1),* because that's the list
  the user is already reading. The alternative is a **Choose version…** item on the Install and Update buttons, which
  opens a picker. It would work from rows too, but adds a second place to show versions.
- **Q2. Exclusion checked by default (R3.2).** *Proposed: yes.* Without it, the next Update all reinstalls the version
  the user just left, which is the surprise P2 rules out. The box is visible in the dialog, so it can be unchecked.
- **Q3. `Force` (see "What WinGet offers").** If WinGet refuses an older version unless `Force` is set, should the app
  set it? *Proposed: decide after the VM check.* `Force` also lets WinGet go on past other non-security warnings, so
  the design has to say exactly what it skips.
  **Decided 2026-11-08 after T1: no.** `Force` changed nothing for any installer type.
- **Q4. Overlap with 013 (honest results).** R2.5 checks that WinGet afterwards reports the chosen version. 013, now
  done, already warns when an update reported Ok but the old version is still listed ("Update not confirmed").
  *Proposed:* R2.5 reuses 013's check and its warning, compared against the chosen version instead of the latest.
- **Q5. Uninstall first (from T1).** Going back directly only works for portable and NSIS packages. For MSI, Inno and
  MSIX packages the user would have to uninstall and then install the older version, which R3.4 leaves to them.
  *Proposed:* when going back fails (R3.3), the result offers **Uninstall, then install {version}**. It asks first,
  saying that the program's settings may be lost, which is certain for MSIX apps: Windows deletes their data. If the
  install then fails, the result says the package is no longer installed and offers to install the version that was
  there before. This replaces R3.4 with "only after the user confirms". The alternative is to keep R3.4 and the
  manual route.
  **Decided 2026-11-08: offer it.** R3.4 amended, R3.5 to R3.7 added.
