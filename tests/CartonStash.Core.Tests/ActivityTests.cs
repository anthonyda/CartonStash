using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 009: operation logs and the pinned Activity pane.</summary>
public sealed class ActivityTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CartonStashTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    // Wired like the app, with a real log store in a temp folder.
    private (ScriptedInstaller Installer, PackageOperationQueue Queue, OperationLogStore Logs, FakeDialogs Dialogs, FakeSettings Settings, ActivityViewModel Vm) Create()
    {
        _ = new TestApp(); // installs the inline synchronization context
        var installer = new ScriptedInstaller();
        var logs = new OperationLogStore(_folder);
        var queue = new PackageOperationQueue(installer, logs);
        var dialogs = new FakeDialogs();
        var settings = new FakeSettings();
        return (installer, queue, logs, dialogs, settings, new ActivityViewModel(queue, logs, dialogs, settings));
    }

    [Fact]
    public void Each_run_gets_its_own_log_path_passed_to_the_installer() // R2.2
    {
        var (installer, queue, _, _, _, _) = Create();

        var op = queue.Enqueue(OperationKind.Install, Pkg.Remote(@"ARP\Machine\X64\Odd:Name"));

        var path = Assert.Single(installer.LogPaths);
        Assert.Equal(op.LogFilePath, path);
        Assert.StartsWith(_folder, path);
        Assert.EndsWith(".log", path);
        Assert.DoesNotContain("Odd:Name", Path.GetFileName(path)); // sanitized

        installer.Complete(@"ARP\Machine\X64\Odd:Name", OperationResult.Failed, "no");
        op.RetryCommand.Execute(null);
        Assert.Equal(2, installer.LogPaths.Count);
    }

    [Fact]
    public void Log_has_the_summary_and_the_installer_log() // R2.2
    {
        var (installer, queue, logs, _, _, _) = Create();
        var op = queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("Git.Git", "Git", update: true));
        File.WriteAllText(op.LogFilePath!, "installer line 1");

        installer.Complete("Git.Git", OperationResult.Failed, "The installer failed with error code 1603.");

        var text = logs.Read(op);
        Assert.Contains("Action: Update", text);
        Assert.Contains("Package: Git (Git.Git)", text);
        Assert.Contains("Result: Failed", text);
        Assert.Contains("Message: The installer failed with error code 1603.", text);
        Assert.Contains("installer line 1", text);
        Assert.Equal(text, File.ReadAllText(op.LogFilePath!)); // survives restarts
    }

    [Fact]
    public void Log_says_so_when_the_installer_wrote_nothing() // R2.3
    {
        var (installer, queue, logs, _, _, _) = Create();
        var op = queue.Enqueue(OperationKind.Uninstall, Pkg.Installed("A"));

        installer.Complete("A");

        Assert.Contains("The installer didn't write a log.", logs.Read(op));
    }

    private sealed class FixedRecords(WinGetRecord record) : IWinGetRecordReader
    {
        public (DateTimeOffset Started, DateTimeOffset Finished)? Asked { get; private set; }

        public WinGetRecord Find(DateTimeOffset startedAt, DateTimeOffset finishedAt)
        {
            Asked = (startedAt, finishedAt);
            return record;
        }
    }

    private (ScriptedInstaller Installer, PackageOperationQueue Queue, OperationLogStore Logs) CreateWith(IWinGetRecordReader records)
    {
        _ = new TestApp();
        var installer = new ScriptedInstaller();
        var logs = new OperationLogStore(_folder, records);
        return (installer, new PackageOperationQueue(installer, logs), logs);
    }

    [Fact]
    public void WinGets_record_comes_last_and_is_saved() // R2.8
    {
        var records = new FixedRecords(new WinGetRecord("winget line 1", null));
        var (installer, queue, logs) = CreateWith(records);
        var op = queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        File.WriteAllText(op.LogFilePath!, "installer line 1");

        installer.Complete("A");

        var text = logs.Read(op);
        Assert.True(text.IndexOf("installer line 1", StringComparison.Ordinal) < text.IndexOf("--- WinGet log ---", StringComparison.Ordinal));
        Assert.True(text.IndexOf("--- WinGet log ---", StringComparison.Ordinal) < text.IndexOf("winget line 1", StringComparison.Ordinal));
        Assert.Contains("winget line 1", File.ReadAllText(op.LogFilePath!));
        Assert.Equal((op.StartedAt!.Value, op.FinishedAt!.Value), records.Asked);
    }

    [Fact]
    public void A_missing_record_is_explained_and_the_operation_still_succeeds() // R2.9
    {
        var (installer, queue, logs) = CreateWith(new FixedRecords(new WinGetRecord(null, WinGetRecordReader.NotFound)));
        var op = queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));

        installer.Complete("A");

        Assert.Equal(OperationResult.Succeeded, op.Result);
        Assert.Contains(WinGetRecordReader.NotFound, logs.Read(op));
    }

    [Fact]
    public void Details_are_included_in_the_summary() // R2.2
    {
        var op = NewOperation(new OperationOutcome(OperationResult.Failed, "no",
            WinGetPackageInstaller.FormatDetails("InstallError", 1603, new InvalidOperationException { HResult = unchecked((int)0x8A150011) })));

        var text = OperationLogStore.Compose(op, null);

        Assert.Contains("WinGet status: InstallError", text);
        Assert.Contains("Installer exit code: 1603", text);
        Assert.Contains("Extended error: 0x8A150011", text);
    }

    [Fact]
    public void An_unusable_log_folder_never_fails_the_operation() // R2.5
    {
        _ = new TestApp();
        Directory.CreateDirectory(Path.GetDirectoryName(_folder)!);
        File.WriteAllText(_folder, "a file where the folder should be");
        var installer = new ScriptedInstaller();
        var logs = new OperationLogStore(_folder);
        var queue = new PackageOperationQueue(installer, logs);

        var op = queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        installer.Complete("A");

        Assert.Null(op.LogFilePath);
        Assert.Equal(OperationResult.Succeeded, op.Result);
        Assert.Contains("Action: Install", logs.Read(op)); // the summary still shows
        File.Delete(_folder);
    }

    [Fact]
    public void Only_the_newest_logs_are_kept() // R2.5
    {
        Directory.CreateDirectory(_folder);
        var start = DateTime.UtcNow.AddHours(-1);
        for (var i = 0; i < OperationLogStore.KeptLogs + 5; i++)
        {
            var file = Path.Combine(_folder, $"old-{i}.log");
            File.WriteAllText(file, "x");
            File.SetLastWriteTimeUtc(file, start.AddMinutes(i));
        }

        _ = new OperationLogStore(_folder);

        Assert.Equal(OperationLogStore.KeptLogs, Directory.GetFiles(_folder, "*.log").Length);
        Assert.False(File.Exists(Path.Combine(_folder, "old-0.log")));
        Assert.True(File.Exists(Path.Combine(_folder, $"old-{OperationLogStore.KeptLogs + 4}.log")));
    }

    [Fact]
    public void Very_long_logs_show_their_end() // design: last 1 MB
    {
        var (installer, queue, logs, _, _, _) = Create();
        var op = queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        File.WriteAllText(op.LogFilePath!, new string('x', OperationLogStore.MaxShownCharacters + 10) + "THE END");
        installer.Complete("A");

        var text = logs.Read(op);

        Assert.StartsWith("(Only the end of this log is shown.", text);
        Assert.Contains("THE END", text);
    }

    [Fact]
    public void Log_button_opens_the_dialog_with_the_log() // R2.1, R2.2, R2.4
    {
        var (installer, queue, _, dialogs, _, _) = Create();
        var op = queue.Enqueue(OperationKind.Install, Pkg.Remote("A", "Contoso"));
        Assert.False(op.ShowLogCommand.CanExecute(null)); // finished only

        installer.Complete("A");
        Assert.True(op.ShowLogCommand.CanExecute(null));
        op.ShowLogCommand.Execute(null);

        var shown = Assert.Single(dialogs.LogsShown);
        Assert.Equal("Install Contoso", shown.Title);
        Assert.Contains("Package: Contoso (A)", shown.Text);
        Assert.Equal(_folder, shown.Folder);
    }

    [Fact]
    public void Pinning_is_saved_in_settings() // R3.4
    {
        var (_, _, _, _, settings, vm) = Create();
        Assert.False(vm.IsPinned);
        var raised = 0;
        vm.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(ActivityViewModel.IsPinned) ? 1 : 0;

        vm.IsPinned = true;

        Assert.True(settings.Current.ActivityPinned);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Older_settings_files_load_unpinned() // R3.4
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, """{ "Theme": "Dark" }""");

        var (loaded, reset) = new JsonSettingsStore(path).Load();

        Assert.False(loaded.ActivityPinned);
        Assert.False(reset);
    }

    private static PackageOperation NewOperation(OperationOutcome outcome)
    {
        _ = new TestApp();
        var installer = new ScriptedInstaller { AutoComplete = outcome };
        var queue = new PackageOperationQueue(installer);
        var op = queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        return op;
    }
}
