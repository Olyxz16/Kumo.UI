using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
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
            resourceDictionary.ThemeDictionaries.Count > 0)
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
}
