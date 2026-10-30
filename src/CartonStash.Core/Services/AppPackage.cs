using System.Runtime.InteropServices;
using Windows.Storage;

namespace CartonStash.Core.Services;

/// <summary>Whether the app runs from its MSIX (spec 007) and where that package keeps its data.</summary>
public static class AppPackage
{
    private const int AppModelErrorNoPackage = 15700;

    /// <summary>True when installed from the MSIX; false for the portable zip and developer builds.</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    /// <summary>The package's local app-data folder, or null when not packaged (007 R4.1).</summary>
    public static string? LocalFolder => IsPackaged ? ApplicationData.Current.LocalFolder.Path : null;

    private static bool DetectPackaged()
    {
        uint length = 0;
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) != AppModelErrorNoPackage;
    }

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
