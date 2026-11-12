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
- [x] **T20.** Window layout: `WindowLayout` and `NavPaneOpen` in `AppSettings`; save on close and restore on start in `MainWindow`. *(R6.1 to R6.5)*
  - Verify: `SettingsTests.Window_layout_survives_a_restart`, `File_from_before_window_layout_keeps_defaults`; manual in the VM (resize, move, maximize, collapse the pane, pin Activity, restart).
- [ ] **T6.** Mark `Done`.

### Amendment R5: start page (approved 2026-10-30)

- [x] **T7.** `StartPage` enum + `AppSettings.StartPage` (default Search); store accepts files without it, rejects unknown values. *(R5.1, R5.4)*
  - Verify: `SettingsTests.*` (round trip, old file without StartPage → Search, unknown value → reset).
- [x] **T8.** `SettingsViewModel.StartPageIndex` + ComboBox in Settings → Appearance. *(R5.1, R5.3)*
  - Verify: `SettingsTests.View_model_shows_and_writes_the_start_page`; manual: changing it doesn't navigate (see log).
- [x] **T9.** `MainWindow` opens and selects the saved start page at launch. *(R5.2)*
  - Verify: manual (UIA): set Updates, restart → Updates page shown and selected; reset to Search afterwards.

### Amendment R4.3: safe diagnostics (approved 2026-10-30)

- [x] **T10.** `SafeSourceAddress` and use it in `BuildDiagnosticText`. *(R4.3)*
  - Verify: `SettingsTests.Diagnostics_remove_secrets_from_source_addresses` (token in the query, user:password in the URL, local path).

### Amendment R3.4: startup check and source agreements (approved 2026-11-01)

Starts after 011 is merged. T12 to T16 and T18 don't depend on T11's result.

- [x] **T11.** Probe: install App Installer through `AddPackageByUriAsync`, unelevated, in a clean Sandbox, from the portable build and the MSIX. Record whether the MSIX needs `packageManagement`; update the design first if the plan changes. *(R3.7)*
  - Verify: results recorded in the log below.
- [x] **T12.** `SourceAgreementAnswer`, `AppSettings.SourceAgreementAnswers`, `SourceAgreementPolicy`. *(R3.8, R3.9)*
  - Verify: `SourceAgreementPolicyTests` (every row of the design's table); store round trip; older file loads with no reset.
- [x] **T13.** `CatalogConnection` uses the saved answers and leaves sources out; `LeftOutSources`; `SourceInfo.Id` and `AgreementStatus`; replace the session `HashSet` and `AcceptSourceAgreements`. *(R3.8, R3.9)*
  - Verify: integration test that an unanswered `msstore` isn't in the composite; `dotnet test`.
- [x] **T14.** `ShowSourceAgreementAsync`, `SourceAgreementCheck`, and move the installed preload behind the check. *(R3.4, R3.5, R3.6, R3.12)*
  - Verify: `SourceAgreementCheckTests` with fake dialogs (each answer; region line for `msstore` only).
- [x] **T15.** **Read and accept** on Search and Settings, **Withdraw** in Settings, and the left-out source message on rows. *(R3.10, R3.11)*
  - Verify: `SearchViewModelTests`, `SettingsTests`.
- [x] **T16.** Handle every source left out: Search shows the button, Installed keeps working. *(R3.10)*
  - Verify: unit test, plus T19.
- [x] **T17.** `AppInstallerSetup` and **Set up for me**, as T11 decided: download, signature check, install, Check again, fallback to the link. *(R3.5, R3.7)*
  - Verify: unit tests for the signature refusal and the outcomes; T19.
- [x] **T18.** Update `PRIVACY.md` and its date, the README, and specs 001 and 010 where they mention agreements. Reopen the 2026-10-30 decision in 008's design. Check that the Store listing discloses WinGet at the start of its description (Store policy 10.2.4). *(R3.13)*
  - Verify: review. Done in the repo; the Store listing is the maintainer's to check and change, and `PRIVACY.md` must go live with the release.
- [x] **T19.** Manual in Windows Sandbox: agreements unanswered with the Store endpoint blocked or captured, then each answer; `winget search` still prompts after Accept; clean Sandbox setup with portable and MSIX, including a failed download; keyboard, light, dark and high contrast. *(R3.4 to R3.12, Q5)*
  - Verify: results recorded in the log below.

## Amendment R3.14 to R3.17: explicit sources (2026-11-12, bug review BR-01)

- [x] **T20.** `AppSettings.SearchedExplicitSources`; `CatalogConnection.Counts` in `ConnectAsync` and `SourcesInUse`;
  `GetSourcesAsync` doesn't read an unused explicit source's agreements; Settings: a **Use in searches** box per
  explicit source, the paragraph, Refresh sources follows the box. *(R3.14 to R3.17)*
  - Verify: `ExplicitSourceTests`.
- [ ] **T21.** Manual in the test VM with an explicit source (`winget-font`, or one added with `--explicit`): box off,
  a font only it has isn't found, as with `winget search`; box on, it is found and installs; Installed and Updates the
  same either way; the paragraph; keyboard and Narrator read the box with the source name. *(R3.14 to R3.17)*

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1 | 2026-10-29 | Pass | Theme Dark → Light applied instantly, title bar buttons included (screenshot) |
| R1.2 | 2026-10-29 | Pass | `settings.json` written; after restart the Theme combo shows Light. Reset to "Use system setting" afterwards |
| R2.2 | 2026-10-29 | Pass | "All users (machine)" shows the UAC InfoBar; "Let WinGet decide" hides it |
| R3.1 | 2026-10-29 | Pass | msstore (Microsoft.Rest), winget, winget-font (explicit) with trust level and last update time |
| R3.2 | 2026-10-29 | Pass | Refresh sources → "Sources refreshed." |
| R3.3 | 2026-10-29 | Pass, **withdrawn 2026-11-01** | msstore Agreements → dialog "Terms of Transaction" [Accept] [Decline]; Decline accepted nothing. This checked the dialog only: the app used `msstore` anyway. Re-verify in T19 |
| R4.1, R4.2 | 2026-10-29 | Pass | "CartonStash 1.0.0 · WinGet 1.29.380"; clipboard holds versions, OS build and the 3 sources |
| R5.1, R5.3 | 2026-10-30 | Pass | UIA: Settings → Start page → Updates; app stays on Settings; `settings.json` has `"StartPage": "Updates"` |
| R5.2 | 2026-10-30 | Pass | Restart → Updates page shown, Updates selected in navigation (screenshot). Reset to Search afterwards |
| R5.4 | 2026-10-30 | Pass (unit) | `SettingsTests.File_from_before_start_page_opens_on_search_without_a_reset`; live file from before R5 also loaded without a notice |
| R4.3 | 2026-10-30 | Pass | `Diagnostics_remove_secrets_from_source_addresses` covers a token, user:password and local paths; `Copied_diagnostics_hold_no_source_secrets` goes through the Copy command |
| R3.7 (T11) | 2026-11-01 | Pass | Clean Sandbox per run, unelevated token, v1.29.380 bundle and x64 dependencies through `AddPackageByUriAsync`: unpackaged exe and test MSIX (`runFullTrust` only) both registered; `winget` v1.29.380 ran afterwards |
| R3.5, R3.7 | 2026-11-01 | Pass | Clean Sandbox, App Installer count 0. Portable and MSIX: Set up for me shown, with the size; real download and install; main window opens in the same process |
| R3.4, R3.6, R3.9 | 2026-11-01 | Pass | Portable: dialog at start, with the Terms link and region line (screenshot). Escape: not in use, asked again next start. Decline: not asked again. MSIX: accepted start shows no dialog, process responding at +8 s and +33 s |
| R3.8 | 2026-11-01 | Pass | MSIX, Store address sent to a local listener (self-test 2 hits). Declined, through start and search: **0** attempts. Unread, Store unreachable: search still returns `winget` results and names `msstore` (found a bug: the read failure failed every search; fixed). After Accept on the real network, `msstore` results appear |
| R3.10 | 2026-11-01 | Pass | Search notice with Read and accept, after start and after search; accepted: notice gone (found a bug: it stayed open with no text, because x:Bind doesn't re-run a function for a null argument; fixed) |
| R3.8 | 2026-11-01 | Pass | After one real read, WinGet keeps a copy: Read and accept showed the dialog with the Store unreachable and 0 attempts. With that copy deleted and the source declined: 0 attempts at start, then 1 on Read and accept, and the "can't be reached" message with OK |
| R3.7 | 2026-11-01 | Pass | Portable, `github.com` blocked: the reason and "You can still use Get App Installer" shown, nothing installed. Unblocked: Set up for me in the same window worked. The message was then reworded to drop network jargon |
| R3.11 | 2026-11-01 | Pass | Portable: Withdraw in Settings → status "msstore isn't in use", button back to Read and accept, Search notice back, no dialog at next start. Focus moves to Copy diagnostic info afterwards, because the list reloads |
| Q5, R3.5, R3.10 | 2026-11-01 | Pass | Final portable build, keyboard: dialog opens on the Terms link, Tab reaches Accept, Enter accepts. From the Search box, Tab reaches Read and accept, Enter opens the dialog. Dark and high contrast (High Contrast Black, confirmed on): notice, dialog and Settings are readable (screenshots) |
| R3.12 | 2026-11-01 | Pass | Portable: after Accept in the app, `winget search --source msstore` still prompted |
| R1.1 (high contrast) | – | **Pending** | Check the app with a Windows contrast theme on (constitution Q5) |
| R6.4 | 2026-11-12 | Pass | VM, Store MSIX 1.3.99: first start at 1200 × 780, navigation expanded |
| R6.1, R6.2, R6.5 | 2026-11-12 | Pass | Set to 300,100 1400 × 900, navigation collapsed, Activity pinned → restart restored all three, pane beside the page (screenshot) |
| R6.2 | 2026-11-12 | Pass | Maximized → restart opens maximized; un-maximize returns to 300,100 1400 × 900 |
| R6.3 | 2026-11-12 | Pass | Saved position set to 6000,3000 → window on screen at 156,156, still 1400 × 900 |
| R6.1 | 2026-11-12 | Pass | Navigation expanded again → restart keeps it expanded. No crashes during the run |
