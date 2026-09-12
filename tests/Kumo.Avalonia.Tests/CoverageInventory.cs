using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Kumo.Demo;
using Xunit;

namespace Kumo.Avalonia.Tests;

/// <summary>
/// Empirical coverage inventory: enumerates every public concrete
/// TemplatedControl shipped by the Avalonia packages we reference and
/// reports whether a ControlTheme keyed to its type resolves from the
/// app resources. A missing theme means the control renders blank
/// (no Fluent fallback exists), so this list is the exact to-do list
/// for full completeness.
/// </summary>
public class CoverageInventory
{
    /// <summary>The style key each control looks themes up by (respecting
    /// StyleKeyOverride, e.g. ToggleSplitButton keys as SplitButton so the
    /// shared ControlTheme applies).</summary>
    private static string StyleKeyOf(Type type)
    {
        // Known StyleKeyOverride mappings; keep explicit since reflecting a
        // virtual getter requires a live instance which some controls refuse.
        var overrides = new Dictionary<string, string>
        {
            ["Avalonia.Controls.ToggleSplitButton"] = "Avalonia.Controls.SplitButton",
        };
        return overrides.TryGetValue(type.FullName!, out var known) ? known : type.FullName!;
    }

    private static Type? GetAvaloniaType(string key)
    {
        var asm = typeof(global::Avalonia.Controls.Control).Assembly;
        return asm.GetType(key) ?? asm.GetExportedTypes().FirstOrDefault(t => t.FullName == key);
    }

    public static List<string> Missing()
    {
        var appAsm = typeof(App).Assembly;
        var avaloniaAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName is not null &&
                        (a.FullName.StartsWith("Avalonia.Controls,") ||
                         a.FullName.StartsWith("Avalonia.Base,") ||
                         a.FullName.StartsWith("Avalonia,")))
            .ToList();

        var types = new List<Type>();
        foreach (var asm in avaloniaAssemblies)
            types.AddRange(asm.GetTypes());

        var concrete = types
            .Where(t => t.IsPublic && !t.IsAbstract && !t.IsGenericTypeDefinition)
            .Where(t => typeof(TemplatedControl).IsAssignableFrom(t))
            // Types that are never instantiated directly by consumers.
            .Where(t => t.FullName?.StartsWith("Avalonia.Controls.Presenters") != true ||
                        t.Name is "ContentPresenter" or "ItemsPresenter")
            .Where(t => t != typeof(Popup) && !typeof(Popup).IsAssignableFrom(t))
            .Where(t => !t.Name.Contains("CellEdit")) // DataGrid editizenry
            .Distinct()
            .ToList();

        // Controls whose look lives inside another control's theme
        // (our ListBox styles are scoped to AutoCompleteBox items, etc.).
        var app = Application.Current!;
        var result = new List<string>();
        foreach (var type in concrete)
        {
            var key = StyleKeyOf(type);
            var keyType = key == type.FullName ? type
                : Type.GetType(key) ?? GetAvaloniaType(key);

            var themeFound = false;
            foreach (var variant in new[] { ThemeVariant.Default, ThemeVariant.Light, ThemeVariant.Dark })
            {
                app.TryGetResource(type, variant, out var value);
                if (value is ControlTheme)
                {
                    themeFound = true;
                    break;
                }
                if (keyType is not null && keyType != type &&
                    app.TryGetResource(keyType, variant, out var keyed) && keyed is ControlTheme)
                {
                    themeFound = true;
                    break;
                }
            }
            if (!themeFound)
            {
                if (type.Name.Contains("ToggleSplit"))
                {
                    result.Add("DEBUG key=" + key + " keyType=" + (keyType is null ? "NULL" : keyType.FullName));
                }
                result.Add(type.FullName!);
            }
        }
        return result;
    }

    /// <summary>Controls that are accepted to render unthemed: Avalonia
    /// base classes, page/host types we don't ship, and internal presenters.
    /// Everything else that is missing is a real consumer-facing gotcha and
    /// fails this test. Remove entries from here as ControlThemes get added;
    /// entries in <see cref="KnownUnthemed"/> below are the planned work.</summary>
    private static readonly string[] Accepted = new[]
    {
        // Base classes / never instantiated directly.
        "Avalonia.Controls.Primitives.TemplatedControl",
        "Avalonia.Controls.Primitives.HeaderedContentControl",
        "Avalonia.Controls.Primitives.HeaderedItemsControl",
        "Avalonia.Controls.Primitives.HeaderedSelectingItemsControl",
        "Avalonia.Controls.Primitives.SelectingItemsControl",
        "Avalonia.Controls.Primitives.TabStrip",
        "Avalonia.Controls.Primitives.TabStripItem",
        "Avalonia.Controls.Primitives.TextSelectionHandle",
        "Avalonia.Controls.WindowBase",
        "Avalonia.Controls.PageNavigationHost",
        "Avalonia.Controls.NativeMenuBar",
        "Avalonia.Controls.Primitives.OverlayPopupHost",
        // Page navigation stack (Avalonia 12) — not part of the theme surface.
        "Avalonia.Controls.CarouselPage",
        "Avalonia.Controls.ContentPage",
        "Avalonia.Controls.DrawerPage",
        "Avalonia.Controls.NavigationPage",
        "Avalonia.Controls.TabbedPage",
    };

    /// <summary>Consumer-facing controls that currently render unthemed.
    /// Each of these needs a real ControlTheme (see docs/THEME-AUDIT.md §3).
    /// This test fails when a control disappears from the list (prune it
    /// here) or a new one appears (theme it).</summary>
    private static readonly string[] KnownUnthemed = new[]
    {
        "Avalonia.Controls.CalendarDatePicker",
        "Avalonia.Controls.Calendar",
        "Avalonia.Controls.Carousel",
        "Avalonia.Controls.DatePicker",
        "Avalonia.Controls.DatePickerPresenter",
        "Avalonia.Controls.TimePicker",
        "Avalonia.Controls.TimePickerPresenter",
        "Avalonia.Controls.PipsPager",
        "Avalonia.Controls.RefreshContainer",
        "Avalonia.Controls.RefreshVisualizer",
        "Avalonia.Controls.TableView",
        "Avalonia.Controls.TableViewCell",
        "Avalonia.Controls.TableViewColumnHeader",
        "Avalonia.Controls.TableViewRow",
        "Avalonia.Controls.TreeView",
        "Avalonia.Controls.TreeViewItem",
        "Avalonia.Controls.Primitives.CalendarButton",
        "Avalonia.Controls.Primitives.CalendarDayButton",
        "Avalonia.Controls.Primitives.CalendarItem",
    };

    [AvaloniaFact]
    public void Unthemed_control_list_matches_the_known_plan()
    {
        var missing = Missing();
        var unexpected = missing.Where(n => !Accepted.Contains(n) && !KnownUnthemed.Contains(n)).ToList();
        var staleKnown = KnownUnthemed.Where(n => !missing.Contains(n)).ToList();

        var message = "";
        if (unexpected.Count > 0)
            message += "Unthemed controls outside the plan (must be themed "
                     + "or added to the plan):\n  " + string.Join("\n  ", unexpected) + "\n";
        if (staleKnown.Count > 0)
            message += "Controls that are now themed (remove from the plan):\n  "
                     + string.Join("\n  ", staleKnown);

        Assert.True(message == "", message);
    }
}
