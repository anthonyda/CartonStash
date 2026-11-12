# 006 – Settings: Design

**Status:** Approved (2026-10-29), amended during implementation. Amendment R4.3 approved 2026-10-30. Amendment R3.4 approved 2026-11-01. Amendment R6 approved 2026-11-12
**Requirements:** [requirements.md](requirements.md)

## Overview

A `SettingsService` persists a small `AppSettings` record as JSON. A `SettingsViewModel` binds the page.
Source information comes from the WinGet service. Theme is applied by setting `RequestedTheme` on the window's
root element and updating the title-bar button colours.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `AppSettings` | `Theme` (System/Light/Dark), `InstallScope` (Any/User/System), `StartPage` (Search/Installed/Updates, R5) | new; `StartPage` added by amendment R5 |
| Core/Models | `SourceInfo` | Name, Type, Argument (URL), TrustLevel, Explicit, LastUpdated, AgreementsPending | new |
| Core/Services | `ISettingsStore` + `JsonSettingsStore` | Load / save `%LOCALAPPDATA%\CartonStash\settings.json` (*amended 2026-10-30:* folder renamed with the app; the old `WinGetFrontend` file is copied once); tolerant of missing or corrupt files (fall back to defaults, keep a `.bak`) | new |
| Core/Services | `ISettings` (singleton) | Current settings + `Changed` event; used by the 003 installer for scope (R2.1) | new |
| Core/Services | `IPackageService` | `GetSourcesAsync`, `RefreshSourceAsync(name)` (*amended:* one source per call; the view model loops and reports "n of m"), `GetWinGetVersion()`, `AcceptSourceAgreements(name)` | new methods |
| Core/ViewModels | `SettingsViewModel` | Theme, scope, start page (R5), sources, refresh command, versions, diagnostic text | new |
| App | `SettingsPage` | `SettingsCard`-style layout (from WinUI Gallery patterns), `NavigationView` settings item | new |
| App | `ThemeService` | Applies `ElementTheme` + title-bar colours (R1.1) | new |
| App | `IClipboardService` | Copies the diagnostic text (R4.2) | new |

## Start page (amendment R5, approved 2026-10-30)

- `AppSettings.StartPage` is a new enum `StartPage { Search, Installed, Updates }`, default `Search`. Stored by name in the
  same JSON file (`"StartPage": "Updates"`).
- Older settings files have no `StartPage`: System.Text.Json leaves the record's default (`Search`), so no reset notice (R5.4).
  An unknown value (`"StartPage": "Purple"`) counts as a corrupt file, like an unknown theme today.
- `SettingsViewModel.StartPageIndex` binds a ComboBox under **Appearance → Start page** and calls `ISettings.Update` (saved at once).
  Nothing navigates when it changes (R5.3).
- `MainWindow` reads `ISettings.Current.StartPage` once at startup, selects the matching `NavigationViewItem` and navigates the
  frame to that page (R5.2). Today it always opens Search.
- The installed list still preloads in the background at startup (004 badge), so opening on Installed or Updates shows the
  progress bar briefly, then the list. No extra WinGet calls.

## WinGet API usage

- Sources: `GetPackageCatalogs()` → per reference `Info` (`Name`, `Type`, `Argument`, `TrustLevel`, `LastUpdateTime`,
  `Explicit` via `IPackageCatalogInfo2`). Read by index (W2).
- Refresh (R3.2): `PackageCatalogReference.RefreshPackageCatalogAsync()` (`IPackageCatalogReference7`) per source,
  sequentially, with progress "*n* of *m*". Afterwards `CatalogConnection.Invalidate()`.
- Agreements (R3.3): `PackageCatalogReference.SourceAgreements`. Accepting sets `AcceptSourceAgreements = true`
  on the references used by `CatalogConnection` for that session. This replaces 001's advice to run `winget search` in a terminal.
  *Amended:* the COM API doesn't say whether a source's agreements were already accepted, so every source that has
  agreements shows an **Agreements** button (Accept / Decline). The connect error now points to Settings → Sources.
- Versions (R4.1): `PackageManager.Version` (`IPackageManager7`). App version comes from the assembly informational version.

## Data flow

Settings change → `ISettings.Update(...)` → save JSON → `Changed` → `ThemeService` applies the theme; the installer
reads the scope on its next operation. Settings page load → `GetSourcesAsync` + versions.

## UI

```
Settings
 Appearance       Theme                 [ Use system setting ▾ ]
                  Start page            [ Search ▾ ]            (R5: Search / Installed / Updates)
 Installing       Default install scope [ Let WinGet decide ▾ ]
                  ⓘ "All users" may show a UAC prompt for each install.
 Sources          winget     Microsoft.PreIndexed.Package   Trusted   updated 2 h ago
                  msstore    Microsoft.Rest                 Trusted   updated 2 h ago
                  [ Refresh sources ]
 About            CartonStash 0.1.0 · WinGet 1.29.380   [ Copy diagnostic info ]
```

## Error handling and edge cases

- Corrupt settings file (bad JSON or unknown enum values) → defaults + `.bak` copy + a one-time InfoBar "Settings were reset".
- Saves write a temp file and then move it into place, so a crash mid-save can't leave a half-written file.
- Source refresh failure → per-source error line, others continue.
- Scope "User" with a package that only has machine installers → WinGet returns `NoApplicableInstallers` →
  the 003 message suggests changing the scope in Settings.

## Amendment R4.3: safe diagnostics (approved 2026-10-30)

- `SettingsViewModel.BuildDiagnosticText` prints a cleaned address from a new pure helper, `SafeSourceAddress(string)`.
- Web URLs drop user name, password, query and fragment. Anything that isn't an http(s) URL becomes `(local source)`, because local paths can hold the user name.
- Only the copied text changes. The Settings page still shows the full address to the user.

## Amendment R3.4: startup check and source agreements (approved 2026-11-01)

Requires 011 merged first: it brings `WinGetAvailability`, `WinGetSetupWindow`, `_failedSources` and `SourceNotice`.

### Startup order

```
App.OnLaunched
  WinGetAvailability.IsInstalled()        011, nothing activates WinGet before this
    no  → WinGetSetupWindow               R3.5, R3.7
    yes → MainWindow
Root.Loaded (dialogs can show)
  SourceAgreementCheck.RunAsync()         R3.4: GetPackageCatalogs reads the list, it doesn't connect
    for each source needing an answer → dialog (R3.5, R3.6)
  installed.EnsureLoadedAsync()           moved here from the MainWindow constructor
```

The preload has to move: today it connects before anything can ask (R3.8). Search stays behind the modal dialog until it closes.

### Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `AppSettings.SourceAgreementAnswers` | Dictionary of source ID → `SourceAgreementAnswer(bool Accepted, string TextHash)`. Empty in older files, with no reset notice | New |
| Core/Services | `SourceAgreementPolicy` | Pure: `Needs(answer, hash)` and `MayUse(answer, hash)`, the hash of the agreements, the notice text for a source | New |
| Core/Services | `CatalogConnection` | Takes `ISettings`. `ConnectAsync` leaves out sources that `MayUse` refuses, and `LeftOutSources` lists them. The session `HashSet` goes | Changed |
| Core/Services | `IPackageService` | `AcceptSourceAgreements(name)` becomes `SetSourceAgreementAnswer(source, accepted)`, plus `LeftOutSources`. Withdrawing saves a decline. `SourceInfo` gains `Id` and `AgreementStatus` | Changed |
| Core/Services | `SourceAgreementCheck` | Runs the dialogs at start and for each button, keeps the list of sources not in use for the pages, and reloads the installed list after an acceptance | New |
| Core/Services | `AppInstallerSetup` | Downloads, checks and installs App Installer (R3.7). Uses `Windows.Management.Deployment`, not WinGet's API | New |
| Core/ViewModels | `WinGetSetupViewModel` (011) | **Set up for me** command, progress and outcome | Changed |
| Core/ViewModels | `SearchViewModel`, `SettingsViewModel` | **Read and accept** command, withdrawal | Changed |
| App | `IDialogService.ShowSourceAgreementAsync` | Returns Accepted, Declined or Dismissed. Escape counts as Dismissed, so it asks again at the next start (R3.9) | New |
| App | `WinGetSetupWindow`, `SearchPage`, `SettingsPage` | The buttons | Changed |

### Which sources are used (R3.8, R3.9)

- The key is `PackageCatalogInfo.Id` (`StoreEdgeFD` for `msstore`), so a renamed source keeps its answer.
- The hash is SHA-256 over each agreement's label, text and URL, read by index (W2).
- `ConnectAsync`, for each reference: declined → leave it out without reading its agreements, so nothing reaches the source. Otherwise read
  them: no agreements → add it. Accepted with a matching hash → set `AcceptSourceAgreements = true` and add it. Anything else → leave
  it out, because one refused reference fails the whole connect.
- The read reaches the source and throws when it can't (T19). The source then joins 011's failed sources until the next `Invalidate()`,
  so Search names it and the others keep working. `GetSourcesAsync` reports it as `AgreementStatus.Unknown`, which isn't asked about.
- `GetSourcesAsync` doesn't read a declined source either. **Read and accept** reads it through `ReadSourceAgreementsAsync`, and says
  so if it can't be reached.
- Answers change through `ISettings`, then `Invalidate()` drops the catalogs. This also meets 011 R1.5.
- If every source is left out, Search shows its usual empty message with the **Read and accept** notice above it, and no catalog is
  connected. The Installed list keeps working through `GetInstalledOnlyAsync`.
- A row whose package is from a left-out source gets the message "`msstore` isn't in use. Read and accept its agreements in Settings."
  This extends 011's `NotFound` check of `_failedSources`.

| Answer | Connect | Dialog at start | Button |
|--------|---------|-----------------|--------|
| None, or Dismissed | Left out | Yes | Read and accept |
| Accepted, same text | Used | No | Withdraw (Settings) |
| Accepted, text changed | Left out | Yes | Read and accept |
| Declined, same text | Left out | No | Read and accept |
| Declined, text changed | Left out | No | Read and accept |

### Dialog (R3.6, R3.12)

```
  msstore: source agreement
  Terms of Transaction                     (text or link from WinGet)
  This source receives your PC's 2-letter region, such as US.
  The winget command asks for its own acceptance.
                                [Decline]  [Accept]
```

The region line is fixed text for ID `StoreEdgeFD`. Other sources show only the agreements and the `winget` line.

### Search and Settings (R3.10, R3.11)

Search shows an `InfoBar` beside 011's notice, not inside it: "`msstore` isn't in use, so results leave it out." with
**Read and accept**. A failed source and a source not yet accepted are different problems. Settings shows the same button on the source row, or
**Withdraw** once accepted. Installed and Updates get no banner, as R3.10 requires only that they keep working.

### Installing App Installer (R3.7)

`WinGetSetupWindow` gains **Set up for me** beside **Get App Installer** and **Check again**. Every step needs that click (P2).

1. Download `Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle` and `DesktopAppInstaller_Dependencies.zip` from one pinned
   `microsoft/winget-cli` GitHub release, v1.29.380. These are the files 011 installed in a clean Sandbox. Progress is shown (P3).
2. Check both against the SHA-256 hashes pinned in the code, and refuse anything else. Windows checks the signature again on install.
   *Amended during T17:* the design said "the latest release, signed by Microsoft". Pinned hashes are stricter, need no signature
   code in the app, and an old App Installer still updates itself. If the release is ever removed, setup fails and the link remains.
3. Pass the dependencies for this PC's architecture and the bundle to `PackageManager.AddPackageByUriAsync`.
4. Run the existing Check again step, which 011 showed works in the same process. If anything fails, show why and keep the link.

Store policy, version 7.20: 10.2.3 forbids offering software that "does not enhance the functionality" of the product, and App
Installer is what the app needs to work, so it should be allowed. 10.2.4 requires the dependency to be disclosed at the start of the Store
description, so the listing needs checking. Store reviewers decide in the end.

T11 tested this in clean Sandboxes, unelevated: a test exe and a test MSIX with only `runFullTrust` both installed App Installer through
`AddPackageByUriAsync`, and `winget` then ran. So the MSIX needs no `packageManagement` capability, and both builds offer **Set up for me**.

### Testing

| Criteria | How |
|----------|-----|
| R3.8, R3.9 | `SourceAgreementPolicyTests`: every row of the table |
| R3.9 | `JsonSettingsStore` round trip; older file without answers loads with no reset |
| R3.5, R3.6 | `SourceAgreementCheckTests` with fake dialogs: each answer, region line for `msstore` only |
| R3.10, R3.11 | `SearchViewModelTests`, `SettingsTests` |
| R3.4, R3.8 | Integration in Sandbox: agreements unanswered, then no connection to the Store endpoint at start or on search, checked with a network capture or by blocking it |
| R3.12 | Manual: after Accept in the app, `winget search` still prompts |
| R3.7 | Manual in a clean Sandbox: portable and MSIX; fail the download too |
| R3.13 | Review of `PRIVACY.md` |

### Risks

- **The agreement text can change often:** each change asks accepted users again, by design (R3.9).
- **A GitHub release could lack the expected file names:** setup fails with the link as fallback, and nothing else depends on it.
- **Withdrawing leaves Store packages without update information:** this is the same as Decline, and the requirements state that effect.

## Amendment R6: window layout (approved 2026-11-12)

- `AppSettings.Window` (`WindowLayout?`: X, Y, Width, Height in physical pixels, `Maximized`) and `AppSettings.NavPaneOpen`
  (default true). Both missing in older files, which keep today's defaults (R6.4).
- `MainWindow` tracks the last *restored* bounds through `AppWindow.Changed`, so a maximized window still saves its normal
  size (R6.2). It saves through `ISettings.Update` in `AppWindow.Closing`, only when the close goes ahead (not when 003 R6.2
  cancels it).
- On start: `DisplayArea.GetFromRect(rect, DisplayAreaFallback.None)`; null → resize only (R6.3). Sizes under 500 × 400 are
  ignored. Then `OverlappedPresenter.Maximize()` if saved maximized, and `Nav.IsPaneOpen` from settings.

## Amendment R3.14 to R3.17: explicit sources (2026-11-12, Draft)

- `AppSettings.SearchedExplicitSources`: the IDs of explicit sources whose box is checked (case-insensitive set,
  default empty, missing reads as empty), with the hand-written `Equals` updated. Keyed by source ID, as
  `SourceAgreementAnswers` is (R3.17).
- `CatalogConnection.ConnectAsync` takes which explicit sources count. The remote-first catalog (search, details,
  install) passes the checked IDs; the local-first catalog (Installed, Updates, background check) passes none. A
  reference whose `Info.Explicit` is true and whose ID isn't counted is skipped before its agreements are read
  (R3.14). `SourcesInUse` follows the same rule, so source isolation never tests a source that wasn't used (011 R1.3).
- The rule is a small static method next to `IsInUse`, so it is unit tested without COM.
- Checking or unchecking a box saves the setting and calls `CatalogConnection.Invalidate()`, so the next search
  reconnects (R3.15).
- `SourceInfo` gains `UsedInSearches`. Settings page: in each explicit source's row, a `CheckBox` "Use in searches"
  with the source name in its accessible name (Q5), bound through the source row's view model. The R3.16 paragraph
  is a `TextBlock` above the list, shown only when a source is explicit. `DetailText` keeps "explicit" as a word, without
  "(excluded from default searches)", since the paragraph explains it.
- Refresh sources skips an explicit source whose box is off; the background check keeps skipping all of them.
- Tests: the rule for each catalog and box state; the setting's default and a file without it; the paragraph's
  visibility (unit). Manual check on the maintainer's PC, where `winget-font` exists: box off, a font only
  `winget-font` has isn't found, as with `winget search`; box on, it is found and installs; Installed and Updates the
  same either way; keyboard and Narrator read the box with the source name.

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.2, R2.1, corrupt file | Unit tests on `JsonSettingsStore` (temp dir) and on the installer reading scope via a fake `ISettings` |
| R3.1, R4.1 | Integration test: sources include `winget`; version not empty |
| R3.2 | Integration test (refresh `winget` source succeeds), marked slow |
| R4.2 | Unit test on diagnostic text format |
| R1.1, R2.2, R3.3 | Manual |

## Risks and alternatives considered

- **`ApplicationData.Current` for settings:** unavailable in unpackaged apps (throws). Hence JSON in LocalAppData until 007.
