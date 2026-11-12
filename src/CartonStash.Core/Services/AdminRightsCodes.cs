namespace CartonStash.Core.Services;

/// <summary>
/// 017 R1.2: results that mean the installer lacked administrator rights. Measured cases only; nothing else is guessed
/// (P1). Add a code here with the case that showed it.
/// </summary>
public static class AdminRightsCodes
{
    private static readonly HashSet<uint> InstallerCodes =
    [
        0x88A30033, // Microsoft 365 Copilot's installer updating the copy installed for all users (VM, 2026-11-12)
        5,          // ERROR_ACCESS_DENIED
        0x80070005, // E_ACCESSDENIED
        1925,       // Windows Installer: "You do not have sufficient privileges to complete this installation"
    ];

    /// <summary>
    /// The installer's exit code only. WinGet's own "access denied" with no exit code happens before any installer runs
    /// and keeps 013 R1.1's message.
    /// </summary>
    public static bool Lacks(uint installerCode, int extendedError) => installerCode != 0 && InstallerCodes.Contains(installerCode);
}
