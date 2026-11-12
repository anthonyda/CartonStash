# 017 – Retry as administrator: Tasks

**Status:** Approved (2026-11-12), Q1 as answered by the maintainer (R1.8), Q2 and Q3 as proposed

## Measure first

- [ ] **T1.** VM: a small elevated console app calls WinGet's COM API (`FindPackages`, then `UpgradePackageAsync` on
  Microsoft 365 Copilot one version back). Record whether the server accepts an elevated caller, and the result.
  Update Q1 and the design. *(Q1)*

## Core

- [ ] **T2.** `AdminRightsCodes`; `MapInstallResult` gives the R1.1 message and `OffersRetryAsAdmin`. *(R1.1, R1.2)*
- [ ] **T3.** `OperationOptions.Elevated`; queue routes it to `IElevatedRunner`; declined prompt → Cancelled.
  *(R1.4 to R1.7)*

## App

- [ ] **T4.** `ElevatedRunner` and `Program --run-elevated`; **Retry as administrator** on the card and the row. *(R1.3, R1.4)*

## Manual checks

- [ ] **T5.** VM, by hand at the console: Copilot update fails with the R1.1 message; Retry as administrator; accept →
  updated; decline → Cancelled. *(R1.1, R1.3 to R1.5)*
