namespace CartonStash.Core.Services;

/// <summary>Restarts CartonStash on the same page (004 R6.3). Lives in the App layer: Core has no WinUI (A1).</summary>
public interface IAppRestarter
{
    /// <summary>
    /// Starts a new copy that finishes App Installer's update, and ends this one. Returns only if the new copy couldn't be
    /// started, with false: the app stays open.
    /// </summary>
    /// <param name="pendingVersion">The App Installer version still in use, so the new copy can tell whether it took over (R6.4).</param>
    bool Restart(string? pendingVersion);
}
