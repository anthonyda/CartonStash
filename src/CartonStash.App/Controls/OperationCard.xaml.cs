using System.ComponentModel;
using CartonStash.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CartonStash.App.Controls;

/// <summary>
/// One operation in the Activity pane (009 R1). The state's icon and colors are set in code from
/// <see cref="PackageOperation.State"/>, so recycled list items always show their own state.
/// </summary>
public sealed partial class OperationCard : UserControl
{
    public static readonly DependencyProperty OperationProperty =
        DependencyProperty.Register(nameof(Operation), typeof(PackageOperation), typeof(OperationCard), new PropertyMetadata(null, OnOperationChanged));

    public OperationCard()
    {
        InitializeComponent();
    }

    public PackageOperation? Operation
    {
        get => (PackageOperation?)GetValue(OperationProperty);
        set => SetValue(OperationProperty, value);
    }

    private static void OnOperationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (OperationCard)d;
        if (e.OldValue is PackageOperation old)
        {
            old.PropertyChanged -= card.OnOperationPropertyChanged;
        }
        if (e.NewValue is PackageOperation operation)
        {
            operation.PropertyChanged += card.OnOperationPropertyChanged;
            card.Icon.Package = operation.Package;
        }
        card.ShowState();
    }

    private void OnOperationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PackageOperation.State) or nameof(PackageOperation.StatusText) && ReferenceEquals(sender, Operation))
        {
            ShowState();
        }
    }

    private void ShowState()
    {
        if (Operation is not { } operation)
        {
            return;
        }

        var (glyph, iconStyle) = operation.State switch
        {
            OperationState.Queued => ("", "NeutralIcon"), // clock
            OperationState.Running => ("", "NeutralIcon"), // sync
            OperationState.Succeeded => ("", "SuccessIcon"), // check
            OperationState.RestartRequired => ("", "CautionIcon"), // warning
            OperationState.Cancelled => ("", "NeutralIcon"), // cross
            _ => ("", "CriticalIcon"), // error badge
        };
        StateIcon.Glyph = glyph;
        StateIcon.Style = (Style)Resources[iconStyle];
        Card.Style = (Style)Resources[operation.State == OperationState.Failed ? "FailedCard" : "NormalCard"];
        AutomationProperties.SetName(this, $"{operation.Title}: {operation.StatusText}");
    }
}
