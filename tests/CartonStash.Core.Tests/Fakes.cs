using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

internal static class Pkg
{
    public static PackageInfo Remote(string id, string? name = null) => new(id, name ?? id, null, "1.0", "winget");

    public static PackageInfo Installed(string id, string? name = null, bool update = false, string source = "winget") =>
        new(id, name ?? id, "1.0", source.Length > 0 ? (update ? "2.0" : "1.0") : null, source, update);
}

/// <summary>Spec 012: GitHub release notes, scripted. Records every request; only what a test puts in <see cref="Cache"/> is cached.</summary>
internal sealed class FakeReleaseNotes : IReleaseNotesService
{
    public Func<GitHubRelease, CancellationToken, Task<GitHubNotesResult>> Get { get; set; } =
        (_, _) => Task.FromResult(new GitHubNotesResult(GitHubNotesOutcome.NotFound));

    public List<(GitHubRelease Release, CancellationToken Token)> Requests { get; } = [];

    public Dictionary<GitHubRelease, GitHubNotes> Cache { get; } = [];

    public Task<GitHubNotesResult> GetGitHubNotesAsync(GitHubRelease release, CancellationToken cancellationToken)
    {
        Requests.Add((release, cancellationToken));
        return Get(release, cancellationToken);
    }

    public GitHubNotes? TryGetCached(GitHubRelease release) => Cache.GetValueOrDefault(release);
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

    public IReadOnlyList<SourceFailure> SourceFailures { get; set; } = [];

    public Dictionary<string, string?> RefreshErrors { get; } = [];

    public List<string> Refreshed { get; } = [];

    public List<(string Source, bool Accepted)> AgreementAnswers { get; } = [];

    public IReadOnlyList<string> LeftOutSources { get; set; } = [];

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

    /// <summary>015 R1.3: whether a version offers both scopes; false by default.</summary>
    public Func<PackageInfo, string, bool> ScopeChoice { get; set; } = (_, _) => false;

    public List<string> ScopeChoiceAsked { get; } = [];

    public Task<bool> OffersScopeChoiceAsync(PackageInfo package, string version, CancellationToken cancellationToken = default)
    {
        ScopeChoiceAsked.Add(version);
        return Task.FromResult(ScopeChoice(package, version));
    }

    /// <summary>014 R2.6, R2.7: what a fresh read returns; null (the default) is a read that failed.</summary>
    public Func<PackageInfo, PackageInfo?> ReadInstalled { get; set; } = _ => null;

    public int ReadInstalledCalls { get; private set; }

    public Task<PackageInfo?> ReadInstalledAsync(PackageInfo package, CancellationToken cancellationToken = default)
    {
        ReadInstalledCalls++;
        return Task.FromResult(ReadInstalled(package));
    }

    public Dictionary<string, string?> IconUrls { get; } = [];

    public List<string> IconRequests { get; } = [];

    public Task<string?> GetIconUrlAsync(PackageInfo package, CancellationToken cancellationToken = default)
    {
        IconRequests.Add(package.Id);
        return Task.FromResult(IconUrls.GetValueOrDefault(package.Id));
    }

    public void InvalidateCache() => Invalidations++;

    /// <summary>Thrown by <see cref="GetSourcesAsync"/> when set.</summary>
    public Exception? SourcesError { get; set; }

    public Task<IReadOnlyList<SourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default) =>
        SourcesError is { } error ? Task.FromException<IReadOnlyList<SourceInfo>>(error) : Task.FromResult<IReadOnlyList<SourceInfo>>(Sources.ToList());

    public Task<string?> RefreshSourceAsync(string sourceName, CancellationToken cancellationToken = default)
    {
        Refreshed.Add(sourceName);
        return Task.FromResult(RefreshErrors.GetValueOrDefault(sourceName));
    }

    public IReadOnlyList<Agreement> AgreementsWhenRead { get; set; } = [];

    public Task<SourceInfo> ReadSourceAgreementsAsync(SourceInfo source, CancellationToken cancellationToken = default) =>
        Task.FromResult(source with { Agreements = AgreementsWhenRead });

    public void SetSourceAgreementAnswer(SourceInfo source, bool accepted)
    {
        AgreementAnswers.Add((source.Name, accepted));
        var i = Sources.FindIndex(s => s.Id == source.Id);
        if (i >= 0)
        {
            Sources[i] = Sources[i] with { AgreementStatus = accepted ? AgreementStatus.Accepted : AgreementStatus.Declined };
        }
    }

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

    /// <summary>Per package ID; packages not listed get <see cref="Agreements"/>.</summary>
    public Dictionary<string, IReadOnlyList<Agreement>> AgreementsById { get; } = [];

    public bool AgreementsFail { get; set; }

    /// <summary>The version each agreement read asked for (014 R2.2).</summary>
    public List<string?> AgreementVersions { get; } = [];

    /// <summary>The version each operation ran with (014 R2.1).</summary>
    public List<string?> Versions { get; } = [];

    public Task<IReadOnlyList<Agreement>> GetAgreementsAsync(PackageInfo package, string? version = null, CancellationToken cancellationToken = default)
    {
        AgreementVersions.Add(version);
        return AgreementsFail
            ? Task.FromException<IReadOnlyList<Agreement>>(new WinGetException("Couldn't reach the source.", null))
            : Task.FromResult(AgreementsById.GetValueOrDefault(package.Id, Agreements));
    }

    public List<string?> LogPaths { get; } = [];

    /// <summary>The explicit scope each operation ran with (014 R3.8).</summary>
    public List<InstallScope?> Scopes { get; } = [];

    /// <summary>Where each explicit scope came from (015 R1.5).</summary>
    public List<ScopeSource> ScopeSources { get; } = [];

    public Task<OperationOutcome> RunAsync(OperationKind kind, PackageInfo package, bool acceptAgreements, string? version, string? logPath,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken, InstallScope? scope = null, ScopeSource scopeSource = ScopeSource.Installed)
    {
        ScopeSources.Add(scopeSource);
        Calls.Add((kind, package.Id, acceptAgreements));
        Versions.Add(version);
        Scopes.Add(scope);
        LogPaths.Add(logPath);
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

    /// <summary>003 R8.3: the installer's notice that Windows may ask for permission.</summary>
    public void ReportPermission(string id) => _running[id].Progress.Report(new(OperationPhase.Queued) { MayAskPermission = true });

    public void Complete(string id, OperationResult result = OperationResult.Succeeded, string message = "ok") =>
        _running[id].Done.SetResult(new OperationOutcome(result, message));

    public void Complete(string id, OperationOutcome outcome) => _running[id].Done.SetResult(outcome);

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

    /// <summary>What the checkbox ends as when the user confirms; null keeps what the app proposed (014 R3.2).</summary>
    public bool? OptionAnswer { get; set; }

    public List<(string Title, string Message, string? Option, bool Checked)> OptionPrompts { get; } = [];

    public Task<(bool Confirmed, bool OptionChecked)> ConfirmWithOptionAsync(string title, string message, string confirmText,
        string? optionText, bool optionChecked)
    {
        OptionPrompts.Add((title, message, optionText, optionChecked));
        var isChecked = optionText is not null && (OptionAnswer ?? optionChecked);
        return Task.FromResult((Answer, Answer && isChecked));
    }

    public List<(string Title, string Text, string Folder)> LogsShown { get; } = [];

    public Task ShowLogAsync(string title, string text, string folder)
    {
        LogsShown.Add((title, text, folder));
        return Task.CompletedTask;
    }

    public AgreementAnswer SourceAnswer { get; set; } = AgreementAnswer.Accepted;

    public List<(string Source, string? Receives)> SourcePrompts { get; } = [];

    public Action? SourceAsked { get; set; }

    public Task<AgreementAnswer> ShowSourceAgreementAsync(SourceInfo source, string? receivesNotice)
    {
        SourcePrompts.Add((source.Name, receivesNotice));
        SourceAsked?.Invoke();
        return Task.FromResult(SourceAnswer);
    }

    /// <summary>Answers to agreement prompts, in order; <see cref="Answer"/> once they run out.</summary>
    public Queue<bool> AgreementAnswers { get; } = new();

    public Task<bool> ShowAgreementsAsync(string title, IReadOnlyList<Agreement> agreements)
    {
        AgreementPrompts.Add((title, agreements));
        return Task.FromResult(AgreementAnswers.TryDequeue(out var answer) ? answer : Answer);
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
        Actions = new PackageActions(Queue, Installer, Dialogs, Packages, Settings);
        Store = new InstalledPackagesStore(Packages, Queue);
        Agreements = new SourceAgreementCheck(Packages, Dialogs, Store);
    }

    public SourceAgreementCheck Agreements { get; }

    public FakePackageService Packages { get; } = new();

    public FakeReleaseNotes ReleaseNotes { get; } = new();

    public ScriptedInstaller Installer { get; } = new();

    public FakeDialogs Dialogs { get; } = new();

    public PackageOperationQueue Queue { get; }

    public PackageActions Actions { get; }

    public InstalledPackagesStore Store { get; }

    public FakeSettings Settings { get; } = new();

    public void SetInstalled(params PackageInfo[] packages) =>
        Packages.Installed = _ => Task.FromResult<IReadOnlyList<PackageInfo>>(packages);
}

internal sealed class InlineSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => d(state);

    public override SynchronizationContext CreateCopy() => this;
}
