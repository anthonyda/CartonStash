# 005 – Package details: Tasks

**Status:** Approved
**Design:** [design.md](design.md)

- [ ] **T1.** `PackageDetails` model + `GetDetailsAsync` (metadata, versions sorted via `CompareToVersion`). *(R1.2, R2.1, R1.5)*
  - Verify: integration test `Details_for_git_have_publisher_and_homepage`.
- [ ] **T2.** `PackageDetailsViewModel`: load / cancel-on-switch / states / http(s)-only links. *(R1.4, R1.5)*
  - Verify: `PackageDetailsViewModelTests.*`.
- [ ] **T3.** `PackageDetailsPane` UserControl (links, tags, release notes expander, versions, actions). *(R1.2, R1.3, R2.2, R3.1)*
  - Verify: manual.
- [ ] **T4.** Adaptive two-column layout on Search / Installed / Updates; Esc closes and refocuses the row. *(R1.1, R4.1)*
  - Verify: manual (UIA script).
- [ ] **T5.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
