using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.BadgeNotifications;
using Windows.Networking.Connectivity;
using CartonStash.Core.Services;

namespace CartonStash.App.Services;

/// <summary>One fixed tag, so a new notification replaces the last one (010 R2.3).</summary>
public sealed class WindowsNotifier : INotifier
{
    private const string Tag = "updates";

    public void ShowUpdates(string title, string body)
    {
        var notification = new AppNotificationBuilder()
            .AddArgument("page", "updates")
            .AddText(title)
            .AddText(body)
            .BuildNotification();
        notification.Tag = Tag;
        AppNotificationManager.Default.Show(notification);
    }
}

/// <summary>
/// The number on the taskbar icon (016). Windows keeps it after the app closes. Without package identity, or with badges
/// turned off, it does nothing (R3.1); Windows shows "99+" above 99 (R3.2).
/// </summary>
public sealed class WindowsTaskbarBadge : ITaskbarBadge
{
    public void Show(int count)
    {
        try
        {
            if (count > 0)
            {
                BadgeNotificationManager.Current.SetBadgeAsCount((uint)count);
            }
            else
            {
                BadgeNotificationManager.Current.ClearBadge();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar badge: {ex.Message}"); // R3.1: no error, no message
        }
    }
}

public sealed class WindowsNetworkState : INetworkState
{
    public bool IsMetered =>
        NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost().NetworkCostType is NetworkCostType.Fixed or NetworkCostType.Variable;
}

/// <summary>
/// Lets a background check see what the open app is doing (010 R1.5, R2.5). A state is on while its named mutex
/// exists, so it ends with the app even after a crash, and a crash can't block checks.
/// </summary>
public sealed class AppActivityMarker : IAppActivity
{
    private const string OperationsName = @"Local\CartonStash.OperationsRunning";
    private const string ForegroundName = @"Local\CartonStash.WindowInForeground";

    private Mutex? _operations;
    private Mutex? _foreground;

    public bool OperationsRunning => Exists(OperationsName);

    public bool WindowInForeground => Exists(ForegroundName);

    public void SetOperationsRunning(bool running) => Set(ref _operations, OperationsName, running);

    public void SetWindowInForeground(bool inForeground) => Set(ref _foreground, ForegroundName, inForeground);

    private static bool Exists(string name)
    {
        if (!Mutex.TryOpenExisting(name, out var mutex))
        {
            return false;
        }
        mutex.Dispose();
        return true;
    }

    private static void Set(ref Mutex? mutex, string name, bool on)
    {
        if (on)
        {
            mutex ??= new Mutex(false, name);
        }
        else
        {
            mutex?.Dispose();
            mutex = null;
        }
    }
}
