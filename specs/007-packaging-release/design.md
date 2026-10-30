# 007 – Packaging and release: Design

**Status:** Approved (2026-10-30). Amendment R5 (release hardening) approved 2026-10-30
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
| Tools | `tools/Publish-Release.ps1` | *Amended by R5:* the one build script for CI and local builds: stamps the manifest version from the tag, builds unsigned MSIX + portable zip per architecture, copies the `.cer` into `artifacts\` |
| Tools | `tools/Sign-Release.ps1` | *Added by R5:* signs the MSIX and portable binaries with timestamps, checks every signature, writes `SHA256SUMS.txt` |
| Build | `Version` | *Decided:* tag-derived `-p:Version` (assembly, shown in Settings) and the manifest `Version` stamped by `Publish-Release.ps1` (R3.2) |
| Core | `JsonSettingsStore` + `AppPackage` | *Amended:* same JSON store, path chosen by `AppPackage.IsPackaged` (`GetCurrentPackageFullName`): the package's `ApplicationData.LocalFolder` when packaged, `%LOCALAPPDATA%\CartonStash` otherwise; the earlier file is copied once (R4.1) |
| Tools | `tools/New-SigningCert.ps1` | *Amended by R5:* creates the self-signed `CN=Anthony` certificate (3-year validity), only needed to replace the key: exports `CartonStash.pfx` (to a path outside the repo) and `CartonStash.cer`, and leaves no key in the store |
| CI | `.github/workflows/release.yml` | *Amended by R5:* on `v*` tag, a `build` job (unit tests, x64 + ARM64 MSIX and portable zips) then a `release` job (sign, GitHub Release with `.msix` ×2, `.zip` ×2, `CartonStash.cer`, `SHA256SUMS.txt`, install steps in the release notes) and an `attest` job |
| CI | `.github/workflows/ci.yml` | On PR/push: build + unit tests (integration tests excluded; they need WinGet with network) |

## WinGet COM in a packaged app (R1.3)

Packaged apps can activate the WinGet server through normal packaged COM. The static factory DLL from ComInterop
works in both modes, so **no service code changes** are expected. Verify by running the integration tests
against a packaged build (manual, documented in tasks). Keep W1 (winmd next to the exe) in the package layout.

## Signing (decided: self-signed for now)

- One certificate: `CN=Anthony`, code-signing EKU, 3 years. The PFX is never committed; CI gets it from two encrypted
  GitHub secrets (see amendment R5). `CartonStash.cer` (public part) is attached to every release.
- **User install steps (R1.5)**, in the release notes and README: trust the certificate once, as administrator,
  `Import-Certificate -FilePath CartonStash.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`; then double-click the
  `.msix`. Updates install over the top the same way (same identity and publisher).
- Moving to a trusted certificate later: change the manifest `Publisher` to the new subject and the CI signing step. Users
  then uninstall the self-signed build once (different publisher = different package identity).

## Portable flavour (R1.4)

*Decided:* a zip of the self-contained app folder. A single-file `.exe` was tried (see requirements, Decisions) and crashes in WinUI.


`dotnet publish -c Release -r win-x64 --self-contained -p:EnableMsixTooling=true`
→ zip. Includes the WinGet `.winmd` / factory DLL (W1). Settings use JSON (006).

`EnableMsixTooling` makes publish include `CartonStash.pri`, which holds the compiled XAML. Without it the app crashes at
start (`0xc000027b` in `Microsoft.UI.Xaml.dll`), so `Publish-Release.ps1` fails if the file is missing (R1.6). "Starts" means
the window opens and no crash is logged: a process that's still running proves nothing, because Windows Error Reporting
keeps a crashed process alive while it writes its report.

## winget-pkgs submission (R2.2): deferred until a trusted certificate

After the first release: create a manifest with `wingetcreate new <msix-url>`, open a PR to `microsoft/winget-pkgs`.
Later releases: `wingetcreate update` in the release workflow (needs a GitHub token secret).

## Amendment R5: release hardening (approved 2026-10-30)

The certificate and key stay the same (see requirements, Decisions).

### Release workflow (R5.1–R5.4, R5.7, R5.9)

Three jobs, so build code and the key never run together, and attesting needs neither. The workflow sets `permissions: {}`; each job asks for what it needs.

| Job | Token | Secrets | Steps |
|-----|-------|---------|-------|
| `build` | `contents: read` | None | Check the tag format, restore from the lock file, unit tests, `Publish-Release.ps1`, upload the unsigned `artifacts\` (kept 1 day) |
| `release` | `contents: write` | Signing key, from the `release` environment | Download the artifacts, `Sign-Release.ps1`, remove the key, upload the signed files (kept 7 days), `gh release create` |
| `attest` | `contents: read`, `id-token: write`, `attestations: write` | None | Download the signed files, attest every file |

- The `release` job never runs `dotnet`, so no NuGet package code runs near the key. `signtool` comes from the Windows SDK on the runner, not from a NuGet package.
- `attest` runs after publishing as its own job. GitHub refuses attestations on a private repo on the free plan, so a release made before going public shows that job as failed. Re-running only `attest` after the visibility change attests the same signed files, as long as they're still kept. Once public, it passes in the normal run.
- The key and password are in the environment of the signing step only.
- Every `actions/checkout` uses `persist-credentials: false`. All actions are `actions/*`, pinned to a full SHA with the version in a comment. `ci.yml` gets `permissions: contents: read` and the same pins.
- The tag reaches scripts only as `env: TAG: ${{ github.ref_name }}`, read as `$env:TAG`. No `${{ }}` inside any `run:` block.

### Scripts (R5.1, R5.5, R5.6, R5.11)

- `tools/Publish-Release.ps1` builds only: unsigned MSIX, portable zip and the `.cer`. Its signing parameters and the certificate-store path go away.
- New `tools/Sign-Release.ps1 -PfxPath -PfxPassword`: signs each MSIX, signs the three first-party binaries inside each portable zip (expand, sign, zip again), then writes `SHA256SUMS.txt`.
- Every signature uses `/fd SHA256 /tr http://timestamp.digicert.com /td SHA256`.
- After signing, the script checks every signed file with `Get-AuthenticodeSignature`: the signer thumbprint must equal `tools/CartonStash.cer` and a timestamp must be present, otherwise the release fails. It checks the thumbprint rather than trust because the runner doesn't trust the certificate.
- `signtool` refuses an MSIX unless the certificate's subject equals the manifest `Publisher` (it reports only "unexpected internal error"), so a test key needs the subject `CN=Anthony` and `-Certificate` pointing at its own `.cer`.
- `tools/New-SigningCert.ps1` (only needed to replace the key): asks for the password instead of generating one, writes no password or base64 file, and removes the key from the certificate store after exporting the PFX.

### What users can check (R5.7, R5.8)

- README and release notes publish the thumbprint `99EE388A0F4388D0118678B7043D8626AA904441` and a check to run before the import step: `(Get-PfxCertificate .\CartonStash.cer).Thumbprint`. The README is a second channel: replacing a release file doesn't change it.
- `gh attestation verify <file> --repo anthonyda/CartonStash` shows a file was built by this repository's workflow.
- The README says what signing means here: signed with the project's own certificate, which Windows doesn't trust by default.

### GitHub settings (R5.9, R5.10, R5.13)

A private repo on the free plan accepts some of these, tested 2026-10-30.

Accepted while private:

- Environment `release` limited to `v*` tags.
- Immutable releases, Dependabot alerts and security updates.
- Actions limited to GitHub-owned actions.

Refused until the repo is public:

- The maintainer as required reviewer on `release`. `SIGNING_PFX_BASE64` and `SIGNING_PFX_PASSWORD` then move there and the repo-level secrets are deleted.
- Rulesets: `main`, no force-push or deletion; `v*` tags, no update or deletion.
- Secret scanning with push protection, private vulnerability reporting.
- Approval required for workflows from outside contributors.

### Key custody (R5.11)

The maintainer saves the PFX and password in a password manager, then removes from the PC: `%USERPROFILE%\.cartonstash\` and the
private key in `Cert:\CurrentUser\My`. The public `tools/CartonStash.cer` and the trust entries in `TrustedPeople` stay. `.gitignore`
also covers `*.p12`, `*.pem`, `*.key`, `*.snk`, `*.pfx.*` and `.env.*`.

### Dependencies (R5.12)

- `nuget.config` with `<clear/>` and nuget.org only.
- `RestorePackagesWithLockFile`, with `RuntimeIdentifiers` covering `win-x64` and `win-arm64` so one lock file fits both. CI restores in locked mode.
- `.github/dependabot.yml`: weekly, for `nuget` and `github-actions`.
- *Kept floating on purpose:* `dotnet-version: 10.0.x` and `windows-latest`. The app bundles .NET, so a rebuild picking up the newest patch is how .NET security fixes reach users.

### Rollout order

1. Before going public: workflows, scripts, docs, lock file, Dependabot config, `SECURITY.md`, key moved off the PC, and the settings a private repo accepts.
2. Release `v1.0.2` through the new pipeline and check it, so the repo goes public with a release to download. Its `attest` job fails, as expected. `v1.0.1` stays published but isn't attested, and its portable zip crashes at start, so `v1.0.2` is the first good release.
3. Make the repo public.
4. Right after: the remaining GitHub settings, then re-run the failed `attest` job within 7 days.

## Amendment R6: Microsoft Store (draft, needs review)

- **Partner Center product.** Type *MSIX or PWA app*. Store ID `9MT37M7DBZRK`, package family name `51376anthonydapps.CartonStash_zq0btdd4d5nd6`. These values are public, so they live in the script, not in secrets.
- **Build.** `Publish-Release.ps1` gets a third build per architecture: it swaps `Identity Name`, `Publisher` and `PublisherDisplayName` in the manifest, as it already does for the version, and restores the file afterwards.
  `Package.appxmanifest` itself keeps the `CN=Anthony` identity.
- **Output.** `store\CartonStash_<version>_<arch>.msix`, a folder apart from `artifacts\`, so `Sign-Release.ps1` and the GitHub Release never see these files (R6.3). `Sign-Release.ps1` would fail on them anyway, because their publisher isn't `CN=Anthony`.
- **Workflow.** The `build` job uploads `store` with 7-day retention and no secrets (R5.1). `release` and `attest` don't change.
- **Submitting.** The maintainer approves the release, downloads the `store` artifact and uploads both files in Partner Center. The `runFullTrust` justification: the app runs as a desktop app and uses WinGet's COM API to install and manage software at the user's request.
- **App behaviour.** No code changes expected: settings, logs and the `Caller:` match (009 R2.9) already come from the running package. Store and GitHub installs keep separate settings and logs.
- **Docs.** `PRIVACY.md` holds the policy text. The README adds a Store link only once the listing is live.

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.1, R1.4, R3.1, R2.1 | CI run on a test tag (`v0.0.1-test`) produces all artifacts |
| R1.2, R1.3, R1.5 | Manual: on this PC and in Windows Sandbox, import the `.cer`, install the MSIX, check Start entry and icon, search + install + uninstall a small package, then uninstall the app |
| R4.1 | Unit test on migration logic (fake stores) |
| R3.2 | Manual: version shown in Settings matches the tag |
| R1.6 | Manual: run `CartonStash.exe` from the downloaded zip: the main window opens and no crash appears in the Application event log. `Publish-Release.ps1` fails without `CartonStash.pri` |
| R5.1–R5.4 | Review of the workflows, plus the `v1.0.2` run: the `build` job has no secrets in its environment, and a tag such as `v1';calc;'` is rejected without running anything |
| R5.5, R5.6 | `Sign-Release.ps1` checks itself in CI; manual `Get-AuthenticodeSignature` on the downloaded `v1.0.2` files, and a local run with a throwaway certificate must fail the thumbprint check |
| R5.7, R5.8 | Manual: `gh attestation verify` and the README's thumbprint check on downloaded files |
| R5.9, R5.10, R5.13 | Manual: read the settings with `gh api`; pushing a `v*` tag waits for approval |
| R5.11 | Manual: the key folder is gone and `Cert:\CurrentUser\My` has no `CN=Anthony` private key |
| R5.12 | Locked restore passes on all four builds; changing a version in `Directory.Packages.props` makes it fail |

*Amendment R6 (draft):* R6.1–R6.3 by reading the built packages' manifests and running `Sign-Release.ps1` on a test release; R6.5 with the Windows App Certification Kit; R6.1 end to end when Partner Center accepts the upload and the Store build installs.

## Risks and alternatives considered

- **Store review (amendment R6, draft).** An app that installs other software with `runFullTrust` may get questions or be refused, and how review treats it is unknown until the first submission. The GitHub files don't depend on the outcome.

- **Windows Sandbox lacks WinGet by default:** install App Installer in the sandbox first (script it).
- **MSIX + WinGet server activation regressions:** caught by R1.3 manual check before each release.
- **Sparse packages / unpackaged-only:** rejected; MSIX gives clean install/uninstall and identity.
- **Single-file exe:** rejected after a test build crashed in `Microsoft.UI.Xaml.dll` at start.
- **Self-signed friction:** users must import a certificate as admin once. Accepted for now; trusted signing removes it later.
- **Settings move (R4.1)** changes where settings live for MSIX users only; the portable zip keeps JSON in `%LOCALAPPDATA%\CartonStash`.
- **Full account takeover (R5):** a stolen token with `repo` scope can approve its own deployment, so the `release` environment stops outsiders and accidents, not that. Only a hardware key or a signing service would, which belongs with a trusted certificate later.
- **A compromised build dependency (R5):** it can no longer take the key, but it can still alter the app before signing. The lock file makes that harder; an attestation shows where a file was built, not that it's safe.
- **Timestamp server down (R5.5):** the release fails and is run again. Accepted: an unstamped release is worse than a late one.
- **Lock file across architectures and MSIX (R5.12):** unproven, because `PackageMsix` changes what gets restored. T9 proves it first. If it can't work, keep `nuget.config` and NuGet audit, and update this spec.
- **Attestations need a public repo on the free plan (R5.7):** `v1.0.2` is published before its attestation exists. If the signed files expire before the `attest` job is re-run, it can't be attested and the next release is the first that is.
- **Publishing before attesting (R5.7):** once public too, a release is visible for a moment before its attestation. Accepted: it costs nothing, and a failed `attest` job can be re-run alone.
