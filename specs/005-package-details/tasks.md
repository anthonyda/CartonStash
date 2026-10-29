# 005 – Package details: Tasks

**Status:** In progress (code done; one manual check pending)
**Design:** [design.md](design.md)

- [x] **T1.** `PackageDetails` model + `GetDetailsAsync` (metadata, versions sorted via `CompareToVersion`). *(R1.2, R2.1, R1.5)*
  - Verify: integration test `Details_for_git_have_publisher_and_homepage`.
- [x] **T2.** `PackageDetailsViewModel`: load / cancel-on-switch / states / http(s)-only links. *(R1.4, R1.5)*
  - Verify: `PackageDetailsViewModelTests.*`.
- [x] **T3.** `PackageDetailsPane` UserControl (links, tags, release notes "Show more", versions, actions). *(R1.2, R1.3, R2.2, R3.1)*
  - Verify: manual, see log.
- [x] **T4.** Adaptive two-column layout on Search / Installed / Updates; Esc closes and refocuses the row. *(R1.1, R4.1)*
  - Verify: manual (UIA script).
- [x] **T6.** Package icons: `GetIconUrlAsync`, lazy row loading, row + pane display. *(R5.1, R5.2)*
  - Verify: `PackageRowIconTests.*` + manual, see log.
- [ ] **T5.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1 | 2026-10-29 | Pass | UIA: Enter on the "Wget" row opens the pane (keyboard path) |
| R1.2, R2.1 | 2026-10-29 | Pass | Wget: publisher, description, GPL-3.0-or-later + link, Homepage, Publisher website, tags, versions `1.21.4 · 1.21.3 · …` |
| R2.2 | 2026-10-29 | Pass | 7-Zip: release notes text + "Release notes online" link |
| R3.1 | 2026-10-29 | Pass | The pane shows the same Install button as the row |
| R4.1 | 2026-10-29 | Pass | UIA: Esc closes the pane; focus returns to ListItem "Wget" |
| Adaptive layout | 2026-10-29 | Pass | 1200 px: right column; 800 px: pane covers the list (opaque background, fixed during verification) |
| R5.1, R5.2 | 2026-10-29 | Pass | Screenshots: Firefox, CPU-Z, CrystalDiskMark icons in rows; glyph for packages without one (Git, Mozilla.Firefox.MSIX); list shown before icons arrive |
| Esc tooltip | 2026-10-29 | Pass | User report: hovering showed "Esc" everywhere. Page accelerator placement set to Hidden; user confirmed it is gone |
| R5.1 (bug) | 2026-10-30 | Fixed | User report: Chatterino showed the VS Code icon, Fusion / Blender a setup icon. WinGet has no icon for any of the three; recycled rows kept old pictures. After fix: glyph for all three after heavy scrolling; VS Code keeps its icon; pane switches VS Code → Chatterino correctly |
| R1.3 | – | **Pending** | Click a link and confirm the default browser opens (skipped so as not to open your browser) |
