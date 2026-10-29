using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.Services;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.Core.Tests;

internal static class Pkg
{
    public static PackageInfo Remote(string id, string? name = null) => new(id, name ?? id, null, "1.0", "winget");

    public static PackageInfo Installed(string id, string? name = null, bool update = false, string source = "winget") =>
        new(id, name ?? id, "1.0", source.Length > 0 ? (update ? "2.0" : "1.0") : null, source, update);
}

internal sealed class FakePackageService : IPackageService
{
    public Func<string, CancellationToken, Task<IReadOnlyList<PackageInfo>>> Search { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<PackageInfo>>([]);

    public Func<CancellationToken, Task<IReadOnlyList<PackageInfo>>> Installed { get; set; } =
        _ => Task.FromResult<IReadOnlyList<PackageInfo>>([]);

    public Func<PackageInfo, CancellationToken, Task<PackageDetails?>> Details { get; set; } =
        (_, _) => Task.FromResult<PackageDetails?>(null);

    public List<SourceInfo> Sources { get; } = [];

    public Dictionary<string, string?> RefreshErrors { get; } = [];

    public List<string> Refreshed { get; } = [];

    public List<string> AcceptedAgreements { get; } = [];

    public int InstalledCalls { get; private set; }

    public int Invalidations { get; private set; }

    public Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default) =>
        Search(query, cancellationToken);

    public Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        InstalledCalls++;
        return Installed(cancellationToken);
    }

    public Task<PackageDetails?> GetDetailsAsync(PackageInfo package, CancellationToken cancellationToken = default) =>
        Details(package, cancellationToken);

    public void InvalidateCache() => Invalidations++;

    public Task<IReadOnlyList<SourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SourceInfo>>(Sources.ToList());

    public Task<string?> RefreshSourceAsync(string sourceName, CancellationToken cancellationToken = default)
    {
        Refreshed.Add(sourceName);
        return Task.FromResult(RefreshErrors.GetValueOrDefault(sourceName));
    }

    public void AcceptSourceAgreements(string sourceName) => AcceptedAgreements.Add(sourceName);

    public string GetWinGetVersion() => "1.29.380";
}

/// <summary>An installer whose operations finish only when the test says so.</summary>
internal sealed class ScriptedInstaller : IPackageInstaller
{
    private readonly Dictionary<string, (TaskCompletionSource<OperationOutcome> Done, IProgress<OperationProgress> Progress)> _running = [];

    public List<(OperationKind Kind, string Id, bool AcceptAgreements)> Calls { get; } = [];

    public IReadOnlyList<Agreement> Agreements { get; set; } = [];

    /// <summary>When set, every operation completes immediately with this outcome.</summary>
    public OperationOutcome? AutoComplete { get; set; }

    public Task<IReadOnlyList<Agreement>> GetAgreementsAsync(PackageInfo package, CancellationToken cancellationToken = default) =>
        Task.FromResult(Agreements);

    public Task<OperationOutcome> RunAsync(OperationKind kind, PackageInfo package, bool acceptAgreements,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        Calls.Add((kind, package.Id, acceptAgreements));
        if (AutoComplete is { } outcome)
        {
            return Task.FromResult(outcome);
        }

        var done = new TaskCompletionSource<OperationOutcome>();
        cancellationToken.Register(() => done.TrySetCanceled(cancellationToken));
        _running[package.Id] = (done, progress);
        return done.Task;
    }

    public bool IsRunning(string id) => _running.TryGetValue(id, out var r) && !r.Done.Task.IsCompleted;

    public void Report(string id, OperationPhase phase, double? percent = null) => _running[id].Progress.Report(new(phase, percent));

    public void Complete(string id, OperationResult result = OperationResult.Succeeded, string message = "ok") =>
        _running[id].Done.SetResult(new OperationOutcome(result, message));

    public void Throw(string id, Exception ex) => _running[id].Done.SetException(ex);
}

internal sealed class FakeDialogs : IDialogService
{
    public bool Answer { get; set; } = true;

    public List<string> Confirmations { get; } = [];

    public List<(string Title, IReadOnlyList<Agreement> Agreements)> AgreementPrompts { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText = "Cancel")
    {
        Confirmations.Add($"{title} | {message}");
        return Task.FromResult(Answer);
    }

    public Task<bool> ShowAgreementsAsync(string title, IReadOnlyList<Agreement> agreements)
    {
        AgreementPrompts.Add((title, agreements));
        return Task.FromResult(Answer);
    }
}

internal sealed class FakeSettings(AppSettings? initial = null) : ISettings
{
    public AppSettings Current { get; private set; } = initial ?? new AppSettings();

    public bool WasReset { get; init; }

    public event EventHandler? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeClipboard : IClipboardService
{
    public string? Text { get; private set; }

    public void SetText(string text) => Text = text;
}

/// <summary>Everything a view model test needs, wired like the app.</summary>
internal sealed class TestApp
{
    public TestApp()
    {
        // The app runs the queue on the UI thread; here, run posted callbacks inline so tests are deterministic.
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
        Queue = new PackageOperationQueue(Installer);
        Actions = new PackageActions(Queue, Installer, Dialogs);
        Store = new InstalledPackagesStore(Packages, Queue);
    }

    public FakePackageService Packages { get; } = new();

    public ScriptedInstaller Installer { get; } = new();

    public FakeDialogs Dialogs { get; } = new();

    public PackageOperationQueue Queue { get; }

    public PackageActions Actions { get; }

    public InstalledPackagesStore Store { get; }

    public void SetInstalled(params PackageInfo[] packages) =>
        Packages.Installed = _ => Task.FromResult<IReadOnlyList<PackageInfo>>(packages);
}

internal sealed class InlineSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => d(state);

    public override SynchronizationContext CreateCopy() => this;
}
