using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Kumo.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildPaletteSections();
    }

    private void OnToggleTheme(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant =
            ThemeToggle.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private void BuildPaletteSections()
    {
        var (semantic, primitives) = CollectTokenNames();
        FillSwatches(SemanticSwatches, semantic);
        FillSwatches(PrimitiveSwatches, primitives);
    }

    private static (IReadOnlyList<string> semantic, IReadOnlyList<string> primitives)
        CollectTokenNames()
    {
        var app = Application.Current ?? throw new InvalidOperationException("no app");
        var palette = FindTokenDictionary(app.Resources)
            ?? throw new InvalidOperationException("Kumo palette not found");

        var primitiveNames = palette.Keys.OfType<string>()
            .Where(k => k.StartsWith("KumoBrush")).OrderBy(k => k).ToList();

        var semanticNames = palette.ThemeDictionaries.Values
            .SelectMany(v => v is ResourceDictionary rd
                ? rd.Keys.OfType<string>().Where(k => k.StartsWith("KumoBrush"))
                : Enumerable.Empty<string>())
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        return (semanticNames, primitiveNames);
    }

    private static ResourceDictionary? FindTokenDictionary(IResourceDictionary dictionary)
    {
        if (dictionary is ResourceDictionary resource &&
            resource.ThemeDictionaries.Values
                .Any(v => v is ResourceDictionary rd && rd.Keys.Contains("KumoBrushBrand")))
        {
            return resource;
        }
        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (merged is ResourceDictionary mergedResource)
            {
                var found = FindTokenDictionary(mergedResource);
                if (found is not null)
                {
                    return found;
                }
            }
        }
        return null;
    }

    private static void FillSwatches(Panel host, IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            var brushKey = key;
            var colorName = brushKey.Replace("KumoBrush", "");
            var chip = new Border
            {
                Width = 64,
                Height = 40,
                CornerRadius = new CornerRadius(6),
                [!Border.BackgroundProperty] = new DynamicResourceExtension(brushKey),
                [!Border.BorderBrushProperty] = new DynamicResourceExtension("KumoBrushHairline"),
                BorderThickness = new Thickness(1),
            };
            var label = new TextBlock
            {
                Text = colorName,
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("KumoBrushTextSubtle"),
            };
            host.Children.Add(new StackPanel
            {
                Width = 64,
                Margin = new Thickness(0, 0, 8, 8),
                Spacing = 2,
                Children = { chip, label },
            });
        }
    }
}
