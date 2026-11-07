using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CartonStash.App.Controls;

/// <summary>
/// Shows its one child at full height, or clipped to <see cref="CollapsedHeight"/> while <see cref="IsCollapsed"/>,
/// and says whether the child is taller than that (spec 012 R1.6, R2.8). Only the view knows how tall wrapped text is.
/// </summary>
public sealed partial class CollapsingPanel : Panel
{
    private bool _isTall;

    /// <summary>About 10 lines of body text (20 px each).</summary>
    public double CollapsedHeight { get; set; } = 200;

    public bool IsCollapsed
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                InvalidateMeasure();
            }
        }
    } = true;

    /// <summary>True when the child is taller than <see cref="CollapsedHeight"/>, so "Show more" has something to show.</summary>
    public bool IsTall => _isTall;

    public event EventHandler? IsTallChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return default;
        }
        var child = Children[0];
        child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var full = child.DesiredSize.Height;

        var tall = full > CollapsedHeight + 1;
        if (tall != _isTall)
        {
            _isTall = tall;
            // Not during layout: the handler changes the visibility of a sibling.
            DispatcherQueue.TryEnqueue(() => IsTallChanged?.Invoke(this, EventArgs.Empty));
        }
        return new Size(child.DesiredSize.Width, IsCollapsed ? Math.Min(full, CollapsedHeight) : full);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }
        var child = Children[0];
        child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, child.DesiredSize.Height)));
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, finalSize.Width, finalSize.Height) };
        return finalSize;
    }
}
