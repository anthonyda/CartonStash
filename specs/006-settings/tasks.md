# 006 – Settings: Tasks

**Status:** In progress (code done; one manual check pending)
**Design:** [design.md](design.md)

- [x] **T1.** `AppSettings`, `ISettingsStore` / `JsonSettingsStore`, `ISettings` singleton. *(R1.2)*
  - Verify: `JsonSettingsStoreTests.*` (round trip, missing file, corrupt file).
- [x] **T2.** Installer (003) uses `ISettings.InstallScope`. *(R2.1)*
  - Verify: `SettingsTests.Scope_maps_to_winget`, `SettingsTests.View_model_writes_theme_and_scope`. The read of `ISettings` inside `WinGetPackageInstaller` needs COM, so it's covered by review, not a unit test.
- [x] **T3.** Service: sources, refresh, agreements, WinGet version. *(R3.1–R3.3, R4.1)*
  - Verify: `Sources_include_winget_and_version_is_known`, `Refresh_winget_source_succeeds`.
- [x] **T4.** `SettingsViewModel` + diagnostic text. *(R4.2)*
  - Verify: unit tests.
- [x] **T5.** `SettingsPage`, `ThemeService` (incl. title bar), clipboard. *(R1.1, R2.2)*
  - Verify: manual in light / dark (done) / high contrast (pending), see log.
- [ ] **T6.** Mark `Done`.

### Amendment R5: start page (approved 2026-10-30)

- [x] **T7.** `StartPage` enum + `AppSettings.StartPage` (default Search); store accepts files without it, rejects unknown values. *(R5.1, R5.4)*
  - Verify: `SettingsTests.*` (round trip, old file without StartPage → Search, unknown value → reset).
- [x] **T8.** `SettingsViewModel.StartPageIndex` + ComboBox in Settings → Appearance. *(R5.1, R5.3)*
  - Verify: `SettingsTests.View_model_shows_and_writes_the_start_page`; manual: changing it doesn't navigate (see log).
- [x] **T9.** `MainWindow` opens and selects the saved start page at launch. *(R5.2)*
  - Verify: manual (UIA): set Updates, restart → Updates page shown and selected; reset to Search afterwards.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1 | 2026-10-29 | Pass | Theme Dark → Light applied instantly, title bar buttons included (screenshot) |
| R1.2 | 2026-10-29 | Pass | `settings.json` written; after restart the Theme combo shows Light. Reset to "Use system setting" afterwards |
| R2.2 | 2026-10-29 | Pass | "All users (machine)" shows the UAC InfoBar; "Let WinGet decide" hides it |
| R3.1 | 2026-10-29 | Pass | msstore (Microsoft.Rest), winget, winget-font (explicit) with trust level and last update time |
| R3.2 | 2026-10-29 | Pass | Refresh sources → "Sources refreshed." |
| R3.3 | 2026-10-29 | Pass | msstore Agreements → dialog "Terms of Transaction" [Accept] [Decline]; Decline accepted nothing |
| R4.1, R4.2 | 2026-10-29 | Pass | "CartonStash 1.0.0 · WinGet 1.29.380"; clipboard holds versions, OS build and the 3 sources |
| R5.1, R5.3 | 2026-10-30 | Pass | UIA: Settings → Start page → Updates; app stays on Settings; `settings.json` has `"StartPage": "Updates"` |
| R5.2 | 2026-10-30 | Pass | Restart → Updates page shown, Updates selected in navigation (screenshot). Reset to Search afterwards |
| R5.4 | 2026-10-30 | Pass (unit) | `SettingsTests.File_from_before_start_page_opens_on_search_without_a_reset`; live file from before R5 also loaded without a notice |
| R1.1 (high contrast) | – | **Pending** | Check the app with a Windows contrast theme on (constitution Q5) |
