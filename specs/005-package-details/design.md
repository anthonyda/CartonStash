# 005 – Package details: Design

**Status:** Approved (2026-10-29), amended during implementation
**Requirements:** [requirements.md](requirements.md)

## Overview

New service method `GetDetailsAsync(PackageInfo)` returns a plain `PackageDetails` record built from WinGet's
`CatalogPackageMetadata`. A `PackageDetailsViewModel` drives a shared `PackageDetailsPane` UserControl hosted by
each list page in a two-column layout (adaptive via `VisualStateManager`).

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `PackageDetails` | Publisher, ShortDescription, Description, License, LicenseUrl, Tags, links (Homepage, Publisher, Support, Privacy, ReleaseNotesUrl), ReleaseNotes, Versions (newest first) | new |
| Core/Services | `IPackageService.GetDetailsAsync(PackageInfo, ct)` | Re-find by ID (as in 003), read metadata | new method |
| Core/ViewModels | `PackageDetailsViewModel` | Load with cancel-on-switch (as 001 R3.2), state (Loading / Loaded / Partial / Error), actions forwarded to the queue | new |
| App | `PackageDetailsPane` | Layout, `HyperlinkButton`s using `Launcher.LaunchUriAsync`, expander for release notes | new |
| App | Search / Installed / Updates pages | Two-column adaptive layout; selection → details VM; Esc closes and refocuses the row | changed |

## WinGet API usage

- `CatalogPackage.DefaultInstallVersion.GetCatalogPackageMetadata()` (`IPackageVersionInfo4`) →
  `Publisher`, `ShortDescription`, `Description`, `License`, `LicenseUrl`, `Tags`, `PackageUrl`, `PublisherUrl`,
  `PublisherSupportUrl`, `PrivacyUrl`, `ReleaseNotes`, `ReleaseNotesUrl`.
- `CatalogPackage.AvailableVersions` (`PackageVersionId.Version`), read by index (W2), kept in WinGet's own order.
  *Amended:* instead of sorting with one cross-process `CompareToVersion` call per comparison, one call checks the
  direction (default version vs. first entry) and reverses the list if needed. Live: Git, Wget and 7-Zip come newest first.
- Unmanaged packages: `DefaultInstallVersion == null` → `Partial` state with only `PackageInfo` data (R1.5).
- Metadata calls may block briefly, so they run inside `Task.Run` on the service side and honour cancellation.

## Package icons (R5, added 2026-10-29)

- Data: `CatalogPackageMetadata.Icons` (`Url`, `FileType`, `Resolution`, `Theme`). Measured on the dev machine: the
  winget source has `.ico` files on `cdn.winget.microsoft.com` for about a quarter to a half of packages (7-Zip, VS Code,
  Firefox, PowerToys yes; Git, Chrome, VLC no). msstore packages returned none.
- Cost: one metadata call per package (~17 ms; ~1.5 s for 89 managed installed apps), too slow to add to list loads.
- `IPackageService.GetIconUrlAsync(PackageInfo)`: the service keeps the `PackageVersionInfo` of each package from the last
  `SearchAsync` / `GetInstalledAsync` (COM objects stay inside Services, A2), reads `Icons` on a worker thread (at most 4 at a time),
  and caches the URL per package ID for the session. Unknown package → null (no extra search).
- `PackageRowViewModel.IconUri` is loaded by `EnsureIconAsync()`, which the `PackageRow` control calls when it gets a row.
  `ListView` virtualization means only rows on screen ask (R5.2). The pane header uses the row's icon.
- Display: `BitmapImage` (WIC decodes `.ico`), 32 px in rows, 48 px in the pane; a package glyph when there's no icon.
  *Amended 2026-10-30 (bug fix):* a `PackageIcon` control sets the picture in code whenever its row changes. With an x:Bind
  function, recycled list rows kept the previous package's icon when the new package had none (Chatterino showed VS Code's).

## Data flow

Row selected → `DetailsVM.Load(pkg)` cancels the previous load → `GetDetailsAsync` → state → pane binds.
Action button → same commands as the row → queue. The pane reflects the queue state for that package ID.

## UI

```
│ list …                     │ Git                                   [✕] │
│ ▸ Git        Installed     │ The Git Development Community         │
│   GitHub CLI               │ Distributed version control system.   │
│                            │ [Update to 2.57.0] [Uninstall]        │
│                            │ License: GPL-2.0 ↗   Homepage ↗       │
│                            │ Tags: git · vcs · cli                 │
│                            │ ▸ Release notes                       │
│                            │ Versions: 2.57.0 · 2.56.0.3 (installed) · … │
```

- Wide: pane is a right column 380 px wide. Narrow: pane covers the list; its Close button acts as Back.
  *Amended 2026-10-29:* the switch is on the **page** width (860 px), not the window width, so an expanded navigation pane
  can't squeeze the list. Rows reflow below 620 px (versions move under the name, names never wrap), and the
  navigation collapses to icons / a menu button on narrow windows (`PaneDisplayMode="Auto"`).
- `HyperlinkButton`s have accessible names like "Homepage (opens browser)".

## Error handling and edge cases

- Very long descriptions: `TextBlock` wrapping inside a `ScrollViewer`. Release notes are collapsed by default.
- Only `http`/`https` links are launched. Anything else (e.g. `file:`) is shown as plain text (security).

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.4, R1.5, R2.1, R2.2 (truncation flag), R4.1 (VM part) | Unit tests on `PackageDetailsViewModel` with fake service |
| R1.2 | Integration test: details for `Git.Git` have publisher + homepage |
| Link scheme filter | Unit test |
| R1.1, R1.3, R3.1, R4.1 | Manual (UIA script + click a link) |

## Risks and alternatives considered

- **WebView2 for rich release notes:** rejected (R1.3; heavy dependency, security surface). Plain text only.
