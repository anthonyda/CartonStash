# 017 – Retry as administrator: Design

**Status:** Draft (2026-11-12), written against the proposed answers; finished once Q1 is decided and T1 measured.
**Requirements:** [requirements.md](requirements.md)

## Overview

The installer's result is classified once more after 013's mapping: a code on the "needs administrator rights" list
(R1.2) gives the R1.1 message and `OffersRetryAsAdmin`. The button queues the same operation with
`OperationOptions.Elevated = true`. The queue runs it through an `IElevatedRunner`, which starts an elevated
CartonStash process for that one operation (Q1 a) and reads its result back.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | `AdminRightsCodes` | The R1.2 list and `Lacks(installerCode, extendedError)` | new |
| Core/Services | `WinGetPackageInstaller.MapInstallResult` | R1.1 message and `OffersRetryAsAdmin` for those codes | changed |
| Core/Models | `OperationOutcome`, `PackageOperation` | `OffersRetryAsAdmin`; `OperationOptions.Elevated` | changed |
| Core/Services | `PackageOperationQueue` | An elevated operation goes to `IElevatedRunner` instead of `IPackageInstaller`; same queue, same card | changed |
| Core/Services | `IElevatedRunner` | Runs one operation elevated, returns its `OperationOutcome`; declined prompt → Cancelled (R1.5) | new |
| App/Services | `ElevatedRunner` | Starts `CartonStash.exe --run-elevated <file>` with the `runas` verb; the request and the result are JSON files in the app's data folder | new |
| App | `Program` | `--run-elevated`: no window, runs the operation through `WinGetPackageInstaller`, writes the result, exits | changed |
| App/Controls | `OperationCard`, row | **Retry as administrator** next to Retry (R1.3) | changed |

## Data flow

```
Result: Failed, installer code on the list → message R1.1, OffersRetryAsAdmin
  → user presses Retry as administrator
  → queue.Enqueue(kind, package, options with Elevated = true, same Version and Scope)  (R1.4, R1.6)
  → ElevatedRunner: request file → ShellExecute runas CartonStash.exe --run-elevated
       declined: ERROR_CANCELLED (1223) → Cancelled, "Windows' permission prompt was declined." (R1.5)
  → elevated process: WinGetPackageInstaller.RunAsync, its log path from the request (009 R2) → result file
  → outcome as any other (013)
```

Progress: the elevated process can't report WinGet's progress back through the queue's `IProgress`, so the card
shows "Installing…" / "Updating…" with no percentage, and "May ask for permission" from the start (R1.5).

## Risks

- **T1 may show that WinGet's COM server refuses an elevated caller.** Then Q1 (b): the runner starts
  `winget.exe upgrade|install --id <id> --exact --source <source> --silent --accept-package-agreements
  --disable-interactivity [--version] [--scope]` elevated, and maps its exit code. Needs the A6 amendment.
- A request file in the data folder could be changed by another process of the same user before the elevated process
  reads it. The elevated process only accepts the operations the app can queue (install/update one package ID from a
  configured source), never a command line.

## Testing

| Requirement | Verification |
|-------------|--------------|
| R1.1, R1.2 | `AdminRightsCodes` and `MapInstallResult` (unit) |
| R1.3, R1.6, R1.7 | Queue and card with a fake `IElevatedRunner` (unit) |
| R1.4, R1.5 | VM: Microsoft 365 Copilot update fails, Retry as administrator, accept the prompt (needs the console, by hand): updated. Decline: Cancelled |
