using System.Reflection;

namespace CartonStash.Core;

/// <summary>Product identity shown to users (spec 007: the name must not contain "WinGet").</summary>
public static class AppInfo
{
    public const string Name = "CartonStash";

    /// <summary>The version being run, e.g. "1.1.2", without the commit hash the SDK appends.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Sent to GitHub, which refuses requests without one (spec 012 R2.3). No contact details.</summary>
    public static string UserAgent => $"{Name}/{Version}";

    private static string ReadVersion()
    {
        var version = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }

    /// <summary>Folder name under %LOCALAPPDATA%.</summary>
    public const string DataFolder = Name;

    /// <summary>Settings folder used before the rename; read once if the new one has no settings yet.</summary>
    internal const string LegacyDataFolder = "WinGetFrontend";
}
