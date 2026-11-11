using System.Runtime.InteropServices;
using System.Text;
using CartonStash.Core.Models;
using Microsoft.Management.Deployment;

namespace CartonStash.Core.Services;

/// <summary>
/// Windows Installer's own record of how an MSI product is installed (014 R3.8). WinGet's <c>InstalledScope</c> can be
/// wrong: T18 measured Pandoc's per-user MSI reported as installed for all users, because its uninstall entry is under
/// HKLM either way. Windows Installer knows the product's context.
/// </summary>
public interface IInstallContext
{
    /// <summary><see cref="InstallScope.System"/>, <see cref="InstallScope.User"/>, or null when Windows Installer doesn't have the product.</summary>
    InstallScope? ContextOf(string productCode);
}

/// <summary><see cref="IInstallContext"/> on <c>msi.dll</c>.</summary>
public sealed class MsiInstallContext : IInstallContext
{
    // MSIINSTALLCONTEXT values.
    internal const int UserManaged = 1;
    internal const int UserUnmanaged = 2;
    internal const int Machine = 4;

    private const uint Success = 0;
    private const uint MoreData = 234;

    public InstallScope? ContextOf(string productCode) => FirstContext(productCode, Has);

    /// <summary>
    /// The first context Windows Installer has the product in, machine first (T18). A product code that isn't a GUID
    /// in braces isn't an MSI product: nothing is asked.
    /// </summary>
    internal static InstallScope? FirstContext(string productCode, Func<string, int, bool> has)
    {
        if (!IsProductCode(productCode))
        {
            return null;
        }
        try
        {
            return has(productCode, Machine) ? InstallScope.System
                : has(productCode, UserUnmanaged) || has(productCode, UserManaged) ? InstallScope.User
                : null;
        }
        catch (Exception)
        {
            return null; // a failed read is never an error of its own (R3.8: the next source is asked)
        }
    }

    internal static bool IsProductCode(string? code) =>
        code is { Length: 38 } && code[0] == '{' && code[^1] == '}' && Guid.TryParse(code, out _);

    // Any property answers "is the product in this context"; InstallLocation is short and always readable.
    private static bool Has(string productCode, int context)
    {
        var size = 260;
        var value = new StringBuilder(size);
        var result = MsiGetProductInfoEx(productCode, null, context, "InstallLocation", value, ref size);
        return result is Success or MoreData;
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiGetProductInfoEx(string product, string? userSid, int context, string property, StringBuilder value, ref int size);
}

/// <summary>
/// How an installed version is installed (014 R3.8): Windows Installer's context for an MSI product, else WinGet's
/// <c>InstalledScope</c>. Used by the installer when going back and by the package read behind Q7's notice.
/// </summary>
internal static class InstalledScopes
{
    /// <summary>Both sources, in order; null when neither knows.</summary>
    internal static InstallScope? Read(PackageVersionInfo? installed, IInstallContext context) =>
        MsiContextOf(installed, context) ?? FromWinGetScope(WinGetScopeOf(installed));

    /// <summary>
    /// WinGet's <c>InstalledScope</c> metadata: "User", or "System" (what WinGet 1.29 returns, T18's log; "Machine" is
    /// accepted too, the manifest's word). Anything else (Unknown, empty) is unknown.
    /// </summary>
    internal static InstallScope? FromWinGetScope(string? value) =>
        string.Equals(value, "User", StringComparison.OrdinalIgnoreCase) ? InstallScope.User
        : string.Equals(value, "System", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Machine", StringComparison.OrdinalIgnoreCase)
            ? InstallScope.System
        : null;

    /// <summary>Windows Installer's context for the first of the version's product codes it has (W2: by index).</summary>
    internal static InstallScope? MsiContextOf(PackageVersionInfo? installed, IInstallContext context)
    {
        try
        {
            var codes = installed?.ProductCodes;
            if (codes is null)
            {
                return null;
            }
            for (var i = 0; i < codes.Count; i++)
            {
                if (context.ContextOf(codes[i]) is { } found)
                {
                    return found;
                }
            }
            return null;
        }
        catch (Exception)
        {
            return null; // the next source is asked (R3.8)
        }
    }

    internal static string? WinGetScopeOf(PackageVersionInfo? installed)
    {
        try
        {
            return installed?.GetMetadata(PackageVersionMetadataField.InstalledScope);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
