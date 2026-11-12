# 017 – Retry as administrator: Requirements

**Status:** Draft (2026-11-12)
**Depends on:** 003, 004, 009, 013

## Problem

Some installers need administrator rights and don't ask for them. Found in the test VM on 2026-11-12 with Microsoft 365
Copilot (`Microsoft.365Copilot`, 19.2506.56051.0 → 19.2609.33020.0):

| Step | Result |
|------|--------|
| Update from CartonStash 1.4.x | Failed: "The installer failed with error code 0x88A30033." WinGet status `InstallError`, extended error `0x8A150006` |
| `winget upgrade --id Microsoft.365Copilot` as the user | Same exit code 0x88A30033 |
| The installer's own log (`%TEMP%\xpd_M365CopilotDesktopInstaller_*.log`) | `PerMachineInstall` → `PackageManager.FindPackages`: `UnauthorizedAccessException` (0x80070005), `IsProcessElevated: false`. The app is installed for all users (`Microsoft.MicrosoftOfficeHub`), so the installer updates it for all users |
| The same installer, same arguments, run as administrator | Exit 0; 19.2611.32040.0 installed |
| Its WinGet manifest | `exe` installer, no `Scope`, no `ElevationRequirement`: WinGet has no reason to elevate it |

So WinGet runs it with the user's rights, the installer never shows a permission prompt, and the card only shows a
code. Nothing the app offers today gets the update done. **Retry with another scope** (015) doesn't apply: the
manifest has one installer and no scope, and the installer chooses all users by itself.

## User stories

- **US1.** As a user whose update or install failed for lack of administrator rights, I want to be told so and to retry
  it as administrator, so that I can finish it from CartonStash with one permission prompt.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | If an install or update fails with a result that means the installer lacked administrator rights (R1.2), then the result shall say so in words: "{name}'s installer needs administrator rights and didn't ask for them." The installer's code stays in the log (013 R4.1). |
| R1.2 | –     | A result counts as "lacked administrator rights" when the installer's exit code or WinGet's extended error is on a list kept in the design, built from measured cases. The first entries: 0x88A30033 (Microsoft 365 Copilot's installer), 0x80070005 and 5 (access denied), and MSI 1925 (insufficient privileges). Nothing else is guessed (P1). |
| R1.3 | US1   | Such a result shall offer **Retry as administrator** on its Activity card and on the row, next to Retry. |
| R1.4 | US1   | When the user presses it, the app shall run the same operation again (same package, version and scope) with administrator rights, so that Windows shows its permission prompt once. The app itself keeps running without them (constitution A5). |
| R1.5 | US1   | While it runs, the card and the row shall show progress as for any operation (P3), with "May ask for permission" (003 R8.3). If the user declines the prompt, the result shall be **Cancelled** with "Windows' permission prompt was declined." |
| R1.6 | –     | The elevated run shall end with a result like any other (013): success, failure with its code, or not confirmed, and its own log (009 R2). It shall go through the queue: one operation at a time (003 R4.1), never the same package twice (R4.2). |
| R1.7 | –     | **Update all** and background checks shall never retry as administrator on their own: only the user's press does (P2). |

## Out of scope

- Running every operation as administrator, or a setting for it.
- Fixing the Copilot manifest. To be reported to `microsoft/winget-pkgs` separately (it should declare
  `ElevationRequirement: elevationRequired`).

## Open questions

- **Q1. How does the retry get administrator rights?** WinGet's COM API has no "run elevated" option, and its server
  runs with the caller's rights.
  - *(a) An elevated CartonStash process for one operation.* The app starts its own exe with a permission prompt and an
    argument naming the operation; that process runs it through the COM API and hands back the result, then exits. No
    `winget.exe`. Fits A5 as written ("elevation per operation"). **Not yet measured:** whether WinGet's COM server
    accepts an elevated caller; T1 checks it in the VM first.
  - *(b) Elevated `winget.exe`.* `winget upgrade|install --id … --exact` started with a permission prompt. Known to work
    (the installer elevated succeeded), but needs the CLI exception A6 (still undecided, spec 008), and A6's draft says
    "never elevated", so it would change too. Progress is only "running", and the result comes from the exit code.
  - *Proposed:* (a) if T1 passes, else (b) with A6 amended for this one command.
- **Q2. Which failures offer it?** *Proposed:* only the list of R1.2, extended as cases are measured. The alternative,
  offering it after any installer failure, would also show it where it can't help.
- **Q3. Release.** *Proposed:* 1.4.2 waits for this spec, as asked. R1.1 alone (the words, no button) could ship first
  if the mechanism takes longer.
