# 001 – Package search: Tasks

**Status:** Done
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit.

## Done during project setup (walking skeleton)

- [x] **T1.** Solution layout: Core / App / Tests, central package versions, .NET 10 + WinAppSDK 2.6.
  - Verify: `dotnet build` is green.
- [x] **T2.** `WinGetPackageService.SearchAsync` over a composite catalog, mapped to `PackageInfo`. *(R1.1, R1.3)*
  - Verify: `WinGetPackageServiceIntegrationTests.Search_finds_git` (live WinGet).
- [x] **T3.** `SearchViewModel` with `SearchCommand` + blank-query guard + error message. *(R1.2, R3.4 partial)*
  - Verify: `SearchViewModelTests.*`.
- [x] **T4.** `MainWindow`: search box (Enter submits), button, progress bar, error InfoBar, results list. *(R1.4 partial, R3.1)*
  - Verify: manual, the app launches and shows the window.

## Remaining

- [x] **T5.** `PackageInfo.UpdateAvailable` from WinGet's `IsUpdateAvailable` + unit tests for `IsInstalled` / `HasUpdate`,
  including the "Unknown" version case. *(R2.1, R2.2)*
  - Verify: `PackageInfoTests.*`.
- [x] **T6.** Show installed version + "Installed" / "Update available" badges in the result row. *(R2.1, R2.2)*
  - Verify: manual (UI Automation), see log.
- [x] **T7.** Cancel the in-flight search when a new one starts. Only the latest results are shown. *(R3.2)*
  - Verify: `SearchViewModelTests.New_search_supersedes_running_one`.
- [x] **T8.** Empty state "No packages found for '*query*'" (view model `EmptyMessage`). *(R3.3)*
  - Verify: `SearchViewModelTests.No_matches_shows_empty_message` + manual, see log.
- [x] **T9.** Keep the query after a failure; InfoBar shows a readable message, not the raw HRESULT. *(R3.4)*
  - Verify: `SearchViewModelTests.WinGet_failure_shows_its_message_and_keeps_query`, `Unexpected_failure_gets_readable_prefix`,
    `New_search_clears_previous_error`, `WinGetErrorMessageTests.*`.
- [x] **T10.** Accessible names on the search box, results list, rows and progress bar; explicit TabIndex order
  (box → button → results). *(R3.5)*
  - Verify: names via UI Automation + human keyboard/Narrator check (see log).
- [x] **T11.** Mark the spec `Done` once all criteria are verified.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1 | 2026-10-29 | Pass | Live search "Git.Git" → `Git.Git 2.56.0.3 (installed 2.56.0.3) winget` |
| T4   | 2026-10-29 | Pass | App starts, window "WinGet Frontend" shown |
| R1.4, R2.1 | 2026-10-29 | Pass | UIA: search "Git.Git" → row `Git · Git.Git · Installed 2.56.0.3 · 2.56.0.3 · winget` |
| R2.2 | 2026-10-29 | Pass | UIA: search "Anaconda.Miniconda3" → `Installed py312_24.11.1-0 · Update available · py314_26.9.4-0` |
| R3.3 | 2026-10-29 | Pass | UIA: search "zzqqxxnotapackage" → "No packages found for 'zzqqxxnotapackage'" |
| R3.5 (names) | 2026-10-29 | Pass | UIA: Edit "Search packages", List "Search results" found by name |
| R3.1 | 2026-10-29 | Pass | Human: progress bar visible during search (user confirmed) |
| R3.5 (keyboard) | 2026-10-29 | Pass | Human: Tab order box → Search → results; Narrator reads names (user confirmed) |
