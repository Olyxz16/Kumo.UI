# Theme completeness audit

Status: **setup complete — full parity not yet reached.** Everything needed to *author*
the theme exists; what remains is per-control styling work plus a few upstream data
sources that are not part of `theme-kumo.css`.

Audited against `@cloudflare/kumo` v2.13.1.

## 1. Tools in place (ready)

| Tool | State |
| --- | --- |
| Token pipeline `scripts/generate_tokens.py` | Complete. Parses all 54 upstream semantic tokens (oklch, `light-dark()`, `color-mix(in oklch)`, `var()` fallbacks, alpha), converts oklch→sRGB (verified against culori), emits `Palette.axaml` Light/Dark ThemeDictionaries + `docs/TOKENS.md`. `--check` mode for CI. |
| Color tokens | Complete: 54/54 upstream tokens × Light/Dark, both `KumoColor*` (raw) and `KumoBrush*` resources, applied via `ThemeDictionaries` so variant switching is automatic. |
| Non-color tokens (`Tokens.axaml`) | Present: font sizes 12/13/14/16/20/24/30 px (Kumo overrides Tailwind: content text = 14), 4 px spacing scale, radii 4/6/8/12/full, shadows xs/sm/md + card ring-shadow, stroke widths. |
| Starter control styles (`Controls.axaml`) | Button (secondary default + `primary`/`danger`/`ghost`/disabled), TextBox (hover/focus/disabled), ToolTip, CheckBox/RadioButton foreground, `Border.layer-card`. |
| Demo app (`src/Kumo.Demo`) | Surfaces, status pills, buttons, inputs, text scale; live light/dark toggle. |
| Headless tests (`tests/Kumo.Avalonia.Tests`) | 4 passing: token counts per variant, upstream value spot-checks, DynamicResource variant switching, style application. Run with `dotnet run --project tests/Kumo.Avalonia.Tests`. |
| Upstream snapshots (`design/kumo/`) | `theme-kumo.css` (tokens), `theme-fedramp.css`, `kumo.css`, `kumo-standalone.css` (component CSS), `kumo-binding.css` (animations/vars). |

## 2. Remaining for full parity

### 2a. Control styling (the bulk of the work)

Kumo is a React/Tailwind library, so each component must be re-expressed as Avalonia
`ControlTheme`s layered on Fluent (current approach) or as standalone control themes.
Suggested mapping and status:

| Kumo component | Avalonia target | Status |
| --- | --- | --- |
| Button | `Button` (+ `primary`/`danger`/`ghost` classes) | Styled |
| Input / InputArea / InputGroup / Sensitive Input | `TextBox` / `MaskedTextBox` / composed | Partial (plain TextBox only) |
| Label / Text | `TextBlock`, `Label` | Token-level only (font sizes in Tokens.axaml) |
| Checkbox | `CheckBox` | Foreground only — box visuals still Fluent |
| Radio | `RadioButton` | Foreground only |
| Switch | `ToggleSwitch` | Not restyled (Fluent default) |
| Select / Combobox / Autocomplete | `ComboBox` / `AutoCompleteBox` | Not restyled |
| Dialog / Popover / Dropdown / Toast / Tooltip | `Window`+`OverlayPopupHost` / `Popup` / `Flyout` / `ToolTip` / `NotificationCard` | ToolTip restyled; rest Fluent |
| Tabs / Toolbar / Breadcrumbs / Pagination | `TabControl` / `ToolBar` / ItemsControl | Not restyled |
| Table | `DataGrid` or custom `ItemsControl` | Not restyled (needs DataGrid package) |
| Layer Card | `Border.layer-card` class | Styled (class-level) |
| Badge / Banner | `Border` + text presets | Token-level only (badge/banner tint tokens exist) |
| Loader / Skeleton Line | `ProgressBar` / animation | Not restyled |
| Meter / Progress | `ProgressBar` | Not restyled |
| Collapsible | `Expander` | Not restyled |
| Sidebar / Grid / Flow / Empty / Clipboard Text / Command Palette / Date Picker / Table of Contents / Cloudflare Logo / CodeHighlighted | App-level compositions; no direct Avalonia primitive | Out of theme scope (documented) |

### 2b. Upstream data gaps (sources outside `theme-kumo.css`)

| Gap | Upstream source | Notes |
| --- | --- | --- |
| Chart color ramps | https://kumo-ui.com/charts/colors | 0 `chart` tokens in theme CSS; needed only if charts are themed. |
| Motion tokens | `kumo-binding.css` (`--animate-*`, `--ease-bounce`, transition durations) + dialog/skeleton keyframes | Kumo rule: no hover color transitions; motion is for enter/exit + skeletons. Needs an Avalonia `Animation`/`Transitions` mapping. |
| Icon set | `@phosphor-icons/react` | No direct Avalonia equivalent; pick Phosphor font/SVG port or Fluent icons. Theme only needs sizes/alignment. |
| FedRAMP theme | `design/kumo/theme-fedramp.css` | Only 3 overrides (`canvas`, `base`, `hairline`). Easy: add a second generator pass emitting `Kumo.FedRamp.axaml` merged dictionaries, or a `FedRamp` ThemeDictionary group. |
| Exact font family | `ui-sans-serif` system stack | Not mandated upstream. Demo ships Inter via `Avalonia.Fonts.Inter`; document the substitution. |
| Density/size variants | Kumo component props (`size`) | Recreate as style classes (`size-sm` etc.) per control, from component CSS in `kumo-standalone.css`. |

### 2c. Design-rule checklist (from https://kumo-ui.com/skill.md)

Rules a complete theme must honor, and current standing:

- [x] 14 px content text (`KumoFontSizeBase` = 14; Fluent default overridden in demo window)
- [x] Concentric radii: `KumoRadius*` scale (outer = inner + padding) — enforce in composition docs
- [ ] **No hover color transitions**: Fluent ControlThemes still animate; strip `Transitions` when restyling each control
- [ ] **No border + drop shadow** (use ring): Avalonia has no CSS ring; emulate with 1 px `BorderBrush` matching surface + `BoxShadow` (as `layer-card` does), or accept hairline borders for sticky separators
- [ ] `font-semibold` not `bold`: use `FontWeight.SemiBold`/`Medium` presets; never `Bold` (starter styles comply)
- [ ] Sentence case: content responsibility, not the theme
- [ ] Inline mono at 0.9 em: needs a `TextBlock.mono` style once mono styling is added
- [x] Status solid + tint pairs: `KumoBrush{Info,Success,Warning,Danger}` and `KumoBrush{...}Tint` both generated
- [ ] Sticky borders: provide `Border.sticky-separator` preset when chrome controls are styled

## 3. Suggested roadmap

1. **Controls wave 1** — ToggleSwitch, CheckBox/RadioButton visuals, ComboBox, TabControl, Expander, ProgressBar (strip hover transitions while restyling).
2. **Overlays wave 2** — Popup/Flyout/Window chrome, Toast notifications, Dialog pattern with the "never conditionally render" rule mapped to Avalonia lifetime.
3. **Feedback** — Badge/Banner presets from existing tint tokens, Loader/Skeleton animations from `kumo-binding.css`.
4. **FedRAMP** — second generator pass + theme switch key.
5. **Charts** (optional) — fetch ramp values from the Charts docs page and emit `KumoChart*` tokens.

## 4. Verification loop

After each wave: `dotnet build` → `dotnet run --project tests/Kumo.Avalonia.Tests`
(add one test per control) → eyeball `src/Kumo.Demo`.
