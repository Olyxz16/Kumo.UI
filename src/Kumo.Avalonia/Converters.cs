using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace KumoThemeSupport.Converters;

/// <summary>Keeps only the specified corners of a CornerRadius; used by
/// the split-button templates so the primary/secondary halves carry the
/// correct rounding.</summary>
public sealed class CornerRadiusFilterConverter : IValueConverter
{
    public bool TopLeft { get; set; }
    public bool TopRight { get; set; }
    public bool BottomLeft { get; set; }
    public bool BottomRight { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CornerRadius radius)
        {
            return value;
        }

        return new CornerRadius(
            TopLeft ? radius.TopLeft : 0,
            TopRight ? radius.TopRight : 0,
            BottomRight ? radius.BottomRight : 0,
            BottomLeft ? radius.BottomLeft : 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Keeps only the specified edges of a Thickness; splits the host
/// border across split-button components.</summary>
public sealed class ThicknessFilterConverter : IValueConverter
{
    public bool Left { get; set; }
    public bool Top { get; set; }
    public bool Right { get; set; }
    public bool Bottom { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Thickness thickness)
        {
            return value;
        }

        return new Thickness(
            Left ? thickness.Left : 0,
            Top ? thickness.Top : 0,
            Right ? thickness.Right : 0,
            Bottom ? thickness.Bottom : 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
