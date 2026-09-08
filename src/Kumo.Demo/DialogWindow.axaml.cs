using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kumo.Demo;

public partial class DialogWindow : Window
{
    public DialogWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
