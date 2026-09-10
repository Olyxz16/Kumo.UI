using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KumoThemeSupport.Controls;

/// <summary>
/// Kumo toolbar: a single surface with the toolbar ring/shadow that presents
/// its <see cref="Content"/> in a horizontal group. Extra trailing material
/// (labels, secondary commands) can be attached through
/// <see cref="Leading"/>, rendered at the leading edge, or <see cref="Trailing"/>.
/// </summary>
public class KumoToolbar : ContentControl
{
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<KumoToolbar, object?>(nameof(Leading));

    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<KumoToolbar, object?>(nameof(Trailing));

    public object? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }
}
