using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace KumoThemeSupport;

/// <summary>
/// Slides the native segmented-tab indicator pill (PART_Indicator) between
/// tabs: the pill positions itself over the selected tab and, on selection
/// change, animates 200ms translate + scaleX between the source and target
/// geometry (matching upstream `transition-all duration-200`), with a
/// scale-0.9 pop-in on first render.
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
                control.TemplateApplied += OnTemplateApplied;
            }
            else
            {
                control.SelectionChanged -= OnSelectionChanged;
                control.TemplateApplied -= OnTemplateApplied;
                States.Remove(control);
            }
        });
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        var control = (TabControl)sender!;
        Queue(control, control.SelectedItem as TabItem, initial: true);
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
        SlideWhenReady(control, tab, previous, initial);
    }

    private static void SlideWhenReady(TabControl control, TabItem tab, TabItem? previous, bool initial, int attempt = 0)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var host = control.GetVisualDescendants().OfType<Panel>()
                .FirstOrDefault(p => p.Name == "PART_TabsHost");
            var indicator = control.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(b => b.Name == "PART_Indicator");
            var selectedTab = control.SelectedItem as TabItem;
            if (host is null || indicator is null || selectedTab is null)
            {
                if (attempt < 20)
                {
                    SlideWhenReady(control, tab, previous, initial, attempt + 1);
                }

                return;
            }

            var target = Rect(selectedTab, host);
            var source = previous is not null && !ReferenceEquals(previous, tab)
                ? Rect(previous, host)
                : target;

            indicator.IsVisible = target is { Width: > 0 };
            indicator.Width = target.Width;
            indicator.Height = target.Height;
            Canvas.SetLeft(indicator, target.X);
            Canvas.SetTop(indicator, target.Y);

            var dx = source.X - target.X;
            var scaleY = target.Height > 0 ? source.Height / target.Height : 1.0;
            var scaleX = target.Width > 0 ? source.Width / target.Width : 1.0;

            var start = dx != 0
                ? TransformOperations.Parse($"translateX({dx}px) scaleX({scaleX:0.####}) scaleY({ScaleClamp(scaleY):0.####})")
                : TransformOperations.Parse("scale(0.9)");
            var end = TransformOperations.Parse("translateX(0px) scaleX(1) scaleY(1)");

            var transitions = EnsureTransitions(indicator);
            indicator.Transitions = null;
            indicator.RenderTransform = start;
            indicator.Transitions = transitions;
            Dispatcher.UIThread.Post(() => indicator.RenderTransform = end, DispatcherPriority.Background);
        }, DispatcherPriority.Loaded);
    }

    private static double ScaleClamp(double v) => double.IsFinite(v) ? v : 1.0;

    private static Rect Rect(TabItem item, Visual host)
    {
        var origin = item.TranslatePoint(new Point(0, 0), host) ?? new Point(0, 0);
        return new Rect(origin, new Size(Math.Max(0, item.Bounds.Width), item.Bounds.Height));
    }

    private static Transitions EnsureTransitions(Avalonia.Animation.Animatable target)
    {
        var list = new Transitions();
        if (target.Transitions is Transitions current)
        {
            foreach (var transition in current)
            {
                list.Add(transition);
            }
        }

        if (list.OfType<TransformOperationsTransition>()
                .All(t => t.Property != Visual.RenderTransformProperty))
        {
            list.Add(new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = new CubicEaseOut(),
            });
        }

        return list;
    }
}
