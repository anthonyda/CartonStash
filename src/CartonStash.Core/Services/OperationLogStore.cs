using System.Text;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>Per-operation log files (spec 009 R2). Never lets a logging problem affect an operation (R2.5).</summary>
public interface IOperationLogStore
{
    /// <summary>The folder holding the logs, for "Open folder" (R2.4).</summary>
    string Folder { get; }

    /// <summary>A new log path for this run, or null if the folder can't be used.</summary>
    string? CreateLogPath(PackageOperation operation);

    /// <summary>Writes the summary at the top of the log, keeping what the installer wrote (R2.2, R2.3).</summary>
    void Complete(PackageOperation operation);

    /// <summary>The text to show: the log file, or the summary alone if the file can't be read (R2.5).</summary>
    string Read(PackageOperation operation);
}

/// <summary>
/// Logs as text files, by default in the app's data folder. Inside the MSIX that is the package's real LocalFolder:
/// WinGet's server runs in another process and doesn't see the virtualized %LOCALAPPDATA% (009 design).
/// </summary>
public sealed class OperationLogStore : IOperationLogStore
{
    /// <summary>Operations whose logs are kept (R2.5).</summary>
    internal const int KeptLogs = 50;

    /// <summary>Longest text shown or copied; the end of a log matters most (009 design).</summary>
    internal const int MaxShownCharacters = 1024 * 1024;

    private const string NoInstallerLog = "(The installer didn't write a log.)";

    public OperationLogStore()
        : this(Path.Combine(
            AppPackage.LocalFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.DataFolder),
            "logs"))
    {
    }

    public OperationLogStore(string folder)
    {
        Folder = folder;
        Prune();
    }

    public string Folder { get; }

    public string? CreateLogPath(PackageOperation operation)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var time = (operation.StartedAt ?? DateTimeOffset.Now).ToLocalTime();
            var name = $"{time:yyyyMMdd-HHmmss}-{operation.Kind}-{Sanitize(operation.Package.Id)}";

            // Two runs within a second stay separate files.
            var path = Path.Combine(Folder, name + ".log");
            for (var i = 2; File.Exists(path); i++)
            {
                path = Path.Combine(Folder, $"{name}-{i}.log");
            }
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Complete(PackageOperation operation)
    {
        if (operation.LogFilePath is not { } path)
        {
            return;
        }

        try
        {
            var installerLog = File.Exists(path) ? File.ReadAllText(path) : null;
            File.WriteAllText(path, Compose(operation, installerLog));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            operation.LogFilePath = null; // the dialog falls back to the summary
        }
    }

    public string Read(PackageOperation operation)
    {
        try
        {
            if (operation.LogFilePath is { } path && File.Exists(path))
            {
                var text = File.ReadAllText(path);
                return text.Length <= MaxShownCharacters
                    ? text
                    : $"(Only the end of this log is shown. The full log is in {path})\n\n{text[^MaxShownCharacters..]}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return Compose(operation, null);
    }

    /// <summary>The summary, then the installer's log or a note that there is none (R2.2, R2.3).</summary>
    internal static string Compose(PackageOperation operation, string? installerLog)
    {
        var package = operation.Package;
        var text = new StringBuilder()
            .AppendLine($"{AppInfo.Name} operation log")
            .AppendLine($"Action: {operation.KindText}")
            .AppendLine($"Package: {package.Name} ({package.Id})")
            .AppendLine($"Installed version: {package.InstalledVersion ?? "none"}")
            .AppendLine($"Available version: {package.AvailableVersion ?? "none"}")
            .AppendLine($"Source: {package.SourceText}")
            .AppendLine($"Started: {operation.StartedAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Finished: {operation.FinishedAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Result: {operation.StatusText}")
            .AppendLine($"Message: {operation.Message}");
        if (operation.Details is { } details)
        {
            text.AppendLine(details.Trim());
        }

        return text.AppendLine()
            .AppendLine("--- Installer log ---")
            .AppendLine(string.IsNullOrWhiteSpace(installerLog) ? NoInstallerLog : installerLog)
            .ToString();
    }

    /// <summary>Keeps the newest logs (R2.5).</summary>
    private void Prune()
    {
        try
        {
            if (!Directory.Exists(Folder))
            {
                return;
            }
            foreach (var old in new DirectoryInfo(Folder).GetFiles("*.log")
                         .OrderByDescending(f => f.LastWriteTimeUtc)
                         .Skip(KeptLogs))
            {
                old.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Package IDs can hold characters that aren't valid in a file name (ARP\Machine\...).
    private static string Sanitize(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(id.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return safe.Length <= 60 ? safe : safe[..60];
    }
}
