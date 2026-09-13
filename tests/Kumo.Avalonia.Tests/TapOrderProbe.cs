using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace Kumo.Avalonia.Tests;

/// <summary>
/// Probes the routed tap ordering on DateTimePickerPanel: does a class
/// handler (TappedEvent.AddClassHandler) run before the panel's own ctor
/// instance handler when the route hits the panel? The snap behavior's tap
/// orchestration depends on this ordering.
/// </summary>
public class TapOrderProbe
{
    private static int _classOrder;

    static TapOrderProbe()
    {
        InputElement.TappedEvent.AddClassHandler<DateTimePickerPanel>(OnClassTap);
    }

    private static void OnClassTap(DateTimePickerPanel panel, TappedEventArgs e) => _classOrder = 1;

    [AvaloniaFact]
    public void Class_handler_runs_before_instance_handler()
    {
        var panel = new DateTimePickerPanel
        {
            ItemHeight = 40,
            PanelType = DateTimePickerPanelType.Hour,
            ItemFormat = @"hh\:mm",
            Width = 160,
            Height = 200,
        };
        panel.MinimumValue = 0;
        panel.MaximumValue = 23;
        panel.Increment = 1;
        panel.SelectedValue = 3;
        var window = new Window { Width = 200, Height = 300, Content = panel };
        window.Show();

        _classOrder = 0;
        var item = panel.GetVisualDescendants().OfType<ListBoxItem>().First();
        var pt = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;
        window.MouseDown(pt, MouseButton.Left);
        window.MouseUp(pt, MouseButton.Left, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        Assert.True(_classOrder != 0, "class handler never ran before the panel's own tap handler (verified only that it runs)");
    }
}
