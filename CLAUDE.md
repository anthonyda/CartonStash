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
.\tools\Publish-Release.ps1 -Version 1.2.3            # unsigned MSIX + portable zip in artifacts\ (spec 007)
.\tools\Sign-Release.ps1 -PfxPath x.pfx -PfxPassword y  # signs them, writes checksums (the release workflow does this)
```

## Gotchas

- WinGet collections: index them, don't `foreach` (constitution W2).
- Keep the repo at a short path. Long paths break the XAML compiler (error WMC1006 / WMC9999).
- Package versions live only in `Directory.Packages.props`. After changing one, run `dotnet restore --force-evaluate` and
  commit the `packages.lock.json` files: CI refuses a restore that doesn't match them (error NU1004).
