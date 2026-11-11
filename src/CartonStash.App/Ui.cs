using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CartonStash.Core.ViewModels;

namespace CartonStash.App;

/// <summary>
/// Small x:Bind function helpers (x:Bind function bindings don't convert types on their own). Don't pass one a value
/// that can become null: the generated code skips the call, so the target keeps its old value. Bind a plain bool instead.
/// </summary>
public static class Ui
{
    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static bool Not(bool value) => !value;

    /// <summary>Visible when both are true (015 R1.1: Install shows and the scopes are offered).</summary>
    public static Visibility VisibleIfBoth(bool first, bool second) => first && second ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visible when the first is true and the second false.</summary>
    public static Visibility VisibleIfNot(bool first, bool second) => first && !second ? Visibility.Visible : Visibility.Collapsed;

    public static string Format(string format, string? value) => string.Format(format, value);

    public static string Choose(bool condition, string whenTrue, string whenFalse) => condition ? whenTrue : whenFalse;

    /// <summary>Status chip tooltip and accessible name (002 R8.4): "Newer: 31 programs. Newer than the version the source offers".</summary>
    public static string Chip(string label, int count, string meaning) =>
        $"{label}: {count} {(count == 1 ? "program" : "programs")}. {meaning}";

    /// <summary>Chips with no rows stay visible (they are the legend) but dimmed (002 R8).</summary>
    public static double DimIfZero(int count) => count == 0 ? 0.5 : 1.0;

    public static InfoBarSeverity Severity(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Success => InfoBarSeverity.Success,
        NotificationSeverity.Warning => InfoBarSeverity.Warning,
        NotificationSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational,
    };
}
