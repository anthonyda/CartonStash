# 008 – Pin management: Design

**Status:** Draft, needs review (see the note in [requirements.md](requirements.md))
**Requirements:** [requirements.md](requirements.md)

## Overview

The COM API has no pin support (reflection over ComInterop 1.29.380: no pin types or members). Pins are
therefore read and changed through the `winget pin` CLI, isolated in **one** adapter class in `Core/Services`
that returns plain records. This needs a constitution amendment (below). Pin state is merged into the
installed data from 002 and consumed by the Installed and Updates pages. Fail closed: if anything about the
CLI output is unexpected, the app shows "Pin status unavailable" rather than a guess.

## Constitution amendment (part of this review)

Approving this spec approves the following change to `constitution.md`. Applied as task T0.

- **A2** (amended): *"Only classes in `Core/Services` touch WinGet, through `Microsoft.Management.Deployment`,
  or through the CLI under A6. They return plain records (`Core/Models`), never COM objects or raw text."*
- **A6** (new): **CLI fallback, by exception only.** The app may call `winget.exe` only for a capability the
  COM API doesn't offer, each case approved in a spec that names it. Current list: `winget pin` (spec 008).
  Rules: one adapter class per command group in `Core/Services`; prefer exit codes over output; parsing of
  output is tolerant of column widths and language, covered by fixture tests of real captured output, and
  fails closed (an unparseable result is an error, never a guess); no `--force` or destructive call without
  the user action required by P2; never elevated (A5). Revisit each exception when a WinGet release adds
  the capability to the COM API.

Reason: pins are a core WinGet feature users rely on with "update all", and WinGet offers no other access.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `PinType` enum | `Pinning`, `Blocking`, `Gating` | new |
| Core/Models | `PinInfo` record | `PackageId`, `Source`, `Type`, `GatedVersion?` (range), `PinnedVersion?` | new |
| Core/Models | `PackageInfo` | add `PinInfo? Pin` | changed |
| Core/Services | `IPinService` | `ListAsync`, `AddAsync(id, source, type, range?, installed, replace)`, `RemoveAsync(id, source)`, `ResetAsync()`; all take a `CancellationToken` | new |
| Core/Services | `WinGetCliPinService` | Implements `IPinService` with `winget pin`. The only A6 adapter. | new |
| Core/Services | `IProcessRunner` / `ProcessRunner` | Runs a process, returns exit code + stdout + stderr; fakeable for tests | new |
| Core/Services | `PinListParser` | Pure function: `winget pin list` text → `IReadOnlyList<PinInfo>` or parse error | new |
| Core/Services | `WinGetLocator` | Finds `winget.exe` (R5.4) | new |
| Core/Services | `InstalledPackagesStore` (004 T2) | Loads installed packages and pins in parallel, joins on (Id, Source); pin failure → `PinStatusAvailable = false` (R1.4) | changed |
| Core/ViewModels | `PinActionsViewModel` | Pin / Block / Range / Unpin commands per row, enablement, confirmation for replace, range prefill (R2.x, R3.1) | new |
| Core/ViewModels | `InstalledViewModel` | "Pinned only" filter (R1.3) | changed |
| Core/ViewModels | `UpdatesViewModel` | Excludes Pin/Block from Update all, skipped count in confirmation, Update disabled for Block (R4.x) | changed |
| Core/ViewModels | `SettingsViewModel` | Remove all pins (R3.2) | changed |
| App | `PinBadge` control, Pin `MenuFlyout`, range `ContentDialog` | View only | new |

## WinGet CLI usage

Every call adds `--disable-interactivity --accept-source-agreements` so nothing can prompt and hang.

| Action | Command |
|--------|---------|
| List | `winget pin list` |
| Pin | `winget pin add --id <id> --exact --source <src> [--installed] [--force]` |
| Block | … `--blocking` |
| Gate | … `--version <range>` |
| Unpin | `winget pin remove --id <id> --exact --source <src>` |
| Reset | `winget pin reset --force` |

- Arguments are passed through `ProcessStartInfo.ArgumentList` (no string concatenation, no shell).
- `StandardOutputEncoding` = UTF-8; VT escape sequences and progress spinner characters are stripped before parsing.
- Exit code 0 = success. Non-zero exit codes map to `WinGetException(ExtendedErrorCode = exit code)` with a
  readable message (pin exists, package not found, invalid version range…). The exact `APPINSTALLER_CLI_ERROR_*`
  codes are captured in T1, not assumed. stderr / stdout text is only shown as a fallback detail.
- Timeout 30 s per call, then the process is killed and an error shown. Cancellation kills the process.
- **Locating winget (R5.4):** `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe` (App Execution Alias), then
  `PATH`. Verified once per session with `winget --version`; result cached for the session.

### Parsing `winget pin list` (the risky part)

Output is a localized, column-aligned table:

```
Name     Id           Version  Source  Pin type  Pinned version
---------------------------------------------------------------
Git      Git.Git      2.56.0   winget  Pinning
7-Zip    7zip.7zip    24.08    winget  Gating    24.*
```

- Columns are located from the **header line's start offsets** (the line just above the `---` rule), and
  identified by **position**, not by header text (headers are localized). Six columns expected; anything else
  = parse error.
- Header offsets are in display columns. Wide (CJK) characters are counted as 2 when slicing rows.
- **Pin type** values may be localized. Known English values map directly; for unknown values: a non-empty
  "Pinned version" column → `Gating`; otherwise parse error (fail closed, R1.4). Fixtures for at least
  English and one other language (French, the user's) are captured in T1 to replace this guess with real values.
- **Truncation:** WinGet truncates long cells with `…` to fit the console width. With redirected output the
  width is fixed (to be confirmed in T1). A truncated Id is matched by prefix against installed IDs; if the
  prefix is ambiguous or matches nothing, that pin is shown on no row and reported once ("1 pin could not be
  matched"). Never attached to a guessed package.
- No-pins message ("There are no pins configured.", localized): recognized by the **absence of a `---` rule**
  together with exit code 0 → empty list. Exit code ≠ 0 → error.
- `PinListParser` is pure and has no I/O, so all of the above is unit-testable on Linux CI.

## Data flow

- **Load:** `InstalledPackagesStore.ReloadAsync` → `Task.WhenAll(packageService.GetInstalledAsync, pinService.ListAsync)`
  → join on (Id, Source), case-insensitive → `PackageInfo with { Pin = … }` → `Changed`.
- **Change:** Pin menu → `PinActionsViewModel` (confirm replace if needed, R2.5) → `IPinService.AddAsync` →
  success message → `store.ReloadPinsAsync()` (re-reads `pin list` only, not the whole installed catalog, R5.1).
- Pin calls are not part of the 003 operation queue (they take < 1 s and don't touch installers); one pin call
  at a time, guarded by a `SemaphoreSlim` in `WinGetCliPinService`.

## Interaction with 004 (Updates)

- Update all = updates where `Pin is null or { Type: Gating }`. Confirmation: "Update 5 packages? 2 pinned
  packages will be skipped."
- Block → row Update disabled (R4.2). Pin → row Update enabled (R4.3).
- **Gate (R4.3):** whether `UpgradePackageAsync` (COM) honours pins is unknown; T2 verifies it.
  - If COM honours gates: nothing more to do, WinGet picks the highest version in range or reports no applicable upgrade.
  - If COM ignores pins: the app must not bypass the user's pin. Gated rows then offer Update only when WinGet's
    available version matches the range; otherwise "Newer version is outside the pin range". Matching uses WinGet's
    own rules (prefix match up to the `*`), done as a string prefix check on the version string, documented
    as the one place we interpret a version, or the gated rows' Update is disabled entirely. Decided after T2, spec updated first.
  - Same question for Block and for Pin with Update all: if COM ignores pins, R4.1/R4.2 are enforced by the
    app alone (already the design).

## UI

```
Installed                                   Filter [____]  [☐ Pinned only]
Git        Git.Git     2.56.0   winget   📌 Pinned         [⋯]
7-Zip      7zip.7zip   24.08    winget   📌 Gated 24.*     [⋯]
                                                           └ Pin ▸ Pin / Block updates / Pin to version range… / Unpin
```

- Badge: `FontIcon` (Pin glyph) + text, theme resources only (high contrast). Accessible name e.g. "Gated to 24.*".
- Range dialog: TextBox (accessible name "Version range"), checkbox "Pin the installed version", Pin / Cancel.
  Enter = Pin, Esc = Cancel. Validation error shown under the TextBox (R2.6).
- "Pin status unavailable" as an `InfoBar` (warning) at the top of Installed / Updates with a Retry action (R1.4).
- Busy state: the row's menu button shows a `ProgressRing` while a pin call runs (R5.2).

## Error handling and edge cases

- Same package installed from two sources: pins are per (Id, Source), each row gets its own.
- Pin on a package that is not matched to a source ("Not managed by WinGet"): Pin menu not shown.
- Pin listed for a package no longer installed: ignored by the UI (out of scope), still counted by Remove all pins.
- `winget` upgraded to a version with a different output layout → parse error → R1.4. Fixture tests make it visible.
- Group Policy disabling the CLI or pins → non-zero exit code → readable message.
- App packaged as MSIX (007): launching the App Execution Alias from a packaged app must work; checked in 007's tasks.

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.1, R1.4, parsing (all rules above) | `PinListParserTests` on captured fixture files (`tests/.../Fixtures/pin-list-*.txt`): English, French, no pins, truncated Id, wide characters, unknown layout |
| R2.x, R3.1, R5.1, R5.4 | `PinActionsViewModelTests` with a fake `IPinService` |
| Command lines, exit-code mapping, timeout, cancellation | `WinGetCliPinServiceTests` with a fake `IProcessRunner` |
| R1.3, R4.1, R4.2 | `InstalledViewModelTests`, `UpdatesViewModelTests` |
| Real pin add / list / remove round trip | Integration test (Windows only), cleans up its pin |
| R1.2, R2.6, R3.2, R5.3, accessibility | Manual on the user's PC |

## Risks and alternatives considered

- **Output parsing is fragile** (localized, aligned, truncated). Mitigated by fail-closed parsing, fixtures, and
  using it only for reading; writes rely on exit codes.
- **Alternative: read WinGet's pin database directly** (SQLite under the App Installer package's LocalState).
  Rejected: undocumented internal file, schema can change, worse than CLI text.
- **Alternative: no pin support** (004 option A). Rejected by the user: "Update all" must respect pins.
- **Alternative: wait for COM API support.** Kept as the exit plan: A6 says revisit each WinGet release.
