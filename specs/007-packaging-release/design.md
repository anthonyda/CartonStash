# 007 – Packaging and release: Design

**Status:** Approved (2026-10-30)
**Requirements:** [requirements.md](requirements.md)

## Overview

Use **single-project MSIX** (`WindowsPackageType=MSIX` in a Release-only build configuration), so day-to-day
development stays unpackaged and F5-friendly. A GitHub Actions workflow builds both flavours, signs the MSIX,
and attaches everything to a GitHub Release.

## Components

| Area | Item | Responsibility |
|------|------|----------------|
| App | `Package.appxmanifest` | Identity `Name="Anthony.CartonStash"`, `Publisher="CN=Anthony"`, `PublisherDisplayName="Anthony"`, display name "CartonStash", description "An app manager for WinGet", logos, `runFullTrust`, `uap:VisualElements` |
| App | `Assets/` | App icon set (Square44, Square150, Wide310, StoreLogo, SplashScreen) at required scales, rendered from `AppIcon.svg` by `tools/Render-Logos.ps1` (headless Edge, as for `AppIcon.ico`) and committed |
| Build | `CartonStash.App.csproj` | *Amended:* a `PackageMsix=true` property (not a separate configuration) switches a Release publish to MSIX: `WindowsPackageType=MSIX`, `EnableMsixTooling`, self-contained .NET + Windows App SDK, unsigned output in `AppPackages\` |
| Tools | `tools/Publish-Release.ps1` | *Added:* the one release script for CI and local builds: stamps the manifest version from the tag, builds MSIX + portable zip per architecture, signs with `signtool` (PFX in CI, certificate store locally), copies the `.cer`, writes `SHA256SUMS.txt` into `artifacts\` |
| Build | `Version` | *Decided:* tag-derived `-p:Version` (assembly, shown in Settings) and the manifest `Version` stamped by `Publish-Release.ps1` (R3.2) |
| Core | `JsonSettingsStore` + `AppPackage` | *Amended:* same JSON store, path chosen by `AppPackage.IsPackaged` (`GetCurrentPackageFullName`): the package's `ApplicationData.LocalFolder` when packaged, `%LOCALAPPDATA%\CartonStash` otherwise; the earlier file is copied once (R4.1) |
| Tools | `tools/New-SigningCert.ps1` | Creates the self-signed `CN=Anthony` certificate once (3-year validity): exports `CartonStash.pfx` (to a path outside the repo) and `CartonStash.cer`. Prints the base64 PFX for the GitHub secret |
| CI | `.github/workflows/release.yml` | On `v*` tag: restore → unit tests → publish x64 + ARM64 MSIX and portable zips → sign MSIX with `signtool` (PFX from secrets `SIGNING_PFX_BASE64` / `SIGNING_PFX_PASSWORD`) → checksums → GitHub Release with `.msix` ×2, `.zip` ×2, `CartonStash.cer`, `SHA256SUMS.txt`, install steps in the release notes |
| CI | `.github/workflows/ci.yml` | On PR/push: build + unit tests (integration tests excluded; they need WinGet with network) |

## WinGet COM in a packaged app (R1.3)

Packaged apps can activate the WinGet server through normal packaged COM. The static factory DLL from ComInterop
works in both modes, so **no service code changes** are expected. Verify by running the integration tests
against a packaged build (manual, documented in tasks). Keep W1 (winmd next to the exe) in the package layout.

## Signing (decided: self-signed for now)

- One certificate for local test builds and CI: `CN=Anthony`, code-signing EKU, 3 years. The PFX is never committed;
  CI gets it from two encrypted GitHub secrets. `CartonStash.cer` (public part) is attached to every release.
- **User install steps (R1.5)**, in the release notes and README: trust the certificate once, as administrator,
  `Import-Certificate -FilePath CartonStash.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`; then double-click the
  `.msix`. Updates install over the top the same way (same identity and publisher).
- Moving to a trusted certificate later: change the manifest `Publisher` to the new subject and the CI signing step. Users
  then uninstall the self-signed build once (different publisher = different package identity).

## Portable flavour (R1.4)

*Decided:* a zip of the self-contained app folder. A single-file `.exe` was tried (see requirements, Decisions) and crashes in WinUI.


`dotnet publish -c Release -r win-x64 --self-contained -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None`
→ zip. Includes the WinGet `.winmd` / factory DLL (W1). Settings use JSON (006).

## winget-pkgs submission (R2.2): deferred until a trusted certificate

After the first release: create a manifest with `wingetcreate new <msix-url>`, open a PR to `microsoft/winget-pkgs`.
Later releases: `wingetcreate update` in the release workflow (needs a GitHub token secret).

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.1, R1.4, R3.1, R2.1 | CI run on a test tag (`v0.0.1-test`) produces all artifacts |
| R1.2, R1.3, R1.5 | Manual: on this PC and in Windows Sandbox, import the `.cer`, install the MSIX, check Start entry and icon, search + install + uninstall a small package, then uninstall the app |
| R4.1 | Unit test on migration logic (fake stores) |
| R3.2 | Manual: version shown in Settings matches the tag |

## Risks and alternatives considered

- **Windows Sandbox lacks WinGet by default:** install App Installer in the sandbox first (script it).
- **MSIX + WinGet server activation regressions:** caught by R1.3 manual check before each release.
- **Sparse packages / unpackaged-only:** rejected; MSIX gives clean install/uninstall and identity.
- **Single-file exe:** rejected after a test build crashed in `Microsoft.UI.Xaml.dll` at start.
- **Self-signed friction:** users must import a certificate as admin once. Accepted for now; trusted signing removes it later.
- **Settings move (R4.1)** changes where settings live for MSIX users only; the portable zip keeps JSON in `%LOCALAPPDATA%\CartonStash`.
