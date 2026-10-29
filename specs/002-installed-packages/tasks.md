# 002 – Installed packages: Tasks

**Status:** Approved
**Design:** [design.md](design.md)

- [ ] **T1.** `CatalogConnection` with cached remote-first / local-first composites + `Invalidate()`; `SearchAsync` uses it.
  - Verify: existing tests stay green; integration search still finds Git.Git.
- [ ] **T2.** `IPackageService.GetInstalledAsync` + `PackageInfo.IsManaged`. *(R1.1, R1.2, R3.1)*
  - Verify: integration test `GetInstalled_contains_git`.
- [ ] **T3.** `InstalledViewModel`: load once, refresh, filter, sort, counts, error + retry. *(R1.3, R2.1–R2.3, R5.1)*
  - Verify: `InstalledViewModelTests.*`.
- [ ] **T4.** Shell: `NavigationView` + `Frame`, `INavigationService`, move search UI into `SearchPage`. *(R4.1, R4.2)*
  - Verify: manual, 001 behaviour unchanged (rerun 001 UIA checks).
- [ ] **T5.** `PackageRow` UserControl shared by Search and Installed. *(R1.2, R3.1, R3.2)*
  - Verify: manual.
- [ ] **T6.** `InstalledPage` with filter box, count (LiveRegion), refresh, progress, error InfoBar + Retry. *(R1.4, R2.2, R5.1)*
  - Verify: manual (UIA script).
- [ ] **T7.** Mark `Done`.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
