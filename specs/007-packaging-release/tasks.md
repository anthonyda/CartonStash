# 007 – Packaging and release: Tasks

**Status:** In progress (v1.0.0 released 2026-10-30; Windows Sandbox check and winget-pkgs pending). Amendment R5: approved, in progress
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
- [x] **T6.** `ci.yml` (build + unit tests) and `release.yml` (artifacts, signing, checksums, GitHub Release). *(R1.4, R2.1, R3.1)*
  - Secrets set by the maintainer 2026-10-30. CI green on every push; release run 41032148232 green (4m36s).
  - Verify: test tag produces all artifacts.
- [x] **T7.** First public release (tag `v1.0.0`) with install steps. *(R1.5, R2.1)* winget-pkgs PR deferred (R2.2).
  - https://github.com/anthonyda/CartonStash/releases/tag/v1.0.0
- [ ] **T8.** Mark `Done`.

### Amendment R5: release hardening (approved 2026-10-30)

Before going public:

- [x] **T9.** `nuget.config`, lock file proven on all four builds (x64 and ARM64, MSIX and portable), `.github/dependabot.yml`. *(R5.12)*
  - Verify: locked restore passes on all four; changing a version in `Directory.Packages.props` makes it fail (see log).
- [x] **T10.** Split the scripts: `Publish-Release.ps1` builds only; new `Sign-Release.ps1` signs with timestamps, checks every signature, writes checksums. *(R5.1, R5.5, R5.6, R5.11)*
  - Verify: local run with a throwaway certificate (subject `CN=Anthony`, see design) signs everything with a timestamp; the thumbprint check then fails against `tools/CartonStash.cer`, as it must (see log).
- [x] **T11.** `release.yml` as two jobs and `ci.yml` hardened: `permissions`, `persist-credentials: false`, SHA pins, tag through `env`, tag check first, attestation. *(R5.1–R5.4, R5.7)*
  - Verify: CI green; no `${{ }}` inside any `run:` block; `git check-ref-format` confirms the bad-tag example is a legal ref that the check rejects (see log).
  - `release.yml` itself first runs in T16.
- [x] **T12.** README, release notes and `CLAUDE.md`: thumbprint and check, attestation, accurate signing wording, new script commands. `SECURITY.md`. *(R5.8, R5.13)*
  - Verify: the README steps work as written on a downloaded `.cer` (see log).
- [x] **T13.** `New-SigningCert.ps1` writes no password or base64 file and leaves no key in the store; `.gitignore` additions. *(R5.11)*
  - Verify: run with a throwaway subject and folder, then check the folder and the store (see log).
- [x] **T14. Maintainer.** Save the PFX and password in a password manager, then remove `%USERPROFILE%\.cartonstash\` and the private key in `Cert:\CurrentUser\My`. *(R5.11)*
  - Verify: see design, Testing strategy, and the log.

- [x] **T15a.** The GitHub settings a private repo accepts: Actions limited to GitHub-owned, Dependabot alerts and security updates, immutable releases, `release` environment limited to `v*` tags. *(R5.3, R5.9, R5.10, R5.13)*
  - Verify: read back with `gh api` (see log).
- [ ] **T11b.** Move attestation to its own job after publishing, so a release can go out before the repo is public. *(R5.2, R5.7)*
  - Verify: on the private repo, `release` publishes and `attest` fails only on GitHub's refusal.
- [ ] **T16.** Release `v1.0.1` through the new pipeline. *(R5.1–R5.6, R5.8)*
  - Verify: downloaded files have timestamps and the right thumbprint, the `build` job log shows no secrets.

After going public:

- [ ] **T15b.** The remaining GitHub settings: required reviewer on `release`, secrets moved there and repo-level ones deleted, rulesets, secret scanning, private reporting, approval for outside contributors. *(R5.9, R5.10, R5.13)*
  - Verify: read back with `gh api`.
- [ ] **T17.** Re-run the failed `attest` job for `v1.0.1`, within 7 days. *(R5.7)*
  - Verify: `gh attestation verify` passes on a downloaded file.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1, R1.4 | 2026-10-30 | Pass | `Publish-Release.ps1 -Version 0.9.0`: signed x64 + arm64 MSIX (~91 MB each, "Successfully signed"), portable zips, `CartonStash.cer`, `SHA256SUMS.txt` |
| R1.5 | 2026-10-30 | Pass | Without the certificate trusted, install fails with 0x800B0109; after `Import-Certificate … LocalMachine\TrustedPeople` (needs admin) the MSIX installs |
| R1.2 | 2026-10-30 | Pass | Installed `Anthony.CartonStash 0.9.0.0` (publisher CN=Anthony); Start entry "CartonStash"; `Remove-AppxPackage` removed package, Start entry and data folder |
| R1.3 | 2026-10-30 | Pass | Launched from Start (runs from `C:\Program Files\WindowsApps\…`): Installed list "119 of 362", jqlang.jq installed and uninstalled from the packaged app |
| R3.2 | 2026-10-30 | Pass | Settings shows "CartonStash 0.9.0 · WinGet 1.29.380" |
| R4.1 | 2026-10-30 | Pass | Package `LocalState\settings.json` created with the portable settings (StartPage Updates); portable file unchanged |
| R2.1, R3.1 | 2026-10-30 | Pass | Tag v1.0.0 → release workflow built, signed and published 2 MSIX, 2 portable zips, CartonStash.cer, SHA256SUMS.txt |
| R1.1, R3.2 (CI build) | 2026-10-30 | Pass | Downloaded x64 MSIX: signature CN=Anthony valid, checksum matches SHA256SUMS.txt; installs as 1.0.0.0, Settings "CartonStash 1.0.0", WinGet search works, uninstalls cleanly |
| Single-file exe | 2026-10-30 | Rejected | 92 MB self-extracting exe crashes at start in Microsoft.UI.Xaml.dll (0xc000027b) |
| R5.12 | 2026-10-30 | Pass | With `CI=true`: Release build, unit tests, MSIX and portable for x64 and ARM64 all restore from the lock files. Changing a version in `Directory.Packages.props` fails with NU1004; the lock files are unchanged afterwards. No SDK-tied package in them, so the floating `10.0.x` is safe. CI itself not run yet |
| R5.5, R5.6, R5.1 | 2026-10-30 | Pass (local) | Real x64 build, throwaway key: `Publish-Release.ps1` leaves it unsigned with no checksums; `Sign-Release.ps1` signs the MSIX and the 3 portable binaries, each with a DigiCert timestamp; 525 files survive the re-zip; checksums match; the signed portable app starts. Against `tools/CartonStash.cer` the script refuses: "is not signed with certificate 99EE…" |
| R5.11 (script) | 2026-10-30 | Pass | `New-SigningCert.ps1` with a throwaway key: only the PFX in the folder, one ACL rule, certificate store unchanged, `tools/CartonStash.cer` untouched. `.gitignore` covers `*.pfx.*`, `*.p12`, `*.pem`, `*.key`, `*.snk`, `.env.*`, and no tracked file is ignored. Moving the real key is T14 |
| R5.1–R5.4 (review) | 2026-10-30 | Pass | Parsed both workflows: all actions `actions/*` pinned to a full SHA, `persist-credentials: false` on every checkout, no `${{ }}` in any `run:`, secrets only in the `Sign` step, no `dotnet` in the job with the key. `v1';calc;'` is a legal git ref; the tag check rejects it, and `v1.2` and `v1.2.3-test` too |
| R5.11 (key) | 2026-10-30 | Pass | Maintainer saved the PFX and password in KeePass and checked the saved copy's thumbprint before deleting. Checked afterwards: `%USERPROFILE%\.cartonstash\` gone, no `CN=Anthony` certificate in `CurrentUser\My` or `LocalMachine\My`, no key files in Desktop, Downloads, Documents, Temp, OneDrive or `C:\dev`. Public entries remain in `TrustedPeople` |
| R5.2, R5.3, R5.12 (CI) | 2026-10-30 | Pass | First run on GitHub's runner, run 41070202520 on `4166d0e`: hardened `ci.yml` green, restore from the lock files, build, 170 unit tests |
| R5.8 | 2026-10-30 | Pass | `(Get-PfxCertificate tools\CartonStash.cer).Thumbprint` prints the thumbprint in the README, `SECURITY.md` and release notes; the file's SHA-256 equals the v1.0.0 release asset |
| R5.3, R5.9, R5.10, R5.13 (T15a) | 2026-10-30 | Pass | Tried every setting on the private repo. Read back with `gh api`: Actions `selected`, GitHub-owned only; security updates on; immutable releases on; `release` environment limited to `v*` tags. Refused: required reviewer, rulesets, secret scanning, private reporting, outside-contributor approval (T15b) |
| Repo recreated | 2026-10-30 | Done | Fresh repo at the same URL with history rewritten to the GitHub noreply address, so the personal address isn't published. `v1.0.0` and its release stayed in the old repo, so the first release here is `v1.0.1`. Commit SHAs and run IDs earlier in this log refer to the old repo |
