# Theme completeness audit

Status: **controls wave complete.** All 54 semantic tokens plus the raw Tailwind
primitives are generated, and the core interactive controls are themed and
covered by headless tests. What remains is composition-level work (documented
in section 2).

Audited against `@cloudflare/kumo` v2.13.1. Component specs were extracted from
the unminified component chunks shipped in the npm package (`dist/chunks/*.js`
carry the exact Tailwind class strings per state).

## 1. In place (ready + verified)

| Area | State |
| --- | --- |
| Token pipeline `scripts/generate_tokens.py` | Complete. Parses all 54 upstream semantic tokens (oklch, `light-dark()`, `color-mix(in oklch)`, `var()` fallbacks, alpha) **plus the raw Tailwind palette primitives** (`blue-*`, `neutral-*`, `emerald-*`, ...) that component classes reference, converts oklch→sRGB (verified against culori), emits `Palette.axaml` + `docs/TOKENS.md`. `--check` mode for CI. |
| Color tokens | Complete: 54/54 semantic tokens × Light/Dark (Color + Brush each) and 60+ primitives, applied via `ThemeDictionaries`. |
| Non-color tokens (`Tokens.axaml`) | Complete: font sizes 12-30 px, 4 px spacing scale, radii 4/6/8/12/full, shadows xs/sm/md/lg + card, stroke widths. |
| Derived component brushes (`KumoTheme.axaml`) | Light/Dark pairs the components need that upstream expresses as `dark:` pairs of primitives (switch track/thumb, focus ring 50%, danger ring 50%, skeleton fill, toast backdrop). Fluent's `ControlCornerRadius`/`OverlayCornerRadius` and the ComboBox popup resources are redefined to Kumo values inside `Controls.axaml` resources so base-theme chrome follows Kumo radii too. |
| Controls (`Controls.axaml`) | Button (primary/danger/ghost + focus ring), TextBox (line ring, focus ring `kumo-focus/50`), ToolTip, **ToggleSwitch** (36×18 squircle track, 18px thumb, blue-500/600 checked; full ControlTheme), **CheckBox** (16px box, contrast fill + check/minus glyphs, error variant), **RadioButton** (2px line ring, contrast + dot), **ComboBox + ComboBoxItem** (control trigger, base popup, tint highlight), **TabControl/TabItem** (segmented: recessed list, base active tab with line ring + shadow), **ProgressBar** (brand/success/danger classes), **Expander** (left-border content), **FlyoutPresenter/MenuFlyoutPresenter/ContextMenu/MenuItem/Separator** (base surface, line ring, tint highlight, hairline separators), **Badge presets** (primary/secondary/info/success/warning/error/outline/beta + 7 color badges), **Banner presets** (info/warning/error/secondary), **Toast presets** (status ring colors), **Skeleton line**. |
| Demo app (`src/Kumo.Demo`) | Surfaces, status pills, buttons, inputs (checkbox states, radios, switches, combobox), segmented tabs, badges (2 rows), banners, progress bars, menus/flyout/popover, collapsible, toasts, text scale; live light/dark toggle. |
| Headless tests (`tests/Kumo.Avalonia.Tests`) | 10 passing: token counts per variant, primitives present, upstream value spot-checks, variant switching, button/switch/checkbox/combobox/tab/badge styling resolution. Run with `dotnet run --project tests/Kumo.Avalonia.Tests`. |
| Upstream snapshots (`design/kumo/`) | `theme-kumo.css` (tokens), `theme-fedramp.css`, `kumo.css`, `kumo-standalone.css` (component CSS), `kumo-binding.css` (animations/vars). |

## 2. Remaining for full parity

### 2a. Composition-level work

| Kumo component | Avalonia target | Status |
| --- | --- | --- |
| Dialog | `Window` + `OverlayPopupHost` chrome | Not styled (needs window chrome + scrim decisions) |
| Toast (live notifications) | `NotificationCard` ControlTheme + `WindowNotificationManager` | Done: Kumo toast surface, status ring via `:success`/`:warning`/`:error`/`:information` pseudoclasses, enter/exit animation, 4s auto-dismiss in demo |
| Table | `DataGrid` (needs Avalonia.Controls.DataGrid package) | Not styled |
| Select with search / Combobox / Autocomplete | `AutoCompleteBox` restyle | Not styled |
| Input group / sensitive input / InputArea | composed TextBox styles | Partial (plain TextBox only) |
| Sidebar / Grid / Flow / Empty / Command Palette / Date Picker / Table of Contents / Clipboard Text / CodeHighlighted / Cloudflare Logo | App-level compositions | Out of theme scope |
| Chart color ramps | https://kumo-ui.com/charts/colors | Not extracted (only needed for charts) |
| Motion tokens | `kumo-binding.css` (`--animate-*`, `--ease-bounce`) | Not mapped to Avalonia `Animation`s (Kumo rule: no hover color transitions; motion is enter/exit only) |
| Icon set | `@phosphor-icons/react` | No Avalonia equivalent; pick Phosphor port or Fluent icons |
| FedRAMP theme | `theme-fedramp.css` (3 token overrides) | Easy second generator pass |
| Size variants (`size` prop on Button/Switch/Tabs/...) | `size-sm`/`size-lg` style classes | Not added |

### 2b. Known approximations

- Switch thumb is a circle (Avalonia Ellipse); upstream is a squircle (`corner-shape`). Radius 5 track matches.
- Dark-mode unchecked switch thumb: upstream uses `neutral-850`, which does not exist in the compiled CSS (not tree-shaken out) — approximated with `neutral-800`.
- `Border.badge.beta` uses a solid brand border; Avalonia `Border` cannot dash. 
- Checkbox/radio glyph icons are paths instead of Phosphor bold check/minus (same geometry, 12px equivalent).
- Hover treatment follows the Kumo rule "no color transitions": no `Transitions` on colors anywhere.
- Expander expands without a height animation; upstream Kumo animates the collapsible via motion tokens (see roadmap).
- Toast close button: upstream toasts have a close X; the Avalonia NotificationCard template has none — cards auto-dismiss (4s in demo) instead.

### 2c. Design-rule checklist (from https://kumo-ui.com/skill.md)

- [x] 14 px content text (`KumoFontSizeBase` = 14)
- [x] Concentric radii: `KumoRadius*` scale + `ControlCornerRadius`/`OverlayCornerRadius` redefined
- [x] No hover color transitions
- [x] Ring instead of border+shadow: popup chrome = 1px line ring + shadow (as upstream)
- [x] `font-semibold` not `bold`: Medium/SemiBold presets only
- [x] Status solid + tint pairs: all generated
- [x] Sentence case demo copy
- [ ] Inline mono at 0.9 em: needs a `TextBlock.mono` style (pending)
- [ ] Sticky separator preset (pending)

## 3. Suggested roadmap

1. **Dialogs + live toasts** — Window/OverlayPopup chrome, scrim, `NotificationCard` ControlTheme.
2. **Table** — add DataGrid package + ControlTheme.
3. **Size variants** — `size-sm`/`size-lg` classes per control from upstream size tables.
4. **FedRAMP** — second generator pass + theme switch key.
5. **Charts** (optional) — fetch ramp values and emit `KumoChart*` tokens.

## 4. Verification loop

After each wave: `dotnet build` → `dotnet run --project tests/Kumo.Avalonia.Tests`
(add one test per control) → eyeball `src/Kumo.Demo`.
