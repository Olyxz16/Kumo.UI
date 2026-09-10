using Avalonia;
using Avalonia.Controls.Primitives;

namespace KumoThemeSupport.Controls;

/// <summary>
/// Kumo empty state: icon slot over title over description, with a command
/// area matched to the upstream Empty component paddings.
/// Sizes via <see cref="Size"/> (sm, base, lg).
/// </summary>
public class KumoEmptyState : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<KumoEmptyState, string>(nameof(Title), defaultValue: "");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<KumoEmptyState, string>(nameof(Description), defaultValue: "");

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<KumoEmptyState, object?>(nameof(Icon));

    public static readonly StyledProperty<object?> CommandContentProperty =
        AvaloniaProperty.Register<KumoEmptyState, object?>(nameof(CommandContent));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public object? CommandContent
    {
        get => GetValue(CommandContentProperty);
        set => SetValue(CommandContentProperty, value);
    }
}
