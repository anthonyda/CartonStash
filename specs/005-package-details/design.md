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

- Wide (≥ 900 px): pane is a right column 380 px wide. Narrow: pane covers the list and has a Back button.
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
