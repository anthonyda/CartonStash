using System.Runtime.InteropServices;

namespace CartonStash.App.Services;

/// <summary>
/// Bringing the open window to the front after a notification click (010 R2.2, 018 R1.4). Windows gives the right to take
/// the foreground to the copy it starts for the click, not to the open window that copy hands over to; the copy passes it
/// on before handing over (<see cref="AllowFor"/>), and the open window then takes it (<see cref="Bring"/>).
/// </summary>
internal static partial class Foreground
{
    private const int SwRestore = 9;

    /// <summary>In the copy started for the click: lets the open copy take the foreground.</summary>
    public static void AllowFor(uint processId) => AllowSetForegroundWindow(processId);

    /// <summary>In the open copy: restores the window if minimized and brings it to the front.</summary>
    public static void Bring(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, SwRestore);
        }
        SetForegroundWindow(window);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr window, int command);
}
