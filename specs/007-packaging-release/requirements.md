# 007 – Packaging and release: Requirements

**Status:** Approved (2026-10-30). Amendment R5 (release hardening) approved 2026-10-30. Amendment R6 (Microsoft Store) approved 2026-10-30. Amendment R7 (smaller packages) approved 2026-10-31
**Depends on:** 001–006 (can start in parallel once 003 is done)

## Problem

Today the app runs only from a developer build folder. Users need a normal way to install it, get updates,
and trust what they're running.

*Amendment R5 (security review, 2026-10-30):* users trust the release certificate across their whole PC, and a self-signed
certificate can't be revoked. A leaked key would let anyone ship an MSIX that Windows accepts as a CartonStash update, so the
release path has to protect the key and let users check what they download.

## User stories

- **US1.** As a user, I want to install the app like any Windows app (Start menu entry, clean uninstall).
- **US2.** As a user, I want to get new versions easily.
- **US3.** As the maintainer, I want releases to be built the same way every time.
- **US4.** As a user, I want to check that a download really came from this project before I trust its certificate.
- **US5.** As the maintainer, I want the signing key to stay safe even if a build dependency is compromised.
- **US6.** As a user, I want to install the app from the Microsoft Store, without trusting a certificate by hand. *(Amendment R6.)*
- **US7.** As a user, I want a small download that doesn't fill my disk with parts the app never uses. *(Amendment R7.)*

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | The project shall produce a signed MSIX package for x64 and ARM64, publisher "Anthony". Until a trusted certificate is bought, it is signed with the project's own self-signed certificate. |
| R1.5 | US1   | Each release shall include the public certificate (`CartonStash.cer`) and short install steps, so a user can trust it once and then install the MSIX by double-clicking it. *(New 2026-10-30, follows from self-signing.)* |
| R1.2 | US1   | When installed from the MSIX, the app shall appear in Start with its name and icon, and uninstall cleanly from Windows Settings. |
| R1.3 | US1   | The packaged app shall use WinGet through the same service code, with packaged COM activation, and pass the same integration tests. |
| R1.4 | US1   | The project shall also produce a portable (unpackaged, self-contained) zip for users who can't install MSIX. |
| R1.6 | US1   | The portable app shall start from the unzipped folder and open its main window. The release shall fail if the build is missing what it needs to do so. *(New 2026-10-30: the v1.0.1 portable zip crashed at start.)* |
| R2.1 | US2   | Each release shall be published on GitHub Releases with the MSIX, the portable zip, and SHA-256 checksums. |
| R2.2 | US2   | **Deferred** until the MSIX is signed with a trusted certificate: winget-pkgs validation installs the package, which a self-signed MSIX fails. ~~After the first public release, the app shall be submitted to the `winget-pkgs` repository so it can be installed with WinGet itself.~~ |
| R3.1 | US3   | A GitHub Actions workflow shall build, run unit tests, and produce release artifacts on every tag `v*`. |
| R3.2 | US3   | The app version shall come from the git tag (one source of truth) and appear in Settings (006 R4.1). |
| R4.1 | –     | Settings (006) shall move to `ApplicationData` when packaged, migrating an existing JSON file once. |
| R5.1 | US5   | The release workflow shall build every artifact without access to the signing key or its password. The key shall exist only in a separate signing job that restores no packages and runs no build. |
| R5.2 | US5   | The workflows shall run with the least access each job needs: no default token permissions, a read-only token for building, write access only where the release is published, and no token left in the checkout. |
| R5.3 | US5   | Every action used by a workflow shall be GitHub-owned and pinned to a full commit SHA. |
| R5.4 | US5   | The tag name shall never be pasted into a script: workflows shall pass it as an environment variable, and the release workflow shall reject a tag that isn't `v` + `major.minor.patch` before doing anything else. |
| R5.5 | US4   | Every signature shall carry an RFC 3161 timestamp, so it stays valid after the certificate expires. If a timestamp can't be obtained, the release shall fail. |
| R5.6 | US4   | The first-party binaries in the portable zip (`CartonStash.exe`, `CartonStash.dll`, `CartonStash.Core.dll`) shall be signed with the same certificate as the MSIX. |
| R5.7 | US4   | Each release shall carry a build attestation for every artifact, so anyone can check it was built by this repository's workflow. |
| R5.8 | US4   | The README and release notes shall publish the certificate's thumbprint with the steps to check it before trusting the certificate, and shall describe accurately what is signed. |
| R5.9 | US5   | The signing key and password shall be available only to the release workflow on `v*` tags, through a protected GitHub environment that needs the maintainer's approval for each release. |
| R5.10 | US5  | `main` and `v*` tags shall be protected against force-push and deletion, and published releases shall be immutable. |
| R5.11 | US5  | The private key and its password shall not stay in usable form on the development PC. They shall be kept in the maintainer's password manager. Release builds on the PC shall be unsigned unless a PFX is given explicitly. |
| R5.12 | US5  | Package restores shall use only nuget.org, and CI shall restore from a lock file. Dependabot shall propose updates for NuGet packages and pinned actions. |
| R5.13 | US5  | The repository shall have a `SECURITY.md` explaining how to report a vulnerability privately, with private vulnerability reporting, secret scanning with push protection, and Dependabot alerts turned on. |
| R6.1 | US6   | Each tagged release shall also produce an MSIX for the Microsoft Store, for x64 and ARM64, from the same code and version as the GitHub files. |
| R6.2 | US6   | The Store MSIX shall carry the identity Partner Center assigned: name `51376anthonydapps.CartonStash`, publisher `CN=8E7BFF24-3095-4F83-84B3-2D4A3E780750`, publisher display name `anthonydapps`. |
| R6.3 | US6   | The Store MSIX shall not be signed by the project, shall not be published on GitHub Releases, and shall never reach the signing job. Microsoft signs it after certification. |
| R6.4 | US6   | The GitHub MSIX and portable zip shall stay as they are (R1.1, R1.4, R2.1, R5.x), with the `CN=Anthony` identity and certificate. |
| R6.5 | US6   | The Store MSIX shall pass the Windows App Certification Kit before it's submitted. |
| R6.6 | US6   | The Store's privacy policy text shall be kept in the repository as `PRIVACY.md`, and shall match what the app does. |
| R7.1 | US7   | The app shall reference only the Windows App SDK parts it uses, so that every package leaves out the AI, machine learning, search and widget libraries, if the app works without them. |
| R7.2 | US7   | The Store MSIX shall use the shared Windows App SDK runtime, which the Store installs with the app, instead of bundling its own copy. |
| R7.3 | US7   | The .NET runtime shall stay bundled in every package, and the app shall not be trimmed. |
| R7.4 | US7   | The GitHub MSIX and portable zip shall keep their own copy of the Windows App SDK, and shall still work on a PC with no Store access (R1.2, R1.4). |
| R7.5 | US7   | Every package shall pass the same checks as before: unit tests, the portable start check (R1.6), and search, install and uninstall on a real install (R1.3). |
| R7.6 | US7   | A Store update shall install over the existing Store version and keep its settings and logs. |

## Out of scope

- ~~Microsoft Store listing (possible later; needs a Partner Center account).~~ *Moved in scope by amendment R6.*
- Submitting to the Store from the workflow: the maintainer uploads each package in Partner Center by hand.
- Automatic in-app updater for the portable zip.
- A publicly trusted certificate or cloud signing service (amendment R5 hardens the self-signed setup; R1.1 still applies).

## Decisions

- **Signing: decided 2026-10-30: self-signed for now.** One project certificate, subject `CN=Anthony`, kept outside the repo
  (GitHub secret for CI). Can be swapped for Azure Trusted Signing or an OV certificate later without other changes
  (only the manifest `Publisher` must match the new certificate's subject).
- **Publisher: decided 2026-10-30: "Anthony".**
- **Portable zip: decided 2026-10-30: keep it (R1.4).** A single-file `.exe` was tried and rejected: `PublishSingleFile`
  with self-extraction builds (92 MB) but crashes at start inside `Microsoft.UI.Xaml.dll` (0xc000027b).
- **Hosting: decided 2026-10-30: GitHub** (`anthonyda/CartonStash`, GitHub Releases).
- **Amendment R5: decided 2026-10-30:**
  - *Same key.* The security review found no leak in git history, Actions logs or release files, so the certificate stays and users don't have to trust a new one.
  - *CI still signs.* Signing on the maintainer's PC was rejected: that PC runs third-party installers by design, and it would drop R3.1's automation.
  - *Portable zip signed too (R5.6).* With a self-signed certificate Windows still shows an unknown publisher, so this only helps someone who checks the thumbprint. It costs little and matches what the README promises.

- **Amendment R6: signing through the Store, decided 2026-10-30.** No certificate is bought. Store-only signing leaves the GitHub files self-signed, and
  R2.2 (winget-pkgs) stays deferred. Store users can install with `winget install 9MT37M7DBZRK --source msstore` once the app is listed.

## Open questions (amendment R7)

- **What if a Store user's PC can't get the shared runtime?** **Decided 2026-10-31: accept it.** The Store delivers it with the app, so this should only affect managed PCs that block the Store's framework packages, and they can use the GitHub MSIX or portable zip.
- **Link to the runtime in the README?** **Decided 2026-10-31: no.** Under R7.4 nobody needs it, and removing the runtime from the GitHub files would add a step to an already harder install.
- **Stop if the saving is small?** **Decided 2026-10-31: yes.** If T31 shows that R7.2 saves less than 50 MB, drop it and keep R7.1 only, because a separate runtime setup isn't worth a small gain.

## Open questions (amendment R6)

- **Where the Store MSIX is built.** **Decided 2026-10-30: in the release workflow's `build` job, kept as a short-lived `store` artifact.** Same machine and code as the GitHub files, and no build on the PC that runs third-party installers. The alternative is building it locally with `Publish-Release.ps1`.
- **Keep offering the GitHub MSIX?** **Decided 2026-10-30: yes.** Some users can't use the Store, and it keeps winget-pkgs possible later. The Store and GitHub versions have different identities, so they don't update each other and can both be installed.

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
