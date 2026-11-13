# Spec-driven workflow

Every feature is built from a spec in this folder. Code follows the spec, not the other way round.

## Layout

```
specs/
  constitution.md          Project-wide rules every spec and every change must respect
  _template/               Copy this folder to start a new feature
  NNN-feature-name/
    requirements.md        WHAT and WHY: user stories + EARS acceptance criteria
    design.md              HOW: components, data flow, WinGet API calls, UI, risks
    tasks.md               Ordered, checkable steps, each traced to requirement IDs
```

## The loop (one feature at a time)

1. **Requirements**: copy `_template/` to `specs/NNN-name/`, write `requirements.md`.
   Status `Draft`, then a human reviews it and sets `Approved`. No design before approval.
2. **Design**: write `design.md` against the approved requirements and the constitution.
   Human review, then `Approved`.
3. **Tasks**: break the design into small tasks in `tasks.md`. Each task names the requirement
   IDs it satisfies and how it is verified (test name or manual check).
4. **Implement**: work through tasks in order, ticking `[x]` as each one is done and verified.
   One commit per task (or a tight group), message prefixed with the spec number, e.g. `001: add search view model`.
5. **Close**: every acceptance criterion is covered by a passing test or a recorded manual check.
   Set the spec status to `Done`.

If implementation reveals the spec is wrong, **stop, update the spec first**, then continue.

## Status values

`Draft` → `Approved` → `In progress` → `Done` (or `Superseded by NNN`)

## Roadmap

| #   | Feature                                   | Status      |
|-----|-------------------------------------------|-------------|
| 001 | [Package search](001-package-search/)                  | Done; amendment R4 (Tab into new results never closes the app): done |
| 002 | [Installed packages list](002-installed-packages/)     | Done; amendment R6 done; amendment R7 done; amendment R8 (status filter chips) done; amendment R9 (the list changes in place, Tab crash): done |
| 003 | [Install / uninstall with progress](003-install-uninstall/) | Done; amendment R7 (say when a program is running): done; amendment R8 (all-users MSI packages: Default mode, honest uninstall failure): done; R8.4 checked by unit tests only |
| 004 | [Available updates](004-updates/)                      | In progress: code done; live update (R2.1) done; R3 pins deferred, open; amendment R6 (updating App Installer): done (T8 to T15); amendment R7 (license agreements on update): done (T16, T17); amendment R8 (rows stay in place on reload, Tab crash): done (T18, T19); amendment R9 (Update all without a confirmation): done |
| 005 | [Package details pane](005-package-details/)           | In progress: code done; link-opens-browser check (R1.3) pending; amendment R6 (https icons) done |
| 006 | [Settings](006-settings/)                              | In progress: code done; high-contrast check pending; amendment R5 (start page) approved; amendment R4.3 (safe diagnostics) done; amendment R3.4 (startup check and source agreements): done (T11 to T19); R3.8 reworded during T19, Store listing check pending (Store policy 10.2.4); amendment R3.14 to R3.17 (explicit sources, BR-01): done, font install and Narrator not checked |
| 007 | [Packaging and release](007-packaging-release/)        | In progress: Sandbox check pending; public; amendment R5 (release hardening) done; amendment R6 (Microsoft Store): done, listed; amendment R7 (smaller packages): code done, Store update (T33) pending; amendment R8 (shared runtime on GitHub): GitHub MSIX withdrawn by R9; amendment R9 (Microsoft Store only): approved, in progress |
| 008 | [Pin management](008-pin-management/)                  | Draft, on hold (updated against current code; constitution exception A6 undecided; issue #1) |
| 009 | [Activity pane](009-activity-pane/)                    | Amendment R4 (filter, tabs and sort): done, Narrator and high contrast pending. Done (card redesign, operation logs, pin the pane; issues #2, #3, #4); high-contrast check pending; amendment R2.7 (log privacy note): code done, visual check pending; amendment R2.8–R2.10 (WinGet's record in the log): done, released in v1.0.3 |
| 010 | [Background update checks](010-background-updates/)    | Done, including amendment R3.14 (startup entry for a missed check at sign-in); issue #14; amendment R3.18 (two triggers in one process, BR-06): done; amendment R3.13 (failure reasons keep their paths, BR-09): done |
| 011 | [Source failures and missing WinGet](011-source-resilience/) | In progress: code done; high-contrast check and R3.5 pending |
| 012 | [Readable release notes](012-release-notes/)           | Done (2026-11-07) |
| 013 | [Honest operation results](013-honest-results/)        | Done (2026-11-07); R2.2 checked by unit tests only |
| 014 | [Install a chosen version](014-choose-version/)        | Done (2026-11-09); Store packages and the notification text checked by unit tests only; amendment R1.6, R1.7 (action next to the selected version, Excluded rows open details): done; amendment R2.6, R2.7, R3.8, R3.9 (installed scope when going back, fresh installed state): done; R3.9 through the app and the refusing MSIs checked by unit tests only; amendment R2.8 (going back from the newest version, BR-02): done, unit tests only |
| 015 | [Choose the install scope per install](015-install-scope-choice/) | Done (2026-11-11); keyboard by hand, dark and high contrast not checked |
| 016 | [Updates count on the taskbar icon](016-taskbar-badge/) | Done (2026-11-12); R1.4 checked by unit tests only; 99+ and badges turned off in Windows not checked |
| 017 | [Retry as administrator](017-retry-as-admin/) | Done (2026-11-12); the permission prompt itself and declining it checked by unit tests only |
| 018 | [Say when Windows' permission prompt is waiting](018-permission-prompt-waiting/) | In progress: code done, VM check passed (banner, card, row, notification after a minute, prompt declined); amendment R2 (a declined prompt is Cancelled): done; accepting the prompt, notification click, Narrator, dark and high contrast not checked |

Suggested build order: 002 → 003 → 004 → 005 → 006 → 007 (006's scope setting slots into 003's installer when it lands).
