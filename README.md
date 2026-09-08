# Kumo for Avalonia

An [Avalonia 11/12](https://avaloniaui.net/) theme implementing [Kumo](https://kumo-ui.com/),
Cloudflare's product design system ([`@cloudflare/kumo`](https://github.com/cloudflare/kumo)).

## What is here

| Path | Purpose |
| --- | --- |
| `src/Kumo.Avalonia` | Theme library: generated palette, shared tokens, starter control styles |
| `src/Kumo.Demo` | Showcase app (surface/status swatches, buttons, inputs, light/dark toggle) |
| `tests/Kumo.Avalonia.Tests` | Headless tests verifying tokens and variant switching |
| `design/kumo/` | Upstream token CSS from `@cloudflare/kumo` v2.13.1 (source of truth) |
| `scripts/generate_tokens.py` | Converts upstream CSS (oklch / `light-dark()` / `color-mix`) to Avalonia XAML |
| `docs/TOKENS.md` | Generated reference of all 54 semantic color tokens and their resource keys |
| `docs/THEME-AUDIT.md` | What exists, what is missing for full parity, and how to get there |

## Quick start

```bash
dotnet build
dotnet run --project src/Kumo.Demo        # showcase window with light/dark toggle
dotnet run --project tests/Kumo.Avalonia.Tests
```

## Using the theme

Reference the resources and styles after a base theme (Fluent) so controls keep their templates:

```xml
<Application xmlns="https://github.com/avaloniaui"
             x:Class="MyApp.App"
             RequestedThemeVariant="Default">

    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceInclude Source="avares://Kumo.Avalonia/Themes/KumoTheme.axaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>

    <Application.Styles>
        <FluentTheme />
        <StyleInclude Source="avares://Kumo.Avalonia/Themes/Controls.axaml" />
    </Application.Styles>
</Application>
```

Switch light/dark with `Application.Current.RequestedThemeVariant = ThemeVariant.Dark`.
Every token is exposed twice: `<Color>` under `KumoColor*` and `<SolidColorBrush>` under `KumoBrush*`
(54 semantic tokens per variant, mapped 1:1 from the upstream CSS custom properties).
See `docs/TOKENS.md` for the full mapping.

### Class presets

Control look comes from style classes on standard Avalonia controls. Note that
`Classes` is **space-separated** in Avalonia XAML (a comma would form one
literal class name):

```xml
<Button Classes="primary" Content="Deploy" />
<Border Classes="badge success"><TextBlock Text="Healthy" /></Border>
<Border Classes="banner warning"><TextBlock Text="Certificate expires soon" /></Border>
<Border Classes="toast error"><TextBlock Text="Deployment failed" /></Border>
<ProgressBar Classes="success" Value="100" />
<Border Classes="layer-card"> ... </Border>
<Border Classes="skeleton-line" Width="240" />
```

Available presets: buttons (`primary`, `danger`, `ghost`), progress (`success`,
`danger`), badges (14 variants), banners (`info`, `warning`, `error`,
`secondary`), toasts (4 status variants), `layer-card`, `skeleton-line`,
checkbox/radio `error`.

## Regenerating the palette

The palette XAML is generated; never edit it by hand.

```bash
python3 scripts/generate_tokens.py          # regenerate Palette.axaml + docs/TOKENS.md
python3 scripts/generate_tokens.py --check  # exit 1 if stale (for CI)
```

To move to a newer Kumo release, refresh the CSS files in `design/kumo/`
(e.g. `https://unpkg.com/@cloudflare/kumo@X.Y.Z/dist/styles/theme-kumo.css`),
bump `KUMO_VERSION` in the script, and regenerate.

## Design rules baked in

The [`kumo-design` skill](https://kumo-ui.com/skill.md) rules that apply to a theme
port are captured in `docs/THEME-AUDIT.md` (14px content text, sentence case, no font-bold,
no hover color transitions, ring-instead-of-border shadows, concentric radii, ...).
