namespace CartonStash.Core.Services;

/// <summary>
/// What a restart for App Installer's update hands to the new copy (004 R6.3, R6.4):
/// <c>--finish-winget-update &lt;app pid&gt; &lt;server pids&gt; --pending &lt;version&gt; --page &lt;key&gt;</c>.
/// </summary>
/// <param name="AppProcessId">The old copy, which the new one waits for before it takes the "main" instance key (010 R2.2).</param>
/// <param name="ServerProcessIds">WinGet's servers when the old copy restarted: the new copy waits for them so it doesn't connect to the old App Installer.</param>
/// <param name="PendingVersion">The App Installer version still in use, if known.</param>
/// <param name="Page">The navigation key to open again ("updates", "settings"…).</param>
public sealed record RestartArguments(int AppProcessId, IReadOnlyList<int> ServerProcessIds, string? PendingVersion, string? Page)
{
    public const string Switch = "--finish-winget-update";
    private const string None = "-";

    public string Format() =>
        $"{Switch} {AppProcessId} {(ServerProcessIds.Count > 0 ? string.Join(",", ServerProcessIds) : None)} " +
        $"--pending {PendingVersion ?? None} --page {Page ?? None}";

    /// <summary>Null unless the arguments start a restart, so a normal start never waits.</summary>
    public static RestartArguments? Parse(IReadOnlyList<string> args)
    {
        var at = IndexOf(args, Switch);
        if (at < 0 || at + 2 >= args.Count || !int.TryParse(args[at + 1], out var app))
        {
            return null;
        }

        var servers = new List<int>();
        if (args[at + 2] != None)
        {
            foreach (var part in args[at + 2].Split(','))
            {
                if (!int.TryParse(part, out var id))
                {
                    return null;
                }
                servers.Add(id);
            }
        }
        return new RestartArguments(app, servers, Value(args, "--pending"), Value(args, "--page"));
    }

    private static string? Value(IReadOnlyList<string> args, string name)
    {
        var at = IndexOf(args, name);
        return at >= 0 && at + 1 < args.Count && args[at + 1] != None ? args[at + 1] : null;
    }

    private static int IndexOf(IReadOnlyList<string> args, string value)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == value)
            {
                return i;
            }
        }
        return -1;
    }
}
