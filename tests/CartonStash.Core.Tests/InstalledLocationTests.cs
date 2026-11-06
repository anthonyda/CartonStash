using Microsoft.Management.Deployment;
using Microsoft.Win32;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>
/// 003 amendment R7 (T9): WinGet's COM API gives a portable package's install folder, which R7.2 needs to find the
/// program that keeps it from updating. Read-only: nothing is installed or removed.
/// </summary>
[Trait("Category", "Integration")]
public class InstalledLocationTests
{
    [Fact]
    public async Task Portable_package_reports_an_existing_folder() // R7.2
    {
        // WinGet's own record of its portable installs, read independently of its COM API.
        var portables = PortableEntriesInRegistry();
        if (portables.Count == 0)
        {
            return; // no portable package on this PC: nothing to check (xunit 2 has no runtime skip)
        }

        var connection = new CatalogConnection(new FakeSettings());
        var checkedCount = 0;
        foreach (var (id, expectedFolder) in portables)
        {
            CatalogPackage found;
            try
            {
                found = await connection.FindByIdAsync(id, "winget", localFirst: true, CancellationToken.None);
            }
            catch (WinGetException)
            {
                continue; // gone from the winget source (Yuzu on the dev PC): CartonStash can't update it anyway
            }

            var folder = found.InstalledVersion.GetMetadata(PackageVersionMetadataField.InstalledLocation);

            Assert.True(Directory.Exists(folder), $"{id}: '{folder}' isn't a folder");
            Assert.Equal(Path.GetFullPath(expectedFolder), Path.GetFullPath(folder), ignoreCase: true);
            checkedCount++;
        }
        Assert.True(checkedCount > 0, "No portable package was found in the winget source.");
    }

    private static List<(string Id, string Folder)> PortableEntriesInRegistry()
    {
        var list = new List<(string, string)>();
        using var uninstall = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        foreach (var name in uninstall?.GetSubKeyNames() ?? [])
        {
            using var entry = uninstall!.OpenSubKey(name);
            if (entry?.GetValue("WinGetInstallerType") as string == "portable"
                && entry.GetValue("WinGetSourceIdentifier") is "Microsoft.Winget.Source_8wekyb3d8bbwe" // the winget source
                && entry.GetValue("WinGetPackageIdentifier") is string id
                && entry.GetValue("InstallLocation") is string folder && Directory.Exists(folder))
            {
                list.Add((id, folder));
            }
        }
        return list;
    }
}
