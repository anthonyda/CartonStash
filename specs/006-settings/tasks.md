# 006 – Settings: Tasks

**Status:** Draft
**Design:** [design.md](design.md)

- [ ] **T1.** `AppSettings`, `ISettingsStore` / `JsonSettingsStore`, `ISettings` singleton. *(R1.2)*
  - Verify: `JsonSettingsStoreTests.*` (round trip, missing file, corrupt file).
- [ ] **T2.** Installer (003) uses `ISettings.InstallScope`. *(R2.1)*
  - Verify: unit test with fake settings.
- [ ] **T3.** Service: sources, refresh, agreements, WinGet version. *(R3.1–R3.3, R4.1)*
  - Verify: integration tests.
- [ ] **T4.** `SettingsViewModel` + diagnostic text. *(R4.2)*
  - Verify: unit tests.
- [ ] **T5.** `SettingsPage`, `ThemeService` (incl. title bar), clipboard. *(R1.1, R2.2)*
  - Verify: manual in light / dark / high contrast.
- [ ] **T6.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
