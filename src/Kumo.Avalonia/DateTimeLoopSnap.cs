using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace KumoThemeSupport;

/// <summary>
/// Snap behavior for date/time picker loop columns (a DateTimePickerPanel
/// wrapped in a ScrollViewer). Stock scrolling can rest between two values,
/// where the half-shown pair does not map to a real selection. The attached
/// behavior:
/// - clamps when a scroll session demonstrably ends, never while it is in
///   motion or grabbed: pointer grab end (all pointer types, registered with
///   handledEventsToo so selection handlers cannot mask it), touch/pen
///   inertia (clamped only when the scroll gesture actually ends, not at
///   finger lift), a mouse wheel notch pause, or the strip resting after a
///   wheel stream — a trackpad that stops streaming deltas while the fingers
///   still rest on it must never clamp, so nothing is scheduled on the wheel
///   stream itself; the clamp waits for the next decisive event,
/// - adds mouse click-and-drag to the loop — the ScrollGestureRecognizer
///   only drives touch/pen, so with a mouse the strip would otherwise only
///   respond to the wheel,
/// - taps: only the centered (selected) value is clickable for proceeding —
///   tapping it commits the picker; tapping any other value glides it to
///   the center and selects it without committing,
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
    private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds(16);
    private const double DragThreshold = 8;
    private const int MaxFollowFrames = 24;

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, bool>("IsEnabled", false);

    public static void SetIsEnabled(ScrollViewer obj, bool value) => obj.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(ScrollViewer obj) => obj.GetValue(IsEnabledProperty);

    private static readonly AttachedProperty<object?> AnimProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, object?>("Anim");

    private static readonly AttachedProperty<bool> PressedProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, bool>("Pressed");

    // a touch/pen pan (ScrollGestureStarted..Ended, inertia included)
    private static readonly AttachedProperty<bool> SlidingProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, bool>("Sliding");

    // most recent non-animated settled offset (source for tap animations)
    private static readonly AttachedProperty<object?> RestOffsetProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, object?>("RestOffset");

    // a tapped (clicked) item is driving the current animation; the settle
    // confirms the picker
    private static readonly AttachedProperty<bool> TapPendingProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, bool>("TapPending");

    // mouse click-drag state
    private static readonly AttachedProperty<object?> DragPointProperty =
        AvaloniaProperty.RegisterAttached<DateTimeLoopSnap, ScrollViewer, object?>("DragPoint");

    static DateTimeLoopSnap()
    {
        // taps land on the panel's items BEFORE the panel's own tap handler
        // teleports the strip: class handlers run ahead of instance handlers,
        // so reading the offset here gives the true pre-tap resting position
        InputElement.TappedEvent.AddClassHandler<DateTimePickerPanel>(OnPanelTap);
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

                // grab anywhere on the strip cancels any in-flight glide;
                // nothing may move under a held pointer, nothing may clamp
                // while the wheel/touch stream is being consumed
                sv.SetValue(PressedProperty, true);
                CancelGlide(sv);

            // mouse click-and-drag: the recognizer only drives touch/pen;
            // skip presses that are already owned (scrollbars etc.). Capture
            // starts only once the pointer actually drags past the tap
            // threshold, so plain presses/taps still reach the inner items
            if (point.Pointer.Type == PointerType.Mouse &&
                point.Properties.IsLeftButtonPressed &&
                !a.Handled &&
                a.Source is not ScrollBar and not Thumb)
            {
                sv.SetValue(DragPointProperty, point.Position);
            }
            }
            void OnReleased(object? s, PointerReleasedEventArgs a)
            {
                sv.SetValue(PressedProperty, false);
                FinishDrag(sv);
                // touch/pen releases keep scrolling by inertia; the gesture
                // end event fires the clamp for those, a mouse release is the
                // drag end here and clamps now. A pending tap glide owns the
                // strip and commits on settle, so the release stays passive.
                if (a.Pointer.Type == PointerType.Mouse && !sv.GetValue(TapPendingProperty))
                {
                    EndSession(sv);
                }
            }
            void OnScrollGesture(object? s, ScrollGestureEventArgs a)
            {
                // a real scroll session (touch/pen pan): deltas stop once
                // the pan (incl. inertia) ends with the ended-event; no
                // clamping rides on pointer release visibility here
                sv.SetValue(SlidingProperty, true);
                CancelGlide(sv);
            }
            void OnScrollGestureEnded(object? s, ScrollGestureEndedEventArgs a)
            {
                sv.SetValue(SlidingProperty, false);
                EndSession(sv);
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
                    EndSession(sv);
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

        // capture starts at the drag threshold so taps and clicks on inner
        // items keep flowing without our capture in the way
        if (point.Pointer.Captured != sv)
        {
            if (Math.Abs(current.Y - start.Y) < DragThreshold)
            {
                return;
            }
            point.Pointer.Capture(sv);
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
                // an interrupted tap must still commit its value
                if (sv.GetValue(TapPendingProperty))
                {
                    CommitTap(sv);
                }
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
        if (sv.GetValue(AnimProperty) is null)
        {
            // settled strip position (debug/inspection aid)
            sv.SetValue(RestOffsetProperty, sv.Content is DateTimePickerPanel p ? p.Offset : null);
        }
        // wheel/trackpad deltas intentionally do not schedule a clamp: a
        // trackpad that rests its fingers on the pad stops streaming deltas
        // with no end-of-scroll event at app level, so any timer would
        // clamp mid-touch. The clamp fires on the next decisive event
        // (release, gesture end, tap settle).
    }

    /// <summary>
    /// A tapped (clicked) item: no teleport — the strip glides to the tapped
    /// item as if scrolled there, and when it settles the picker commits
    /// (accept). The panel's own tap handler (which sets the selection and
    /// canonical offset directly) is suppressed by marking the event handled;
    /// we drive selection + motion + commit ourselves. Class-handler level
    /// because the pre-tap offset must be read before the panel's handler.
    /// </summary>
    private static void OnPanelTap(DateTimePickerPanel panel, TappedEventArgs e)
    {
        PanelTapProbeCount++;
        var itemHeight = panel.ItemHeight;
        if (itemHeight <= 0) return;
        if (e.Source is not Visual source) return;
        var walk = source;
        while (walk is not null && walk is not ListBoxItem)
        {
            walk = walk.GetVisualParent();
        }
        if (walk is not ListBoxItem clickedItem || clickedItem.Tag is not int value)
        {
            return;
        }

        var sv = panel.GetVisualAncestors().OfType<ScrollViewer>()
            .FirstOrDefault(p => GetIsEnabled(p));
        if (sv is null)
        {
            return; // no snap behavior: leave the panel's own handling alone
        }
        e.Handled = true; // suppress the panel's own teleporting tap handler

        var first = panel.MinimumValue;
        var index = (value - first) / panel.Increment;
        var y0 = panel.Offset.Y;
        CancelGlide(sv);

        double target;
        if (panel.ShouldLoop)
        {
            var setHeight = panel.Extent.Height / 100.0;
            if (setHeight <= 0)
            {
                return;
            }
            target = index * itemHeight + setHeight * Math.Round((y0 - index * itemHeight) / setHeight);
        }
        else
        {
            target = index * itemHeight;
        }

        // selection/highlight to the tapped value at once, then glide
        SyncSelection(panel, index, new Point(0, y0));

        if (Math.Abs(target - y0) <= 0.01)
        {
            // only the centered (selected) value is clickable for proceeding:
            // tapping it commits and closes; or when the strip rests exactly
            // on the tapped item already, it IS the centered value
            CommitTap(sv);
            return;
        }
        // any other value: glide it to the center and select it, but do not
        // commit — committing is reserved for tapping the centered value
        var anim = new SnapAnimation(sv, panel, target);
        sv.SetValue(AnimProperty, anim);
        anim.Start();
    }

    private static void CommitTap(ScrollViewer sv)
    {
        sv.SetValue(TapPendingProperty, false);
        TapCommitProbeCount++;
        var presenter = sv.GetVisualAncestors().OfType<TemplatedControl>()
            .FirstOrDefault(a => a is DatePickerPresenter or TimePickerPresenter);
        CommitPresenterProbeCount += presenter is null ? 0 : 1;
        if (presenter is null)
        {
            return;
        }
        var accept = presenter.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "PART_AcceptButton");
        CommitButtonProbeCount += accept is null ? 0 : 1;
        if (accept is { } button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }

    private static void CancelGlide(ScrollViewer sv)
    {
        if (sv.GetValue(AnimProperty) is SnapAnimation anim)
        {
            anim.Stop();
            sv.SetValue(AnimProperty, null);
        }
    }

    private static void EndSession(ScrollViewer sv) => SnapNow(sv);

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
            if (sv.GetValue(TapPendingProperty))
            {
                CommitTap(sv);
            }
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
            if (sv.GetValue(TapPendingProperty))
            {
                CommitTap(sv);
            }
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
        private readonly bool _committedOnEnd;
        private int _frames;
        public double LastWritten { get; private set; }

        public SnapAnimation(ScrollViewer owner, DateTimePickerPanel panel, double to, bool committedOnEnd = false)
            : base(FrameTime, DispatcherPriority.Background, (self, _) => ((SnapAnimation)self!).TickNext())
        {
            _owner = owner;
            _panel = panel;
            _to = to;
            _committedOnEnd = committedOnEnd;
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
                var committed = _committedOnEnd;
                _owner.SetValue(AnimProperty, null);
                if (committed)
                {
                    CommitTap(_owner);
                }
                return;
            }
            _panel.Offset = new Vector(0, next);
            LastWritten = next;
        }

        private static double MaxStep => 6; // never yank more than 6px per frame

        public void TickProbe() => TickNext();
    }

    /// Debug/release-probe accessors used by the headless tests.
    public static object? AnimPropertyProbe(ScrollViewer sv) => sv.GetValue(AnimProperty);
    public static bool TapPendingProbe(ScrollViewer sv) => sv.GetValue(TapPendingProperty);
    public static double HourItemHeight(ScrollViewer sv) => (sv.Content as DateTimePickerPanel)?.ItemHeight ?? 0;

    // deterministic-machinery counters used by the headless tap probes
    public static int TapCommitProbeCount;
    public static int PanelTapProbeCount;
    public static int CommitPresenterProbeCount;
    public static int CommitButtonProbeCount;

    /// <summary>
    /// Headless test hook: run one animation frame; if the target has been
    /// reached this finalizes with selection sync and (for taps) commit.
    /// </summary>
    public static void AnimFinalizeProbe(ScrollViewer sv)
    {
        (sv.GetValue(AnimProperty) as SnapAnimation)?.TickProbe();
    }

    /// <summary>
    /// Point the panel's selection at the value the strip settles on, so the
    /// highlight matches the clamp target rather than staying on the item
    /// the offset floor-mapping left highlighted. SelectedValue is the
    /// highlight driver (UpdateItems marks item.IsSelected off it); setting
    /// it rewrites the loop offset to its canonical mid-wheel position, so
    /// the settle position is restored right after — synchronously, no
    /// render in between.
    /// </summary>
    internal static void SyncSelection(DateTimePickerPanel panel, int index, Point? restorePosition = null)
    {
        var first = panel.MinimumValue;
        var items = (panel.MaximumValue - first) / panel.Increment + 1;
        index = ((index % items) + items) % items;
        var value = first + index * panel.Increment;
        if (panel.SelectedValue == value && !restorePosition.HasValue)
        {
            return;
        }
        var restore = restorePosition ?? panel.Offset;
        panel.SelectedValue = value;
        panel.Offset = restore;
    }
}
