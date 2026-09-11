# Theme completeness audit

# Theme completeness audit

Status: **self-contained.** All styling lives in per-control `ControlTheme`s
under `src/Kumo.Avalonia/Themes/Controls/*` (one file per component family);
`Controls.axaml` is a pure include registry. **The Fluent theme is no longer
required or referenced** — `KumoTheme.axaml` + `Controls.axaml` provide every
template. All 54 semantic tokens plus the component base colors are generated,
and the interactive controls, presets and real `Kumo*` composition controls
are themed and covered by headless tests (99 passing).

The exact set of Avalonia-templated controls that still render without a
Kumo theme is enforced empirically by
`tests/Kumo.Avalonia.Tests/CoverageInventory.cs`: it enumerates every public
concrete `TemplatedControl` in the referenced Avalonia assemblies and diffs
each against the app resources. Consumer-facing controls that are missing
are listed in that test's `KnownUnthemed` allowlist — that list **is** the
work queue; the test fails if anything is added or if a planned control
becomes themed without pruning.

| Kumo component | Avalonia mapping |
| --- | --- |
| KumoBadge | `Kumo.Avalonia.Controls.KumoBadge` (Variant property; all 14 variant palettes) |

Audited against `@cloudflare/kumo` v2.13.1 (48 components in
`https://kumo-ui.com/api/component-registry`). Component specs (colors,
geometry, state treatments) were extracted from the unminified component
chunks shipped in the npm package (`dist/chunks/*.js`).

## 1. Registry coverage matrix (48 components)

### Themed as ControlThemes / style classes (Themes/Controls/*)

| Kumo component | Avalonia mapping |
| --- | --- |
| Button | `Button` + `primary`/`danger`/`ghost`/`outline`/`secondary-destructive`/`sm`/`icon`/`icon-sm`/`icon-circle`/`breadcrumb`/`page` classes; emphasis variants (primary/danger) use the upstream light gradient (`emphasis-bg` = token+white 30%, sheen = token+white 15% → token, ring = token+black 10%) as a top-to-bottom `LinearGradientBrush` with hover gradient collapse |
| Input | `TextBox` (kumo input ring + focus) |
| InputArea | `TextBox.multiline` |
| SensitiveInput | `TextBox.password` (mask glyph, mono; reveal toggles `PasswordChar`) |
| InputGroup | `Border.input-group` + descendant reset styles, `:focus-within` container ring, `invalid` class |
| Autocomplete | `AutoCompleteBox` restyle (input + popup via `AutoCompleteBoxSuggestionsList*` resource overrides, kumo item styles) |
| Combobox (single select) | `ComboBox` + `ComboBoxItem` |
| Checkbox / Radio / Switch | `CheckBox` / `RadioButton` / `ToggleSwitch` full ControlThemes (+ `error` variants) |
| Select | `ComboBox` (same trigger + popup treatment as upstream Select) |
| Field | `TextBlock.field-label` / `field-description` / `field-error` / `field-optional` presets |
| Label | `TextBlock.field-label` |
| Badge | `Border.badge` + 14 variant classes |
| Banner | `Border.banner` + status classes |
| Toasty | `NotificationCard` ControlTheme + `WindowNotificationManager`; deck stacking via measure-based `ToastDeck` panel (older toasts tucked 34px behind the newest with stepped opacity), enter = slide-up 28px, exit = slide-down 56px + fade; `Border.toast` presets |
| Collapsible | `Expander` (Kumo left-border content, expand fade/slide) |
| Tabs | `TabControl`/`TabItem` segmented (recessed track + labels on top); **native sliding indicator** `PART_Indicator` pill template element animated by `KumoThemeSupport.TabSlide` attached behavior — 200ms translate + scaleX morph between source/target geometry (matching upstream `transition-all duration-200`), scale-0.9 pop-in on first render; `KumoTabs` control turns the slide on natively |
| Tooltip | `ToolTip` |
| Dialog | `Window.dialog` + `Border.dialog-surface` (+ `.sm/.lg/.xl`, `dialog-title`/`dialog-description`); window chrome: `SystemDecorations=None` + transparent backdrop (set locally — direct properties can't be styled in Avalonia) |
| Popover / DropdownMenu | `FlyoutPresenter` / `ContextMenu` / `MenuFlyoutPresenter` / `MenuItem` / `Separator` |
| MenuBar (upstream deprecated) | `Border.menubar` + active-item styles (toolbar-style nav strip) |
| Toolbar | `Border.toolbar` (internal dividers via `:nth-child(n+2)`, edge rounding on first/last child) |
| Pagination | Upstream composition: `TextBlock.pagination-info`, `Border.pagination-separator`, and an input-group nav strip (`Button.icon` first/prev/next/last + centered page `TextBox` width 50); `Button.page`/`page-selected` presets kept for manual number lists (spec-locked) |
| Breadcrumbs | `Button.breadcrumb`, `TextBlock.breadcrumb-current`, inactive chevron separators |
| Table | Composition presets: `Border.table`, `table-header-cell` (+ `compact`), `table-cell`, `table-row` (+ `alt`/`selected`, row hover); row selection via checkbox column + click-to-toggle (demo wires `IsCheckedChanged` -> `selected` class) |
| Meter | `ProgressBar.meter` (+ `success`/`warning`/`danger`/`info`) |
| Progress | `ProgressBar` base + status classes |
| Loader | `ContentControl.loader` (+ `loader-sm`/`loader-lg`) template: rotating arc + track, `TextElement.Foreground` for color |
| Link | `HyperlinkButton` (kumo link color, underline; `.plain` clears underline via `KumoStatics.NoTextDecorations`), `TextBlock.link` |
| Code | `TextBlock.code` inline + `Border.code-block` block (mono 13px, fill border) |
| ClipboardText | `TextBox` readonly + copy `Button.icon` (compose in app; copy logic is app-level) |
| Text | `TextBlock.title`/`title-lg`/`title-xl`/`secondary`/`danger`/`mono`/`mono-secondary` |
| Empty | `Border.empty` (+ `empty-sm`/`empty-lg`), `empty-title`/`empty-description`, `Border.empty-command` + mono presets |
| Surface | `Border.surface` (flat card: base bg, line ring, xs shadow) |
| LayerCard | `Border.layer-card` + `layer-secondary` |
| Skeleton | `Border.skeleton-line` (shimmer) |
| Grid (CSS layout) | N/A in Avalonia; use `Grid`/`WrapPanel` directly |
| CloudflareLogo | Out of scope (brand asset) |
| Charts (Chart, TimeseriesChart, BubbleMap, ChoroplethMap, SankeyChart) | N/A (ECharts wrappers; chart tokens not extracted) |
| Sidebar / CommandPalette / TableOfContents | App-level compositions; building blocks (toolbar, menu items, badges, input group) are themed |
| DatePicker / DateRangePicker | Remaining: needs a `Calendar*` ControlTheme redefinition (Fluent's is WinUI-style ring selection, not Kumo's solid accent fill). Note: no `Calendar*`/`DatePicker`/`Slider`/`DataGrid` ControlTheme is bundled — Fluent is gone, so controls outside the demo surface have no template. Add them on demand. |
| Kumo controls (`Kumo*`) | `KumoBadge` (Variant × 14), `KumoAvatar` (initials + deterministic hue + xs/sm/base/lg), `KumoEmptyState` (icon/title/description/command slots), `KumoToolbar` (ring surface, leading/trailing slots), `KumoTabs` (native sliding indicator) — all with own ControlThemes in `Themes/Controls/Kumo.axaml` |

## 2. In place (verified)

| Area | State |
| --- | --- |
| Token pipeline `scripts/generate_tokens.py` | Parses all 54 upstream semantic tokens (oklch, `light-dark()`, `color-mix(in oklch)`, `var()` fallbacks, alpha) **plus the component base colors** (fixed `blue-*`/`neutral-*`/`emerald-*`/... shades), converts oklch→sRGB (verified against culori), emits `Palette.axaml` + `docs/TOKENS.md`. `--check` mode for CI. |
| Color tokens | 54/54 semantic tokens × Light/Dark (Color + Brush each) and 60+ primitives, applied via `ThemeDictionaries`. |
| Non-color tokens (`Tokens.axaml`) | Font sizes 12–30 px, 4 px spacing scale, radii 4/6/8/12/full, shadows xs/sm/md/lg + card, stroke widths. |
| Derived component brushes (`KumoTheme.axaml`) | Light/Dark pairs: switch track/thumb, focus ring 50%, danger ring 50%, skeleton fill, toast backdrop, dialog shadow, `KumoFontMono`. `ControlCornerRadius`/`OverlayCornerRadius` and ComboBox/AutoCompleteBox popup resources redefined to Kumo values. |
| Headless tests | 99 passing: token counts/spot-checks, variant switching, per-control spec diff (`design/specs/*.json`), style resolution (button/switch/checkbox/combobox/tab/badge/toast/link/loader/input-group/meter/table/toolbar/empty/autocomplete/dialog/table/Kumo controls) and the unthemed-control coverage guard. Run with `dotnet run --project tests/Kumo.Avalonia.Tests`. |
| Demo app | Full showcase incl. palette grid, inputs, tabs, badges, banners, menus, collapsible, toasts, links & code, loaders & meters, toolbar & menubar, field/input-group/multiline/password/autocomplete, breadcrumbs & pagination, table, empty state, themed dialog window, `Kumo*` control section. Fluent-free. |

## 3. Remaining for full parity

### 3a. Exact unthemed-control work queue (from CoverageInventory)

See `docs/COVERAGE.md` — the canonical, test-enforced list of remaining
Avalonia controls (35) that currently render blank, grouped by family.
`tests/Kumo.Avalonia.Tests/CoverageInventory.cs` fails when the real
inventory and the planned list diverge in either direction.

Floated out of the queue as accepted-unthemed: Avalonia base classes
(`TemplatedControl`, `SelectingItemsControl`, `Headered*Control`,
`TabStrip*`, `TextSelectionHandle`, `WindowBase`), the Avalonia 12 Page
navigation types, `NativeMenuBar`, `OverlayPopupHost`, `UserControl`.

| Item | Notes |
| --- | --- |
| Calendar / DatePicker | Redefine `CalendarDayButton`/`CalendarButton`/`CalendarItem` ControlThemes (solid accent fill selection, today = brand text). |
| Table (DataGrid) | If a real grid control is needed, add `Avalonia.Controls.DataGrid` + ControlTheme; composition presets cover static tables now. |
| Size variants (`size` prop) | `size-sm`/`size-lg` classes per control from upstream size tables (input sizes: xs 20, sm 26, base 36, lg 40/44). |
| FedRAMP theme | `theme-fedramp.css` (3 token overrides) — easy second generator pass. |
| Chart tokens | https://kumo-ui.com/charts/colors — only needed if charts get themed. |
| Motion tokens | `kumo-binding.css` — enter/exit animations exist for toast + collapsible; full token mapping optional (Kumo rule: no hover color transitions). |
| Icon set | `@phosphor-icons/react` — no Avalonia equivalent; demo uses inline stroke paths. |
| Size variants on Switch | Upstream `sm` (32×16) / `base` (36×18) / `lg` (40×20) tracks with matching square thumbs — only base implemented. |
| Dialog scrim (OS-window) | Resolved for the demo: the modal renders as an in-app overlay (`KumoBrushDialogScrim` + `dialog-surface`, no OS window involved). A see-through scrim on a real `Window.dialog` child window still needs `TransparencyLevelHint` + acrylic — platform-dependent. |

## 4. Known approximations

- Switch thumb is a 16px rounded-square (radius 5) inset 1px inside the 36×18 ring track; upstream thumb spans the ring-less track at full height with a `corner-shape:squircle` (10px radius when supported) — visually equivalent.
- Dark-mode unchecked switch thumb: `neutral-850` does not exist in the compiled CSS — approximated with `neutral-800`.
- `Border.badge.beta` uses a solid brand border; Avalonia `Border` cannot dash.
- Checkbox/radio glyphs are paths instead of Phosphor bold check/minus.
- Hover treatment follows the Kumo rule "no color transitions": no `Transitions` on colors anywhere.
- Expander animates content fade/slide (150 ms); upstream animates height (needs known content size — Avalonia has no layout animation).
- Toast close button: NotificationCard template has none — cards auto-dismiss (4 s in demo).
- Toolbar/menu bar dividers use `:nth-child(n+2)` left borders; upstream rounds only the outermost children — matched via `:nth-child(1)`/`:nth-last-child(1)`.
- Loader is a rotating arc approximation of the upstream SMIL dash animation (2 s rotation, round caps, 15% track).
- Link underline decoration color follows the text color (upstream uses 35% alpha `currentColor` underline via `color-mix`).
- Emphasis buttons: the `inset 0 1px 0 0 emphasis-bg` highlight is rendered as a `PART_TopSheen` 1px top-edge overlay (solid `emphasis-bg` per variant); gradient from = `white 15%` mix, hover starts at `white 30%`. Buttons get a `loading` class (spinning arc on `PART_Loading`, label hidden, hit-testing disabled).
- Toast deck overlap is fixed at 34px per older card (upstream overlaps by a fixed peek too); layout margin (not z-order) creates the stack.
- Table child clipping: `Border.table` uses `Padding=1` + `ClipToBounds` so row backgrounds don't overpaint the rounded border at the corners.

## 5. Design-rule checklist (from https://kumo-ui.com/skill.md)

- [x] 14 px content text (`KumoFontSizeBase` = 14)
- [x] Concentric radii: `KumoRadius*` scale + `ControlCornerRadius`/`OverlayCornerRadius` redefined
- [x] No hover color transitions
- [x] Ring instead of border+shadow: popup chrome = 1px line ring + shadow
- [x] `font-semibold` not `bold`: Medium/SemiBold presets only
- [x] Status solid + tint pairs: all generated
- [x] Sentence case demo copy
- [x] Inline mono: `TextBlock.code` (13px mono subtle) and `TextBlock.mono`
- [ ] Sticky separator preset (minor; use `Border.pagination-separator` pattern)

## 6. Verification loop

After each wave: `dotnet build` → `dotnet run --project tests/Kumo.Avalonia.Tests`
(one test per new control/preset) → eyeball `src/Kumo.Demo`.
