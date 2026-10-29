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
| 001 | Package search                            | Approved (walking skeleton in place) |
| 002 | Installed packages list                   | Not started |
| 003 | Install / uninstall with progress         | Not started |
| 004 | Available updates + update / update all   | Not started |
| 005 | Package details pane                      | Not started |
| 006 | Settings (sources, scope, theme)          | Not started |
| 007 | MSIX packaging and release                | Not started |
