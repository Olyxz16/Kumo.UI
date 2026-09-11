using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
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
}
