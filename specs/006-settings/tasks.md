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
| R1.1 (high contrast) | – | **Pending** | Check the app with a Windows contrast theme on (constitution Q5) |
