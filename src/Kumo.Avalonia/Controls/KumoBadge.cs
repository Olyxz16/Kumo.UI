using Avalonia;
using Avalonia.Controls.Primitives;

namespace KumoThemeSupport.Controls;

/// <summary>
/// Kumo badge: rounded-full pill with the upstream variant palette.
/// Pick a variant through <see cref="Variant"/> (primary, secondary, info,
/// success, warning, error, outline, beta) or full color chips
/// (red, green, neutral, orange, purple, teal, blue).
/// </summary>
public class KumoBadge : TemplatedControl
{
    public static readonly StyledProperty<string> VariantProperty =
        AvaloniaProperty.Register<KumoBadge, string>(nameof(Variant), defaultValue: "secondary");

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<KumoBadge, string>(nameof(Text), defaultValue: "");

    public string Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
