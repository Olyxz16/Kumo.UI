using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Kumo.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
}
