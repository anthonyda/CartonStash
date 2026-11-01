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
    private readonly SourceAgreementCheck _agreements;
    private readonly IClipboardService _clipboard;

    public SettingsViewModel(ISettings settings, IPackageService packages, SourceAgreementCheck agreements, IClipboardService clipboard)
    {
        _settings = settings;
        _packages = packages;
        _agreements = agreements;
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
    [NotifyPropertyChangedFor(nameof(HasSourcesStatus))]
    public partial string? SourcesStatus { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourcesError))]
    public partial string? SourcesError { get; private set; }

    // Plain bools for the view: x:Bind doesn't call a function again once its argument is null (011 T8).
    public bool HasSourcesStatus => SourcesStatus is not null;

    public bool HasSourcesError => SourcesError is not null;

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
        // A source that isn't accepted stays out of this too (R3.8).
        var names = Sources.Where(s => s.AgreementStatus is AgreementStatus.None or AgreementStatus.Accepted).Select(s => s.Name).ToList();
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

    /// <summary>Read and accept, or Withdraw once accepted (R3.3, R3.10, R3.11).</summary>
    [RelayCommand]
    private async Task ReviewAgreementsAsync(SourceInfo source)
    {
        if (source.AgreementStatus == AgreementStatus.Accepted)
        {
            await _agreements.WithdrawAsync(source);
            SourcesStatus = $"{source.Name} isn't in use.";
        }
        else
        {
            var answer = await _agreements.ReadAndAcceptAsync(source);
            SourcesStatus = answer switch
            {
                AgreementAnswer.Accepted => $"Agreements for {source.Name} accepted.",
                AgreementAnswer.Declined => $"{source.Name} isn't in use.",
                _ => SourcesStatus,
            };
        }
        await ReloadSourcesAsync();
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
                $"  {s.Name} | {s.Type} | {SafeSourceAddress(s.Argument)} | {s.TrustText}{(s.IsExplicit ? " | explicit" : "")} | {s.LastUpdatedText}");
        }
        return text.ToString();
    }

    /// <summary>A source's address fit for a public bug report: no credentials, tokens or personal paths (R4.3).</summary>
    internal static string SafeSourceAddress(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped)
            : "(local source)";

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
