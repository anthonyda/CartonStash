# 003 – Install / uninstall with progress: Tasks

**Status:** Done. Amendment R7 (say when a program is running): done 2026-11-06
**Design:** [design.md](design.md)

- [x] **T1.** Models: `PackageOperation`, `OperationPhase`, `OperationResult`, `OperationProgress`, `OperationOutcome`.
  - Verify: builds; covered by T3 tests.
- [x] **T2.** `IPackageInstaller` + `WinGetPackageInstaller` (re-find by ID, install, uninstall, agreements, progress, result mapping). *(R1.2, R1.3, R5.2, R5.3)*
  - Verify: `WinGetErrorMessageTests.*` (incl. hash mismatch, restart required); manual install of a small package (see log).
  - Fixed 2026-10-30 (security review): `FindByIdAsync` fell back to the first match from any source, against R1.2. Verify: `Only_the_requested_source_counts`, `Package_is_not_found_in_a_source_that_does_not_have_it`, `Installed_package_is_found_in_its_own_source`.
- [x] **T3.** `PackageOperationQueue`: FIFO single worker, dedupe, cancel, retry, `Completed`, `Drained`. *(R3.2, R4.1, R4.2, R5.1)*
  - Verify: `PackageOperationQueueTests.*`.
- [x] **T4.** `IDialogService` + uninstall confirmation, agreements dialog, close guard. *(R2.2, R1.3, R6.2)*
  - Verify: `PackageActionsTests.*`, `ShellViewModelTests.Close_*` with fake dialogs; manual, see log.
- [x] **T5.** Row actions (`PackageActionsView` in `PackageRow`: Install / Uninstall / progress / Cancel). *(R1.1, R2.1, R3.1)*
  - Verify: `PackageActionsTests.Row_*` + manual, see log.
- [x] **T6.** Activity pane + footer badge; success / failure InfoBars; Installed refresh on completion. *(R3.1, R5.1, R5.2)*
  - Verify: `ShellViewModelTests.*` + manual, see log.
- [x] **T7.** Manual end-to-end: install → uninstall a small package; one admin-scope installer shows UAC only for itself. *(R6.1)*
  - Install → uninstall done (jqlang.jq, Mozilla.Firefox). UAC checked on the Firefox install (see log).
- [x] **T8.** Mark `Done`.

### Amendment R7: say when a program is running (approved 2026-11-06)

- [x] **T9.** Integration test first: WinGet's COM API gives a portable package's install folder through
  `InstalledVersion.GetMetadata(PackageVersionMetadataField.InstalledLocation)`. If it doesn't, stop and update the design. *(R7.2)*
  - Verify: `InstalledLocationTests.Portable_package_reports_an_existing_folder` (`Category=Integration`; returns early when no
    portable package is installed, as xunit 2 has no runtime skip). Run on the dev PC: it only reads, nothing is installed.
  - Done 2026-11-06: passes on the dev PC. The folder matches the Apps & features `InstallLocation` and exists for Claude Code,
    ripgrep, Driver Store Explorer and OMP Manager. Yuzu is no longer in the winget source, so the test skips it.
- [x] **T10.** `IRunningPrograms` and `RunningPrograms` (process paths through `QueryFullProcessImageNameW`, read-only, every
  `Process` disposed), the folder check, DI in `App.xaml.cs`. *(R7.2, R7.4)*
  - Verify: `RunningProgramsTests.Folder_holds_only_paths_inside_it` (prefix, case, `Foo` vs `FooBar`, trailing `\`, `..`);
    `RunningProgramsTests.Finds_the_test_process_in_its_own_folder`, `Finds_nothing_in_an_empty_folder` (`Category=Integration`).
  - Done 2026-11-06: 11 tests pass. R7.4 holds by construction: the only right asked for is `PROCESS_QUERY_LIMITED_INFORMATION`.
- [x] **T11.** Result mapping: the in-use codes, the portable codes with and without running files, for install, update and
  uninstall; `MapUninstallResult` takes the extended error. `Details` unchanged. *(R7.1, R7.2, R7.3, R7.5)*
  - Verify: `WinGetErrorMessageTests.In_use_*`, `WinGetErrorMessageTests.Portable_*`,
    `WinGetErrorMessageTests.Running_files_alone_change_nothing`, `WinGetErrorMessageTests.Details_are_unchanged_*`.
  - Done 2026-11-06: 416 unit tests pass. The in-use messages apply only to `InstallError` / `UninstallError`, where WinGet
    reports installer failures.
- [x] **T12.** `WinGetPackageInstaller`: after a portable code only, read the install folder and ask `IRunningPrograms`.
  A missing folder, a COM error or an enumeration error falls back to today's message. *(R7.2, R7.3)*
  - Verify: `WinGetErrorMessageTests.Running_files_come_from_the_install_folder`, `WinGetErrorMessageTests.Running_files_fallback_*`
    (fake `IRunningPrograms` that throws or finds nothing, no folder, other codes); all unit tests pass.
  - Done 2026-11-06: all 445 tests pass, including the integration tests (read-only). The check runs off the caller's
    thread (`Task.Run`, constitution A4).
- [x] **T13.** Manual in the test VM, Store test build: install an older version of a portable GUI package with
  `winget install --version`, start it, update it in CartonStash. Check the message names its file and the program stays
  open; close it, Retry, and check that it updates. Then the same with Uninstall. *(R7.2, R7.4)*
  - Verify: logged below.
  - Done 2026-11-06 with Claude Code itself, the case that started R7: a console app, not a GUI one, which also shows the
    check doesn't need a window. The first run proved nothing, because `claude` didn't start from the `WinGet\Links` path
    in the scheduled task, and both operations simply succeeded. Started from its package folder in a `cmd` window, it passed.
- [x] **T14.** Mark amendment R7 `Done` here, in requirements.md, design.md and the README roadmap.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1 | 2026-10-29 | Pass | UIA: "Install Wget" / "Install jq" buttons on not-installed search rows; none on installed Git |
| R1.2, R3.1 | 2026-10-29 | Pass | jqlang.jq via UI: Downloading… (0.3 s) → Installing… (1.3 s) → Installed (1.8 s); `winget list` confirms 1.8.2 |
| R5.1 | 2026-10-29 | Pass | InfoBar "Install jq: Installed · Installed successfully."; jq appears on the Installed page without pressing Refresh |
| R6.2 | 2026-10-29 | Pass | Closing during the jq download → "1 operation is still running or queued…" [Quit anyway] [Wait]; Wait kept the app open and the install finished |
| R2.1, R2.2 | 2026-10-29 | Pass | Installed page "Uninstall jq" → dialog "Uninstall jq? jq (jqlang.jq) will be removed from this PC." → confirmed → Uninstalled; `winget list` no longer finds it; list back to 329 |
| R5.2 | 2026-10-29 | Pass | JernejSimoncic.Wget failed live (host served HTML, hash mismatch): row "Failed", Activity entry with the reason and Retry. Message improved afterwards (see design) |
| Activity pane | 2026-10-29 | Pass | Lists "Uninstall jq · Uninstalled" and "Install jq · Installed" with messages; opens via mouse, keyboard and UIA select |
| R1.2 (source) | 2026-10-30 | Pass | Read-only probe of the 90 managed IDs on the dev PC: 89 found in their own source, none in another. The fallback never fired, so removing it changes nothing here. `Microsoft.PowerShell` returned no local-first match before and after the fix (separate bug) |
| R1.2, R2.2, R5.1 | 2026-10-30 | Pass | Mozilla.Firefox through the app: install took 19 s to Program Files with the app unelevated; the uninstall confirmation named the package. "Uninstalled" appeared after 4 s while Firefox's own uninstaller finished in the background, and the files were gone about 30 s later |
| R3.2 (live) | – | Not run | Covered by `PackageOperationQueueTests.Cancel_*`; jq downloads too fast to cancel by hand |
| R1.3 (live) | – | Not run | Covered by `PackageActionsTests.Install_with_agreements_*`; needs a package with agreements (e.g. an msstore app) |
| R6.1 | 2026-10-30 | Pass | Mozilla.Firefox to Program Files: Windows' UAC prompt appeared and was approved by hand, and the app stayed unelevated |
| R7.2, R7.4, R7.5 (T13) | 2026-11-06 | Pass | Test VM from `clean`, Store test build 1.1.13. Claude Code 2.1.309 installed with the `winget` command, `claude.exe` running from its package folder in a console. **Update** failed after 6 s: "Claude Code is running (claude.exe), so it couldn't be updated. Close it, then press Retry." in the notification, row and log; the log kept `InstallError`, exit code 2316632067, `0x8A150052`; `claude` was still running. Closed it, **Retry Update Claude Code** in Activity: "Updated successfully.", `winget list` shows 2.1.317 |
| R7.2, R7.4, R7.5 (T13) | 2026-11-06 | Pass | Same run, `claude` 2.1.317 started again. **Uninstall** (confirmed) failed after 1 s: "Claude Code is running (claude.exe), so it couldn't be uninstalled. Close it, then press Retry."; log `UninstallError`, `0x8A150057`; `claude` still running. Closed it, Retry: "Uninstalled successfully.", `winget list` no longer finds it |
| R7.1 (live) | – | Not run | Covered by `WinGetErrorMessageTests.In_use_*`; needs an installer whose manifest maps its exit code to an in-use code |
