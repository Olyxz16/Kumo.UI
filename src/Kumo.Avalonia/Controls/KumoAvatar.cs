using System;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace KumoThemeSupport.Controls;

/// <summary>
/// Kumo avatar: rounded circle with deterministic fallback color and
/// initials derived from <see cref="DisplayName"/>, or a custom image.
/// Sizes via <see cref="Size"/> (xs, sm, base, lg).
/// </summary>
public class KumoAvatar : TemplatedControl
{
    private static readonly string[] PaletteHues =
        ["info", "warning", "danger", "success", "purple", "teal", "blue"];

    public static readonly StyledProperty<string> DisplayNameProperty =
        AvaloniaProperty.Register<KumoAvatar, string>(nameof(DisplayName), defaultValue: "");

    public static readonly StyledProperty<string> SizeProperty =
        AvaloniaProperty.Register<KumoAvatar, string>(nameof(Size), defaultValue: "base");

    public static readonly StyledProperty<string> HueProperty =
        AvaloniaProperty.Register<KumoAvatar, string>(nameof(Hue), defaultValue: "info");

    public string DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public string Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public string Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<KumoAvatar, double>(nameof(StrokeWidth), defaultValue: 0);

    /// <summary>Separation ring width in px (0 = ring off).</summary>
    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public KumoAvatar()
    {
        this.GetObservable(DisplayNameProperty).Subscribe(new AnonymousObserver<string>(UpdateHue));
        UpdateHue(DisplayName);
    }

    private sealed class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        public AnonymousObserver(Action<T> onNext) => _onNext = onNext;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _onNext(value);
    }

    private void UpdateHue(string value)
    {
        SetCurrentValue(HueProperty, ComputeHue(value));
    }

    private static string ComputeHue(string name)
    {
        var hash = 0u;
        foreach (var ch in name ?? "")
        {
            hash = (hash * 31) + ch;
        }
        return PaletteHues[hash % PaletteHues.Length];
    }

    /// <summary>Initials (up to 2 characters) derived from the display name.</summary>
    public string Initials
    {
        get
        {
            var name = (DisplayName ?? "").Trim();
            if (name.Length == 0)
            {
                return "?";
            }
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
            }
            return (parts[0][0].ToString() + parts[^1][0]).ToUpperInvariant();
        }
    }
}
