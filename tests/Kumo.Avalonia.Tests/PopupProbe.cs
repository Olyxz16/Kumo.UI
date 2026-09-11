using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using KumoThemeSupport.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kumo.Demo;
using Xunit;

namespace Kumo.Avalonia.Tests;

public class PopupProbe
{
    [AvaloniaFact]
    public void Toast_manager_renders_cards()
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var wnm = window.GetVisualDescendants().OfType<WindowNotificationManager>().FirstOrDefault();
        Assert.NotNull(wnm);
        wnm!.Show(new TextBlock { Text = "hi" }, NotificationType.Information, TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();
        var screens = window.GetVisualDescendants().OfType<NotificationCard>().ToList();
        Assert.True(screens.Count > 0, "toast card not rendered");
        var items = wnm.GetVisualDescendants().OfType<Panel>()
            .First(p => p.Name == "PART_Items");
        Assert.IsType<ToastDeck>(items);
        Assert.Equal(1, items.Children.OfType<NotificationCard>().Count());
    }

    [AvaloniaFact]
    public void Menu_flyout_closes_on_outside_click()
    {
        var window = new Window();
        var target = new Button { Content = "Open" };
        var flyout = new MenuFlyout { Items = { new MenuItem { Header = "Deploy" } } };
        target.Flyout = flyout;
        window.Content = target;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        flyout.ShowAt(target);
        Dispatcher.UIThread.RunJobs();
        Assert.True(flyout.IsOpen, "flyout should be open");

        window.MouseDown(new Point(10, 300), MouseButton.Left);
        window.MouseUp(new Point(10, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.False(flyout.IsOpen, "flyout should close on outside click");
    }

    [AvaloniaFact]
    public void Context_menu_opens_and_closes()
    {
        var window = new Window();
        var target = new Button { Content = "MENU" };
        var menu = new ContextMenu { Items = { new MenuItem { Header = "Copy" } } };
        target.ContextMenu = menu;
        window.Content = target;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        target.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        Assert.True(menu.IsOpen, "context menu should open");

        window.MouseDown(new Point(10, 300), MouseButton.Left);
        window.MouseUp(new Point(10, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        Assert.False(menu.IsOpen, "context menu should close on outside click");
    }

    [AvaloniaFact]
    public void Menu_flyout_surface_applies_vertical_padding()
    {
        var window = new Window();
        var target = new Button { Content = "Open" };
        target.Flyout = new MenuFlyout { Items = { new MenuItem { Header = "Deploy" } } };
        window.Content = target;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        target.Flyout!.ShowAt(target);
        Dispatcher.UIThread.RunJobs();

        var presenter = window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().First();
        var border = presenter.GetVisualDescendants().OfType<Border>()
            .First(b => b.Name == "LayoutRoot");
        Assert.Equal(new Thickness(0, 6, 0, 6), border.Padding);
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
