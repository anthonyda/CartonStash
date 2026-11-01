using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Keeps one failing source from stopping the rest (011 R1). WinGet reports only the first failure of a composite search and
/// drops every match, so a failed attempt tests each source alone, leaves out the ones that fail, and tries again.
/// </summary>
internal static class SourceIsolation
{
    /// <param name="attempt">The real call. Marks a failure that one source could cause with <see cref="WinGetException.MayBeOneSource"/>.</param>
    /// <param name="sourcesInUse">The sources <paramref name="attempt"/> uses now.</param>
    /// <param name="alreadyLeftOut">Sources left out earlier, reported too if the rest fail (R2.5).</param>
    /// <param name="probe">Tests one source alone. Returns WinGet's code, or null when it works.</param>
    /// <param name="describe">Turns a source and code into a failure.</param>
    /// <param name="leaveOut">Records failed sources so the next attempt skips them.</param>
    public static async Task<T> RunAsync<T>(
        Func<Task<T>> attempt,
        Func<IReadOnlyList<string>> sourcesInUse,
        Func<IReadOnlyList<SourceFailure>> alreadyLeftOut,
        Func<string, Task<int?>> probe,
        Func<string, int, SourceFailure> describe,
        Action<IReadOnlyList<SourceFailure>> leaveOut)
    {
        WinGetException error;
        try
        {
            return await attempt(); // R1.4: nothing more happens while every source works
        }
        catch (WinGetException ex) when (ex.MayBeOneSource)
        {
            error = ex;
        }

        var sources = sourcesInUse();
        if (sources.Count == 0)
        {
            throw error;
        }

        // With one source there's nothing to compare, so no need to ask it again.
        var failed = sources.Count == 1
            ? [describe(sources[0], error.HResult)]
            : await ProbeAsync(sources, probe, describe);

        if (failed.Count == sources.Count)
        {
            throw new WinGetException(SourceFailureMessages.Join([.. alreadyLeftOut(), .. failed]), error.HResult); // R2.5
        }
        if (failed.Count > 0)
        {
            leaveOut(failed);
        }
        return await attempt();
    }

    private static async Task<List<SourceFailure>> ProbeAsync(IReadOnlyList<string> sources, Func<string, Task<int?>> probe,
        Func<string, int, SourceFailure> describe)
    {
        var codes = await Task.WhenAll(sources.Select(probe));
        var failed = new List<SourceFailure>();
        for (var i = 0; i < sources.Count; i++)
        {
            if (codes[i] is { } code)
            {
                failed.Add(describe(sources[i], code));
            }
        }
        return failed;
    }
}
