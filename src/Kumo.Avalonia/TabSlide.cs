using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace KumoThemeSupport;

/// <summary>
/// Slides the segmented TabControl active indicator between tabs
/// (Kumo tabs use a 200ms translate transition on the active surface).
/// </summary>
public class TabSlide
{
    private static readonly ConditionalWeakTable<TabControl, Box> States = new();

    private sealed class Box
    {
        public TabItem? Previous;
    }

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TabSlide, TabControl, bool>("IsEnabled", false);

    public static void SetIsEnabled(TabControl control, bool value) => control.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(TabControl control) => control.GetValue(IsEnabledProperty);

    static TabSlide()
    {
        IsEnabledProperty.Changed.AddClassHandler<TabControl>((control, e) =>
        {
            if (e.NewValue is true)
            {
                control.SelectionChanged += OnSelectionChanged;
                Queue(control, control.SelectedItem as TabItem, initial: true);
            }
            else
            {
                control.SelectionChanged -= OnSelectionChanged;
            }
        });
    }

    private static void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TabControl control)
        {
            Queue(control, control.SelectedItem as TabItem, initial: false);
        }
    }

    private static void Queue(TabControl control, TabItem? tab, bool initial)
    {
        if (tab is null)
        {
            return;
        }

        var state = States.GetOrCreateValue(control);
        var previous = state.Previous;
        state.Previous = tab;
        SlideWhenReady(tab, previous, initial);
    }

    private static void SlideWhenReady(TabItem tab, TabItem? previous, bool initial, int attempt = 0)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var border = tab.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(b => b.Name == "PART_LayoutRoot");
            if (border is null)
            {
                if (attempt < 20)
                {
                    SlideWhenReady(tab, previous, initial, attempt + 1);
                }

                return;
            }

            EnsureTransitions(border);

            var dx = 0.0;
            if (!initial && previous is not null && !ReferenceEquals(previous, tab))
            {
                var parent = tab.GetVisualParent();
                if (parent is not null && ReferenceEquals(previous.GetVisualParent(), parent))
                {
                    dx = previous.Bounds.X - tab.Bounds.X;
                }
            }

            var start = TransformOperations.Parse(
                dx != 0 ? $"translateX({dx}px) scale(1)" : "translateX(0px) scale(0.9)");
            border.RenderTransform = start;
            Dispatcher.UIThread.Post(() =>
                border.RenderTransform = TransformOperations.Parse("translateX(0px) scale(1)"),
                DispatcherPriority.Background);
        }, DispatcherPriority.Loaded);
    }

    private static void EnsureTransitions(Border border)
    {
        var hasTransformTransition = border.Transitions is Transitions existing &&
            existing.OfType<TransformOperationsTransition>()
                .Any(t => t.Property == Visual.RenderTransformProperty);
        if (hasTransformTransition)
        {
            return;
        }

        var list = new Transitions();
        if (border.Transitions is Transitions current)
        {
            foreach (var transition in current)
            {
                list.Add(transition);
            }
        }

        list.Add(new TransformOperationsTransition
        {
            Property = Visual.RenderTransformProperty,
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new CubicEaseOut(),
        });
        border.Transitions = list;
    }
}
