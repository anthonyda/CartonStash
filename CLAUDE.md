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

## Gotchas

- WinGet collections: index them, don't `foreach` (constitution W2).
- Don't pass a value that can become null to an x:Bind function: the generated code skips the call and the target keeps its old value. Bind a plain bool from the view model.
- Keep the repo at a short path. Long paths break the XAML compiler (error WMC1006 / WMC9999).
- Package versions live only in `Directory.Packages.props`. After changing one, run `dotnet restore --force-evaluate` and
  commit the `packages.lock.json` files: CI refuses a restore that doesn't match them (error NU1004).
- Add the version's section to `CHANGELOG.md` before tagging. Without it the release stops, and `v*` tags can't be moved or deleted, so that version number is lost.
- Published releases are immutable: files can't be removed, and a deleted release's tag can't be used again.
