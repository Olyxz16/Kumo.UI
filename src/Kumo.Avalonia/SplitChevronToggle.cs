using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace KumoThemeSupport;

/// <summary>
/// ToggleSplitButton behavior helper: the chevron (secondary) half normally
/// opens the flyout, which leaves a flyout-less toggle with a dead half.
/// Opt the chevron into toggling <see cref="ToggleSplitButton.IsChecked"/>
/// instead of doing nothing.
/// </summary>
public class SplitChevronToggle
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<SplitChevronToggle, ToggleSplitButton, bool>("IsEnabled", false);

    public static void SetIsEnabled(ToggleSplitButton control, bool value) => control.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(ToggleSplitButton control) => control.GetValue(IsEnabledProperty);

    static SplitChevronToggle()
    {
        IsEnabledProperty.Changed.AddClassHandler<ToggleSplitButton>((control, e) =>
        {
            if (e.NewValue is not true)
            {
                return;
            }
            Attach(control);
        });
    }

    private static void Attach(ToggleSplitButton toggle)
    {
        if (toggle.IsLoaded)
        {
            Hook(toggle, EventArgs.Empty);
        }
        toggle.Loaded += Hook;
    }

    private static void Hook(object? sender, EventArgs e)
    {
        var toggle = (ToggleSplitButton)sender!;
        var secondary = toggle.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Name == "PART_SecondaryButton");
        if (secondary is not null)
        {
            // The base control routes clicks through its parts and swallows
            // handled ones, so observe Click at the root with
            // handledEventsToo and check the source is the chevron half.
            toggle.AddHandler(
                global::Avalonia.Controls.Button.ClickEvent,
                (_, ev) =>
                {
                    if (ev.Source is Button b && b.Name == "PART_SecondaryButton")
                    {
                        toggle.IsChecked = !toggle.IsChecked;
                    }
                },
                global::Avalonia.Interactivity.RoutingStrategies.Bubble,
                handledEventsToo: true);
        }
        else
        {
            System.Console.Error.WriteLine("KUMO hook found nothing");
        }
    }
}
