using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

/// <summary>Settings page (spec 006).</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettings _settings;
    private readonly IPackageService _packages;
    private readonly IDialogService _dialogs;
    private readonly IClipboardService _clipboard;

    public SettingsViewModel(ISettings settings, IPackageService packages, IDialogService dialogs, IClipboardService clipboard)
    {
        _settings = settings;
        _packages = packages;
        _dialogs = dialogs;
        _clipboard = clipboard;
        ThemeIndex = (int)settings.Current.Theme;
        ScopeIndex = (int)settings.Current.InstallScope;
        StartPageIndex = (int)settings.Current.StartPage;
    }

    /// <summary>ComboBox index of <see cref="AppTheme"/>: System, Light, Dark (R1.1).</summary>
    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    /// <summary>ComboBox index of <see cref="InstallScope"/>: Any, User, System (R2.1).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowScopeWarning))]
    public partial int ScopeIndex { get; set; }

    /// <summary>ComboBox index of <see cref="StartPage"/>: Search, Installed, Updates (R5.1). Applies at next start (R5.3).</summary>
    [ObservableProperty]
    public partial int StartPageIndex { get; set; }

    /// <summary>"All users" may show UAC prompts (R2.2).</summary>
    public bool ShowScopeWarning => ScopeIndex == (int)InstallScope.System;

    public ObservableCollection<SourceInfo> Sources { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string? SourcesStatus { get; private set; }

    [ObservableProperty]
    public partial string? SourcesError { get; private set; }

    public string AppVersion { get; } = ReadAppVersion();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText))]
    public partial string WinGetVersion { get; private set; } = "…";

    public string VersionText => $"{AppInfo.Name} {AppVersion} · WinGet {WinGetVersion}";

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            WinGetVersion = await Task.Run(_packages.GetWinGetVersion);
        }
        catch (Exception ex)
        {
            WinGetVersion = $"unknown ({ex.Message})";
        }
        await ReloadSourcesAsync();
    }

    /// <summary>Refreshes each source in turn, then the next search uses fresh data (R3.2).</summary>
    [RelayCommand]
    private async Task RefreshSourcesAsync()
    {
        IsBusy = true;
        SourcesError = null;
        var names = Sources.Select(s => s.Name).ToList();
        var errors = new List<string>();
        try
        {
            for (var i = 0; i < names.Count; i++)
            {
                SourcesStatus = $"Refreshing {names[i]} ({i + 1} of {names.Count})…";
                string? error;
                try
                {
                    error = await _packages.RefreshSourceAsync(names[i]);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                if (error is not null)
                {
                    errors.Add($"{names[i]}: {error}"); // others continue
                }
            }
            _packages.InvalidateCache();
            SourcesStatus = errors.Count == 0 ? "Sources refreshed." : null;
            SourcesError = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        }
        finally
        {
            IsBusy = false;
        }
        await ReloadSourcesAsync();
    }

    /// <summary>Shows a source's agreements; accepting lets the app use it this session (R3.3).</summary>
    [RelayCommand]
    private async Task ReviewAgreementsAsync(SourceInfo source)
    {
        if (await _dialogs.ShowAgreementsAsync($"{source.Name}: source agreement", source.Agreements))
        {
            _packages.AcceptSourceAgreements(source.Name);
            SourcesStatus = $"Agreements for {source.Name} accepted.";
        }
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        _clipboard.SetText(BuildDiagnosticText());
        SourcesStatus = null;
    }

    /// <summary>Plain text for bug reports: versions, OS build, sources (R4.2).</summary>
    public string BuildDiagnosticText()
    {
        var text = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"{AppInfo.Name}: {AppVersion}")
            .AppendLine(CultureInfo.InvariantCulture, $"WinGet: {WinGetVersion}")
            .AppendLine(CultureInfo.InvariantCulture, $"OS: {Environment.OSVersion.VersionString} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})")
            .AppendLine(CultureInfo.InvariantCulture, $"Install scope: {_settings.Current.InstallScope}")
            .AppendLine("Sources:");
        foreach (var s in Sources)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"  {s.Name} | {s.Type} | {s.Argument} | {s.TrustText}{(s.IsExplicit ? " | explicit" : "")} | {s.LastUpdatedText}");
        }
        return text.ToString();
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (Enum.IsDefined((AppTheme)value))
        {
            _settings.Update(s => s with { Theme = (AppTheme)value });
        }
    }

    partial void OnStartPageIndexChanged(int value)
    {
        if (Enum.IsDefined((StartPage)value))
        {
            _settings.Update(s => s with { StartPage = (StartPage)value });
        }
    }

    partial void OnScopeIndexChanged(int value)
    {
        if (Enum.IsDefined((InstallScope)value))
        {
            _settings.Update(s => s with { InstallScope = (InstallScope)value });
        }
    }

    private async Task ReloadSourcesAsync()
    {
        try
        {
            var sources = await _packages.GetSourcesAsync();
            Sources.Clear();
            foreach (var source in sources)
            {
                Sources.Add(source);
            }
        }
        catch (Exception ex)
        {
            SourcesError = $"Couldn't read the sources: {ex.Message}";
        }
    }

    private static string ReadAppVersion()
    {
        var version = typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = version.IndexOf('+'); // drop the commit hash the SDK appends
        return plus > 0 ? version[..plus] : version;
    }
}
