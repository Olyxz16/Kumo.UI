using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace Kumo.Avalonia.Tests;

/// <summary>
/// Repro: drag the TimePicker hour loop and release; the release must start
/// the clamp (animation). No idle/velocity clamping, no clamping while held.
/// </summary>
public class SnapReleaseProbe
{
    [AvaloniaFact]
    public void Drag_and_release_starts_clamp()
    {
        var timePicker = new TimePicker();
        var window = new Window { Width = 360, Height = 320 };
        try
        {
            window.Content = timePicker;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var tp = timePicker.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_FlyoutButton");
            var pt = tp.TranslatePoint(new Point(tp.Bounds.Width / 2, tp.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pt, MouseButton.Left);
            window.MouseUp(pt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var presenter = window.GetVisualDescendants().OfType<TimePickerPresenter>().First();
            var host = presenter.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PART_HourHost");
            var scroll = host.GetVisualDescendants().OfType<ScrollViewer>().First();
            var panelStart = scroll.TranslatePoint(new Point(scroll.Bounds.Width / 2, scroll.Bounds.Height / 2), window)!.Value;

            // drag the strip downward ~55px then release
            window.MouseDown(panelStart, MouseButton.Left);
            for (var y = panelStart.Y; y <= panelStart.Y + 55; y += 11)
            {
                window.MouseMove(new Point(panelStart.X, y));
                Dispatcher.UIThread.RunJobs();
            }
            var itemH = KumoThemeSupport.DateTimeLoopSnap.HourItemHeight(scroll);

            // the mouse drag itself must scroll the strip (click-and-drag),
            // and while held nothing may clamp it
            Assert.True(Math.Abs(scroll.Offset.Y) > 0.01,
                $"mouse drag did not move the strip: offset={scroll.Offset.Y}");
            Assert.True(Math.Abs(scroll.Offset.Y % itemH) > 0.01 || KumoThemeSupport.DateTimeLoopSnap.AnimPropertyProbe(scroll) is null,
                $"strip clamped while grabbed: offset={scroll.Offset.Y}");

            window.MouseUp(new Point(panelStart.X, panelStart.Y + 55), MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var anim = KumoThemeSupport.DateTimeLoopSnap.AnimPropertyProbe(scroll);
            Assert.True(anim is not null || Math.Abs(scroll.Offset.Y % itemH) < 0.01,
                $"release did not start clamp: anim={anim is not null} offset={scroll.Offset.Y} itemH={itemH}");
        }
        finally
        {
            window.Close();
        }
    }
}
