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

## Remaining work queue (0 controls)

Consumer-facing controls that currently render blank. Grouped by family;
owning file will be `Themes/Controls/<family>.axaml`.

| Family | Controls | Notes |
| --- | --- | --- |
| Trees | - | (`TreeView`/`TreeViewItem` done 2026-09-12: KumoTreeViewExpandCollapseChevron, Level x 16px indent, tint hover, fill-selected, `:empty` spacer) |
| Date/time | - | (all done 2026-09-13 in `DateTime.axaml`: brand-solid selected day pill + brand-tint today, hairline flyout surface with loop columns and brand check/X accept bar; watermark placeholders use the input placeholder token) |

| Lists/containers | - | (`Carousel` stage + rounded corner, `RefreshContainer`/`RefreshVisualizer` brand circular-arrow glyph, `PipsPager` grey/brand pips + ghost nav chevrons — done 2026-09-13) |
| Data grid | - | (`TableView` family done 2026-09-13: sticky hairline header row, 12px cells w/ resizer thumb, hover tint + Fill-tint selected rows; both Avalonia-native `TableView` and the `KumoTable` composition ship) |
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
- `SplitView` (side placements + overlay/inline/compact modes; hairline pane divider) and `GridSplitter` (hairline rail, brand-grow hover) in `Themes/Controls/Panes.axaml`; `Slider` (own file `Themes/Controls/Slider.axaml`; horizontal + vertical + tick bars) and `NumericUpDown` (own file; host ring + inner transparent TextBox + `:left` spinner pseudo-class support in ButtonSpinner) — 2026-09-11
- `MaskedTextBox` — in `Themes/Controls/TextBox.axaml`, `BasedOn` the TextBox
  theme so it inherits the full kumo input treatment
- `Menu` (top-level strip; `KumoTopLevelMenuItem` container theme + popup
  chrome reuse) — appended to `Themes/Controls/PopupChrome.axaml`
- Split/menu buttons: `SplitButton` + `ToggleSplitButton` in
  `Themes/Controls/SplitButton.axaml` (own `CornerRadiusFilterConverter` +
  hairline divider + chevron; toggle checked = tint fill); CommandBar
  `CommandBar`/`CommandBarButton`/`CommandBarToggleButton`/`CommandBarSeparator`
  in `Themes/Controls/CommandBar.axaml` (toolbar surface, overflow popup,
  icon/label stacking, `LabelPosition=Right`) — new
  `src/Kumo.Avalonia/Converters.cs`

Smoke-verified in `tests/Kumo.Avalonia.Tests/MiscProbe.cs` and shown in the demo "Windowed controls" card.

See also `docs/THEME-AUDIT.md` for the full 48-component Kumo registry
matrix and design-rule checklist.
