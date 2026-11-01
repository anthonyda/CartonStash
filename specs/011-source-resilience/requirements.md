# 011 – Source failures and missing WinGet: Requirements

**Status:** In progress
**Depends on:** 001, 003, 005, 006

## Problem

One failing source stops every search, and the message blames the user's internet connection. On a PC without WinGet the app
crashes at start with no explanation. Both came up in 007 T36.

## Findings

Tested 2026-11-01 with WinGet 1.29.380, through the COM API in Windows Sandbox.

**Why one source stops everything.** WinGet's composite catalog records a failure per source and carries on, then
`PackageCatalog::FindPackages` rethrows the first failure and drops all the matches. `FindPackagesResult` has no per-source
detail. The `winget` command handles the same failure with a warning, so it looks fine there.

**Why `msstore` fails in Sandbox.** Sandbox's Windows region is "World" (GeoID 39070), so WinGet sends the market `ZZ`. The Store
answers HTTP 400, "Parameter market was specified with invalid value: ZZ", which WinGet reports as `0x8A15003B`.
Agreements are not the cause: the 400 comes back with them accepted, and search works with them reset once the region is real.

| Scenario | Search | Details | Installed | Status and code |
|----------|--------|---------|-----------|-----------------|
| Region World | Fails | Not run | Works | `CatalogError`, `0x8A15003B` |
| Real region, agreements reset (a fresh PC) | Works | Not run | Works | |
| Real region, Store unreachable | Fails | Fails | Works | `InternalError`, `0x80072EFD` |

**Who is affected.** A PC with a real region is not hit by the `ZZ` case. Any PC is hit when a source can't be reached, for example
behind a firewall, during an outage, or with a broken third-party source. Install and update find their package through the same
lookup as Details, so they should fail too. That last step is read from the code, not tested.

**Crash without WinGet.** `0xc0000409` in `Microsoft.Management.Deployment.dll` at start, seen in 007 T36. This is a fail-fast
in native code, so .NET can't catch it. The design must check for WinGet before activating it, and must confirm where it crashes.

## User stories

- **US1.** As a user with one broken source, I want the other sources to keep working, so that I can still find and install packages.
- **US2.** As a user, I want to know which source is missing and why, so that I fix the real problem and don't check a working connection.
- **US3.** As a user without WinGet, I want the app to open and tell me what's missing, so that I can fix it.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1 | If a search fails in some sources and works in at least one, then the app shall show the results from the working sources. |
| R1.2 | US1 | If a source fails, then the app shall still open details for, install, update and uninstall packages from the working sources. |
| R1.3 | US1 | The app shall decide that a source failed from WinGet's result for that source, whatever the overall status (both `CatalogError` and `InternalError` were seen). |
| R1.4 | US1 | While every source works, the app shall make the same WinGet calls as before, with no extra per-source requests. |
| R1.5 | US1 | When the user refreshes, changes a source, accepts an agreement or restarts the app, the app shall try every source again. |
| R1.6 | US1 | The app shall not remove, disable or otherwise change the user's sources, and shall not store which ones failed. |
| R2.1 | US2 | While results leave out a source, the Search page shall show a notice that names the source and gives the reason, and the results shall stay usable. |
| R2.2 | US2 | When WinGet's error shows a network failure, the app shall say that the source can't be reached. |
| R2.3 | US2 | When a source responds with an error, the app shall say that the source reported a problem, and shall not suggest checking the internet connection. |
| R2.4 | US2 | If a source fails with `0x8A15003B` while the Windows region is not a country, then the app shall suggest setting the country in Windows Settings, Time & language, Language & region. |
| R2.5 | US2 | If every source fails, then the app shall show the error with the same reasons as R2.2 to R2.4, and shall not show "no results". |
| R2.6 | US2 | If an action needs a package from a source that failed, then the app shall say which source failed, and shall not say that the package is gone. |
| R2.7 | US2 | Messages shall stay readable, with no raw HRESULT (001 R3.4). The code stays on `WinGetException`. |
| R3.1 | US3 | If WinGet is not installed, then the app shall open its window and shall not crash. This applies to the MSIX and portable builds. |
| R3.2 | US3 | While WinGet is missing, the app shall explain that it needs WinGet, part of App Installer, and shall offer a link to get it. |
| R3.3 | US3 | While WinGet is missing, the app shall make no WinGet calls, and shall show the explanation in place of the pages that need WinGet. |
| R3.4 | US3 | When the user chooses Check again after installing WinGet, the app shall carry on without a restart, or shall say that a restart is needed. |
| R3.5 | US3 | If WinGet is installed but can't be started, and the failure can be caught, then the app shall show the same explanation and shall not crash. |
| R3.6 | US3 | The explanation shall be keyboard reachable, have accessible names, and work in light, dark and high-contrast themes (Q5). |

## Verification

Each scenario runs in Windows Sandbox: the portable app in all four, the MSIX in at least S3.

| # | Setup | Covers |
|---|-------|--------|
| S1 | Region World, `msstore` present | R1.1, R2.1, R2.3, R2.4 |
| S2 | Real region, Store blocked in the hosts file, new query each run (WinGet caches REST replies) | R1.1, R1.2, R1.3, R2.1, R2.2, R2.6 |
| S3 | App Installer removed | R3.1 to R3.6 |
| S4 | Real region, nothing blocked | R1.4, and no notice shown |

Unit tests cover the logic that doesn't need WinGet: choosing the sources, the error messages and the view models.

## Out of scope

- Installing App Installer for the user (P2), or declaring it as an MSIX dependency.
- Changing the Windows region for the user.
- A notice on Installed or Updates. Installed kept working in every scenario, so there is nothing to report there yet.
- `PackageCatalogReference.AcceptSourceAgreements` defaults to true, so `msstore` is used without the user accepting its agreements. This looks at odds with 006 R3.3, and needs its own decision.
- Reporting the two WinGet behaviours upstream.

## Open questions

- None block this review. The design will settle how to isolate a failed source, and how to detect WinGet safely.
