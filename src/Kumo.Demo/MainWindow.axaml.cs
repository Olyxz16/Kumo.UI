using System;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Kumo.Demo;

public partial class MainWindow : Window
{
    private readonly WindowNotificationManager _toasts;
    private static readonly Dictionary<string, (NotificationType Type, string Title, string Message)> toastText =
        new()
        {
            ["success"] = (NotificationType.Success, "Deployment succeeded", "Version 42 is live in production."),
            ["info"] = (NotificationType.Information, "Sync in progress", "Zone settings are being propagated."),
            ["warning"] = (NotificationType.Warning, "Build took longer than expected", "Cache was skipped for this run."),
            ["error"] = (NotificationType.Error, "Deployment failed", "Rollback completed to version 41."),
        };

    private static readonly string[] Environments =
        ["production", "staging", "development", "preview", "workers-dev"];

    public MainWindow()
    {
        InitializeComponent();
        _toasts = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.BottomRight,
            Margin = new Thickness(0, 0, 16, 16),
            MaxItems = 4,
        };
        Loaded += (_, _) =>
        {
            BuildPaletteSections();
            EnvAutocomplete.ItemsSource = Environments;
        };
    }

    private void OnToggleReveal(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var box = this.FindControl<TextBox>("SensitiveInput");
        if (box is null)
        {
            return;
        }

        box.PasswordChar = box.PasswordChar == '\u2022' ? default : '\u2022';
    }

    private void OnTableRowTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Border row)
        {
            var checkBox = row.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault();
            if (checkBox is not null)
            {
                checkBox.IsChecked = checkBox.IsChecked != true;
            }
        }
    }

    private void OnRowCheckChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            var row = checkBox.GetVisualAncestors().OfType<Border>()
                .FirstOrDefault(b => b.Classes.Contains("table-row"));
            if (row is not null)
            {
                row.Classes.Set("selected", checkBox.IsChecked == true);
            }

            UpdateSelectAllState();
        }
    }

    private void OnSelectAllRows(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not CheckBox selectAll)
        {
            return;
        }

        var body = this.FindControl<StackPanel>("DemoTableBody");
        if (body is null)
        {
            return;
        }

        foreach (var rowCheck in RowChecks(body))
        {
            rowCheck.IsChecked = selectAll.IsChecked;
        }
    }

    private void UpdateSelectAllState()
    {
        var body = this.FindControl<StackPanel>("DemoTableBody");
        var selectAll = this.FindControl<CheckBox>("TableSelectAll");
        if (body is null || selectAll is null)
        {
            return;
        }

        var boxes = RowChecks(body).ToList();
        if (boxes.Count > 0)
        {
            selectAll.IsChecked = boxes.All(b => b.IsChecked == true);
        }
    }

    private static IEnumerable<CheckBox> RowChecks(StackPanel body) =>
        body.GetVisualDescendants().OfType<CheckBox>()
            .Where(c => c.Name?.StartsWith("TableRowCheck", StringComparison.Ordinal) == true);

    private async void OnOpenDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new DialogWindow();
        await dialog.ShowDialog(this);
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

    private void OnShowToast(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !toastText.TryGetValue(tag, out var spec))
        {
            return;
        }

        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(new TextBlock
        {
            Text = spec.Title,
            FontWeight = FontWeight.SemiBold,
            [!TextBlock.ForegroundProperty] = new DynamicResourceExtension($"KumoBrushText{spec.Type switch
            {
                NotificationType.Success => "Success",
                NotificationType.Warning => "Warning",
                NotificationType.Error => "Danger",
                _ => "Info",
            }}"),
        });
        content.Children.Add(new TextBlock
        {
            Text = spec.Message,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("KumoBrushTextDefault"),
        });

        _toasts.Show(content, spec.Type,
            expiration: TimeSpan.FromSeconds(4), onClick: null, onClose: null);
    }

    private void BuildPaletteSections()
    {
        var semantic = CollectTokenNames();
        FillSwatches(SemanticSwatches, semantic);
    }

    private static IReadOnlyList<string> CollectTokenNames()
    {
        var app = Application.Current ?? throw new InvalidOperationException("no app");
        var palette = FindTokenDictionary(app.Resources)
            ?? throw new InvalidOperationException("Kumo palette not found");

        return palette.ThemeDictionaries.Values
            .SelectMany(v => v is ResourceDictionary rd
                ? rd.Keys.OfType<string>().Where(k => k.StartsWith("KumoBrush"))
                : Enumerable.Empty<string>())
            .Distinct()
            .OrderBy(k => k)
            .ToList();
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
