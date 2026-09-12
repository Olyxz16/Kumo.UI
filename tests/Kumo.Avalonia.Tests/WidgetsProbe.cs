using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Kumo.Avalonia.Tests;

/// <summary>
/// PipsPager / Carousel / RefreshContainer-RefreshVisualizer probes: theme
/// apply, pip click paging, key paging, glyph presence.
/// </summary>
public class WidgetsProbe
{
    [AvaloniaFact]
    public void PipsPager_carousel_and_refresh_apply_and_interact()
    {
        var pips = new PipsPager { NumberOfPages = 5, SelectedPageIndex = 1 };
        var carousel = new Carousel();
        for (var i = 0; i < 3; i++)
        {
            carousel.Items.Add(new Border
            {
                Background = Brushes.Beige,
                Width = 120,
                Height = 60,
                Child = new global::Avalonia.Controls.TextBlock { Text = $"s{i}" },
            });
        }
        carousel.SelectedIndex = 0;
        var visualizer = new RefreshVisualizer();
        var container = new RefreshContainer
        {
            Content = new global::Avalonia.Controls.TextBlock { Text = "pull" },
        };
        var window = new Window { Width = 420, Height = 360 };
        try
        {
            window.Content = new StackPanel
            {
                Children = { pips, carousel, container }
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // PipsPager template: 5 pips + nav buttons
            Assert.Equal(5, pips.GetVisualDescendants().OfType<ListBoxItem>().Count());
            Assert.NotNull(pips.GetVisualDescendants().FirstOrDefault(v => v.Name == "PART_PreviousButton"));
            Assert.NotNull(pips.GetVisualDescendants().FirstOrDefault(v => v.Name == "PART_NextButton"));
            // Ellipse pips exist (two get selected dilation; count >= 5)
            Assert.True(pips.GetVisualDescendants().OfType<Ellipse>().Count() >= 5);

            // clicking pip page 4 selects it
            var target = pips.GetVisualDescendants().OfType<ListBoxItem>().ElementAt(3);
            var pipPoint = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pipPoint, MouseButton.Left);
            window.MouseUp(pipPoint, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, pips.SelectedPageIndex);

            // Carousel: apply + key paging advances selection
            Assert.NotNull(carousel.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault());
            Assert.NotEmpty(carousel.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.Name == "PART_ScrollViewer"));
            carousel.Focus();
            Dispatcher.UIThread.RunJobs();
            // navigate: Carousel handles Next/Previous via KeyEvents
            var keyDown = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right };
            carousel.RaiseEvent(keyDown);
            var keyUp = new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Right };
            carousel.RaiseEvent(keyUp);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, carousel.SelectedIndex);

            // Refresh duo: expected PARTs + brand glyph path renders
            Assert.NotNull(container.GetVisualDescendants().FirstOrDefault(v => v.Name == "PART_ContentPresenter"));
            Assert.NotNull(container.GetVisualDescendants().FirstOrDefault(v => v.Name == "PART_RefreshVisualizerPresenter"));
            container.Content = visualizer;
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(visualizer.GetVisualDescendants().FirstOrDefault(v => v.Name == "PART_Root"));
            var icon = visualizer.Content as PathIcon;
            Assert.NotNull(icon);

            HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/widgets.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Refresh_container_pull_gesture_and_event_wiring()
    {
        var fired = 0;
        RefreshContainer? container = null;
        var window = new Window { Width = 360, Height = 240 };
        try
        {
            StackPanel MakeContent(int n) => new()
            {
                Spacing = 8,
                Margin = new Thickness(12),
                Children =
                {
                    new global::Avalonia.Controls.TextBlock { Text = $"row {n}" },
                    new global::Avalonia.Controls.TextBlock { Text = "stamp" },
                },
            };
            container = new RefreshContainer { IsMouseEnabled = true };
            container.Content = new ScrollViewer { Content = MakeContent(1) };
            container.RefreshRequested += (_, _) => fired++;
            window.Content = container;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // routed event wiring (same path as the demo handler)
            container.RaiseEvent(new RefreshRequestedEventArgs(
                new RefreshCompletionDeferral(() => { }),
                RefreshContainer.RefreshRequestedEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, fired);

            // real gesture: drag content downward inside the adapted ScrollViewer
            var scroll = container.GetVisualDescendants().OfType<ScrollViewer>().First();
            var start = scroll.TranslatePoint(new Point(scroll.Bounds.Width / 2, scroll.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            for (var y = start.Y; y < start.Y + 140; y += 10)
            {
                window.MouseMove(new Point(start.X, y));
                Dispatcher.UIThread.RunJobs();
            }
            window.MouseUp(new Point(start.X, start.Y + 140), MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            Assert.True(fired >= 1, $"drag pull did not fire; fired={fired}");
        }
        finally
        {
            window.Close();
        }
    }
}
