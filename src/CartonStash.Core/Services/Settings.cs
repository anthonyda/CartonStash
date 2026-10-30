using System.Text.Json;
using System.Text.Json.Serialization;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>Where settings are kept (006). Spec 007 adds an ApplicationData-backed store for the packaged app.</summary>
public interface ISettingsStore
{
    /// <summary>Loads settings. <c>WasReset</c> is true when a corrupt file was replaced by defaults.</summary>
    (AppSettings Settings, bool WasReset) Load();

    void Save(AppSettings settings);
}

/// <summary>The current settings for the whole app (006 R1.2, R2.1).</summary>
public interface ISettings
{
    AppSettings Current { get; }

    /// <summary>True when the settings file was corrupt and defaults were used (one-time notice).</summary>
    bool WasReset { get; }

    event EventHandler? Changed;

    void Update(Func<AppSettings, AppSettings> change);
}

/// <summary>Settings as JSON, by default in <c>%LOCALAPPDATA%\CartonStash\settings.json</c>.</summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public JsonSettingsStore()
        : this(DefaultPath(AppPackage.LocalFolder, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)))
    {
    }

    /// <summary>
    /// Where settings live: the package's own app-data folder when running from the MSIX (007 R4.1), otherwise
    /// <c>%LOCALAPPDATA%\CartonStash</c> (006, portable zip). Earlier files are copied once, newest location first.
    /// </summary>
    internal static string DefaultPath(string? packageFolder, string localAppData)
    {
        var unpackaged = Path.Combine(localAppData, AppInfo.DataFolder, "settings.json");
        var beforeRename = Path.Combine(localAppData, AppInfo.LegacyDataFolder, "settings.json");

        if (packageFolder is null)
        {
            CopyOnce(unpackaged, beforeRename);
            return unpackaged;
        }

        var packaged = Path.Combine(packageFolder, "settings.json");
        CopyOnce(packaged, unpackaged, beforeRename);
        return packaged;
    }

    /// <summary>Copies the first existing earlier file to <paramref name="target"/> if it has no settings yet.</summary>
    private static void CopyOnce(string target, params string[] earlier)
    {
        if (File.Exists(target) || earlier.FirstOrDefault(File.Exists) is not { } source)
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
        catch (IOException)
        {
        }
    }

    public string FilePath => path;

    public (AppSettings Settings, bool WasReset) Load()
    {
        if (!File.Exists(path))
        {
            return (new AppSettings(), false);
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions)
                ?? throw new JsonException("Empty settings file.");
            if (!Enum.IsDefined(settings.Theme) || !Enum.IsDefined(settings.InstallScope) || !Enum.IsDefined(settings.StartPage))
            {
                throw new JsonException("Unknown setting value.");
            }
            return (settings, false);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            // Keep the broken file for diagnosis, then start over with defaults.
            try
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
            catch (IOException)
            {
            }
            return (new AppSettings(), true);
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }
}

/// <summary>Singleton holding the loaded settings and saving every change.</summary>
public sealed class SettingsService : ISettings
{
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store)
    {
        _store = store;
        (Current, WasReset) = store.Load();
    }

    public AppSettings Current { get; private set; }

    public bool WasReset { get; }

    public event EventHandler? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var updated = change(Current);
        if (updated == Current)
        {
            return;
        }
        Current = updated;
        try
        {
            _store.Save(updated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The setting still applies for this session; it just won't survive a restart.
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
