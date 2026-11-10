# 015 – Choose the install scope per install: Requirements

**Status:** Approved (2026-11-10), with the proposed answers to Q1, Q2 and Q3
**Depends on:** 003 (install and its queue), 004 (updates), 005 (details pane), 006 (R2 install scope setting),
014 (version list; amendment R3.8, which reads how a package is installed)

## Problem

The install scope is one setting for every install (006 R2.1), "Let WinGet decide" by default. Many packages offer
only one installer, so it rarely matters. Some offer two, one for the current user and one for all users. Pandoc's
manifests are an example: a per-user MSI (`Scope: user`) and an all-users one (`Scope: machine`). To install one of
those for all users once, the user has to change Settings, install, and change it back. Nothing in the app says how
an installed package is installed, either. Spec 014's amendment found that this matters: going back with the wrong
scope installed a second copy next to the first.

## User stories

- **US1.** As a user, I want to install a package for all users, or only for me, from its details, without changing
  my default in Settings.
- **US2.** As a user, I want to see whether an installed package is installed for all users or for me, so I know
  what an update or going back will touch.
- **US3.** As a user, I want an update to keep a package the way it's installed, so it doesn't move to all users (and
  ask for UAC) on its own.

## Acceptance criteria (EARS)

### R1. Choosing the scope for one install

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When the details pane shows a package that isn't installed, and WinGet has an installer for this PC for each scope (current user and all users) for the version being installed, the Install action shall offer **Install for all users** and **Install for the current user** besides **Install**. **Install** keeps using the setting (006 R2.1). |
| R1.2 | US1   | Which scopes are offered shall come from WinGet's applicable installers for that version, never from the manifest's text or the package type (constitution P1). If WinGet offers only one scope, or can't say, then only **Install** shall be shown, as today. |
| R1.3 | US1   | R1.1 shall apply both to the pane's Install button (the default version) and to a version's **Install** action in the version list (014 R1.1). |
| R1.4 | US1   | When the user chooses **Install for all users**, the app shall say, before anything is queued or with the choice itself, that the installer may show a UAC prompt (as 006 R2.2 does for the setting). |
| R1.5 | US1   | The chosen scope shall apply to that operation only: the setting doesn't change. **Retry** shall use the same scope. The Activity card shall name it ("Install Pandoc for all users"), and the operation log shall record it. |
| R1.6 | –     | Each choice shall be keyboard reachable and have an accessible name with the package and the scope, for example "Install Pandoc for all users" (constitution Q5). |
| R1.7 | –     | Rows on the Search page, **Update selected** and **Update all** shall not offer the choice: they keep using the setting (R3 for updates). |

### R2. Seeing how a package is installed

| ID   | Story | Criterion |
|------|-------|-----------|
| R2.1 | US2   | The details pane shall show, under the installed version, "Installed for all users" or "Installed for the current user", from the same source as 014 R3.8 (Windows Installer for MSI products, then WinGet). |
| R2.2 | US2   | If neither knows the scope, then the pane shall show nothing about it. It shall not guess from the install folder (constitution P1). |

### R3. Updates keep the installed scope

| ID   | Story | Criterion |
|------|-------|-----------|
| R3.1 | US3   | When the app updates an installed package (Update, Update selected, Update all, a version's **Update to this version**), it shall ask WinGet for the installer with the scope the package is installed with, read as in R2.1. Only if that scope isn't known shall the setting apply. *(Q1.)* |
| R3.2 | US3   | When R3.1 overrides a scope chosen in Settings, the operation's result and log shall say so ("Updated for the current user, as Pandoc is installed"), as 014 R3.8 does in its confirmation. Updates have no confirmation, so the result says it. *(Q2.)* |

## Out of scope

- Moving an installed package from one scope to the other. That's an uninstall and an install, which the user can do.
- Choosing the install location (`--location`).
- A scope choice on rows, in **Update all** or on the Updates page (R1.7).
- Packages from the Microsoft Store source: their scope is the Store's.

## For the design

- **Measure first.** `PackageVersionInfo.GetApplicableInstaller(InstallOptions)` with each `PackageInstallScope`
  looks like the way to ask WinGet which scopes it can install (R1.2). The design starts with a VM check of what it
  returns for Pandoc (two scopes), a package with one installer, and one with only a machine installer.

## Open questions

- **Q1. Should updates keep the installed scope (R3.1)?** *Proposed: yes.* In 014's VM run, WinGet updated a per-user
  Pandoc 3.10 with the all-users installer: UAC prompt, a new folder in Program Files, and the per-user copy
  uninstalled first. T18 showed why: WinGet reads Pandoc's per-user MSI as installed for all users. It's the same
  misreading as 014 R3.8 fixes for going back, so the same source should steer updates. The alternative is to leave
  updates to WinGet, as today, and only show the scope (R2).
  **Decided 2026-11-10: yes.**
- **Q2. Where to say it for updates (R3.2).** *Proposed: in the result and the log,* since updates run without a
  question. The alternative is to say nothing: the update keeps things as they are, which is what the user expects.
  **Decided 2026-11-10: as proposed.**
- **Q3. The control (R1.1).** *Proposed: a split button,* **Install** with a menu holding the two scopes, so the
  common case stays one click. The alternative is two buttons, which takes more room in the pane and on the version
  line (014 R1.6).
  **Decided 2026-11-10: a split button.**
