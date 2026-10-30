# 007 – Packaging and release: Tasks

**Status:** In progress (local packaging done; GitHub release not run yet)
**Design:** [design.md](design.md)

- [x] **T1.** `Package.appxmanifest` (Anthony.CartonStash, CN=Anthony) + logo set rendered from `AppIcon.svg` (`tools/Render-Logos.ps1`). *(R1.2)*
  - Name, publisher and icon decided.
- [x] **T1b.** `tools/New-SigningCert.ps1`; create the certificate once (PFX kept outside the repo). *(R1.1, R1.5)*
  - Created 2026-10-30: CN=Anthony, valid to 2029-10-30; PFX in `%USERPROFILE%\.cartonstash\`; public `tools/CartonStash.cer` committed.
- [x] **T2.** MSIX build (`-p:PackageMsix=true`) signed with the certificate, via `tools/Publish-Release.ps1`. *(R1.1)*
  - Verify: MSIX installs locally; app starts from Start.
- [x] **T3.** Packaged WinGet check: search / install / uninstall from the MSIX build. *(R1.3)*
  - Verify: manual on the dev PC (see log). Windows Sandbox (clean machine) still to do.
- [x] **T4.** Packaged settings location + one-time copy. *(R4.1)*
  - Verify: `SettingsTests.Packaged_app_copies_the_portable_settings_once`, `Packaged_app_falls_back_to_the_pre_rename_settings`, `Unpackaged_app_keeps_settings_in_local_app_data`; manual (see log).
- [x] **T5.** Tag-based versioning. *(R3.2)*
  - Verify: local build `-Version 0.9.0` → package 0.9.0.0, Settings shows "CartonStash 0.9.0".
- [ ] **T6.** `ci.yml` (build + unit tests) and `release.yml` (artifacts, signing, checksums, GitHub Release). *(R1.4, R2.1, R3.1)*
  - Written 2026-10-30, not run yet. Needs the two signing secrets in GitHub.
  - Verify: test tag produces all artifacts.
- [ ] **T7.** First public release (tag `v1.0.0`) with install steps. *(R1.5, R2.1)* winget-pkgs PR deferred (R2.2).
- [ ] **T8.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1, R1.4 | 2026-10-30 | Pass | `Publish-Release.ps1 -Version 0.9.0`: signed x64 + arm64 MSIX (~91 MB each, "Successfully signed"), portable zips, `CartonStash.cer`, `SHA256SUMS.txt` |
| R1.5 | 2026-10-30 | Pass | Without the certificate trusted, install fails with 0x800B0109; after `Import-Certificate … LocalMachine\TrustedPeople` (needs admin) the MSIX installs |
| R1.2 | 2026-10-30 | Pass | Installed `Anthony.CartonStash 0.9.0.0` (publisher CN=Anthony); Start entry "CartonStash"; `Remove-AppxPackage` removed package, Start entry and data folder |
| R1.3 | 2026-10-30 | Pass | Launched from Start (runs from `C:\Program Files\WindowsApps\…`): Installed list "119 of 362", jqlang.jq installed and uninstalled from the packaged app |
| R3.2 | 2026-10-30 | Pass | Settings shows "CartonStash 0.9.0 · WinGet 1.29.380" |
| R4.1 | 2026-10-30 | Pass | Package `LocalState\settings.json` created with the portable settings (StartPage Updates); portable file unchanged |
| Single-file exe | 2026-10-30 | Rejected | 92 MB self-extracting exe crashes at start in Microsoft.UI.Xaml.dll (0xc000027b) |
