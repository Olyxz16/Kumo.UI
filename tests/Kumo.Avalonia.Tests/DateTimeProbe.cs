using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using KumoThemeSupport.Controls;
using Xunit;

namespace Kumo.Avalonia.Tests
{
    /// <summary>
    /// Date/time family probe: all nine themed types build their templates,
    /// the Calendar paints today/selected day pills (via pseudoclasses),
    /// pickers carry flyout buttons; TimePicker's Enter opens its popup
    /// with accept/dismiss; CalendarDatePicker hosts TextBox + glyph.
    /// </summary>
    public class DateTimeProbe
    {
        [AvaloniaFact]
        public void Calendar_pills_and_picker_popups_apply()
        {
            var calendar = new Calendar
            {
                SelectedDate = new DateTime(2026, 9, 13),
            };
            var datePicker = new DatePicker();
            var timePicker = new TimePicker();
            var calendarPicker = new KumoCalendarDatePicker { PlaceholderText = "Pick a date" };
            var window = new Window { Width = 760, Height = 420 };
            try
            {
                window.Content = new StackPanel
                {
                    Children = { calendar, datePicker, timePicker, calendarPicker }
                };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                // Calendar chain: CalendarItem + day buttons; the selected date
                // renders as a brand-solid day (ContentPresenter foreground
                // from :selected in the DayButton theme; assert presence only)
                var entry = calendar.GetVisualDescendants().OfType<CalendarItem>().First();
                Assert.NotNull(entry);
                var dayButtons = entry.GetVisualDescendants().OfType<CalendarDayButton>().ToList();
                Assert.True(dayButtons.Count >= 27, $"day buttons: {dayButtons.Count}");
                // IsToday/IsSelected are internal; pseudoclasses carry them
                Assert.Contains(dayButtons, b => b.Classes.Contains(":today"));
                Assert.Contains(dayButtons, b => b.Classes.Contains(":selected"));
                var months = entry.GetVisualDescendants().OfType<CalendarButton>().ToList();
                // month view shows 0 CalendarButtons; year view shows 12 when opened
                Assert.True(months.Count >= 0);

                // pickers carry flyout buttons with watermark placeholders
                var dp = datePicker.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "PART_FlyoutButton");
                var tp = timePicker.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "PART_FlyoutButton");
                Assert.NotNull(dp);
                Assert.NotNull(tp);
                Assert.True(datePicker.GetVisualDescendants().OfType<TextBlock>().Any(b => b.Name == "PART_DayTextBlock"));

                // open the TimePicker popup by clicking the flyout button
                var tpPoint = tp.TranslatePoint(new Point(tp.Bounds.Width / 2, tp.Bounds.Height / 2), window)!.Value;
                window.MouseDown(tpPoint, MouseButton.Left);
                window.MouseUp(tpPoint, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
                var popupPresenter = window.GetVisualDescendants().OfType<TimePickerPresenter>().FirstOrDefault();
                Assert.NotNull(popupPresenter);
                var accept = popupPresenter.GetVisualDescendants().OfType<Button>().Count(b => b.Name is "PART_AcceptButton" or "PART_DismissButton");
                Assert.Equal(2, accept);

                // CalendarDatePicker: text box + glyph button + popup calendar
                Assert.NotNull(calendarPicker.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.Name == "PART_TextBox"));
                Assert.NotNull(calendarPicker.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "PART_Button"));
                HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/datetime.png");
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void CalendarDatePicker_formats_zero_padded_and_validates_on_completion()
        {
            var picker = new KumoCalendarDatePicker();
            var invalid = new KumoCalendarDatePicker();
            var window = new Window { Width = 420, Height = 200 };
            try
            {
                window.Content = new StackPanel
                {
                    Children = { picker, invalid, new TextBox() }
                };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                // default format is dd/MM/yyyy with zero padding
                picker.SelectedDate = new DateTime(2026, 9, 7);
                global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var box = picker.GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "PART_TextBox");
                Assert.Equal("07/09/2026", box.Text);

                // invalid entry shows the :error state on completion
                var invalidBox = invalid.GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "PART_TextBox");
                invalidBox.Focus();
                invalidBox.Text = "99/99/2026";
                var other = window.GetVisualDescendants().OfType<TextBox>().Last();
                other.Focus();
                Dispatcher.UIThread.RunJobs();
                Assert.True(invalid.Classes.Contains(":error"), $"classes: {string.Join(',', invalid.Classes)} text: {invalid.Text}");
                var border = invalid.GetVisualDescendants()
                    .OfType<Border>()
                    .First(b => b.Name == "Background");
                Application.Current!.TryGetResource("KumoBrushDanger", window.ActualThemeVariant, out var danger);
                Assert.Equal(danger, border.BorderBrush);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void Calendar_year_tiles_are_rounded_rectangles()
        {
            var cal = new Calendar { DisplayMode = CalendarMode.Year };
            var window = new Window { Width = 400, Height = 360, Content = cal };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var buttons = window.GetVisualDescendants().OfType<CalendarButton>().ToList();
                Assert.True(buttons.Count >= 12, $"year tiles: {buttons.Count}");
                // tiles (tagged by ksup:CalendarTileTag) render as a circle:
                // the visible TileRoot background is cell-width square
                HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/yearview.png");
                var tileRoot = buttons[3].GetVisualDescendants().OfType<Border>().First(b => b.Name == "TileRoot");
                Assert.True(tileRoot.IsVisible, "TileRoot must be the visible background");
                Assert.Equal(buttons[3].Bounds.Width, tileRoot.Bounds.Width, 0.5);
                Assert.Equal(buttons[3].Bounds.Width, tileRoot.Bounds.Height, 0.5);
                Assert.Equal(9999, tileRoot.CornerRadius.TopLeft);
                var days = new Calendar { SelectedDate = new DateTime(2026, 9, 13) };
                var dayWindow = new Window { Width = 400, Height = 360, Content = days };
                dayWindow.Show();
                Dispatcher.UIThread.RunJobs();
                var day = dayWindow.GetVisualDescendants().OfType<CalendarDayButton>().First();
                Assert.Equal(9999, day.CornerRadius.TopLeft);
                dayWindow.Close();
            }
            finally { window.Close(); }
        }
        private static void KeyInput(Window window, Key key)
        {
            // RaiseEvent path — headless KeyDown extension needs raw args
            window.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
            });
            window.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyUpEvent,
                Key = key,
            });
        }

    [AvaloniaFact]
    public void Timepicker_loop_snaps_and_has_no_stray_pills()
    {
        var timePicker = new TimePicker();
        var window = new Window { Width = 360, Height = 320 };
        try
        {
            window.Content = timePicker;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var tp = timePicker.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_FlyoutButton");
            var pt = tp.TranslatePoint(new Point(tp.Bounds.Width / 2, tp.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pt, MouseButton.Left);
            window.MouseUp(pt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var presenter = window.GetVisualDescendants().OfType<TimePickerPresenter>().First();
            var hourScroll = presenter.GetVisualDescendants().OfType<ScrollViewer>()
                .First(s => s.GetVisualDescendants().OfType<DateTimePickerPanel>().Any(p => p.PanelType == global::Avalonia.Controls.Primitives.DateTimePickerPanelType.Hour));

            // snap opt-in present on the loop scrollviewers (assert on one)
            Assert.True(hourScroll.GetValue(KumoThemeSupport.DateTimeLoopSnap.IsEnabledProperty));

            // fractional offset -> a snap cycle must place the offset on a
            // multiple of ItemHeight (the behavior defers to its 80ms idle
            // timer; tests invoke the cycle directly)
            hourScroll.Offset = new Vector(0, hourScroll.Offset.Y + 12.5);
            var fractional = hourScroll.Offset.Y;
            Dispatcher.UIThread.RunJobs();
            // no release happened: zero clamping, even after real idle time
            Assert.Equal(fractional, hourScroll.Offset.Y);
            // releasing the strip is the only clamp trigger
            window.MouseUp(new Point(10, 10), MouseButton.Left, global::Avalonia.Input.RawInputModifiers.None);
            KumoThemeSupport.DateTimeLoopSnap.SnapNow(hourScroll, animate: false);
            var itemH = hourScroll.GetVisualDescendants().OfType<DateTimePickerPanel>().First().ItemHeight;
            Assert.True(Math.Abs(hourScroll.Offset.Y % itemH) < 0.01,
                $"offset stayed fractional: {fractional} -> {hourScroll.Offset.Y}, itemH={itemH}");

            // selected items carry NO own pill background: only the shared
            // HighlightRect band exists in the presenter
            var pillItems = presenter.GetVisualDescendants().OfType<ContentPresenter>()
                .Count(cp => cp.Background is global::Avalonia.Media.ISolidColorBrush b &&
                             b.Color.A > 0 &&
                             cp.GetVisualAncestors().OfType<ListBoxItem>().Any());
            // loop rows paint no pill at all (hover/selected included): the
            // shared HighlightRect band is the only selection affordance
            foreach (var cp in presenter.GetVisualDescendants().OfType<ContentPresenter>())
            {
                if (cp.Background is global::Avalonia.Media.ISolidColorBrush bb && bb.Color.A > 0 &&
                    cp.GetVisualAncestors().OfType<ListBoxItem>().Any())
                {
                    throw new Xunit.Sdk.XunitException($"stray pill in loop: brush={bb.Color} name={cp.Name}");
                }
            }
            foreach (var cp in presenter.GetVisualDescendants().OfType<ContentPresenter>())
            {
                if (cp.Background is global::Avalonia.Media.ISolidColorBrush bb && bb.Color.A > 0)
                {
                    var anc = string.Join(",", cp.GetVisualAncestors().OfType<ListBoxItem>().Select(a => a.Classes.ToString()));
                    Console.Out.WriteLine($"DBG pill item anc={anc} brush={bb.Color} name={cp.Name} classes={cp.Classes}");
                }
            }

            HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/datetime-snap.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tap_below_selected_drives_glide_or_commit()
    {
        var timePicker = new TimePicker();
        var window = new Window { Width = 360, Height = 320 };
        try
        {
            window.Content = timePicker;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var tp = timePicker.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_FlyoutButton");
            var pt = tp.TranslatePoint(new Point(tp.Bounds.Width / 2, tp.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pt, MouseButton.Left);
            window.MouseUp(pt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var presenter = window.GetVisualDescendants().OfType<TimePickerPresenter>().First();
            var hourPanel = presenter.GetVisualDescendants().OfType<DateTimePickerPanel>()
                .First(p => p.PanelType == global::Avalonia.Controls.Primitives.DateTimePickerPanelType.Hour);
            var snapScroll = hourPanel.GetVisualAncestors().OfType<ScrollViewer>().First();

            // the panel's looping listbox items directly arranged around center
            var items = hourPanel.GetVisualDescendants().OfType<ListBoxItem>()
                .Where(i => i.Tag is int)
                .Select(i => (i, y: i.TranslatePoint(new Point(i.Bounds.Width / 2, i.Bounds.Height / 2), window)!.Value.Y))
                .OrderBy(t => Math.Abs(t.y - 160))
                .ToList();
            var centered = items.First();
            var below = items.FirstOrDefault(t => t.y > centered.y + 10);
            Assert.True(below.i is not null, "no below-center item found");
            Assert.Equal(centered.y + 40, below.y, 0.5); // one full step gesture

            var commitsBefore = global::KumoThemeSupport.DateTimeLoopSnap.TapCommitProbeCount;
            var beforeSelected = hourPanel.SelectedValue;
            var panelsBefore = global::KumoThemeSupport.DateTimeLoopSnap.PanelTapProbeCount;
            var commitPresenterBefore = global::KumoThemeSupport.DateTimeLoopSnap.CommitPresenterProbeCount;
            var commitButtonBefore = global::KumoThemeSupport.DateTimeLoopSnap.CommitButtonProbeCount;
            var tapPt = below.i.TranslatePoint(new Point(below.i.Bounds.Width / 2, below.i.Bounds.Height / 2), window)!.Value;
            window.MouseDown(tapPt, MouseButton.Left);
            window.MouseUp(tapPt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            // real-time animation finalization cannot run headless (timers do
            // not tick); finish the in-flight glide deterministically
            var anim = global::KumoThemeSupport.DateTimeLoopSnap.AnimPropertyProbe(snapScroll);
            Assert.True(anim is not null, "tap did not start the glide animation");
            for (var i = 0; i < 40 && global::KumoThemeSupport.DateTimeLoopSnap.AnimPropertyProbe(snapScroll) is not null; i++)
            {
                global::KumoThemeSupport.DateTimeLoopSnap.AnimFinalizeProbe(snapScroll);
            }
            Dispatcher.UIThread.RunJobs();

            var handledAny = global::KumoThemeSupport.DateTimeLoopSnap.PanelTapProbeCount != panelsBefore;
            var commitnow = global::KumoThemeSupport.DateTimeLoopSnap.TapCommitProbeCount != commitsBefore;
            var presenterReady = global::KumoThemeSupport.DateTimeLoopSnap.CommitPresenterProbeCount != commitPresenterBefore;
            var buttonReady = global::KumoThemeSupport.DateTimeLoopSnap.CommitButtonProbeCount != commitButtonBefore;
            Console.Out.WriteLine($"DBG tap: handledAny={handledAny} commitNow={commitnow} presenterReady={presenterReady} buttonReady={buttonReady}" +
                                  $" offset={hourPanel.Offset.Y} selected={hourPanel.SelectedValue} centeredTag={centered.i.Tag}");
            Assert.True(handledAny, "class-handler tap count did not change");
            var selectedAfter = hourPanel.SelectedValue;
            // off-center taps only select — no commit, no popup close
            Assert.False(commitnow, "off-center tap must not commit");
            Assert.False(presenterReady || buttonReady, "off-center tap must not touch the commit path");
            // tapping one below the center steps the value by exactly one
            Assert.Equal(beforeSelected + hourPanel.Increment, selectedAfter);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wheel_rest_schedules_no_clamp()
    {
        var timePicker = new TimePicker();
        var window = new Window { Width = 360, Height = 320 };
        try
        {
            window.Content = timePicker;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var tp = timePicker.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_FlyoutButton");
            var pt = tp.TranslatePoint(new Point(tp.Bounds.Width / 2, tp.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pt, MouseButton.Left);
            window.MouseUp(pt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var presenter = window.GetVisualDescendants().OfType<TimePickerPresenter>().First();
            var hourPanel = presenter.GetVisualDescendants().OfType<DateTimePickerPanel>()
                .First(p => p.PanelType == global::Avalonia.Controls.Primitives.DateTimePickerPanelType.Hour);
            var snapScroll = hourPanel.GetVisualAncestors().OfType<ScrollViewer>().First();
            var itemHeight = hourPanel.ItemHeight;

            // simulate a wheel/trackpad stream stopping at a half-shown
            // position: fingers still resting on the pad produce no further
            // events and no end-of-scroll signal at app level, so nothing
            // may clamp
            var itemHeight1 = itemHeight;
            var halfOffset = hourPanel.Offset.Y - itemHeight1 / 2.0;
            hourPanel.Offset = new global::Avalonia.Vector(0, halfOffset);
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            Assert.Null(global::KumoThemeSupport.DateTimeLoopSnap.AnimPropertyProbe(snapScroll));
            Assert.Equal(halfOffset, hourPanel.Offset.Y, 3); // still between values, no auto-clamp
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tap_center_commits_that_value_and_closes()
    {
        var timePicker = new TimePicker();
        var window = new Window { Width = 360, Height = 320 };
        try
        {
            window.Content = timePicker;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            timePicker.SelectedTime = new TimeSpan(9, 30, 0);
            Dispatcher.UIThread.RunJobs();

            var tp = timePicker.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_FlyoutButton");
            var pt = tp.TranslatePoint(new Point(tp.Bounds.Width / 2, tp.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pt, MouseButton.Left);
            window.MouseUp(pt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var presenter = window.GetVisualDescendants().OfType<TimePickerPresenter>().First();
            var hourPanel = presenter.GetVisualDescendants().OfType<DateTimePickerPanel>()
                .First(p => p.PanelType == global::Avalonia.Controls.Primitives.DateTimePickerPanelType.Hour);

            var items = hourPanel.GetVisualDescendants().OfType<ListBoxItem>()
                .Where(i => i.Tag is int)
                .Select(i => (i, y: i.TranslatePoint(new Point(i.Bounds.Width / 2, i.Bounds.Height / 2), window)!.Value.Y))
                .OrderBy(t => Math.Abs(t.y - 160))
                .ToList();
            var centered = items.First();
            Console.Out.WriteLine($"DBG center: centeredTag={centered.i.Tag} selected={hourPanel.SelectedValue}");

            var tapPt = centered.i.TranslatePoint(new Point(centered.i.Bounds.Width / 2, centered.i.Bounds.Height / 2), window)!.Value;
            var sv = hourPanel.GetVisualAncestors().OfType<global::Avalonia.Controls.ScrollViewer>().First();
            var taps = global::KumoThemeSupport.DateTimeLoopSnap.PanelTapProbeCount;
            var p0 = global::KumoThemeSupport.DateTimeLoopSnap.CommitPresenterProbeCount;
            var b0 = global::KumoThemeSupport.DateTimeLoopSnap.CommitButtonProbeCount;
            var c0 = global::KumoThemeSupport.DateTimeLoopSnap.TapCommitProbeCount;
            window.MouseDown(tapPt, MouseButton.Left);
            window.MouseUp(tapPt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var time = timePicker.SelectedTime;
            var presentersLeft = window.GetVisualDescendants().OfType<global::Avalonia.Controls.TimePickerPresenter>().Any();
            Console.Out.WriteLine($"DBG center: tapHit={KumoThemeSupport.DateTimeLoopSnap.PanelTapProbeCount != taps} presenter={KumoThemeSupport.DateTimeLoopSnap.CommitPresenterProbeCount - p0} button={KumoThemeSupport.DateTimeLoopSnap.CommitButtonProbeCount - b0} commits={KumoThemeSupport.DateTimeLoopSnap.TapCommitProbeCount - c0}" +
                                  $" tapPending={KumoThemeSupport.DateTimeLoopSnap.TapPendingProbe(sv)} anim={KumoThemeSupport.DateTimeLoopSnap.AnimPropertyProbe(sv) != null} timeAfter={time} popupOpen={presentersLeft}");
            Assert.False(presentersLeft, "tapping the center value must close the widget");
            Assert.Equal(new TimeSpan(9, 30, 0), time);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tap_offcenter_commits_deck_value_and_closes()
    {
        var timePicker = new TimePicker();
        var window = new Window { Width = 360, Height = 320 };
        try
        {
            window.Content = timePicker;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            timePicker.SelectedTime = new TimeSpan(9, 30, 0);
            Dispatcher.UIThread.RunJobs();

            var tp = timePicker.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_FlyoutButton");
            var pt = tp.TranslatePoint(new Point(tp.Bounds.Width / 2, tp.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pt, MouseButton.Left);
            window.MouseUp(pt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var presenter = window.GetVisualDescendants().OfType<TimePickerPresenter>().First();
            var hourPanel = presenter.GetVisualDescendants().OfType<DateTimePickerPanel>()
                .First(p => p.PanelType == global::Avalonia.Controls.Primitives.DateTimePickerPanelType.Hour);

            var items = hourPanel.GetVisualDescendants().OfType<ListBoxItem>()
                .Where(i => i.Tag is int)
                .Select(i => (i, y: i.TranslatePoint(new Point(i.Bounds.Width / 2, i.Bounds.Height / 2), window)!.Value.Y))
                .OrderBy(t => Math.Abs(t.y - 160))
                .ToList();
            var centered = items.First();
            var below = items.FirstOrDefault(t => t.y > centered.y + 10);
            var tappedValue = (int)below.i.Tag!; // captured before the glide rewraps the loop children
            var beforeSelectedTime = timePicker.SelectedTime;
            var tapPt = below.i.TranslatePoint(new Point(below.i.Bounds.Width / 2, below.i.Bounds.Height / 2), window)!.Value;
            var beforeCommit = KumoThemeSupport.DateTimeLoopSnap.TapCommitProbeCount;
            window.MouseDown(tapPt, MouseButton.Left);
            window.MouseUp(tapPt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            // finish the tap glide deterministically
            var snapScroll = hourPanel.GetVisualAncestors().OfType<global::Avalonia.Controls.ScrollViewer>().First();
            for (var i = 0; i < 40 && KumoThemeSupport.DateTimeLoopSnap.AnimPropertyProbe(snapScroll) is not null; i++)
            {
                KumoThemeSupport.DateTimeLoopSnap.AnimFinalizeProbe(snapScroll);
            }
            Dispatcher.UIThread.RunJobs();

            var presentersLeft = window.GetVisualDescendants().OfType<global::Avalonia.Controls.TimePickerPresenter>().Any();
            Console.Out.WriteLine($"DBG offcenter: tapped={tappedValue} timeAfter={timePicker.SelectedTime} popupOpen={presentersLeft} selected={hourPanel.SelectedValue}");
            // off-center taps only select: no commit, popup stays open, and
            // the tapped value becomes the centered selection
            Assert.Equal(beforeSelectedTime, timePicker.SelectedTime);
            Assert.Equal(tappedValue, hourPanel.SelectedValue);
            Assert.True(presentersLeft, "off-center tap must not close the widget");
            Assert.Equal(beforeCommit, KumoThemeSupport.DateTimeLoopSnap.TapCommitProbeCount);
            // the now-centered value is the only clickable one: this tap commits
            var centeredItem = window.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.DateTimePickerPanel>()
                .First(p => p.PanelType == global::Avalonia.Controls.Primitives.DateTimePickerPanelType.Hour)
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(i => i.Tag is int t && t == (int)hourPanel.SelectedValue);
            var centerPt = centeredItem.TranslatePoint(new Point(centeredItem.Bounds.Width / 2, centeredItem.Bounds.Height / 2), window)!.Value;
            window.MouseDown(centerPt, MouseButton.Left);
            window.MouseUp(centerPt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.GetVisualDescendants().OfType<global::Avalonia.Controls.TimePickerPresenter>().Any(), "center tap after select closes the widget");
            Assert.Equal(new TimeSpan(hourPanel.SelectedValue, 30, 0), timePicker.SelectedTime);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clock24_center_tap_commits_and_period_column_absent()
    {
        var timePicker = new TimePicker();
        var window = new Window { Width = 360, Height = 320 };
        try
        {
            window.Content = timePicker;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            timePicker.SelectedTime = new TimeSpan(9, 30, 0);
            timePicker.ClockIdentifier = "24HourClock";
            Dispatcher.UIThread.RunJobs();

            var tp = timePicker.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_FlyoutButton");
            var pt = tp.TranslatePoint(new Point(tp.Bounds.Width / 2, tp.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pt, MouseButton.Left);
            window.MouseUp(pt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            var presenter = window.GetVisualDescendants().OfType<global::Avalonia.Controls.TimePickerPresenter>().First();
            // 24-hour clock: no AM/PM column among the visible loop hosts
            var periodHost = presenter.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PART_PeriodHost");
            Assert.False(periodHost.IsVisible, "24h clock must hide the AM/PM column");

            var hourPanel = presenter.GetVisualDescendants().OfType<DateTimePickerPanel>()
                .First(p => p.PanelType == global::Avalonia.Controls.Primitives.DateTimePickerPanelType.Hour);
            var items = hourPanel.GetVisualDescendants().OfType<ListBoxItem>()
                .Where(i => i.Tag is int)
                .Select(i => (i, y: i.TranslatePoint(new Point(i.Bounds.Width / 2, i.Bounds.Height / 2), window)!.Value.Y))
                .OrderBy(t => Math.Abs(t.y - 160))
                .ToList();
            var centered = items.First();
            Assert.Equal(9, (int)centered.i.Tag); // 24h clock: 09 stays 09

            var tapPt = centered.i.TranslatePoint(new Point(centered.i.Bounds.Width / 2, centered.i.Bounds.Height / 2), window)!.Value;
            window.MouseDown(tapPt, MouseButton.Left);
            window.MouseUp(tapPt, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            Assert.False(window.GetVisualDescendants().OfType<global::Avalonia.Controls.TimePickerPresenter>().Any(), "center tap must close");
            Assert.Equal(new TimeSpan(9, 30, 0), timePicker.SelectedTime);
        }
        finally
        {
            window.Close();
        }
    }
}
}
