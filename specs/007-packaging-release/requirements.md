# 007 – Packaging and release: Requirements

**Status:** Approved (2026-10-30)
**Depends on:** 001–006 (can start in parallel once 003 is done)

## Problem

Today the app runs only from a developer build folder. Users need a normal way to install it, get updates,
and trust what they're running.

## User stories

- **US1.** As a user, I want to install the app like any Windows app (Start menu entry, clean uninstall).
- **US2.** As a user, I want to get new versions easily.
- **US3.** As the maintainer, I want releases to be built the same way every time.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | The project shall produce a signed MSIX package for x64 and ARM64, publisher "Anthony". Until a trusted certificate is bought, it is signed with the project's own self-signed certificate. |
| R1.5 | US1   | Each release shall include the public certificate (`CartonStash.cer`) and short install steps, so a user can trust it once and then install the MSIX by double-clicking it. *(New 2026-10-30, follows from self-signing.)* |
| R1.2 | US1   | When installed from the MSIX, the app shall appear in Start with its name and icon, and uninstall cleanly from Windows Settings. |
| R1.3 | US1   | The packaged app shall use WinGet through the same service code, with packaged COM activation, and pass the same integration tests. |
| R1.4 | US1   | The project shall also produce a portable (unpackaged, self-contained) zip for users who can't install MSIX. |
| R2.1 | US2   | Each release shall be published on GitHub Releases with the MSIX, the portable zip, and SHA-256 checksums. |
| R2.2 | US2   | **Deferred** until the MSIX is signed with a trusted certificate: winget-pkgs validation installs the package, which a self-signed MSIX fails. ~~After the first public release, the app shall be submitted to the `winget-pkgs` repository so it can be installed with WinGet itself.~~ |
| R3.1 | US3   | A GitHub Actions workflow shall build, run unit tests, and produce release artifacts on every tag `v*`. |
| R3.2 | US3   | The app version shall come from the git tag (one source of truth) and appear in Settings (006 R4.1). |
| R4.1 | –     | Settings (006) shall move to `ApplicationData` when packaged, migrating an existing JSON file once. |

## Out of scope

- Microsoft Store listing (possible later; needs a Partner Center account).
- Automatic in-app updater for the portable zip.

## Decisions

- **Signing: decided 2026-10-30: self-signed for now.** One project certificate, subject `CN=Anthony`, kept outside the repo
  (GitHub secret for CI). Can be swapped for Azure Trusted Signing or an OV certificate later without other changes
  (only the manifest `Publisher` must match the new certificate's subject).
- **Publisher: decided 2026-10-30: "Anthony".**
- **Portable zip: decided 2026-10-30: keep it (R1.4).** A single-file `.exe` was tried and rejected: `PublishSingleFile`
  with self-extraction builds (92 MB) but crashes at start inside `Microsoft.UI.Xaml.dll` (0xc000027b).
- **Hosting: decided 2026-10-30: GitHub** (`anthonyda/CartonStash`, GitHub Releases).

## Open questions (history)

- **Code signing certificate:** a self-signed certificate only works for testing (users must trust it manually).
  Options: Azure Trusted Signing (~$10/month), an OV certificate (~$200–400/year), or Microsoft Store distribution
  (free signing by Microsoft). → Decided: self-signed for now (see Decisions).
- **App name and publisher identity:** "WinGet Frontend" was the working title. A final name, publisher display name and
  icon are needed for the manifest and the winget-pkgs submission. Avoid "WinGet" in the product name to
  reduce trademark and confusion risk with Microsoft's tools.
- **Name rule: decided 2026-10-29.** The product name must **not** contain "WinGet" (Microsoft's product name). WinGet may only be mentioned in descriptions (e.g. "an app manager for WinGet").
- **Name: decided 2026-10-30: CartonStash.** Free in the WinGet catalog (user checked other options were taken). Used for the window, title bar, Settings, diagnostics, the exe (`CartonStash.exe`) and the settings folder (`%LOCALAPPDATA%\CartonStash`, copied once from the old `WinGetFrontend` folder). Renamed everywhere on 2026-10-30: solution, projects, namespaces, repo folder (`C:\dev\CartonStash`) and GitHub repo. Publisher display name: Anthony (see Decisions).
- **Icon: decided 2026-10-29.** Open cardboard box with an up arrow on a green rounded square. Source:
  `src/CartonStash.App/Assets/AppIcon.svg`; `AppIcon.ico` (16–256 px) is rendered from it and already used for the
  unpackaged app (exe, window, taskbar, title bar). The MSIX logo set (T1) is generated from the same SVG.
- **Repository hosting:** GitHub (assumed by R2 and R3)? → Decided: yes.
