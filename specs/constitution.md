# Constitution

Non-negotiable rules for WinGet Frontend. A spec or change that conflicts with these must
amend this file first, explicitly and with a reason.

## 1. Product

- **P1. A thin, honest frontend.** WinGet is the source of truth. We never maintain our own package
  database or guess at state WinGet can tell us.
- **P2. Never surprise the user.** No install, uninstall or upgrade runs without an explicit user action.
  Destructive actions (uninstall, "update all") ask for confirmation.
- **P3. Always show progress and outcome.** Every long operation shows progress, can be cancelled
  where WinGet allows it, and ends with a visible success or error message.

## 2. Architecture

- **A1. Layers.** `WinGetFrontend.Core` holds models, services and view models and has **no WinUI
  dependency**. `WinGetFrontend.App` holds XAML views, DI wiring and platform glue only.
- **A2. One gateway to WinGet.** Only classes in `Core/Services` touch `Microsoft.Management.Deployment`.
  They return plain records (`Core/Models`), never COM objects.
- **A3. MVVM.** Views bind to view models with `x:Bind`. Code-behind is limited to view-only concerns
  (focus, keyboard shortcuts, window sizing). View models use CommunityToolkit.Mvvm source generators.
- **A4. Async everywhere.** WinGet calls are async, accept a `CancellationToken`, and never block the UI thread.
- **A5. Least privilege.** The app runs `asInvoker`. Elevation is requested per operation, never for the whole app.

## 3. WinGet COM interop rules (learned the hard way)

- **W1.** The host `.exe` must sit next to `Microsoft.Management.Deployment.winmd` and the static
  `Microsoft.Management.Deployment.dll`. Core's csproj republishes both; don't remove that.
- **W2.** Read WinGet collections **by index** (`for (i < list.Count)`). `foreach` / LINQ require
  `IIterable`, which the WinGet server does not marshal ("Interface not registered").
- **W3.** Check every `*Result.Status` and turn failures into `WinGetException` carrying `ExtendedErrorCode`.
- **W4.** Keep the repo at a short path (well under ~100 chars). Windows `MAX_PATH` breaks the XAML
  compiler and process launch from deep `bin/` folders.

## 4. Quality

- **Q1. Specs before code.** Every feature has an approved `requirements.md` and `design.md` before implementation.
- **Q2. Traceability.** Every acceptance criterion maps to at least one test or a written manual check in `tasks.md`.
- **Q3. Tests.** View model and service-mapping logic is unit tested against fakes of `IPackageService`.
  Real WinGet behaviour is covered by tests tagged `[Trait("Category", "Integration")]`.
- **Q4. Green build.** `TreatWarningsAsErrors` stays on. `dotnet build` and `dotnet test` pass before every commit.
- **Q5. Accessibility.** Every interactive control is keyboard reachable and has an accessible name.
  The app works in light, dark and high-contrast themes.

## 5. Stack (pinned in Directory.Packages.props)

.NET 10 · WinUI 3 / Windows App SDK 2.6 · Microsoft.WindowsPackageManager.ComInterop 1.29 ·
CommunityToolkit.Mvvm · Microsoft.Extensions.DependencyInjection · xUnit
