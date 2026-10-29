using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 002 amendment R6: one row per installed program.</summary>
public class InstalledJoinTests
{
    private const string OldWsl = @"ARP\Machine\X64\{F8474A47-8B5D-4466-ACE3-78EAB3BF21A8}";
    private const string NewWsl = @"MSIX\MicrosoftCorporationII.WindowsSubsystemForLinux_3.0.4.0_x64__8wekyb3d8bbwe";

    private static InstalledEntry Entry(string localId, string name, string version, params string[] codes) => new(localId, name, version, codes);

    private static MatchedPackage Package(string id, string? installedLocalId, string available, bool update = false, params string[] manifestCodes) =>
        new(id, id + " (source name)", installedLocalId, available, "winget", update, manifestCodes);

    // Exactly what the dev machine reports for WSL (T9).
    private static readonly InstalledEntry[] WslEntries =
    [
        Entry(OldWsl, "Windows Subsystem for Linux Update", "5.10.102.1", "{f8474a47-8b5d-4466-ace3-78eab3bf21a8}"),
        Entry(NewWsl, "Windows Subsystem for Linux", "3.0.4.0", "microsoftcorporationii.windowssubsystemforlinux_8wekyb3d8bbwe"),
    ];

    private static readonly MatchedPackage WslPackage =
        Package("Microsoft.WSL", OldWsl, "2.7.14", update: false, "{020F8AC8-A788-4E39-8E9B-8FECFA843632}", "MicrosoftCorporationII.WindowsSubsystemForLinux_8wekyb3d8bbwe");

    [Fact]
    public void Two_programs_sharing_an_id_get_two_rows_with_their_own_versions() // R6.1, R6.2
    {
        var rows = InstalledJoin.Join(WslEntries, [WslPackage]);

        Assert.Equal(2, rows.Count);
        var old = rows.Single(r => r.InstalledId == OldWsl);
        var current = rows.Single(r => r.InstalledId == NewWsl);
        Assert.Equal(("Windows Subsystem for Linux Update", "5.10.102.1"), (old.Name, old.InstalledVersion));
        Assert.Equal(("Windows Subsystem for Linux", "3.0.4.0"), (current.Name, current.InstalledVersion));
        Assert.All(rows, r => Assert.Equal("Microsoft.WSL", r.Id));
    }

    [Fact]
    public void WinGets_own_match_is_primary_the_manifest_match_is_not() // R6.3, R6.4
    {
        var rows = InstalledJoin.Join(WslEntries, [WslPackage with { UpdateAvailable = true }]);

        var old = rows.Single(r => r.InstalledId == OldWsl);
        var current = rows.Single(r => r.InstalledId == NewWsl);
        Assert.True(old.IsPrimaryMatch);
        Assert.True(old.HasUpdate);
        Assert.False(current.IsPrimaryMatch);
        Assert.False(current.HasUpdate); // never claims WinGet's verdict for another program
        Assert.True(current.IsManaged);
        Assert.Equal("winget", current.Source);
        Assert.Equal("2.7.14", current.AvailableVersion);
    }

    [Fact]
    public void Unmatched_program_is_unmanaged_with_its_local_id() // R6.1, 002 R3.1
    {
        var rows = InstalledJoin.Join([Entry(@"ARP\Machine\X64\Steam App 1450330", "Concordia", "Unknown", "steam app 1450330")], []);

        var row = Assert.Single(rows);
        Assert.False(row.IsManaged);
        Assert.Equal(@"ARP\Machine\X64\Steam App 1450330", row.Id);
        Assert.Equal(row.Id, row.Key);
        Assert.Null(row.AvailableVersion);
    }

    [Fact]
    public void Code_comparison_ignores_case_and_braces() // R6.3
    {
        var rows = InstalledJoin.Join(
            [Entry(@"ARP\Machine\X64\X", "X", "1.0", "{ABCDEF01-0000-0000-0000-000000000000}")],
            [Package("Contoso.X", installedLocalId: null, "2.0", false, "abcdef01-0000-0000-0000-000000000000")]);

        Assert.Equal("Contoso.X", Assert.Single(rows).Id);
    }

    [Fact]
    public void A_program_claimed_by_winget_is_not_also_manifest_matched_elsewhere() // R6.3
    {
        var git = Entry(@"ARP\Machine\X64\Git_is1", "Git", "2.56.0.3", "git_is1");
        var rows = InstalledJoin.Join([git],
        [
            Package("Git.Git", git.LocalId, "2.56.0.3"),
            Package("Other.GitFork", installedLocalId: null, "1.0", false, "git_is1"),
        ]);

        var row = Assert.Single(rows);
        Assert.Equal("Git.Git", row.Id);
        Assert.True(row.IsPrimaryMatch);
    }

    [Fact]
    public void Package_whose_installed_entry_is_missing_keeps_a_row() // fallback: nothing WinGet reports disappears
    {
        var rows = InstalledJoin.Join([], [Package("Contoso.Ghost", @"ARP\Machine\X64\Gone", "1.0") with { Name = "Ghost" }]);

        var row = Assert.Single(rows);
        Assert.Equal("Contoso.Ghost", row.Id);
        Assert.Equal("Ghost", row.Name);
        Assert.True(row.IsPrimaryMatch);
    }

    [Fact]
    public void Rows_for_the_same_package_have_different_keys() // R6.5 / T12
    {
        var rows = InstalledJoin.Join(WslEntries, [WslPackage]);

        Assert.Equal(2, rows.Select(r => r.Key).Distinct().Count());
    }
}
