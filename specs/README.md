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
| 001 | [Package search](001-package-search/)                  | Done |
| 002 | [Installed packages list](002-installed-packages/)     | Done; amendment R6 done; amendment R7 done; amendment R8 (status filter chips) done |
| 003 | [Install / uninstall with progress](003-install-uninstall/) | In progress: code done; UAC check (R6.1) pending |
| 004 | [Available updates](004-updates/)                      | In progress: code done; live update (R2.1) pending; R3 pins deferred, open |
| 005 | [Package details pane](005-package-details/)           | In progress: code done; link-opens-browser check (R1.3) pending |
| 006 | [Settings](006-settings/)                              | In progress: code done; high-contrast check pending; amendment R5 (start page) approved |
| 007 | [Packaging and release](007-packaging-release/)        | In progress: v1.0.0 released (self-signed MSIX + portable zip); Sandbox check and winget-pkgs pending |
| 008 | [Pin management](008-pin-management/)                  | Draft, on hold (updated against current code; constitution exception A6 undecided; issue #1) |
| 009 | [Activity pane](009-activity-pane/)                    | Done (card redesign, operation logs, pin the pane; issues #2, #3, #4); high-contrast check pending |

Suggested build order: 002 → 003 → 004 → 005 → 006 → 007 (006's scope setting slots into 003's installer when it lands).
