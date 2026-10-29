# 006 – Settings: Requirements

**Status:** Approved (2026-10-29)
**Depends on:** 002, 003

## Problem

Install behaviour (user vs machine scope), the app's look, and which WinGet sources are in use are all hidden
today. Users need one place to see and change them.

## User stories

- **US1.** As a user, I want to choose the app theme (Light, Dark, Use system setting).
- **US2.** As a user, I want to choose the default install scope (WinGet default, Current user, All users / machine).
- **US3.** As a user, I want to see WinGet's sources and refresh them when results look stale.
- **US4.** As a user, I want to see the app and WinGet versions to report problems.

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

## Out of scope

- **Adding / removing / editing sources.** The COM API has `AddPackageCatalogAsync`, `RemovePackageCatalogAsync` and
  `EditPackageCatalog`, but they require admin rights (A5 conflict). Possible later spec with a separate elevated helper.
- Proxy, download directory, telemetry and other WinGet `settings.json` options. Users can still edit WinGet's own settings file.
- Language / localization (English only in v1).

## Open questions

- Settings storage? **Decided: JSON in `%LOCALAPPDATA%\WinGetFrontend\settings.json` behind `ISettingsStore`; `ApplicationData` comes with 007.**
