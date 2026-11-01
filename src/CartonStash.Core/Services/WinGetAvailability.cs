using System.Runtime.InteropServices;

namespace CartonStash.Core.Services;

/// <summary>
/// Whether WinGet is on this PC (011 R3.1). Must run before anything activates WinGet: without it,
/// <c>new PackageManager()</c> fails fast inside Microsoft.Management.Deployment.dll, which no <c>catch</c> can stop.
/// </summary>
public static class WinGetAvailability
{
    private const string AppInstallerFamily = "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe";

    public static bool IsInstalled()
    {
        uint count = 0, bufferLength = 0;
        GetPackagesByPackageFamily(AppInstallerFamily, ref count, IntPtr.Zero, ref bufferLength, IntPtr.Zero);
        return count > 0; // the call only asks for sizes, so any package makes it return "buffer too small"
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackagesByPackageFamily(string packageFamilyName, ref uint count, IntPtr packageFullNames,
        ref uint bufferLength, IntPtr buffer);
}
