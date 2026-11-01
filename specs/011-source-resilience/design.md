# 011 – Source failures and missing WinGet: Design

**Status:** Approved
**Requirements:** [requirements.md](requirements.md)

## Overview

WinGet won't say which source failed, and drops every match when one does. So when a composite search fails, the app tests each
source alone with the same request, leaves out the ones that fail, and runs the search again. This lives in `CatalogConnection`, so
search, details, install, update and uninstall all get it. A healthy search takes the same path as today.

For a missing WinGet, the app checks through Windows' package list before anything activates WinGet, and opens a small window
that explains the problem in place of `MainWindow`.

## Prototype results

Tested 2026-11-01 in Windows Sandbox with a harness on the COM API, in the two failing scenarios from the requirements.

| Request | Region World | Store unreachable |
|---------|--------------|-------------------|
| Remote, all sources | `CatalogError`, `0x8A15003B` | `InternalError`, `0x80072EFD` |
| Remote, `msstore` alone | Same failure | Same failure |
| Remote, `winget` alone | Ok | Ok |
| Remote, without `msstore` | Ok | Ok |
| Local-first, by ID, all sources | Ok | `InternalError`, `0x80072EFD` |
| Local-first, by ID, without `msstore` | Ok | Ok |

The local-first row is the lookup behind update and uninstall, so those fail during an outage too. Testing one source took
about 2 seconds when the Store was blocked, and under 200 ms otherwise.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `SourceFailure` | Record: source name, readable reason, HRESULT | New |
| Core/Services | `SourceIsolation` | The retry logic, with WinGet behind delegates so it can be unit tested | New |
| Core/Services | `SourceFailureMessages` | Turns a source name and HRESULT into a reason (R2.2 to R2.4) | New |
| Core/Services | `CatalogConnection` | Excludes failed sources, runs every composite find through `SourceIsolation` | Changed |
| Core/Services | `IPackageService`, `WinGetPackageService` | `SourceFailures` property; find methods go through `CatalogConnection` | Changed |
| Core/Services | `WindowsRegion` | Whether the Windows region is a country | New |
| Core/Services | `WinGetAvailability` | Whether App Installer is installed, without touching WinGet | New |
| Core/ViewModels | `SearchViewModel` | `SourceNotice` text after each search | Changed |
| Core/ViewModels | `WinGetSetupViewModel` | Check again, and the text for each state | New |
| App | `App.xaml.cs` | Chooses the window at start | Changed |
| App | `WinGetSetupWindow` | The explanation | New |
| App/Views | `SearchPage` | `InfoBar` for the notice | Changed |

## WinGet API usage

Only `CreateCompositePackageCatalog`, `ConnectAsync` and `FindPackagesAsync`, as today. A probe is a composite holding one source, so
it behaves the same way as the real search. WinGet collections are read by index (W2), and no COM object leaves Services (A2).

`CatalogConnection.ConnectAsync` skips names in `_failedSources`. `Invalidate()` clears them along with the cached catalogs, which
covers R1.5 for refresh, source changes and accepted agreements. Nothing is written to disk and sources aren't touched (R1.6).

Find requests move to a factory, `Func<FindPackagesOptions>`, because a probe needs a fresh options object.

## Data flow

```
find on the composite
  Ok                                → results (R1.4: no extra calls)
  CatalogError or InternalError     → probe every source in use, in parallel
    none failed                     → find again once, and report what that returns (a brief failure, or another search got there first)
    some failed                     → record them, drop the cached composite, reconnect, find again (R1.1, R1.2)
    all failed                      → throw, with each source's reason (R2.5)
  any other status                  → throw as today, including BlockedByPolicy
```

A failed `ConnectAsync` with `CatalogError` takes the same route. `SourceAgreementsNotAccepted` does not, because it's not a source
failure. With only one source in use there's nothing to compare, so the app skips the probe and describes that source's error.

Two searches can probe at once. That's harmless: recording a failure twice changes nothing, and the second search simply retries.
The failure status isn't used to choose the reason, only the HRESULT (R1.3).

`FindInCatalogAsync` returns not found when a package's source was left out. `NotFound` then checks `_failedSources` and names the
source instead of saying the package is gone (R2.6).

Search reads `IPackageService.SourceFailures` after each search and sets `SourceNotice`. Returning the failures in `SearchAsync`'s
result would be tidier, but would change every caller and fake for one consumer.

### Choosing the reason

| HRESULT | Meaning | Text |
|---------|---------|------|
| `0x80072EE0` to `0x80072FFF` | WinHTTP network errors | `msstore` can't be reached. Check your internet connection or firewall. |
| `0x8A15003B` while the region is not a country | Store rejects the market | `msstore` needs a country. Set one in Windows Settings, Time & language, Language & region. |
| `0x8A15xxxx`, `0x8019xxxx` | The source answered with an error | `msstore` reported a problem. This isn't your connection. |
| Anything else | Unknown | `msstore` failed (WinGet reported an error). |

The notice reads "Results don't include: " followed by the reasons. With several sources, each gets its own sentence.

`WindowsRegion` calls `GetUserDefaultGeoName`: two letters means a country. Sandbox returned `001` as World and `US` once set.

## Missing WinGet

Tested 2026-11-01 in a clean Windows Sandbox, where WinGet is really absent: no package and no `winget.exe` alias, and neither
appeared in the 2 minutes watched.

| Step | Result |
|------|--------|
| `GetPackagesByPackageFamily`, WinGet absent | Returns 0, count 0, process unharmed |
| `new PackageManager()`, WinGet absent | Crashes with `0xc0000409` at offset `0x31a5` in `Microsoft.Management.Deployment.dll`, the same offset as the real app. A `catch` doesn't run |
| App Installer installed while the process waits | Found by the next check, within a second of polling |
| `new PackageManager()` in that same process | Works, with all 3 sources listed |
| `GetPackagesByPackageFamily`, WinGet present | Returns `ERROR_INSUFFICIENT_BUFFER`, count 1 |

So the crash is at activation, and the check must run first. Activating after a late install works, so Check again needs no
restart in the tested case.

`WinGetAvailability.IsInstalled()` calls `GetPackagesByPackageFamily` for `Microsoft.DesktopAppInstaller_8wekyb3d8bbwe` and treats a
count above 0 as installed. It needs no capability, works for packaged and unpackaged apps, and doesn't load
`Microsoft.Management.Deployment.dll`.

`App.OnLaunched` runs the check before resolving anything from DI, because `CatalogConnection` activates WinGet in its constructor.

```
installed       → MainWindow, as today
missing         → WinGetSetupWindow, nothing resolved (R3.1, R3.3)
activation fails with an exception → WinGetSetupWindow, "installed but can't be started" (R3.5)
```

Activation is tried by resolving `CatalogConnection` alone, before `MainWindow`, so a XAML `COMException` from the window can't be
mistaken for a WinGet failure.

Check again calls `IsInstalled()`. If it's true, the app resolves `MainWindow`, activates it and closes the setup window. If that
throws, the window says a restart is needed (R3.4).

## UI

`WinGetSetupWindow` has the same title bar as `MainWindow`, with the content centred:

```
  CartonStash needs WinGet
  WinGet comes with App Installer from Microsoft. Install it, then choose Check again.

  [Get App Installer]   [Check again]
```

- Get App Installer is a `HyperlinkButton` to `https://apps.microsoft.com/detail/9NBLGGH4NNS1`. The app never installs it itself.
- Focus starts on Get App Installer. Both controls take their accessible names from their text, and theme resources cover light, dark and high contrast (R3.6).
- After a failed Check again, a line says WinGet still wasn't found.

On Search, an `InfoBar` with `Severity="Warning"` sits above the results. It stays until the next search and doesn't take focus.
`ErrorMessage` still handles the case where every source failed.

## Error handling and edge cases

- **A source recovers:** it's tried again after the next `Invalidate()`, which also runs after each install, update and uninstall.
- **A blocked source after each operation:** the first search after an operation costs a few seconds again. This is accepted, because it keeps R1.5 simple.
- **Cancellation:** probes take the request's token. A cancelled probe never counts as a failed source.
- **Failure during a retry:** the retry doesn't probe again. A second failure is reported as it stands.
- **Query-specific failure:** a source excluded for one query stays out until the next `Invalidate()`. The notice stays honest, because `SourceFailures` keeps listing it.

## Testing strategy

| Criteria | Test |
|----------|------|
| R1.1, R1.2, R1.3, R1.4, R2.5 | `SourceIsolationTests`, with fake delegates: some fail, all fail, none fail, healthy path makes one call, cancelled probe, each status |
| R1.5, R1.6 | `CatalogConnection` integration test: `Invalidate()` clears the failures; review confirms nothing is stored |
| R2.1 | `SearchViewModelTests`: notice shown with results, cleared by the next search |
| R2.2, R2.3, R2.4, R2.7 | `SourceFailureMessagesTests`: every row of the reason table |
| R2.6 | `SourceIsolationTests` for the message, S2 for the real path |
| R3.2, R3.4 | `WinGetSetupViewModelTests` with a fake check |
| R3.1, R3.3, R3.5, R3.6 | Manual: S3 in Sandbox, MSIX and portable. R3.5 is checked if a catchable failure can be produced |
| All | Manual: S1 to S4 from the requirements |

## Risks and alternatives considered

- **Search each source separately and merge the results in the app.** Rejected: it would reimplement WinGet's merging and correlation (P1).
- **Always leave out `msstore`.** Rejected: it hides a working source, and doesn't help with other sources.
- **Probe before every search.** Rejected: it adds calls to the healthy path (R1.4).
- **Keep the failed source out for the whole session.** Rejected: a brief outage would hide a source until restart (R1.5).
- **Check for `winget.exe` on disk.** Rejected: an execution alias can exist while the package is broken, and the reverse.
- **Declare App Installer as an MSIX dependency.** Out of scope, and wouldn't help the portable build.
- **Risk:** App Installer present but broken may still fail fast at activation, where nothing can catch it. R3.5 covers only failures that can be caught.
- **Risk:** the HRESULT table covers only the codes seen so far, so anything new gets the neutral text, which never blames the connection.
- **Risk:** a later WinGet may change how it reports these failures. The healthy path doesn't depend on any of this, and a fixed WinGet would simply stop reaching the probes.
