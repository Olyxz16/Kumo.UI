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
            SyncPageNav();
            BuildPaletteSections();
            _members.Add(MemberNames[0]);
            _members.Add(MemberNames[1]);
            _members.Add(MemberNames[2]);
            RefreshMemberList();
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

        box.PasswordChar = box.PasswordChar == '\u25CF' ? default : '\u25CF';
    }

    private int _seedMember;

    private static readonly string[] MemberNames =
    [
        "Ada Lovelace",
        "Grace Hopper",
        "Alan Zhang",
        "Zhang Wei",
        "Alan Kay",
        "Margaret Hamilton",
        "Radia Perlman",
        "Barbara Liskov",
    ];

    private readonly List<string> _members = [];

    private void OnMemberFilterChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        RefreshMemberList();
    }

    private void OnToolbarFilterMenu(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }
        var flyout = new MenuFlyout();
        foreach (var option in new[] { "All", "Team", "Guest" })
        {
            var item = new MenuItem { Header = option, Tag = option };
            item.Click += (_, _) => { RefreshMemberList(); };
            flyout.Items.Add(item);
        }
        flyout.ShowAt(button);
    }

    private void OnAddMember(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _members.Add("New member " + _seedMember++);
        RefreshMemberList();
    }

    private void OnInviteMember(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = MemberNames[(_members.Count + 3) % MemberNames.Length] + " (invited)";
        RefreshMemberList();
        _toasts.Show(new Notification("Invite sent", $"Link for {name} copied to clipboard."));
    }

    private void RefreshMemberList()
    {
        if (MemberList is null || _members.Count == 0)
        {
            return;
        }
        var filter = MemberFilter?.Text ?? "";
        MemberList.ItemsSource = _members
            .Where(m => m.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Select(name => new TextBlock { Text = name })
            .ToList();
    }

    private void OnOpenDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ModalOverlay.IsVisible = true;
    }

    private void OnCloseModal(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ModalOverlay.IsVisible = false;
    }

    private static int _demoSpinnerCount = 42;

    private void OnDemoSpin(object? sender, Avalonia.Controls.SpinEventArgs e)
    {
        _demoSpinnerCount = Math.Clamp(_demoSpinnerCount + (e.Direction == Avalonia.Controls.SpinDirection.Increase ? 1 : -1), 0, 999);
        if (this.FindControl<TextBlock>("DemoSpinnerText") is { } text)
        {
            text.Text = $"{_demoSpinnerCount} instances";
        }
    }

    private void OnDemoSlidePrev(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.FindControl<Carousel>("DemoCarousel") is { } carousel)
        {
            carousel.SelectedIndex = (carousel.SelectedIndex - 1 + carousel.ItemCount) % carousel.ItemCount;
        }
    }

    private void OnDemoSlideNext(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.FindControl<Carousel>("DemoCarousel") is { } carousel)
        {
            carousel.SelectedIndex = (carousel.SelectedIndex + 1) % carousel.ItemCount;
        }
    }

    private void OnDemoRefreshRequested(object? sender, global::Avalonia.Controls.RefreshRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        // brief moment for the brand circular-arrow visualizer to show
            _ = global::System.Threading.Tasks.Task.Delay(1200).ContinueWith(_ =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (this.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "DemoRefreshStamp") is { } stamp)
                    {
                        stamp.Text = $"updated {DateTime.Now:HH:mm:ss}";
                    }
                    deferral.Complete();
                });
            });
    }

    private void OnDemoCarouselSlide(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is not Carousel carousel) return;
        // SelectionChanged fires during InitializeComponent, before the name
        // scope is attached; FindControl would throw, so walk the visual tree
        var state = this.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "DemoCarouselState");
        if (state is { })
        {
            state.Text = $"Carousel: slide {carousel.SelectedIndex + 1} of 3";
        }
    }

    private void OnDemoPreflightChecked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.FindControl<TextBlock>("DemoPreflightState") is { } state)
        {
            state.Text = ((global::Avalonia.Controls.ToggleSplitButton)sender!).IsChecked is true
                ? "Preflight expanded (toggle is on)"
                : "Preflight collapsed (toggle is off)";
        }
    }

    private static readonly string[] TransitionMessages =
    [
        "TransitioningContentControl: swap content to see the 400ms fade",
        "Second sample: the CrossFade transition runs on content changes",
        "Third sample: followed by a soft fade back to the start",
    ];

    private int _transitionIndex;

    private void OnNextTransitionDemo(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.FindControl<TransitioningContentControl>("DemoTransitioner") is not { } demo)
        {
            return;
        }

        _transitionIndex = (_transitionIndex + 1) % TransitionMessages.Length;
        demo.Content = new TextBlock { Text = TransitionMessages[_transitionIndex], TextWrapping = TextWrapping.Wrap };
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

    private const int PageCount = 13;

    private int _currentPage = 1;

    private void OnPageNav(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string action })
        {
            return;
        }

        _currentPage = action switch
        {
            "first" => 1,
            "last" => PageCount,
            "prev" => Math.Max(1, _currentPage - 1),
            "next" => Math.Min(PageCount, _currentPage + 1),
            _ => _currentPage,
        };
        SyncPageInput();
        SyncPageNav();
    }

    private void OnPageInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            CommitPageInput();
            e.Handled = true;
        }
    }

    private void OnPageInputLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CommitPageInput();
    }

    private void CommitPageInput()
    {
        var input = this.FindControl<TextBox>("PageInput");
        if (input is null)
        {
            return;
        }

        if (int.TryParse(input.Text, out var page))
        {
            _currentPage = Math.Clamp(page, 1, PageCount);
        }

        SyncPageInput();
    }

    private void SyncPageNav()
    {
        TryPageButton(PageFirst, "first", _currentPage <= 1);
        TryPageButton(PagePrev, "prev", _currentPage <= 1);
        TryPageButton(PageNext, "next", _currentPage >= PageCount);
        TryPageButton(PageLast, "last", _currentPage >= PageCount);
    }

    private void TryPageButton(Button? button, string tag, bool atLimit)
    {
        button.IsEnabled = !atLimit;
        var strokeKey = atLimit ? "KumoBrushTextInactive" : "KumoBrushTextDefault";
        foreach (var branch in button?.GetVisualDescendants() ?? [])
        {
            if (branch is Avalonia.Controls.Shapes.Path path)
            {
                path[!Avalonia.Controls.Shapes.Path.StrokeProperty] = new DynamicResourceExtension(strokeKey);
            }
        }
    }

    private void SyncPageInput()
    {
        var input = this.FindControl<TextBox>("PageInput");
        if (input?.Text != _currentPage.ToString())
        {
            input!.Text = $"{_currentPage}";
        }
        SyncPageNav();
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


public record DemoServerRow(string Region, int Instances, string Status);

public class DemoServerRows : System.Collections.ObjectModel.ObservableCollection<DemoServerRow>
{
    public DemoServerRows()
    {
        Add(new DemoServerRow("us-east-1", 12, "healthy"));
        Add(new DemoServerRow("eu-central-1", 6, "healthy"));
        Add(new DemoServerRow("ap-south-1", 4, "paused"));
        Add(new DemoServerRow("us-west-2", 9, "healthy"));
    }
}
