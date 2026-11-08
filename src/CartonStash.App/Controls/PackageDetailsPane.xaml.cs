using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Controls;

public sealed partial class PackageDetailsPane : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(PackageDetailsViewModel), typeof(PackageDetailsPane), new PropertyMetadata(null, OnViewModelChanged));

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pane = (PackageDetailsPane)d;
        if (e.OldValue is PackageDetailsViewModel old)
        {
            old.PropertyChanged -= pane.OnViewModelPropertyChanged;
        }
        if (e.NewValue is PackageDetailsViewModel now)
        {
            now.PropertyChanged += pane.OnViewModelPropertyChanged;
        }
    }

    /// <summary>
    /// 012 (Q5): tells screen readers the status line changed, from "Loading notes from GitHub…" to the outcome.
    /// LiveSetting alone raised no LiveRegionChanged event (T9), so it is raised here once the binding has updated.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PackageDetailsViewModel.GitHubNotesStatus))
        {
            return;
        }
        DispatcherQueue.TryEnqueue(() =>
        {
            // Several view model changes can land on the same text (the message, then loading ending): announce it once.
            var status = ViewModel?.GitHubNotesStatus;
            if (status == _announced)
            {
                return;
            }
            _announced = status;
            if (status is not null)
            {
                (FrameworkElementAutomationPeer.FromElement(GitHubStatusText) ?? FrameworkElementAutomationPeer.CreatePeerForElement(GitHubStatusText))
                    ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        });
    }

    /// <summary>The status text last announced, so the same line isn't read twice.</summary>
    private string? _announced;

    public PackageDetailsPane()
    {
        InitializeComponent();
    }

    public PackageDetailsViewModel? ViewModel
    {
        get => (PackageDetailsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // 014 R1.1: SelectedItem is an object, which x:Bind can't bind two-way to the view model's VersionItem.
    private void VersionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.SelectedVersion = VersionList.SelectedItem as VersionItem;
        }
    }

    // 005 R1.3: default browser, no embedded web content. The view model only exposes http(s) URIs.
    private async void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Uri uri })
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }
}
