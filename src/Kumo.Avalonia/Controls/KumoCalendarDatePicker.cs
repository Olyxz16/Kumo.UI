using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace KumoThemeSupport.Controls;

/// <summary>
/// CalendarDatePicker with completion-time validation: on Enter or focus
/// leave of the inner text box the text is parsed against the active
/// format (default dd/MM/yyyy). A valid entry sets the selected date and
/// is reformatted zero-padded; an unparseable date raises a data-validation
/// error, which the Kumo theme renders as a red border, keeping the text
/// as typed until the user edits again.
/// </summary>
public class KumoCalendarDatePicker : CalendarDatePicker
{
    private bool _hooked;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<TextBox>("PART_TextBox") is { } box && !_hooked)
        {
            _hooked = true;

            box.LostFocus += (_, _) => CommitValidation(box.Text);
            box.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || !IsEffectivelyEnabled)
                    return;

                var textBefore = box.Text;
                base.OnKeyDown(e); // base commits-parses first
                CommitValidation(textBefore);
            };
            box.GotFocus += (_, _) => ClearError();
        }
    }

    private void CommitValidation(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            ClearError();
            return;
        }

        var fmt = SelectedDateFormat == CalendarDatePickerFormat.Custom &&
                  !string.IsNullOrEmpty(CustomDateFormatString)
            ? CustomDateFormatString
            : CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;

        if (DateTime.TryParseExact(text, fmt, CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces, out var date))
        {
            ClearError();
            SetCurrentValue(SelectedDateProperty, date);
            SetCurrentValue(TextProperty, date.ToString(fmt, CultureInfo.CurrentCulture));
        }
        else
        {
            DataValidationErrors.SetError(this, new FormatException($"'{text}' is not a valid date."));
        }
    }

    private void ClearError()
    {
        DataValidationErrors.SetError(this, null);
    }
}
