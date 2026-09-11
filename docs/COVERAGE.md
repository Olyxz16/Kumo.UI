# Control coverage inventory

The exact reference for which Avalonia controls ship a Kumo `ControlTheme`
and which still render unthemed.

## How to use it

`tests/Kumo.Avalonia.Tests/CoverageInventory.cs` enumerates every public,
concrete `TemplatedControl` in the referenced Avalonia assemblies and checks
whether a `ControlTheme` keyed to its type resolves from the app resources
(the way Avalonia's implicit-theme lookup does). It is a live guardrail, not
documentation that can rot:

```bash
dotnet run --project tests/Kumo.Avalonia.Tests
```

Three things happen on every run:

1. A **new** unthemed control appears (Avalonia upgrade, new package) — the
   test fails with its name. Theme it, or add it to `Accepted` if it is a
   base class / hosting type we deliberately don't ship.
2. A control that was planned (listed in `KnownUnthemed` below) got themed —
   the test fails telling you to **prune** it from `KnownUnthemed` and from
   the table below.
3. Otherwise green — the two lists match reality.

To find out what is currently missing without reading the table, run the
inventory with the guardrail assertion relaxed: the full list is computed by
`CoverageInventory.Missing()` (static, public) — call it from any scratch
test or from the debugger.

## Rules

- Remove a row from below only when a `ControlTheme` exists and the test passes.
- Never add to `KnownUnthemed` anything you don't intend to theme.
- `Accepted` is for types consumers never instantiate directly (Avalonia
  base classes, the Avalonia 12 Page navigation stack, `NativeMenuBar`,
  `OverlayPopupHost`).

## Remaining work queue (29 controls)

Consumer-facing controls that currently render blank. Grouped by family;
owning file will be `Themes/Controls/<family>.axaml`.

| Family | Controls | Notes |
| --- | --- | --- |
| Date/time | `Calendar`, `CalendarItem`, `CalendarButton`, `CalendarDayButton`, `CalendarDatePicker`, `DatePicker`, `DatePickerPresenter`, `TimePicker`, `TimePickerPresenter` | biggest chunk; solid accent-fill selection per Kumo, not WinUI ring; `DatePicker`/`TimePicker` share the button + popup pattern |
| Spin/number entry | `NumericUpDown` | ButtonSpinner theme exists; NumericUpDown mostly inherits it, prune after a functional pass |
| Slider | `Slider` | ring + track from `input-base` spec |
| Split/menu buttons | `SplitButton`, `ToggleSplitButton`, `CommandBar`, `CommandBarButton`, `CommandBarSeparator`, `CommandBarToggleButton` | reuse Button (`DropDownButton` template done) + PopupChrome internals |
| Menus top-level | `Menu` | reuse `MenuItem` styles from PopupChrome |
| Trees | `TreeView`, `TreeViewItem` | row indent + expander chevrons |
| Panes | `SplitView`, `GridSplitter` | (`GroupBox` theme done) |
| Lists/containers | `Carousel`, `RefreshContainer`, `RefreshVisualizer`, `PipsPager` | polish tier (`TransitioningContentControl` theme done) |
| Data grid | `TableView`, `TableViewCell`, `TableViewColumnHeader`, `TableViewRow` | Avalonia 12's new table; decide vs. keep `KumoTable` composition |
| Text | - | (`MaskedTextBox`, `PathIcon` themes done) |

## Deliberately unthemed (accepted)

Avalonia base classes rarely/never instantiated directly
(`TemplatedControl`, `SelectingItemsControl`, `Headered*Control`,
`TabStrip`, `TabStripItem`, `TextSelectionHandle`, `WindowBase`,
`UserControl`, `PageNavigationHost`), the Avalonia 12 Page navigation types
(`CarouselPage`, `ContentPage`, `DrawerPage`, `NavigationPage`,
`TabbedPage`), `NativeMenuBar`, `OverlayPopupHost`.

## Out of scope by design

Charts (ECharts wrappers), Phosphor icon set (no Avalonia equivalent; demo
uses inline stroke paths), `CloudflareLogo` (brand asset).

## Closed

- `UserControl`, `Label` — 2026-09-11 (first entries; `UserControl` matters
  most — subclassing it without a theme renders nothing)
- `PathIcon`, `TransitioningContentControl`, `GroupBox` — in
  `Themes/Controls/Shared.axaml`
- `DropDownButton`, `ButtonSpinner` — appended to `Themes/Controls/Button.axaml`
  (`DropDownButton` is `BasedOn` Button + chevron; `ButtonSpinner` styles the
  `PART_IncreaseButton`/`PART_DecreaseButton` repeat chevrons)
- `MaskedTextBox` — in `Themes/Controls/TextBox.axaml`, `BasedOn` the TextBox
  theme so it inherits the full kumo input treatment

Smoke-verified in `tests/Kumo.Avalonia.Tests/MiscProbe.cs` and shown in the demo "Windowed controls" card.

See also `docs/THEME-AUDIT.md` for the full 48-component Kumo registry
matrix and design-rule checklist.
