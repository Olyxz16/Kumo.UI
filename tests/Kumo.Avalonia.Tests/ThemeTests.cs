using System;using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
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
}
