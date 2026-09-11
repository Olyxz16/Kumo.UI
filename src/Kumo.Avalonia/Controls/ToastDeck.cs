using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace KumoThemeSupport.Controls;

/// <summary>
/// Toast deck panel: stacks cards with the newest at the visual anchor edge
/// and tucks each older card behind the next-newer one, leaving a fixed strip
/// visible. Positioning is measure-based so variable card heights stay sane
/// (no negative-margin hacks).
/// </summary>
public class ToastDeck : Panel
{
    public static readonly StyledProperty<bool> ReverseOrderProperty =
        AvaloniaProperty.Register<ToastDeck, bool>(nameof(ReverseOrder));

    public static readonly StyledProperty<double> PeekProperty =
        AvaloniaProperty.Register<ToastDeck, double>(nameof(Peek), 34d);

    public static readonly StyledProperty<double> FadeStepProperty =
        AvaloniaProperty.Register<ToastDeck, double>(nameof(FadeStep), 0.08d);

    public static readonly StyledProperty<double> MinOpacityProperty =
        AvaloniaProperty.Register<ToastDeck, double>(nameof(MinOpacity), 0.68d);

    public bool ReverseOrder
    {
        get => GetValue(ReverseOrderProperty);
        set => SetValue(ReverseOrderProperty, value);
    }

    /// <summary>Visible strip height of each older card, in DIPs.</summary>
    public double Peek
    {
        get => GetValue(PeekProperty);
        set => SetValue(PeekProperty, value);
    }

    /// <summary>Opacity removed per card of distance from the newest.</summary>
    public double FadeStep
    {
        get => GetValue(FadeStepProperty);
        set => SetValue(FadeStepProperty, value);
    }

    public double MinOpacity
    {
        get => GetValue(MinOpacityProperty);
        set => SetValue(MinOpacityProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        if (ReverseOrder && Children.Count > 1 && Peek < height)
        {
            height += Peek * (Children.Count - 1);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;

        if (!ReverseOrder)
        {
            // Top-anchored positions: plain downward stack, newest on top.
            double y = 0;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                child.Arrange(new Rect(0, y, child.DesiredSize.Width, child.DesiredSize.Height));
                SetDeckOpacity(child, 0);
                y += child.DesiredSize.Height;
            }

            return finalSize;
        }

        // Bottom-anchored deck: newest (last child) sits flush against the
        // anchor and each older card is laid out above it, overlapped so only
        // its top strip stays visible.
        var bottom = finalSize.Height;
        for (int i = children.Count - 1; i >= 0 && bottom > 0; i--)
        {
            var child = children[i];
            var size = child.DesiredSize;
            var top = bottom - Math.Min(size.Height, bottom);
            var x = (size.Width < finalSize.Width) switch
            {
                true => child.HorizontalAlignment switch
                {
                    HorizontalAlignment.Right => finalSize.Width - size.Width,
                    HorizontalAlignment.Center => (finalSize.Width - size.Width) / 2,
                    _ => 0,
                },
                _ => 0,
            };
            child.Arrange(new Rect(x, top, size.Width, size.Height));

            SetDeckOpacity(child, children.Count - 1 - i);

            // The next (older) card's bottom edge only peeks Peek px below this
            // card's top edge, so this card covers everything else.
            bottom = top + Peek;
        }

        return finalSize;
    }

    private void SetDeckOpacity(Control child, int depth)
    {
        child.SetCurrentValue(OpacityProperty, ReverseOrder
            ? Math.Max(MinOpacity, 1 - FadeStep * depth)
            : 1);
    }
}
