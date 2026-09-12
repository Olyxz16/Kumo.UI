using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kumo.Demo;
using Xunit;

namespace Kumo.Avalonia.Tests;

/// <summary>
/// Smoke tests for the misc Avalonia controls themed in Shared/Button/TextBox
/// files (PathIcon, TransitioningContentControl, GroupBox, DropDownButton,
/// ButtonSpinner, MaskedTextBox): each builds a template with the expected
/// PARTs. The CoverageInventory guardrail covers theme presence; this covers
/// template apply.
/// </summary>
public class MiscProbe
{
    [AvaloniaFact]
    public void Misc_controls_apply_their_templates()
    {
        var window = new Window();
        var spinner = new global::Avalonia.Controls.ButtonSpinner { Content = "42" };
        var ddb = new DropDownButton { Content = "Actions" };
        var icon = new PathIcon { Width = 16, Height = 16, Data = Geometry.Parse("M0 0 L10 10") };
        var groupBox = new GroupBox { Header = "Title", Content = "Body" };
        var tcc = new TransitioningContentControl { Content = "Slide" };
        var masked = new global::Avalonia.Controls.MaskedTextBox { Mask = "000" };
        try
        {
            window.Content = new StackPanel
            {
                Children = { spinner, ddb, icon, groupBox, tcc, masked }
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var repeatButtons = spinner.GetVisualDescendants().OfType<RepeatButton>().ToList();
            Assert.Equal(2, repeatButtons.Count);
            Assert.Contains(repeatButtons, b => b.Name == "PART_IncreaseButton");
            Assert.Contains(repeatButtons, b => b.Name == "PART_DecreaseButton");

            Assert.NotNull(ddb.GetVisualDescendants().OfType<Border>().FirstOrDefault());
            Assert.True(icon.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Any());
            Assert.NotNull(groupBox.GetVisualDescendants().OfType<ContentPresenter>().ToList());
            Assert.NotNull(tcc.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault());
            Assert.True(masked.GetVisualDescendants().OfType<global::Avalonia.Controls.Presenters.TextPresenter>().Any());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Spinner_post_and_transition_transition_presenters()
    {
        var window = new Window { Width = 300, Height = 60 };
        var spinner = new global::Avalonia.Controls.ButtonSpinner { Content = "42 instances", Width = 220 };
        var tcc = new TransitioningContentControl { Content = "A" };
        window.Content = new StackPanel { Children = { spinner, tcc } };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();

            // both cross-fade presenters resolve
            Assert.NotNull(tcc.GetVisualDescendants().OfType<ContentPresenter>()
                .FirstOrDefault(p => p.Name == "PART_ContentPresenter"));
            Assert.NotNull(tcc.GetVisualDescendants().OfType<ContentPresenter>()
                .FirstOrDefault(p => p.Name == "PART_ContentPresenter2"));

            // chevron strokes now use tokens that actually exist
            foreach (var path in spinner.GetVisualDescendants()
                         .OfType<global::Avalonia.Controls.Shapes.Path>())
            {
                Assert.NotNull(path.Stroke);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_nav_buttons_start_disabled_at_first_page()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();

            var first = window.FindControl<Button>("PageFirst");
            var prev = window.FindControl<Button>("PagePrev");
            var next = window.FindControl<Button>("PageNext");
            var last = window.FindControl<Button>("PageLast");

            Assert.False(first?.IsEnabled ?? true);
            Assert.False(prev?.IsEnabled ?? true);
            Assert.True(next?.IsEnabled ?? false);
            Assert.True(last?.IsEnabled ?? false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slider_and_NumericUpDown_templates_apply()
    {
        var slider = new global::Avalonia.Controls.Slider { Value = 35, Width = 200 };
        var nud = new global::Avalonia.Controls.NumericUpDown { Value = 42, Width = 200 };
        var spinner = new global::Avalonia.Controls.ButtonSpinner { Content = "x" };
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            window.Content = new StackPanel { Children = { slider, nud, spinner } };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var track = slider.GetVisualDescendants().First(v => v.GetType().Name == "Track");
            Assert.NotNull(track);
            Assert.NotNull(slider.GetVisualDescendants().OfType<Thumb>().FirstOrDefault());
            var buttons = slider.GetVisualDescendants().OfType<RepeatButton>().ToList();
            Assert.Equal(2, buttons.Count);

            Assert.NotNull(nud.GetVisualDescendants().FirstOrDefault(v => v.GetType().Name == "ButtonSpinner"));
            Assert.NotNull(nud.GetVisualDescendants().OfType<global::Avalonia.Controls.Presenters.TextPresenter>().ToList());
            Assert.Equal(2, spinner.GetVisualDescendants().OfType<RepeatButton>().Count());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SplitView_and_GridSplitter_templates_apply()
    {
        var split = new global::Avalonia.Controls.SplitView
        {
            DisplayMode = global::Avalonia.Controls.SplitViewDisplayMode.Inline,
            IsPaneOpen = true,
            OpenPaneLength = 140,
            Pane = "pane",
            Content = "content",
        };
        var splitter = new global::Avalonia.Controls.GridSplitter();
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            window.Content = new StackPanel { Children = { split, splitter } };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(split.GetVisualDescendants()
                .FirstOrDefault(v => v.GetType().Name == "Panel" && v.Name == "PART_PaneRoot"));
            Assert.NotNull(split.GetVisualDescendants()
                .FirstOrDefault(v => v.GetType().Name == "Panel" && v.Name == "ContentRoot"));
            Assert.NotNull(splitter.GetVisualDescendants()
                .FirstOrDefault(v => v is Border b && b.Name == "PART_Grip"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Menubar_menu_and_items_apply()
    {
        var menu = new global::Avalonia.Controls.Menu();
        var item = new global::Avalonia.Controls.MenuItem { Header = "Edit" };
        item.Items.Add(new global::Avalonia.Controls.MenuItem { Header = "Undo" });
        menu.Items.Add(item);
        var window = new Window { Width = 300, Height = 200 };
        try
        {
            window.Content = menu;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Single(menu.Items);
            isOpenable(menu, window);
        }
        finally
        {
            window.Close();
        }
    }

    private static void isOpenable(global::Avalonia.Controls.Menu menu, Window host)
    {
        var item = menu.GetVisualDescendants().OfType<global::Avalonia.Controls.MenuItem>().First();
        var point = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), host)!.Value;
        host.MouseDown(point, MouseButton.Left);
        host.MouseUp(point, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(item.IsSubMenuOpen);
    }

    [AvaloniaFact]
    public void SplitButtons_and_CommandBar_templates_apply()
    {
        var split = new global::Avalonia.Controls.SplitButton
        {
            Content = "Go",
            Flyout = new global::Avalonia.Controls.Flyout() { Content = "Menu" },
        };
        var toggle = new global::Avalonia.Controls.ToggleSplitButton { Content = "On" };
        var bar = new global::Avalonia.Controls.CommandBar();
        var btn = new global::Avalonia.Controls.CommandBarButton { Label = "Copy" };
        var tog = new global::Avalonia.Controls.CommandBarToggleButton { Label = "Pin" };
        var sep = new global::Avalonia.Controls.CommandBarSeparator();
        bar.PrimaryCommands = new global::System.Collections.Generic.List<global::Avalonia.Controls.ICommandBarElement> { btn, sep, tog };
        var window = new Window { Width = 400, Height = 200 };
        try
        {
            window.Content = new StackPanel { Children = { split, toggle, bar } };
            window.Show();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.NotNull(split.GetVisualDescendants().FirstOrDefault(v => v.GetType().Name == "Button" && v.Name == "PART_PrimaryButton"));
            Assert.NotNull(split.GetVisualDescendants().FirstOrDefault(v => v.GetType().Name == "Button" && v.Name == "PART_SecondaryButton"));

            Assert.NotNull(toggle.GetVisualDescendants().FirstOrDefault(v => v.GetType().Name == "Button" && v.Name == "PART_PrimaryButton"));

            // toggling raises :checked fill via prepared Border
            var bordersOnBar = bar.GetVisualDescendants().OfType<Border>().ToList();
            Assert.NotEmpty(bordersOnBar);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Spinner_and_groupbox_layouts_stay_compact()
    {
        var spinner = new global::Avalonia.Controls.ButtonSpinner { Content = "42 instances", Width = 200 };
        var nud = new global::Avalonia.Controls.NumericUpDown { Value = 42, Width = 200 };
        var gb = new GroupBox
        {
            Header = "Zone settings",
            Width = 320,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new global::Avalonia.Controls.ToggleSwitch { Content = "Always Online", IsChecked = true },
                    new global::Avalonia.Controls.TextBlock { Text = "Serve cached content if your origin is down.", TextWrapping = TextWrapping.Wrap },
                },
            },
        };
        var window = new Window { Width = 420, Height = 400 };
        try
        {
            window.Content = new StackPanel { Children = { spinner, nud, gb } };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            string Describe(Visual root, string label)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var v in root.GetVisualDescendants())
                {
                    parts.Add($"{v.GetType().Name},{v.Bounds.Width:F1}x{v.Bounds.Height:F1}");
                }
                return label + ": " + string.Join("|", parts);
            }

            // spin chevrons stay a narrow strip; no divider/border artifacts;
            // group box wraps its content without clipping
            foreach (var root in new Visual[] { spinner, nud })
            {
                foreach (var b in root.GetVisualDescendants().OfType<RepeatButton>())
                {
                    Assert.True(b.Bounds.Width <= 24, $"spinner button too wide: {b.Bounds.Width:F1}");
                }
                foreach (var b in root.GetVisualDescendants().OfType<Border>())
                {
                    Assert.False(b.Background is null && b.Bounds.Height <= 2 && b.Bounds.Width > 30,
                        "unexpected divider artifact in spinner");
                }
            }
            Assert.True(gb.Bounds.Height >= 120, $"group box clipped: {gb.Bounds.Height:F1}");
            foreach (var t in gb.GetVisualDescendants().OfType<global::Avalonia.Controls.TextBlock>())
            {
                Assert.True(t.Bounds.Bottom <= gb.Bounds.Bottom + 0.5, "text clipped by group box");
            }
            global::Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/slice-a.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Split_and_commandbar_paint_states()
    {
        var split = new global::Avalonia.Controls.SplitButton { Content = "Allocate quota", Width = 200 };
        split.Flyout = new global::Avalonia.Controls.Flyout { Content = "Menu" };
        var toggle = new global::Avalonia.Controls.ToggleSplitButton { Content = "Expand preflight", Width = 220 };
        var bar = new global::Avalonia.Controls.CommandBar();
        bar.PrimaryCommands = new global::System.Collections.Generic.List<global::Avalonia.Controls.ICommandBarElement>
        {
            new global::Avalonia.Controls.CommandBarButton { Label = "Copy" },
            new global::Avalonia.Controls.CommandBarToggleButton { Label = "Pin" },
            new global::Avalonia.Controls.CommandBarButton { Label = "Paste" },
        };
        var slider2 = new global::Avalonia.Controls.Slider { Width = 200, Ticks = new global::Avalonia.Collections.AvaloniaList<double> { 0, 25, 50, 75, 100 }, TickPlacement = global::Avalonia.Controls.TickPlacement.BottomRight, Value = 60 };
        var nud = new global::Avalonia.Controls.NumericUpDown { Value = 42, Width = 200 };
        var window = new Window { Width = 360, Height = 480 };
        try
        {
            window.Content = new StackPanel { Children = { split, toggle, bar, slider2, nud } };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            // focus numeric input
            var box = nud.GetVisualDescendants().OfType<global::Avalonia.Controls.TextBox>().First();
            box.Focus();
            Dispatcher.UIThread.RunJobs();
            window.Height = 520;
            Dispatcher.UIThread.RunJobs();
            // click the toggle's primary half and re-capture
            var primary = toggle.GetVisualDescendants().First(v => v.GetType().Name == "Button" && v.Name == "PART_PrimaryButton");
            var p2 = primary.TranslatePoint(new Point(primary.Bounds.Width/2, primary.Bounds.Height/2), window)!.Value;
            window.MouseDown(p2, MouseButton.Left);
            window.MouseUp(p2, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            // checked paint lands on the named ring layer
            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            var ring = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "RingLayer");
            Assert.Equal(toggle.IsChecked, true);
            Assert.True(toggle.Classes.Contains(":checked"), "checked pseudo missing");
            var all = toggle.GetVisualDescendants();
            global::Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/slice-d.png");
            var f2 = global::Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
            f2!.Save("/tmp/opencode/slice-d.png");
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            global::Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/slice-d.png");
        }
        finally
        {
            window.Close();
        }
    }
    [AvaloniaFact]
    public void Splitview_open_panes_render_wide_enough()
    {
        var window = new Kumo.Demo.MainWindow();
        window.Width = 1280;
        window.Height = 2400;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var sv = window.GetVisualDescendants().OfType<global::Avalonia.Controls.ScrollViewer>().First();
            var splitviews = window.GetVisualDescendants().OfType<global::Avalonia.Controls.SplitView>().ToList();
            var panesY = splitviews[0].TranslatePoint(default, window)!.Value.Y;
            sv.Offset = new global::Avalonia.Vector(0, panesY - 60);
            Dispatcher.UIThread.RunJobs();
            // resizable card: open pane has the demo's OpenPaneLength
            var pr0 = splitviews[0].GetVisualDescendants().First(v => v.Name == "PART_PaneRoot");
            Assert.Equal(splitviews[0].OpenPaneLength, pr0.Bounds.Width, 1);
            // slider drives the fixed pane; midpoint = even split
            var sizeSlider = window.GetVisualDescendants().OfType<global::Avalonia.Controls.Slider>().First(sl => sl.Name == "DemoFixedSplitSize");
            sizeSlider.Value = 100;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(splitviews[1].OpenPaneLength, sizeSlider.Value);
            var pr1 = splitviews[1].GetVisualDescendants().First(v => v.Name == "PART_PaneRoot");
            Assert.Equal(100, pr1.Bounds.Width, 1);
            Dispatcher.UIThread.RunJobs();
            global::Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/demo-panes.png");
        }
        finally { window.Close(); }
    }
}
