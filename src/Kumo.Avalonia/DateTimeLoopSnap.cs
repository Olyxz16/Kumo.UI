using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Threading;

namespace KumoThemeSupport;

/// <summary>
/// Snap behavior for date/time picker loop columns (a DateTimePickerPanel
/// wrapped in a ScrollViewer). Stock scrolling can rest between two values,
/// where the half-shown pair does not map to a real selection. The attached
/// behavior:
/// - clamps when the strip session ends, never while it is in motion or
///   grabbed: pointer grab (all pointer types, registered with
///   handledEventsToo so selection handlers cannot mask it), touch/pen
///   inertia (clamped only when the scroll gesture actually ends, not at
///   finger lift), or a long wheel pause (500ms without offset change and
///   a strip that has actually been quiet — trackpad drivers batch momentum
///   deltas, so the tick re-checks liveness and postpones before clamping),
/// - adds mouse click-and-drag to the loop — the ScrollGestureRecognizer
///   only drives touch/pen, so with a mouse the strip would otherwise only
///   respond to the wheel,
/// - cancels an in-flight clamp the moment anything else moves the strip
///   (grab, wheel, drag): while the glide runs it does not own the strip.
/// The clamp glides with an exponential follow (no overshoot), reading from
/// the panel's own offset — the looping panel rewrites its offset at the
/// wrap edges without informing the ScrollViewer, so per-frame absolute
/// interpolation would read a desynced value.
/// Opt-in from the theme templates: ksup:DateTimeLoopSnap.IsEnabled="True".
/// </summary>
public class DateTimeLoopSnap
{
    private static readonly TimeSpan WheelIdle = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds(16);
    private const int MaxFollowFrames = 24;

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, bool>("IsEnabled", false);

    public static void SetIsEnabled(ScrollViewer obj, bool value) => obj.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(ScrollViewer obj) => obj.GetValue(IsEnabledProperty);

    private static readonly AttachedProperty<object?> IdleTimerProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, object?>("IdleTimer");

    private static readonly AttachedProperty<object?> AnimProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, object?>("Anim");

    private static readonly AttachedProperty<bool> PressedProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, bool>("Pressed");

    // a touch/pen pan (ScrollGestureStarted..Ended, inertia included)
    private static readonly AttachedProperty<bool> SlidingProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, bool>("Sliding");

    // last real offset change (wheel/trackpad glide liveness)
    private static readonly AttachedProperty<object?> MovedAtProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, object?>("MovedAt");

    // mouse click-drag state
    private static readonly AttachedProperty<object?> DragPointProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, object?>("DragPoint");

    static DateTimeLoopSnap()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>((sv, e) =>
        {
            if (e.NewValue is not true)
            {
                return;
            }
            // handledEventsToo: inner items/consume handlers must not stop
            // the press/release from reaching the grab guard
            void OnPressed(object? s, PointerPressedEventArgs a)
            {
                var point = a.GetCurrentPoint(sv);

                // grab anywhere on the strip cancels any in-flight glide and
                // the pending wheel-pause timer; nothing may move under a
                // held pointer
                sv.SetValue(PressedProperty, true);
                CancelGlide(sv);
                (sv.GetValue(IdleTimerProperty) as DispatcherTimer)?.Stop();

                // mouse click-and-drag: the recognizer only drives touch/pen;
                // skip presses that are already owned (scrollbars etc.)
                if (point.Pointer.Type == PointerType.Mouse &&
                    point.Properties.IsLeftButtonPressed &&
                    !a.Handled &&
                    a.Source is not ScrollBar and not Thumb)
                {
                    point.Pointer.Capture(sv);
                    sv.SetValue(DragPointProperty, point.Position);
                }
            }
            void OnReleased(object? s, PointerReleasedEventArgs a)
            {
                sv.SetValue(PressedProperty, false);
                FinishDrag(sv);
                // touch/pen releases keep scrolling by inertia; the gesture
                // end event fires the clamp for those, a mouse release is the
                // drag end here and clamps now
                if (a.Pointer.Type == PointerType.Mouse)
                {
                    Arm(sv, immediate: true);
                }
            }
            void OnScrollGesture(object? s, ScrollGestureEventArgs a)
            {
                // a real scroll session (touch/pen pan): deltas stop once
                // the pan (incl. inertia) ends with the ended-event; no
                // clamping rides on pointer release visibility here
                sv.SetValue(SlidingProperty, true);
                CancelGlide(sv);
                (sv.GetValue(IdleTimerProperty) as DispatcherTimer)?.Stop();
            }
            void OnScrollGestureEnded(object? s, ScrollGestureEndedEventArgs a)
            {
                sv.SetValue(SlidingProperty, false);
                Arm(sv, immediate: true);
            }
            void OnCaptureLost(object? s, PointerCaptureLostEventArgs a)
            {
                // capture ends happen at finger lift or capture transfer; if
                // a scroll session is still running, hold here (the gesture
                // ended event will clamp)
                sv.SetValue(PressedProperty, false);
                FinishDrag(sv);
                if (!sv.GetValue(SlidingProperty))
                {
                    Arm(sv);
                }
            }
            sv.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            sv.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            sv.AddHandler(InputElement.PointerMovedEvent, OnDragMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            sv.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Direct, handledEventsToo: true);
            sv.AddHandler(ScrollViewer.ScrollGestureEvent, OnScrollGesture, RoutingStrategies.Bubble, handledEventsToo: true);
            sv.AddHandler(ScrollViewer.ScrollGestureEndedEvent, OnScrollGestureEnded, RoutingStrategies.Bubble, handledEventsToo: true);
            sv.ScrollChanged += OnScrollChanged;
            sv.DetachedFromVisualTree += (_, _) =>
            {
                (sv.GetValue(AnimProperty) as DispatcherTimer)?.Stop();
                (sv.GetValue(IdleTimerProperty) as DispatcherTimer)?.Stop();
            };
        });
    }

    private static void OnDragMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        if (sv.GetValue(DragPointProperty) is not Point start) return;
        var point = e.GetCurrentPoint(sv);
        if (point.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        var current = point.Position;
        if (Math.Abs(current.Y - start.Y) < 0.01)
        {
            return;
        }
        sv.Offset = new Vector(0, sv.Offset.Y - (current.Y - start.Y));
        sv.SetValue(DragPointProperty, point.Position);
    }

    private static void FinishDrag(ScrollViewer sv)
    {
        if (sv.GetValue(DragPointProperty) is not null)
        {
            sv.SetValue(DragPointProperty, null);
        }
    }

    private static void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        // a glide in flight: it does not own the strip — if something else
        // touched the offset (wheel, drag), cancel it; its own frame writes
        // match the recorded value exactly
        if (sv.GetValue(AnimProperty) is SnapAnimation anim)
        {
            if (e.OffsetDelta.Y != 0 && Math.Abs(anim.LastWritten - sv.Offset.Y) > 0.25)
            {
                CancelGlide(sv);
            }
            else
            {
                return;
            }
        }
        if (sv.GetValue(PressedProperty) || sv.GetValue(SlidingProperty))
        {
            // grabbed strip or an in-progress pan/inertia session: no
            // clamping until the session ends
            return;
        }
        if (e.OffsetDelta.Y != 0)
        {
            // scroll-in-progress: stamp liveness and keep the pause armed;
            // nothing clamps while deltas keep arriving
            sv.SetValue(MovedAtProperty, DateTime.UtcNow);
        }
        Arm(sv);
    }

    /// <summary>
    /// Scroll-end detector for the wheel/trackpad stream. A wheel has no
    /// press or release on Linux, so "still scrolling" vs "scroll ended" can
    /// only be read from the delta stream: no offset change for the pause
    /// interval means the scroll ended. If the pointer is pressed (touchpad
    /// click held), the settle holds until release re-arms it.
    /// </summary>
    private static void OnIdleTick(ScrollViewer sv)
    {
        if (sv.GetValue(MovedAtProperty) is DateTime movedAt &&
            DateTime.UtcNow - movedAt < WheelIdle)
        {
            return; // still scrolling
        }
        SnapNow(sv);
    }

    private static void CancelGlide(ScrollViewer sv)
    {
        if (sv.GetValue(AnimProperty) is SnapAnimation anim)
        {
            anim.Stop();
            sv.SetValue(AnimProperty, null);
        }
    }

    private static void Arm(ScrollViewer sv, bool immediate = false)
    {
        if (immediate)
        {
            SnapNow(sv);
            return;
        }
        if (sv.GetValue(IdleTimerProperty) is not DispatcherTimer idle)
        {
            idle = new DispatcherTimer(WheelIdle, DispatcherPriority.Background, (_, _) => OnIdleTick(sv));
            sv.SetValue(IdleTimerProperty, idle);
        }
        // a postponement may have left the interval short; new input re-arms
        // with the full pause instead of inheriting the postpone cadence
        idle.Interval = WheelIdle;
        idle.Stop();
        idle.Start();
    }

    /// <summary>
    /// Move <paramref name="sv"/> onto the nearest loop item boundary. With
    /// <paramref name="animate"/> the offset glides through an exponential
    /// follow; with false it clamps instantly (tests).
    /// </summary>
    public static void SnapNow(ScrollViewer sv, bool animate = true)
    {
        if (sv.Content is not DateTimePickerPanel panel) return;
        var itemHeight = panel.ItemHeight;
        if (itemHeight <= 0) return;
        if (sv.GetValue(PressedProperty) || sv.GetValue(SlidingProperty)) return; // grabbed: never clamp

        // the panel's own offset is the truth (it wraps in place at the loop
        // edges); sv.Offset can be desynced from it
        var y = panel.Offset.Y;
        var target = (int)Math.Round(y / itemHeight);
        var snapped = target * itemHeight;
        var delta = snapped - y;
        if (Math.Abs(delta) <= 0.01)
        {
            SyncSelection(panel, target);
            return;
        }

        CancelGlide(sv);

        if (!animate)
        {
            panel.Offset = new Vector(0, snapped);
            SyncSelection(panel, target);
            // resync the ScrollViewer's own copy so later clamp math starts
            // from the true position
            sv.Offset = panel.Offset;
            return;
        }

        var anim = new SnapAnimation(sv, panel, snapped);
        sv.SetValue(AnimProperty, anim);
        anim.Start();
    }

    private class SnapAnimation : DispatcherTimer
    {
        private readonly ScrollViewer _owner;
        private readonly DateTimePickerPanel _panel;
        private readonly double _to;
        private int _frames;
        public double LastWritten { get; private set; }

        public SnapAnimation(ScrollViewer owner, DateTimePickerPanel panel, double to)
            : base(FrameTime, DispatcherPriority.Background, (self, _) => ((SnapAnimation)self!).TickNext())
        {
            _owner = owner;
            _panel = panel;
            _to = to;
            LastWritten = panel.Offset.Y;
        }

        private void TickNext()
        {
            var y = _panel.Offset.Y;
            var delta = _to - y;
            var steps = Math.Abs(delta / MaxStep) + 1;
            var k = 1 - Math.Pow(0.55, 1.0 / Math.Max(2, steps)); // cover in roughly `steps` frames
            var next = y + delta * k;

            if (Math.Abs(_to - next) < 0.25 || ++_frames > MaxFollowFrames)
            {
                // write the settled offset and record it BEFORE the change
                // events fire, so the ScrollChanged handler must not mistake
                // the finalizing write for foreign input (it would cancel the
                // glide attendance and re-arm an extra clamp)
                LastWritten = _to;
                _panel.Offset = new Vector(0, _to);
                Stop();
                // the highlighted value must be the one the strip settles on,
                // not the one the panel happens to have marked
                SyncSelection(_panel, (int)Math.Round(_to / _panel.ItemHeight));
                // resync the ScrollViewer copy so later clamp math is sane
                _owner.Offset = _panel.Offset;
                _owner.SetValue(AnimProperty, null);
                return;
            }
            _panel.Offset = new Vector(0, next);
            LastWritten = next;
        }

        private static double MaxStep => 6; // never yank more than 6px per frame
    }

    /// Debug/release-probe accessors used by the headless tests.
    public static object? AnimPropertyProbe(ScrollViewer sv) => sv.GetValue(AnimProperty);
    public static double HourItemHeight(ScrollViewer sv) => (sv.Content as DateTimePickerPanel)?.ItemHeight ?? 0;

    /// <summary>
    /// Point the panel's selection at the value the strip settles on, so the
    /// highlight matches the clamp target rather than staying on the item
    /// the offset floor-mapping left highlighted. SelectedValue is the
    /// highlight driver (UpdateItems marks item.IsSelected off it); setting
    /// it rewrites the loop offset to its canonical mid-wheel position, so
    /// the settle position is restored right after — synchronously, no
    /// render in between.
    /// </summary>
    internal static void SyncSelection(DateTimePickerPanel panel, int index)
    {
        var first = panel.MinimumValue;
        var items = (panel.MaximumValue - first) / panel.Increment + 1;
        index = ((index % items) + items) % items;
        var value = first + index * panel.Increment;
        if (panel.SelectedValue == value)
        {
            return;
        }
        var restore = panel.Offset;
        panel.SelectedValue = value;
        panel.Offset = restore;
    }
}
