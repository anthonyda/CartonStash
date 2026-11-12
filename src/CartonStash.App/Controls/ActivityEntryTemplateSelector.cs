using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CartonStash.Core.Models;

namespace CartonStash.App.Controls;

/// <summary>Activity list entries (009 R4.4): a divider, the Failed divider, or an operation card.</summary>
public sealed partial class ActivityEntryTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Header { get; set; }

    public DataTemplate? FailedHeader { get; set; }

    public DataTemplate? Operation { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        ActivityHeader { IsFailed: true } => FailedHeader,
        ActivityHeader => Header,
        _ => Operation,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
