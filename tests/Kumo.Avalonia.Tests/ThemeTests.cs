using System;using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Kumo.Demo;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Kumo.Avalonia.Tests.TestAppBuilder))]

namespace Kumo.Avalonia.Tests;

public class ThemeTests
{
    private static IReadOnlyDictionary<ThemeVariant, ResourceDictionary> PaletteDictionaries()
    {
        var palette = FindPalette(Application.Current!.Resources);
        Assert.NotNull(palette);
        return palette!.ThemeDictionaries.ToDictionary(
            kv => kv.Key,
            kv => Assert.IsType<ResourceDictionary>(kv.Value));
    }

    private static ResourceDictionary? FindPalette(IResourceDictionary dictionary)
    {
        if (dictionary is ResourceDictionary resourceDictionary &&
            resourceDictionary.ThemeDictionaries.Count > 0 &&
            resourceDictionary.ThemeDictionaries.Values
                .Any(v => v is ResourceDictionary rd && rd.Keys.Contains("KumoBrushBrand")))
        {
            return resourceDictionary;
        }
        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (merged is ResourceDictionary resource)
            {
                var found = FindPalette(resource);
                if (found is not null)
                {
                    return found;
                }
            }
        }
        return null;
    }

    [AvaloniaFact]
    public void Palette_defines_54_brushes_per_variant_with_matching_keys()
    {
        var palette = PaletteDictionaries();
        Assert.Equal(2, palette.Count);
        HashSet<string>? lightBrushKeys = null;
        foreach (var (_, dictionary) in palette)
        {
            var brushKeys = dictionary.Keys.OfType<string>()
                .Where(k => k.StartsWith("KumoBrush")).ToList();
            Assert.Equal(54, brushKeys.Count);
            var colorKeys = dictionary.Keys.OfType<string>()
                .Where(k => k.StartsWith("KumoColor")).ToList();
            Assert.Equal(54, colorKeys.Count);
            foreach (var key in colorKeys)
            {
                Assert.Contains(key.Replace("KumoColor", "KumoBrush"), brushKeys);
            }
            if (lightBrushKeys is null)
            {
                lightBrushKeys = brushKeys.ToHashSet();
            }
            else
            {
                Assert.Equal(lightBrushKeys, brushKeys.ToHashSet());
            }
        }
    }

    [AvaloniaFact]
    public void Component_base_colors_are_available_outside_theme_dictionaries()
    {
        var primitives = new[]
        {
            "KumoColorBlue500", "KumoColorBlue600", "KumoColorNeutral200",
            "KumoColorNeutral700", "KumoColorNeutral800", "KumoColorBlue300",
        };
        foreach (var key in primitives)
        {
            Assert.True(Application.Current!.TryGetResource(key, null, out var color),
                $"missing primitive {key}");
            Assert.IsType<Color>(color);
        }
    }

    [AvaloniaFact]
    public void Brand_brush_matches_upstream_kumo_values()
    {
        var palette = PaletteDictionaries();
        Assert.Equal(Color.Parse("#056DFF"),
            Assert.IsType<SolidColorBrush>(palette[ThemeVariant.Light]["KumoBrushBrand"]).Color);
        Assert.Equal(Color.Parse("#045EDE"),
            Assert.IsType<SolidColorBrush>(palette[ThemeVariant.Dark]["KumoBrushBrand"]).Color);
        Assert.Equal(Color.Parse("#F6821F"),
            Assert.IsType<SolidColorBrush>(palette[ThemeVariant.Light]["KumoBrushTextBrand"]).Color);
    }

    [AvaloniaFact]
    public void Dynamic_resources_switch_when_theme_variant_changes()
    {
        var window = new MainWindow();
        var text = new TextBlock
        {
            [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("KumoBrushTextDefault")
        };
        window.Content = text;
        window.Show();

        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var lightBrush = Assert.IsType<SolidColorBrush>(text.Foreground);

        Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        var darkBrush = Assert.IsType<SolidColorBrush>(text.Foreground);

        Assert.Equal(Color.Parse("#18181B"), lightBrush.Color);
        Assert.Equal(Color.Parse("#F5F5F5"), darkBrush.Color);
        Assert.NotEqual(lightBrush.Color, darkBrush.Color);
    }

    [AvaloniaFact]
    public void Primary_button_resolves_brand_background()
    {
        var window = new MainWindow();
        var button = new Button { Classes = { "primary" }, Content = "Test" };
        window.Content = button;
        window.Show();

        var background = Assert.IsType<SolidColorBrush>(button.Background);
        Assert.Equal(Color.Parse("#056DFF"), background.Color);
        var foreground = Assert.IsType<SolidColorBrush>(button.Foreground);
        Assert.Equal(Color.Parse("#F5F5F5"), foreground.Color);
    }

    [AvaloniaFact]
    public void ToggleSwitch_checked_track_uses_blue_scale()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var toggle = new ToggleSwitch { IsChecked = true };
        window.Content = toggle;
        window.Show();

        var onTrack = toggle.GetVisualDescendants()
            .OfType<Border>().First(b => b.Name == "SwitchKnobBounds");
        Assert.Equal(Color.Parse("#2B7FFF"),
            Assert.IsType<SolidColorBrush>(onTrack.Background).Color);

        var knob = toggle.GetVisualDescendants()
            .OfType<Ellipse>().First(e => e.Name == "SwitchKnobOn");
        Assert.Equal(Color.Parse("#FFFFFF"),
            Assert.IsType<SolidColorBrush>(knob.Fill).Color);

        Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        Assert.Equal(Color.Parse("#155DFC"),
            Assert.IsType<SolidColorBrush>(onTrack.Background).Color);
        Assert.Equal(Color.Parse("#8EC5FF"),
            Assert.IsType<SolidColorBrush>(knob.Fill).Color);
    }

    [AvaloniaFact]
    public void CheckBox_checked_box_fills_with_contrast()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var checkBox = new CheckBox { Content = "Test", IsChecked = true };
        window.Content = checkBox;
        window.Show();

        var box = checkBox.GetVisualDescendants()
            .OfType<Border>().First(b => b.Name == "Box");
        Assert.Equal(4, box.CornerRadius.TopLeft);
        var expectedContrast = Assert.IsType<SolidColorBrush>(
            PaletteDictionaries()[ThemeVariant.Light]["KumoBrushContrast"]).Color;
        Assert.Equal(expectedContrast,
            Assert.IsType<SolidColorBrush>(box.Background).Color);

        var check = checkBox.GetVisualDescendants()
            .OfType<AvaloniaPath>().First(p => p.Name == "CheckGlyph");
        Assert.True(check.IsVisible);
    }

    [AvaloniaFact]
    public void ComboBox_popup_and_trigger_use_kumo_surfaces()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var comboBox = new ComboBox { PlaceholderText = "Pick" };
        comboBox.Items.Add("One");
        comboBox.Items.Add("Two");
        window.Content = comboBox;
        window.Show();

        var popup = Assert.IsType<Popup>(comboBox.GetVisualDescendants()
            .First(c => c is Popup));
        var popupBorder = Assert.IsType<Border>(popup.Child);
        Assert.Equal(Color.Parse("#FFFFFF"),
            Assert.IsType<SolidColorBrush>(popupBorder.Background).Color);
        Assert.Equal(8, popupBorder.CornerRadius.TopLeft);
    }

    [AvaloniaFact]
    public void TabItem_selected_tab_fills_with_base_surface()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var tabControl = new TabControl();
        tabControl.Items.Add(new TabItem { Header = "One" });
        tabControl.Items.Add(new TabItem { Header = "Two" });
        tabControl.SelectedIndex = 1;
        window.Content = tabControl;
        window.Show();

        var selected = Assert.IsType<TabItem>(tabControl.SelectedItem);
        var layoutRoot = selected.GetVisualDescendants()
            .OfType<Border>().First(b => b.Name == "PART_LayoutRoot");
        Assert.Equal(Color.Parse("#FFFFFF"),
            Assert.IsType<SolidColorBrush>(layoutRoot.Background).Color);
        var expectedLine = Assert.IsType<SolidColorBrush>(
            PaletteDictionaries()[ThemeVariant.Light]["KumoBrushLine"]).Color;
        Assert.Equal(expectedLine,
            Assert.IsType<SolidColorBrush>(layoutRoot.BorderBrush).Color);
    }

    [AvaloniaFact]
    public void Demo_main_window_builds_full_palette_showcase()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var semantic = Assert.IsType<WrapPanel>(
            window.FindControl<WrapPanel>("SemanticSwatches")!);
        Assert.Equal(54, semantic.Children.Count);

        var badge = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("badge") && b.Classes.Contains("primary"));
        Assert.NotNull(badge.Background);
        Assert.IsType<SolidColorBrush>(badge.Background);
    }

    [AvaloniaFact]
    public void NotificationCard_toast_uses_kumo_surface_and_status_ring()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var card = new NotificationCard
        {
            Content = new TextBlock { Text = "Done" },
            NotificationType = NotificationType.Success,
        };
        window.Content = card;
        window.Show();

        Assert.Equal(12, card.CornerRadius.TopLeft);
        var expectedSuccess = Assert.IsType<SolidColorBrush>(
            PaletteDictionaries()[ThemeVariant.Light]["KumoBrushSuccess"]).Color;
        Assert.Equal(expectedSuccess,
            Assert.IsType<SolidColorBrush>(card.BorderBrush).Color);
    }

    [AvaloniaFact]
    public void Badge_error_class_applies_danger_tint()
    {
        var window = new MainWindow();
        var badge = new Border { Classes = { "badge", "error" } };
        badge.Child = new TextBlock { Text = "Error" };
        window.Content = badge;
        window.Show();

        var background = Assert.IsType<SolidColorBrush>(badge.Background);
        var expectedTint = Assert.IsType<SolidColorBrush>(
            PaletteDictionaries()[ThemeVariant.Light]["KumoBrushDangerTint"]).Color;
        Assert.Equal(expectedTint, background.Color);
        var textBrush = Assert.IsType<SolidColorBrush>(
            Assert.IsType<TextBlock>(badge.Child).Foreground);
        Assert.NotEqual(textBrush.Color, background.Color);
    }

    [AvaloniaFact]
    public void Link_button_uses_text_link_color_with_underline()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var link = new HyperlinkButton { Content = "Docs" };
        var plain = new HyperlinkButton { Classes = { "plain" }, Content = "Plain" };
        window.Content = new StackPanel { Children = { link, plain } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Color.Parse("#193CB8"),
            Assert.IsType<SolidColorBrush>(link.Foreground).Color);
        var underlined = link.GetVisualDescendants().OfType<TextBlock>().First();
        Assert.Equal(TextDecorations.Underline, underlined.TextDecorations);
        var bare = plain.GetVisualDescendants().OfType<TextBlock>().First();
        Assert.True(bare.TextDecorations is null || bare.TextDecorations.Count == 0,
            "plain link should not be underlined");
    }

    [AvaloniaFact]
    public void Loader_template_renders_spinning_arc()
    {
        var window = new MainWindow();
        var loader = new ContentControl { Classes = { "loader" } };
        window.Content = loader;
        window.Show();

        var spin = loader.GetVisualDescendants().OfType<Panel>()
            .First(p => p.Name == "PART_Spin");
        Assert.True(spin.IsVisible);
        var paths = loader.GetVisualDescendants().OfType<AvaloniaPath>().ToList();
        Assert.Equal(2, paths.Count);
    }

    [AvaloniaFact]
    public void Input_group_children_reset_radii_and_focus_ring_wraps_group()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var group = new Border { Classes = { "input-group" } };
        var input = new TextBox { Width = 200 };
        group.Child = input;
        var probe = new TextBlock
        {
            [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("KumoBrushFocusRing")
        };
        window.Content = new StackPanel { Children = { group, probe } };
        window.Show();

        Assert.Equal(Color.Parse("#FFFFFF"),
            Assert.IsType<SolidColorBrush>(group.Background).Color);
        var borderElement = input.GetVisualDescendants().OfType<Border>()
            .First(b => b.Name == "PART_BorderElement");
        Assert.Equal(0, borderElement.CornerRadius.TopLeft);

        input.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(":focus-within", group.Classes);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(((ISolidColorBrush)probe.Foreground!).Color,
            ((ISolidColorBrush)group.BorderBrush!).Color);
    }

    [AvaloniaFact]
    public void Meter_uses_fill_track_with_8px_height()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var meter = new ProgressBar { Classes = { "meter" }, Value = 50 };
        window.Content = meter;
        window.Show();

        Assert.Equal(8, meter.MinHeight);
        var expectedFill = Assert.IsType<SolidColorBrush>(
            PaletteDictionaries()[ThemeVariant.Light]["KumoBrushFill"]).Color;
        Assert.Equal(expectedFill, Assert.IsType<SolidColorBrush>(meter.Background).Color);

        var warning = new ProgressBar { Classes = { "meter", "warning" }, Value = 50 };
        window.Content = warning;
        var expectedWarning = Assert.IsType<SolidColorBrush>(
            PaletteDictionaries()[ThemeVariant.Light]["KumoBrushWarning"]).Color;
        Assert.Equal(expectedWarning, Assert.IsType<SolidColorBrush>(warning.Foreground).Color);
    }

    [AvaloniaFact]
    public void Table_row_presets_use_elevated_and_tint_backgrounds()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var alt = new Border { Classes = { "table-row", "alt" } };
        var selected = new Border { Classes = { "table-row", "selected" } };
        var stack = new StackPanel { Children = { alt, selected } };
        window.Content = stack;
        window.Show();

        var expectedElevated = Assert.IsType<SolidColorBrush>(
            PaletteDictionaries()[ThemeVariant.Light]["KumoBrushElevated"]).Color;
        Assert.Equal(expectedElevated, Assert.IsType<SolidColorBrush>(alt.Background).Color);
        var expectedTint = Assert.IsType<SolidColorBrush>(
            PaletteDictionaries()[ThemeVariant.Light]["KumoBrushTint"]).Color;
        Assert.Equal(expectedTint, Assert.IsType<SolidColorBrush>(selected.Background).Color);
    }

    [AvaloniaFact]
    public void Toolbar_buttons_get_edge_rounding_only()
    {
        var window = new MainWindow();
        var toolbar = new Border { Classes = { "toolbar" } };
        var first = new Button();
        var middle = new Button();
        var last = new Button();
        toolbar.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { first, middle, last },
        };
        window.Content = toolbar;
        window.Show();

        foreach (var button in new[] { first, middle, last })
        {
            var presenter = button.GetVisualDescendants()
                .OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            if (button == first)
            {
                Assert.Equal(6, presenter.CornerRadius.TopLeft);
                Assert.Equal(0, presenter.CornerRadius.TopRight);
            }
            else if (button == last)
            {
                Assert.Equal(0, presenter.CornerRadius.TopLeft);
                Assert.Equal(6, presenter.CornerRadius.TopRight);
            }
            else
            {
                Assert.Equal(0, presenter.CornerRadius.TopLeft);
                Assert.Equal(0, presenter.CornerRadius.TopRight);
            }
        }
    }

    [AvaloniaFact]
    public void Empty_preset_uses_xl_radius_and_spec_padding()
    {
        var window = new MainWindow();
        var empty = new Border { Classes = { "empty" } };
        window.Content = empty;
        window.Show();

        Assert.Equal(12, empty.CornerRadius.TopLeft);
        Assert.Equal(64, empty.Padding.Top);
        var small = new Border { Classes = { "empty", "empty-sm" } };
        window.Content = small;
        Assert.Equal(32, small.Padding.Top);
    }

    [AvaloniaFact]
    public void Autocomplete_dropdown_uses_control_surface()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var box = new AutoCompleteBox();
        box.ItemsSource = new[] { "alpha", "beta" };
        window.Content = box;
        window.Show();

        var popup = Assert.IsType<Popup>(box.GetVisualDescendants().First(c => c is Popup));
        var container = Assert.IsType<Border>(popup.Child);
        Assert.Equal("PART_SuggestionsContainer", container.Name);
        Assert.Equal(Color.Parse("#FFFFFF"),
            Assert.IsType<SolidColorBrush>(container.Background).Color);
        Assert.Equal(8, container.CornerRadius.TopLeft);
    }

    [AvaloniaFact]
    public void Dialog_window_uses_dialog_surface_and_typography()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow();
        var dialog = new DialogWindow();
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        var surface = dialog.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("dialog-surface"));
        Assert.Equal(Color.Parse("#FFFFFF"),
            Assert.IsType<SolidColorBrush>(surface.Background).Color);
        Assert.Equal(12, surface.CornerRadius.TopLeft);
        var title = dialog.GetVisualDescendants().OfType<TextBlock>()
            .First(b => b.Classes.Contains("dialog-title"));
        Assert.Equal(16, title.FontSize);
    }
}
