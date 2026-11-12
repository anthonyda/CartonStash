using System.Text.Json;
using System.Text.Json.Serialization;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Check history in <c>check-history.json</c> (010 R3.11–R3.13). It stays on the PC, keeps 7 days, and never fails
/// a check: a file that can't be read counts as empty, and one that can't be written is skipped.
/// </summary>
public sealed class CheckHistoryStore(string folder, TimeProvider? clock = null)
{
    // BR-33: the app and a background check can write at the same moment; one writer at a time, so no record is lost.
    private const string WriterMutex = @"Local\CartonStash.CheckHistory";

    public static readonly TimeSpan Kept = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string FilePath => Path.Combine(folder, "check-history.json");

    /// <summary>The last 7 days, newest first. Removes older entries from the file (R3.12).</summary>
    public IReadOnlyList<CheckRecord> Read() => Exclusive(() =>
    {
        var all = ReadFile();
        var kept = Recent(all);
        if (kept.Count != all.Count)
        {
            Write(kept);
        }
        return kept;
    });

    /// <summary>
    /// Adds a record. Its reason is kept as the error gave it, file paths included (R3.13a, BR-09): the history stays
    /// on the PC, and <c>PRIVACY.md</c> says it can hold the user name and paths.
    /// </summary>
    public void Append(CheckRecord record) => Exclusive(() =>
    {
        Write(Recent([record, .. ReadFile()]));
        return true;
    });

    private static T Exclusive<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, WriterMutex);
        var owned = false;
        try
        {
            try
            {
                owned = mutex.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException)
            {
                owned = true; // the other writer ended without releasing: the file is still whole (temp then move)
            }
            return action(); // after 5 s, go on anyway: never block a check (R3.13)
        }
        finally
        {
            if (owned)
            {
                mutex.ReleaseMutex();
            }
        }
    }

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
        string? temp = null;
        try
        {
            Directory.CreateDirectory(folder);
            temp = $"{FilePath}.{Guid.NewGuid():N}.tmp"; // BR-33: never a name another writer uses
            File.WriteAllText(temp, JsonSerializer.Serialize(records, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
        }
    }

    private static void TryDelete(string? path)
    {
        try
        {
            if (path is not null)
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
