using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace KumoThemeSupport;

/// <summary>
/// Tags the month/year/decade tiles inside a CalendarItem with the
/// "kumo-tile" class so their theme can use a rounded-rectangle shape
/// instead of the circular day-cell pill. The tiles are code-populated
/// CalendarButtons, so styling can't distinguish them from day buttons. Runs
/// on template apply and re-checks on layout updates (views repopulate on
/// display-mode changes).
/// </summary>
public class CalendarTileTag
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<CalendarTileTag, CalendarItem, bool>("IsEnabled", false);

    public static void SetIsEnabled(CalendarItem item, bool value) => item.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(CalendarItem item) => item.GetValue(IsEnabledProperty);

    static CalendarTileTag()
    {
        IsEnabledProperty.Changed.AddClassHandler<CalendarItem>((item, _) =>
        {
            item.TemplateApplied += OnTemplateApplied;
            item.LayoutUpdated += OnLayoutUpdated;
        });
    }

    private static void OnTemplateApplied(object? sender, EventArgs e) => Tag((CalendarItem)sender!);

    private static void OnLayoutUpdated(object? sender, EventArgs e) => Tag((CalendarItem)sender!);

    private static void Tag(CalendarItem item)
    {
        var yearView = item.GetVisualDescendants().OfType<Grid>().FirstOrDefault(g => g.Name == "PART_YearView");

        foreach (var button in item.GetVisualDescendants().OfType<CalendarButton>())
        {
            var isTile = yearView != null && button.GetVisualAncestors().Contains(yearView);
            button.Classes.Set("kumo-tile", isTile);
        }
    }
}
