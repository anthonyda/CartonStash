# 002 – Installed packages: Tasks

**Status:** Done
**Design:** [design.md](design.md)

- [x] **T1.** `CatalogConnection` with cached remote-first / local-first composites + `Invalidate()`; `SearchAsync` uses it.
  - Verify: existing tests stay green; integration search still finds Git.Git.
  - Done: `Search_finds_git`, `Cached_catalog_is_reused_and_invalidate_reconnects`.
- [x] **T2.** `IPackageService.GetInstalledAsync` + `PackageInfo.IsManaged`. *(R1.1, R1.2, R3.1)*
  - Verify: integration test `GetInstalled_contains_git`.
- [x] **T3.** `InstalledViewModel`: load once, refresh, filter, sort, counts, error + retry. *(R1.3, R2.1–R2.3, R5.1)*
  - Verify: `InstalledViewModelTests.*`.
- [x] **T4.** Shell: `NavigationView` + `Frame`, page map, move search UI into `SearchPage`. *(R4.1, R4.2)*
  - Verify: manual, 001 behaviour unchanged (rerun 001 UIA checks). See log.
- [x] **T5.** `PackageRow` UserControl shared by Search and Installed. *(R1.2, R3.1, R3.2)*
  - Verify: manual. See log.
- [x] **T6.** `InstalledPage` with filter box, count (LiveRegion), refresh, progress, error InfoBar + Retry. *(R1.4, R2.2, R5.1)*
  - Verify: manual (UIA script). See log.
- [x] **T7.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R4.1 | 2026-10-29 | Pass | UIA: nav items Search, Installed, Updates (+badge), Activity, Settings |
| 001 regression | 2026-10-29 | Pass | UIA: search "Git.Git" → `Git · Git.Git · Installed 2.56.0.3 · 2.56.0.3 · winget` |
| R1.1, R1.2, R3.1 | 2026-10-29 | Pass | UIA: "329 of 329 packages"; 37 rows "Not managed by WinGet" (e.g. `ARP\Machine\X64\Steam App 1450330`); Git row shows `winget` |
| R2.1, R2.2 | 2026-10-29 | Pass | UIA: filter "git" → "3 of 329 packages" (Concordia: Digital Edition, Git, GitHub CLI) |
| R2.3 | 2026-10-29 | Pass | Screenshot: rows sorted by name (3DMark, A Story About My Uncle, Age of Empires IV…) |
| R4.2 | 2026-10-29 | Pass | UIA: query "7zip" (10 results) and filter "micro" kept after switching pages |
| R1.3 | 2026-10-29 | Pass | List reloaded automatically after installing / uninstalling jqlang.jq (see 003) |
| R1.4 | 2026-10-29 | Pass (unit) | `IsLoading` covered by tests; not seen live because the list now preloads at startup (004 badge) and was ready in 0.35 s |
