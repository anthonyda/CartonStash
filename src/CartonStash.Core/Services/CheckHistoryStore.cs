using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Check history in <c>check-history.json</c> (010 R3.11–R3.13). It stays on the PC, keeps 7 days, and never fails
/// a check: a file that can't be read counts as empty, and one that can't be written is skipped.
/// </summary>
public sealed partial class CheckHistoryStore(string folder, TimeProvider? clock = null)
{
    public static readonly TimeSpan Kept = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string FilePath => Path.Combine(folder, "check-history.json");

    /// <summary>The last 7 days, newest first. Removes older entries from the file (R3.12).</summary>
    public IReadOnlyList<CheckRecord> Read()
    {
        var all = ReadFile();
        var kept = Recent(all);
        if (kept.Count != all.Count)
        {
            Write(kept);
        }
        return kept;
    }

    public void Append(CheckRecord record)
    {
        var withoutPaths = record with { Reason = record.Reason is { } reason ? RemovePaths(reason) : null };
        Write(Recent([withoutPaths, .. ReadFile()]));
    }

    /// <summary>A path can hold the user name, so history never keeps one (R3.13).</summary>
    internal static string RemovePaths(string text) => PathPattern().Replace(text, "[path]");

    [GeneratedRegex("""(?:[A-Za-z]:\\|\\\\)[^\s"'<>|]*""")]
    private static partial Regex PathPattern();

    private List<CheckRecord> Recent(IEnumerable<CheckRecord> records)
    {
        var oldest = _clock.GetUtcNow() - Kept;
        return records.Where(r => r.Time >= oldest).OrderByDescending(r => r.Time).ToList();
    }

    private List<CheckRecord> ReadFile()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<List<CheckRecord>>(File.ReadAllText(FilePath), JsonOptions) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Write(List<CheckRecord> records)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(records, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
