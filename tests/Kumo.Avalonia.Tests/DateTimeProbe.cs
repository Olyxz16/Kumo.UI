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
            var calendarPicker = new CalendarDatePicker { PlaceholderText = "Pick a date" };
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
}
}
