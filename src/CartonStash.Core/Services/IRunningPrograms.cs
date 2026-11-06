namespace CartonStash.Core.Services;

/// <summary>
/// Finds the programs running from a folder, so a failed portable update or uninstall can say what holds its files
/// (003 R7.2). It only reads: it never closes or ends a program (R7.4).
/// </summary>
public interface IRunningPrograms
{
    /// <summary>
    /// The file names (for example <c>claude.exe</c>) of the running processes whose executable sits in
    /// <paramref name="folder"/> or below it, each name once. Empty when there are none.
    /// </summary>
    IReadOnlyList<string> FileNamesIn(string folder);
}
