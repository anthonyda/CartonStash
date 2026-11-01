# 007 – Packaging and release: Requirements

**Status:** Approved (2026-10-30). Amendment R5 (release hardening) approved 2026-10-30. Amendment R6 (Microsoft Store) approved 2026-10-30. Amendment R7 (smaller packages) approved 2026-10-31. Amendment R8 (shared runtime on GitHub) approved 2026-10-31. Amendment R9 (Microsoft Store only) approved 2026-11-01
**Depends on:** 001–006 (can start in parallel once 003 is done)

## Problem

Today the app runs only from a developer build folder. Users need a normal way to install it, get updates,
and trust what they're running.

*Amendment R5 (security review, 2026-10-30):* users trust the release certificate across their whole PC, and a self-signed
certificate can't be revoked. A leaked key would let anyone ship an MSIX that Windows accepts as a CartonStash update, so the
release path has to protect the key and let users check what they download.

*Amendment R9 (maintainer's decision, 2026-11-01):* the Microsoft Store is the only build the project publishes. Microsoft signs
it and Windows keeps it up to date, so nobody has to trust a certificate by hand. GitHub keeps the source and the changelog, and
anyone can still build the app from the README.

## User stories

- **US1.** As a user, I want to install the app like any Windows app (Start menu entry, clean uninstall).
- **US2.** As a user, I want to get new versions easily.
- **US3.** As the maintainer, I want releases to be built the same way every time.
- **US4.** As a user, I want to check that a download really came from this project before I trust its certificate.
- **US5.** As the maintainer, I want the signing key to stay safe even if a build dependency is compromised.
- **US6.** As a user, I want to install the app from the Microsoft Store, without trusting a certificate by hand. *(Amendment R6.)*
- **US7.** As a user, I want a small download that doesn't fill my disk with parts the app never uses. *(Amendment R7.)*
- **US8.** As a GitHub MSIX user, I want background update checks too (spec 010), and clear steps for the one extra thing they need. *(Amendment R8.)*
- **US9.** As a user, I want one place to get CartonStash, signed by Microsoft and kept up to date. *(Amendment R9.)*
- **US10.** As a developer, I want to build and run CartonStash from the source with only the README. *(Amendment R9.)*
- **US11.** As someone who installed a GitHub file earlier, I want to know how to move to the Store and undo the certificate trust. *(Amendment R9.)*

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | **Replaced by R9.1.** ~~The project shall produce a signed MSIX package for x64 and ARM64, publisher "Anthony". Until a trusted certificate is bought, it is signed with the project's own self-signed certificate.~~ |
| R1.5 | US1   | **Replaced by R9.1.** ~~Each release shall include the public certificate (`CartonStash.cer`) and short install steps, so a user can trust it once and then install the MSIX by double-clicking it. *(New 2026-10-30, follows from self-signing.)*~~ |
| R1.2 | US1   | When installed from the MSIX, the app shall appear in Start with its name and icon, and uninstall cleanly from Windows Settings. |
| R1.3 | US1   | The packaged app shall use WinGet through the same service code, with packaged COM activation, and pass the same integration tests. |
| R1.4 | US1   | The project shall also produce a portable (unpackaged, self-contained) zip for local builds. *(Reworded by R9.6: it isn't published.)* |
| R1.6 | US1   | The portable app shall start from the unzipped folder and open its main window. The release shall fail if the build is missing what it needs to do so. *(New 2026-10-30: the v1.0.1 portable zip crashed at start. Kept by R9.6, for local builds.)* |
| R2.1 | US2   | **Replaced by R9.2.** ~~Each release shall be published on GitHub Releases with the MSIX, the portable zip, and SHA-256 checksums.~~ |
| R2.2 | US2   | **Withdrawn by R9:** there's no GitHub MSIX to submit. It had been deferred because winget-pkgs validation installs the package, which a self-signed MSIX fails. ~~After the first public release, the app shall be submitted to the `winget-pkgs` repository so it can be installed with WinGet itself.~~ |
| R3.1 | US3   | A GitHub Actions workflow shall build, run unit tests, and produce release artifacts on every tag `v*`. |
| R3.2 | US3   | The app version shall come from the git tag (one source of truth) and appear in Settings (006 R4.1). |
| R4.1 | –     | Settings (006) shall move to `ApplicationData` when packaged, migrating an existing JSON file once. |
| R5.1 | US5   | **Replaced by R9.8.** ~~The release workflow shall build every artifact without access to the signing key or its password. The key shall exist only in a separate signing job that restores no packages and runs no build.~~ |
| R5.2 | US5   | The workflows shall run with the least access each job needs: no default token permissions, a read-only token for building, write access only where the release is published, and no token left in the checkout. |
| R5.3 | US5   | Every action used by a workflow shall be GitHub-owned and pinned to a full commit SHA. |
| R5.4 | US5   | The tag name shall never be pasted into a script: workflows shall pass it as an environment variable, and the release workflow shall reject a tag that isn't `v` + `major.minor.patch` before doing anything else. |
| R5.5 | US4   | **Withdrawn by R9.** ~~Every signature shall carry an RFC 3161 timestamp, so it stays valid after the certificate expires. If a timestamp can't be obtained, the release shall fail.~~ |
| R5.6 | US4   | **Withdrawn by R9.** ~~The first-party binaries in the portable zip (`CartonStash.exe`, `CartonStash.dll`, `CartonStash.Core.dll`) shall be signed with the same certificate as the MSIX.~~ |
| R5.7 | US4   | **Withdrawn by R9.** ~~Each release shall carry a build attestation for every artifact, so anyone can check it was built by this repository's workflow.~~ |
| R5.8 | US4   | **Replaced by R9.7.** ~~The README and release notes shall publish the certificate's thumbprint with the steps to check it before trusting the certificate, and shall describe accurately what is signed.~~ |
| R5.9 | US5   | **Replaced by R9.8.** ~~The signing key and password shall be available only to the release workflow on `v*` tags, through a protected GitHub environment that needs the maintainer's approval for each release.~~ |
| R5.10 | US5  | `main` and `v*` tags shall be protected against force-push and deletion, and published releases shall be immutable. |
| R5.11 | US5  | The private key and its password shall not stay in usable form on the development PC. They shall be kept in the maintainer's password manager. Release builds on the PC shall be unsigned unless a PFX is given explicitly. |
| R5.12 | US5  | Package restores shall use only nuget.org, and CI shall restore from a lock file. Dependabot shall propose updates for NuGet packages and pinned actions. |
| R5.13 | US5  | The repository shall have a `SECURITY.md` explaining how to report a vulnerability privately, with private vulnerability reporting, secret scanning with push protection, and Dependabot alerts turned on. |
| R6.1 | US6   | Each tagged release shall also produce an MSIX for the Microsoft Store, for x64 and ARM64, from the same code and version as the GitHub files. |
| R6.2 | US6   | The Store MSIX shall carry the identity Partner Center assigned: name `51376anthonydapps.CartonStash`, publisher `CN=8E7BFF24-3095-4F83-84B3-2D4A3E780750`, publisher display name `anthonydapps`. |
| R6.3 | US6   | The Store MSIX shall not be signed by the project, shall not be published on GitHub Releases, and shall never reach the signing job. Microsoft signs it after certification. |
| R6.4 | US6   | **Replaced by R9.1.** ~~The GitHub MSIX and portable zip shall stay as they are (R1.1, R1.4, R2.1, R5.x), with the `CN=Anthony` identity and certificate. *(R8.1 changes only the GitHub MSIX's runtime.)*~~ |
| R6.5 | US6   | The Store MSIX shall pass the Windows App Certification Kit before it's submitted. |
| R6.6 | US6   | The Store's privacy policy text shall be kept in the repository as `PRIVACY.md`, and shall match what the app does. |
| R7.1 | US7   | The app shall reference only the Windows App SDK parts it uses, so that every package leaves out the AI, machine learning, search and widget libraries, if the app works without them. |
| R7.2 | US7   | The Store MSIX shall use the shared Windows App SDK runtime, which the Store installs with the app, instead of bundling its own copy. |
| R7.3 | US7   | The .NET runtime shall stay bundled in every package, and the app shall not be trimmed. |
| R7.4 | US7   | **Replaced by R8.1 and R8.3.** ~~The GitHub MSIX and portable zip shall keep their own copy of the Windows App SDK, and shall still work on a PC with no Store access (R1.2, R1.4).~~ |
| R7.5 | US7   | Every package shall pass the same checks as before: unit tests, the portable start check (R1.6), and search, install and uninstall on a real install (R1.3). |
| R7.6 | US7   | A Store update shall install over the existing Store version and keep its settings and logs. |
| R8.1 | US8   | **Withdrawn by R9.** ~~The GitHub MSIX shall use the shared Windows App SDK runtime, as the Store MSIX does (R7.2).~~ |
| R8.2 | US8   | **Withdrawn by R9.** ~~The README and release notes shall say that the GitHub MSIX needs the Windows App SDK runtime, link to Microsoft's download for it, and give the install order. They shall state which runtime version the release needs.~~ |
| R8.3 | US8   | The portable zip shall keep its own copy of the Windows App SDK, and shall work on a PC with no Store access and no runtime installed (R1.4, R1.6). *(Kept by R9.6, for local builds.)* |
| R8.4 | US8   | **Withdrawn by R9.** ~~With the runtime installed, the GitHub MSIX shall work on a PC with no Store access (R1.2).~~ |
| R8.5 | US8   | **Withdrawn by R9.** ~~With the runtime installed, the new GitHub MSIX shall install over an existing one and keep its settings and logs.~~ |
| R8.6 | US8   | **Withdrawn by R9.** ~~The first release under R8 shall tell existing GitHub MSIX users to install the runtime before updating.~~ |
| R9.1 | US9   | The Microsoft Store shall be the only place the project publishes an installable build. The project shall not publish an MSIX, portable zip or certificate anywhere else. |
| R9.2 | US9   | Each release shall be published on GitHub Releases with its changelog and no files. |
| R9.3 | US9   | The changelog shall live in `CHANGELOG.md`. If the tag's version has no section there, the release workflow shall fail before building. |
| R9.4 | US9   | The releases of v1.0.2 to v1.0.5 shall show the changelog only and hold no files. |
| R9.5 | US9   | The README's install section shall point to the Store listing only, and the README shall offer no download link or badge for release files. |
| R9.6 | US10  | The README shall let a developer build and run the app from source with no certificate. `Publish-Release.ps1` shall still produce the portable zip for local use, and shall still fail without `CartonStash.pri` (R1.6). |
| R9.7 | US11  | `SECURITY.md` shall keep the certificate's thumbprint, name v1.0.5 as the last release signed with it, and say how to move from a GitHub install to the Store and how to remove the certificate from `LocalMachine\TrustedPeople`. |
| R9.8 | US9   | The release workflow shall hold no signing step and no signing secrets, and the `SIGNING_PFX_*` secrets shall be deleted. The job that publishes a release shall stay in the `release` environment, behind the maintainer's approval. |
| R9.9 | US9   | The Store MSIX build (R6.1 to R6.3) shall not change: the `build` job has no secrets and keeps the `store` artifact for Partner Center. |

### What amendment R9 replaces

The rows above are marked. This is the record of why.

| Rows | Effect |
|------|--------|
| R1.1, R1.5, R2.1, R6.4 | Replaced by R9.1 and R9.2: no GitHub MSIX, certificate or checksums. |
| R1.4, R1.6, R8.3 | Kept for local builds: the portable zip still builds and starts, but isn't published (R9.6). |
| R2.2 | Withdrawn: there's no GitHub MSIX to submit to `winget-pkgs`. |
| R5.1, R5.9 | Replaced by R9.8: no key in the workflow, and publishing still needs the maintainer's approval. |
| R5.5, R5.6, R5.7 | Withdrawn: the project no longer signs or attests anything. |
| R5.8 | Replaced by R9.7. |
| R5.11 | Stays until the key is dealt with (see Open questions, amendment R9). |
| R8.1, R8.2, R8.4, R8.5, R8.6 | Withdrawn: no GitHub MSIX. T37 is dropped. |

All other rows are unchanged, including R5.2 to R5.4, R5.10, R5.12, R5.13 and the Store rows.

## Out of scope

- ~~Microsoft Store listing (possible later; needs a Partner Center account).~~ *Moved in scope by amendment R6.*
- Submitting to the Store from the workflow: the maintainer uploads each package in Partner Center by hand.
- Automatic in-app updater for the portable zip.
- A publicly trusted certificate or cloud signing service (amendment R5 hardens the self-signed setup; R1.1 still applies).
- Moving GitHub installs to the Store automatically. They keep working, and stop getting updates. *(Amendment R9.)*
- Signing a local MSIX. A local build is the portable zip or `dotnet run`, so background checks (spec 010) need the Store version. *(Amendment R9.)*

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

## Open questions (amendment R9)

- **The files on v1.0.2 to v1.0.5.** **Decided 2026-11-01: delete the releases and remake them with no files** (maintainer). GitHub refuses to remove the files on their own: `Cannot delete asset from an immutable release` (tried on v1.0.2, 2026-11-01). Deleting a release can't be undone. Turning off immutable releases wasn't tried: GitHub's docs don't say it frees existing releases, and it would drop R5.10.
  - *Remaking on the same tag.* **Not possible.** With v1.0.2's release deleted, GitHub refused a new one: `tag_name was used by an immutable release` (2026-11-01). A draft on such a tag is accepted (see the log), but that doesn't help.
  - *New tags.* **Decided 2026-11-01: yes** (maintainer). The releases are remade on `1.0.2` to `1.0.5`, at the same commits as the `v` tags. `release.yml` only runs on `v*`, and each release locks its tag, as R5.10 does for `v*`. Later releases keep the `v`.
- **GitHub's `Source code` archives.** **Decided 2026-11-01: they stay** (maintainer asked; they can't be removed). GitHub lists **Source code (zip)** and **Source code (tar.gz)** on every release, built from the tag. They aren't uploaded files, so `assets` stays 0 and R9.2 still holds, and they're the plain source, not a build for any platform. The repo builds no `tar.gz`.
- **What about users who can't use the Store?** **Decided 2026-11-01: they build from source.** This reverses amendment R6's "Keep offering the GitHub MSIX?" and the fallback in amendment R7's first question.
- **Keep or destroy the private key?** **Not decided.** Nothing signs with it after R9, but until people remove the certificate, anyone holding the key can make an MSIX their PC accepts. Recommended: destroy the KeePass copy once the secrets are deleted.
- **Where do release notes come from?** **Decided 2026-11-01: a hand-written `CHANGELOG.md`.** GitHub's generated notes would list titles such as "011 T8: close messages when they clear", which mean nothing to users.
- **Attest the Store MSIX instead?** **Decided 2026-11-01: no.** Microsoft signs the file after certification, so an attestation wouldn't match what users install. GitHub still makes its own release attestation for every immutable release, with no workflow step: it ties the tag to its commit and covers no build.

## Open questions (amendment R8)

- **Keep the bundled GitHub MSIX and leave it without background checks?** **Decided 2026-10-31: no, move it** (maintainer). Spec 010's T1 showed background tasks crash with the SDK bundled. The cost is one more install step for GitHub MSIX users, on top of the certificate. Anyone who won't do that can use the portable zip, without background checks.
- **What does a user see if the runtime is missing?** **Answered 2026-11-01 (T36):** App Installer shows **Install**, then "App Installer failed to install package dependencies." It doesn't fetch the runtime, and the message doesn't name it, so the README quotes the message.
- **Does the runtime installer need administrator rights?** **Not answered.** T36 ran as Windows Sandbox's administrator account. The docs say Windows may ask for it.

## Open questions (amendment R7)

- **What if a Store user's PC can't get the shared runtime?** **Decided 2026-10-31: accept it.** The Store delivers it with the app, so this should only affect managed PCs that block the Store's framework packages, and they can use the GitHub MSIX or portable zip.
- **Link to the runtime in the README?** **Decided 2026-10-31: no.** Under R7.4 nobody needs it, and removing the runtime from the GitHub files would add a step to an already harder install. *Reversed by amendment R8.*
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
