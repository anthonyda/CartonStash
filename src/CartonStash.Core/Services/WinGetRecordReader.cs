using System.Text.RegularExpressions;

namespace CartonStash.Core.Services;

/// <summary>WinGet's record of one operation, or the reason there is none to show (009 R2.9).</summary>
public sealed record WinGetRecord(string? Text, string? Missing);

/// <summary>Finds what WinGet wrote about an operation (spec 009 R2.8).</summary>
public interface IWinGetRecordReader
{
    WinGetRecord Find(DateTimeOffset startedAt, DateTimeOffset finishedAt);
}

/// <summary>
/// Reads WinGet's own log files. They are WinGet internals, so a missing or unmatched record is normal, and only a file
/// that can belong to this operation alone is used: the queue runs one operation at a time (009 design).
/// </summary>
public sealed partial class WinGetRecordReader(string folder, string caller) : IWinGetRecordReader
{
    internal const string NotFound = "(WinGet's record of this operation wasn't found.)";
    internal const string Unreadable = "(WinGet's record of this operation couldn't be read.)";
    internal const string Ambiguous = "(Several WinGet records matched this operation, so none is shown.)";

    public WinGetRecordReader()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages", "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe", "LocalState", "DiagOutputDir"),
            AppPackage.FamilyName ?? Path.GetFileName(Environment.ProcessPath) ?? "")
    {
    }

    public WinGetRecord Find(DateTimeOffset startedAt, DateTimeOffset finishedAt)
    {
        try
        {
            var directory = new DirectoryInfo(folder);
            if (!directory.Exists)
            {
                return new WinGetRecord(null, NotFound);
            }

            var matches = directory.GetFiles("WinGetCOM-*.log")
                .Where(f => f.CreationTimeUtc >= startedAt.UtcDateTime && f.CreationTimeUtc <= finishedAt.UtcDateTime)
                .Select(f => ReadShared(f.FullName))
                .Where(IsFromThisApp)
                .ToList();

            return matches.Count switch
            {
                0 => new WinGetRecord(null, NotFound),
                1 => new WinGetRecord(Clean(matches[0]), null),
                _ => new WinGetRecord(null, Ambiguous),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new WinGetRecord(null, Unreadable);
        }
    }

    /// <summary>Takes out what a shared log must not carry: URL credentials and queries, download headers (R2.10).</summary>
    internal static string Clean(string text) =>
        Headers().Replace(Urls().Replace(text, m => SafeUrl(m.Value)), "$1 (removed)");

    private static string SafeUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped)
            : "(address removed)";

    private bool IsFromThisApp(string text) =>
        Caller().Match(text) is { Success: true } match
        && string.Equals(match.Groups[1].Value.Trim(), caller, StringComparison.OrdinalIgnoreCase);

    // WinGet may still have the file open when the operation returns.
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\bCaller: ([^\r\n]+)")]
    private static partial Regex Caller();

    [GeneratedRegex(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex Urls();

    [GeneratedRegex(@"(Installer download headers:)[^\r\n]*")]
    private static partial Regex Headers();
}
