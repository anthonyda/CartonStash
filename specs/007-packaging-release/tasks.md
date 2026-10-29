# 007 – Packaging and release: Tasks

**Status:** Draft (blocked on open questions: signing, name, hosting)
**Design:** [design.md](design.md)

- [ ] **T1.** Decide name / publisher / icon; add `Package.appxmanifest` + `Assets/`. *(R1.2)*
- [ ] **T2.** `PackagedRelease` configuration; local MSIX build signed with a dev cert. *(R1.1)*
  - Verify: MSIX installs locally; app starts from Start.
- [ ] **T3.** Packaged WinGet check: search / install / uninstall from the MSIX build. *(R1.3)*
  - Verify: manual in Windows Sandbox.
- [ ] **T4.** `ApplicationDataSettingsStore` + one-time migration. *(R4.1)*
  - Verify: unit tests.
- [ ] **T5.** Tag-based versioning. *(R3.2)*
- [ ] **T6.** `ci.yml` (build + unit tests) and `release.yml` (artifacts, signing, checksums, GitHub Release). *(R1.4, R2.1, R3.1)*
  - Verify: test tag produces all artifacts.
- [ ] **T7.** First public release + winget-pkgs PR. *(R2.2)*
- [ ] **T8.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
