# 002 – Installed packages: Tasks

**Status:** Done (R2.4 added and done 2026-10-29). Amendment R6 done 2026-10-30. Amendment R7 done 2026-10-30 (high-contrast check pending, with 006). Amendment R8 done 2026-10-30
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
- [x] **T8.** "Show apps not managed by WinGet" option, off by default. *(R2.4)*
  - Verify: `InstalledViewModelTests.Unmanaged_apps_are_hidden_until_asked_for` + manual, see log.

### Amendment R6: one row per installed program (approved 2026-10-30)

- [x] **T9.** Probe: does the Microsoft.WSL manifest list the WSL 3.0.4.0 PFN / the old update's product code? Measure the extra load time. Record in design.
  - Result: manifest lists PFN `microsoftcorporationii.windowssubsystemforlinux_8wekyb3d8bbwe` → WSL 3.0.4.0 will be a manifest match.
    `InstalledVersion.Id` = local entry ID for 330/330 packages (exact primary join). 362 entries, all with a code. Timing in design.
- [x] **T10.** `InstalledJoin.Join` (pure) + records for installed entries and composite packages. *(R6.1–R6.4)*
  - Verify: `InstalledJoinTests.*`: WSL case (two entries, one ID), manifest match, no match, case / brace normalisation, fallback.
- [x] **T11.** `GetInstalledAsync` reads both catalogs in parallel and uses the join; `PackageInfo.InstalledId`, `IsPrimaryMatch`, `Key`. *(R6.1, R6.2)*
  - Verify: `GetInstalled_has_one_row_per_installed_program`, `Programs_sharing_a_package_id_keep_their_own_versions`, `GetInstalled_contains_git`.
- [x] **T12.** Queue and row tracking by `Key`; Update only on primary matches. *(R6.4)*
  - Verify: `PackageOperationQueueTests.Programs_sharing_a_package_id_get_their_own_operations`, `UpdatesViewModelTests.Only_wingets_own_match_is_offered_for_update`, `Rows_sharing_an_id_track_only_their_own_operation`.
- [x] **T13.** Uninstall by installed entry when `InstalledId` is set. *(R6.5)*
  - Verify: `WinGetErrorMessageTests.Uninstall_targets_the_installed_entry_when_known`; manual, see log.
- [x] **T14.** Count and UI check: "of N packages" counts rows; WSL shows two correct rows. *(R6.2, R6.6)*
  - Verify: manual (UIA + screenshot).

### Amendment R7: version badge colors (approved 2026-10-30)

- [x] **T15.** `VersionStatus` from WinGet's `CompareToVersion` at load (primary and manifest-matched rows). *(R7.1, R7.2)*
  - Verify: `VersionBadgeTests.*`, `InstalledJoinTests.WinGets_match_shows_wingets_version_others_the_recorded_one`; integration `Version_status_comes_from_winget`.
- [x] **T16.** Badge colors, tooltip and accessible name in rows and the details pane. *(R7.1, R7.3)*
  - Verify: manual screenshots in dark and light (high contrast with the 006 check).

### Amendment R8: status filter chips (approved 2026-10-30)

- [x] **T17.** `StatusFilter` + counts in `InstalledViewModel` (combined with text and unmanaged filters). *(R8.2, R8.3, R8.5)*
  - Verify: `InstalledViewModelTests.*` (each state, toggle off, counts follow text filter, combined with unmanaged option).
- [x] **T18.** Chip row on `InstalledPage` with dots, labels, counts, tooltips; shared badge brushes. *(R8.1, R8.4)*
  - Verify: manual (UIA: names and counts; screenshots dark + light; click each chip).

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R4.1 | 2026-10-29 | Pass | UIA: nav items Search, Installed, Updates (+badge), Activity, Settings |
| 001 regression | 2026-10-29 | Pass | UIA: search "Git.Git" → `Git · Git.Git · Installed 2.56.0.3 · 2.56.0.3 · winget` |
| R1.1, R1.2, R3.1 | 2026-10-29 | Pass | UIA: "329 of 329 packages"; 89 managed, 240 shown as "Not managed by WinGet" (e.g. `ARP\Machine\X64\Steam App 1450330`); Git row shows `winget` |
| R2.1, R2.2 | 2026-10-29 | Pass | UIA: filter "git" → "3 of 329 packages" (Concordia: Digital Edition, Git, GitHub CLI) |
| R2.3 | 2026-10-29 | Pass | Screenshot: rows sorted by name (3DMark, A Story About My Uncle, Age of Empires IV…) |
| R4.2 | 2026-10-29 | Pass | UIA: query "7zip" (10 results) and filter "micro" kept after switching pages |
| R1.3 | 2026-10-29 | Pass | List reloaded automatically after installing / uninstalling jqlang.jq (see 003) |
| R2.4 | 2026-10-29 | Pass | UIA: default "89 of 329 packages", no "Not managed by WinGet" rows; checking "Show apps not managed by WinGet" → "329 of 329" |
| R6.1, R6.2, R6.6 | 2026-10-30 | Pass | UIA: "WSL" filter → two rows: "Windows Subsystem for Linux · Installed 3.0.4.0" and "Windows Subsystem for Linux Update · Installed 5.10.102.1", both Microsoft.WSL / winget. Count "119 of 362" (all shown: "362 of 362", same as `winget list`) |
| R6.3 | 2026-10-30 | Pass | 29 programs linked through manifest codes, all genuine: x86 copies of runtimes (.NET, VCLibs, UI.Xaml, DirectX), side-by-side WindowsAppRuntime / CUDA versions, WSL 3.0.4.0. Managed rows 90 → 119 |
| R6.5 | 2026-10-30 | Pass | jqlang.jq installed from Search, then uninstalled from its Installed row via its installed entry; `winget list` no longer finds it |
| R6 load time | 2026-10-30 | Pass | Installed list ready ~4 s after a cold app start (both catalogs in parallel, background) |
| R7.1 | 2026-10-30 | Pass | Dark + light screenshots: green = same (App Installer, Fusion, Blender, Claude Code), blue = newer than winget (Chatterino 2.5.6 vs 2.5.5, Claude, DisplayWidget Center), amber = Miniconda (update offered) |
| R7.2 | 2026-10-30 | Pass | UIA: WindowsAppRuntime.1.8 x86 / other copies and WSL 3.0.4.0 neutral ("winget has …"). Found during T16: comparing raw MSIX versions (8000.x) with product versions (1.8.10) gave a false "newer"; spec R6.2 / R7.2 amended, primary rows show WinGet's version again ("> 1.8.10") |
| R7.3 | 2026-10-30 | Pass | UIA names: "Installed 2.5.6, newer than winget's 2.5.5", "… update to py314_26.9.4-0 available from winget", "… same as winget" |
| R7 high contrast | – | **Pending** | Check with the 006 high-contrast pass |
| R8.1, R8.4 | 2026-10-30 | Pass | UIA names: "Up to date: 72 programs. Same version as the source", "Update available: 1 program…", "Newer: 17 programs…", "Other: 29 programs…" (72 + 1 + 17 + 29 = 119 managed). Screenshots dark + light |
| R8.2 | 2026-10-30 | Pass | UIA: Newer → "17 of 362", All unchecked; Update available replaces it → "1 of 362"; clicking it again → "119 of 362", All checked |
| R8.3 | 2026-10-30 | Pass | Text "microsoft" → chips 39 / 0 (dimmed) / 11 / 25 |
| R8 selected style | 2026-10-30 | Fixed | First build filled the selected chip with the accent color (green here), clashing with the dots; and light theme showed white text after a live theme switch. Now neutral fill + outline from per-theme dictionaries; checked dark, light after live switch |
| R1.4 | 2026-10-29 | Pass (unit) | `IsLoading` covered by tests; not seen live because the list now preloads at startup (004 badge) and was ready in 0.35 s |
