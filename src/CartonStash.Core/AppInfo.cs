namespace CartonStash.Core;

/// <summary>Product identity shown to users (spec 007: the name must not contain "WinGet").</summary>
public static class AppInfo
{
    public const string Name = "CartonStash";

    /// <summary>Folder name under %LOCALAPPDATA%.</summary>
    public const string DataFolder = Name;

    /// <summary>Settings folder used before the rename; read once if the new one has no settings yet.</summary>
    internal const string LegacyDataFolder = "WinGetFrontend";
}
