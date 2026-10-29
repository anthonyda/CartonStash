# 001 – Package search: Tasks

**Status:** In progress
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

- [ ] **T5.** Unit tests for `PackageInfo.IsInstalled` / `HasUpdate`, including the "Unknown" version case. *(R2.1, R2.2)*
  - Verify: new `PackageInfoTests`.
- [ ] **T6.** Show installed version + "Installed" / "Update available" badges in the result row. *(R2.1, R2.2)*
  - Verify: manual, search `git` with Git installed.
- [ ] **T7.** Cancel the in-flight search when a new one starts. Only the latest results are shown. *(R3.2)*
  - Verify: unit test with a fake service that delays the first call.
- [ ] **T8.** Empty state "No packages found for '*query*'" (view model `IsEmpty` + text). *(R3.3)*
  - Verify: unit test + manual.
- [ ] **T9.** Keep the query after a failure; InfoBar shows a readable message, not the raw HRESULT. *(R3.4)*
  - Verify: unit test (query unchanged after error).
- [ ] **T10.** Accessible names on the search box and results list; check Tab order. *(R3.5)*
  - Verify: manual, Narrator + keyboard only.
- [ ] **T11.** Mark the spec `Done` once all criteria are verified.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1 | 2026-10-29 | Pass | Live search "Git.Git" → `Git.Git 2.56.0.3 (installed 2.56.0.3) winget` |
| T4   | 2026-10-29 | Pass | App starts, window "WinGet Frontend" shown |
