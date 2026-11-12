# 017 – Retry as administrator: Tasks

**Status:** Done (2026-11-12). Approved 2026-11-12, Q1 as answered by the maintainer (R1.8), Q2 and Q3 as proposed

## Measure first

- [x] **T1.** VM: a small elevated console app calls WinGet's COM API (`FindPackages`, then `UpgradePackageAsync` on
  Microsoft 365 Copilot one version back). Record whether the server accepts an elevated caller, and the result.
  Update Q1 and the design. *(Q1)*
  - 2026-11-12: Pass. An elevated, unpackaged console app using Core's `WinGetPackageService` and
    `WinGetPackageInstaller` read the installed list (71 rows) and updated Copilot 19.2506 → 19.2611. Option (a) kept.

## Core

- [x] **T2.** `AdminRightsCodes`; `MapInstallResult` gives the R1.1 message and `OffersRetryAsAdmin`. *(R1.1, R1.2)*
- [x] **T3.** `OperationOptions.Elevated`; queue routes it to `IElevatedRunner`; declined prompt → Cancelled.
  *(R1.4 to R1.7)*

## App

- [x] **T4.** `ElevatedRunner` and `Program --run-elevated`; **Retry as administrator** on the card and the row. *(R1.3, R1.4)*

## Manual checks

- [ ] **T5.** VM, by hand at the console: Copilot update fails with the R1.1 message; Retry as administrator; accept →
  updated; decline → Cancelled. *(R1.1, R1.3 to R1.5)*
  - 2026-11-12, automated part: see the log. The prompt itself and declining it are left to a check by hand.

## Manual verification log

| Requirement | Date | Result | Notes |
|-------------|------|--------|-------|
| R1.1 to R1.4, R1.6, R1.8 (T5) | 2026-11-12 | Pass | VM, Store MSIX 1.4.2 test package, UAC set to elevate without prompting for this run. Update of Microsoft 365 Copilot 19.2506.56051.0: "Microsoft 365 Copilot's installer needs administrator rights and didn't ask for them.", **Retry as administrator** on the card and the row. Pressed: the same card went to "Updating…", then "Updated successfully." (log: WinGet status Ok, exit code 0); 19.2611 installed; one CartonStash window throughout, no crash. Copilot's installer started Copilot afterwards (its `--start` argument) |
| R1.5 (T5) | – | Not checked | The permission prompt shown and declined needs a person at the VM's console |
