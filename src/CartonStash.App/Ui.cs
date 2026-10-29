using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CartonStash.Core.ViewModels;

namespace CartonStash.App;

/// <summary>Small x:Bind function helpers (x:Bind function bindings don't convert types on their own).</summary>
public static class Ui
{
    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIf(object? value) =>
        value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;

    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    public static bool Not(bool value) => !value;

    public static string Format(string format, string? value) => string.Format(format, value);

    public static string Choose(bool condition, string whenTrue, string whenFalse) => condition ? whenTrue : whenFalse;

    public static InfoBarSeverity Severity(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Success => InfoBarSeverity.Success,
        NotificationSeverity.Warning => InfoBarSeverity.Warning,
        NotificationSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational,
    };
}
