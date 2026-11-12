# CartonStash

Windows desktop frontend for WinGet. C# / .NET 10, WinUI 3 (Windows App SDK), WinGet COM API.

## This project is spec-driven

Read `specs/README.md` (workflow) and `specs/constitution.md` (rules) before changing anything.

- Never implement a feature without an `Approved` `requirements.md` **and** `design.md` in `specs/NNN-*/`.
- When asked for a new feature: write or update the spec first, then stop for review.
- When implementing: work through `tasks.md` in order and tick tasks as they are verified.
  Reference requirement IDs (e.g. R1.2) in test names or comments where it helps traceability.
- If the code needs to differ from the spec, update the spec first and say so.

## Layout

- `src/CartonStash.Core`: models, services (the only code that touches WinGet), view models. No WinUI.
- `src/CartonStash.App`: WinUI 3 views, DI wiring in `App.xaml.cs`.
- `tests/CartonStash.Core.Tests`: xUnit. Integration tests are tagged `Category=Integration`.

## Commands

```
dotnet build                                            # whole solution (RID defaults to this machine)
dotnet test                                             # all tests, incl. live WinGet
dotnet test --filter "Category!=Integration"            # unit tests only
dotnet run --project src/CartonStash.App             # run the app
.\tools\Publish-Release.ps1 -Version 1.2.3            # portable zip in artifacts\, Store MSIX in store\ (spec 007)
.\tools\Get-Changelog.ps1 -Version 1.2.3              # that version's CHANGELOG.md section: the GitHub Release notes
```

## Where build files go

Always these folders, in the main checkout `C:\dev\CartonStash`. Never make up another one. Both are git-ignored, and
`Publish-Release.ps1` empties them at the start of each build.

- **Store MSIX packages for Partner Center:** `store\`. That's where a local `Publish-Release.ps1` writes them, and
  where a release's packages are downloaded to: `gh run download <release run id> --name store --dir store`. Empty
  the folder first, so it only holds the version to upload. Give the user the full paths.
- **Portable zips:** `artifacts\`.
- **Test builds for the VM:** `C:\VMs\CartonStash-Test\builds\<run name>\` (see `TEST-ENV.local.md`), never inside the repo.

## Screenshots (README and Store listing)

Take them in the test VM from the release's Store build, 2560x1440 window crops (VM screen 2560x1600, scaling 125%),
half light and half dark (scripts: `C:\VMs\CartonStash-Test\guest\shots2-setup.ps1` elevated, `dpi.ps1`, restart,
then `shots2-run.ps1`; they live in the VM folder, not the repo). Keep this set so shots stay comparable between
versions; change it here first.

- **Setup (all users):** the VM's own apps updated first, except App Installer; Steam (current); PrusaSlicer, VLC,
  7-Zip and Notepad++ one version back (Updates content); Notepad++ excluded; msstore source removed (no agreement
  dialog). **Per user:** Obsidian one version back. Settings: start page Search, scope Any; solid desktop in the
  window's background color.
- **Operations during the run:** per user only (Pandoc for the current user, Obsidian update, Spotify install), so no
  UAC prompt can appear. Programs that start after installing are closed before the next shot.

| Store # | File in `docs/images/` | Theme | Content |
|---|---|---|---|
| 1 | `search.png` | Light | Search for `spotify`, Spotify details open |
| 2 | `installed.png` | Dark | Installed page, list scrolled to the end (the samples, after the Microsoft runtimes) |
| 3 | `updates.png` | Light | Updates: PrusaSlicer, VLC, 7-Zip, Obsidian; Notepad++ under Excluded |
| 4 | `activity.png` | Dark | Activity pinned on Installed: Pandoc, Obsidian, Spotify done |
| 5 | `details.png` | Light | Obsidian from Updates: release notes, newest version selected |
| 6 | `goback.png` | Dark | PrusaSlicer details, older version selected (Go back) |
| 7 | `install-scope.png` | Light | Pandoc's Install split button open |
| 8 | `log.png` | Dark | Log of the Obsidian update |
| 9 | `taskbar.png` | Light | Updates page plus the taskbar with the updates badge (screen crop) |
| 10 | `settings.png` | Dark | Settings, background checks every 24 hours |

The Store allows 10 screenshots: a new one replaces one of these. Where the files go:

- **Raw output of a run:** `C:\VMs\CartonStash-Test\logs\shots\` (`name.png` crop, `full-name.png` whole screen).
  Overwritten by the next run.
- **README:** `docs/images/name.png`, committed.
- **Store upload:** `artifacts\store-listing\<version>_<date taken>\N-name.png` (e.g.
  `1.4.0_2026-11-12\1-search.png`), numbered in the Store order above, one folder per release whose shots were retaken. Git-ignored; `Publish-Release.ps1` only empties the
  files at the top of `artifacts\`, not these folders. The Store logo stays at `artifacts\store-listing\logo-300x300.png`.
  Give the user the full path.

## Gotchas

- WinGet collections: index them, don't `foreach` (constitution W2).
- Don't pass a value that can become null to an x:Bind function: the generated code skips the call and the target keeps its old value. Bind a plain bool from the view model.
- Keep the repo at a short path. Long paths break the XAML compiler (error WMC1006 / WMC9999).
- Package versions live only in `Directory.Packages.props`. After changing one, run `dotnet restore --force-evaluate` and
  commit the `packages.lock.json` files: CI refuses a restore that doesn't match them (error NU1004).
- Add the version's section to `CHANGELOG.md` before tagging. Without it the release stops, and `v*` tags can't be moved or deleted, so that version number is lost.
- Published releases are immutable: files can't be removed, and a deleted release's tag can't be used again.
