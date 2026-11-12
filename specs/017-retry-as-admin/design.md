# 017 – Retry as administrator: Design

**Status:** Approved (2026-11-12). Option (a) unless T1 shows WinGet refuses an elevated caller.
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
| Core/Services | `IElevatedRunner`, `ElevatedRequest`, `ElevatedProtocol` | Runs one operation elevated, returns its `OperationOutcome`; declined prompt → Cancelled (R1.5); the request's encoding and checks | new |
| App/Services | `ElevatedProcessRunner` | Starts `CartonStash.exe --run-elevated <request> <result file>` with the `runas` verb. The request is on the command line (set when Windows asks, so nothing can change it afterwards); the result is a JSON file in the app's data folder | new |
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
- The request travels on the command line, fixed when Windows asks for permission. The elevated process only accepts
  an install or update of one package (`ElevatedProtocol.Decode`), never a command line of its own.
- The elevated copy is started by path, so it runs without the package's identity. It only uses Core (no XAML, no
  Windows App SDK), which T1 and T5 showed works.

## Testing

| Requirement | Verification |
|-------------|--------------|
| R1.1, R1.2 | `AdminRightsCodes` and `MapInstallResult` (unit) |
| R1.3, R1.6, R1.7 | Queue and card with a fake `IElevatedRunner` (unit) |
| R1.4, R1.5 | VM: Microsoft 365 Copilot update fails, Retry as administrator, accept the prompt (needs the console, by hand): updated. Decline: Cancelled |
