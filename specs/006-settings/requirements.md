# 006 – Settings: Requirements

**Status:** Approved (2026-10-29). Amendment R5 (start page) approved 2026-10-30. Amendment R4.3 (safe diagnostics) approved 2026-10-30
**Depends on:** 002, 003

## Problem

Install behaviour (user vs machine scope), the app's look, and which WinGet sources are in use are all hidden
today. Users need one place to see and change them.

## User stories

- **US1.** As a user, I want to choose the app theme (Light, Dark, Use system setting).
- **US2.** As a user, I want to choose the default install scope (WinGet default, Current user, All users / machine).
- **US3.** As a user, I want to see WinGet's sources and refresh them when results look stale.
- **US4.** As a user, I want to see the app and WinGet versions to report problems.
- **US5.** As a user, I want to choose which page the app opens on, so I land where I usually start (e.g. Updates).

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When the user changes the theme, the app shall apply it immediately without restarting. |
| R1.2 | –     | The app shall save settings per Windows user and restore them on start. |
| R2.1 | US2   | The install scope setting (default "Let WinGet decide") shall be used for every install and upgrade (003 / 004). |
| R2.2 | US2   | When "All users" is selected, the app shall explain that installers may show a UAC prompt. |
| R3.1 | US3   | The Settings page shall list WinGet's configured sources with name, type, URL, trust level, whether they are explicit (excluded from default searches), and last update time. |
| R3.2 | US3   | When the user presses **Refresh sources**, the app shall refresh each source, show progress, and then clear the cached catalogs so the next search uses fresh data (002 `CatalogConnection.Invalidate`). |
| R3.3 | US3   | If a source requires accepting an agreement, then the app shall show the agreement text with Accept / Decline before using that source. |
| R4.1 | US4   | The Settings page shall show the app version and the WinGet (App Installer) version. |
| R4.2 | US4   | The Settings page shall show a "Copy diagnostic info" button that copies versions, OS build and source list as plain text. |
| R4.3 | US4   | The copied diagnostic text shall not carry secrets or personal paths from a source's address: web URLs shall keep only scheme, host, port and path, and anything else shall appear as `(local source)`. *(Security review, 2026-10-30: the README invites public bug reports, and a private source's URL can hold a token.)* |
| R5.1 | US5   | The Settings page shall offer a **Start page** setting with the choices Search, Installed and Updates. The default is Search. |
| R5.2 | US5   | When the app starts, it shall open the chosen start page and show it as selected in the navigation. |
| R5.3 | US5   | Changing the start page shall take effect the next time the app starts; it shall not navigate away from Settings. |
| R5.4 | –     | If the saved start page is missing (settings from an older version), the app shall use Search without reporting a reset. |

## Out of scope

- **Adding / removing / editing sources.** The COM API has `AddPackageCatalogAsync`, `RemovePackageCatalogAsync` and
  `EditPackageCatalog`, but they require admin rights (A5 conflict). Possible later spec with a separate elevated helper.
- Proxy, download directory, telemetry and other WinGet `settings.json` options. Users can still edit WinGet's own settings file.
- Language / localization (English only in v1).

## Open questions

- **Start page (R5): also offer "Last used page"?** **Decided 2026-10-30: no for now.** 002 R4.1 deliberately doesn't remember the last page in v1;
  a fixed choice is predictable. Can be added later as a fourth option without changing the stored format much.
- **Start page: offer Settings as a choice?** **Decided 2026-10-30: no.** Nobody needs to land on Settings every day.

- Settings storage? **Decided: JSON in `%LOCALAPPDATA%\CartonStash\settings.json` behind `ISettingsStore`; `ApplicationData` comes with 007.**
