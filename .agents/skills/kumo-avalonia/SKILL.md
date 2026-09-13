---
name: kumo-avalonia
description: Use and explain the Kumo.Avalonia theme library — setup, tokens, class presets, Kumo* controls, and the token/spec regeneration pipeline. Triggers on "kumo", "Kumo.Avalonia", or questions about using the theme in an Avalonia app.
---

# Kumo.Avalonia

Kumo (Cloudflare's product design system, `@cloudflare/kumo`) as a
self-contained [Avalonia](https://avaloniaui.net/) theme. No base theme
(Fluent/Simple) is needed, and no reference to Fluent remains in the package.

Reference: https://kumo-ui.com/ — colors at https://kumo-ui.com/colors/,
design-rules skill at https://kumo-ui.com/skill.md.

## Using the theme in an app

Two entry points, both required. Resources first, styles second:

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
        <StyleInclude Source="avares://Kumo.Avalonia/Themes/Controls.axaml" />
    </Application.Styles>
</Application>
```

- Switch light/dark with `Application.Current.RequestedThemeVariant = ThemeVariant.Dark`.
- Every token is exposed twice: `<Color>` under `KumoColor*` and
  `<SolidColorBrush>` under `KumoBrush*` (54 semantic tokens per variant,
  mapped 1:1 from the upstream CSS custom properties). Full mapping lives in
  `docs/TOKENS.md`.

Look comes from style classes on standard Avalonia controls. `Classes` is
**space-separated** in Avalonia XAML (a comma would form one literal class
name):

```xml
<Button Classes="primary" Content="Deploy" />
<Border Classes="badge success"><TextBlock Text="Healthy" /></Border>
<Border Classes="banner warning"><TextBlock Text="Certificate expires soon" /></Border>
<Border Classes="toast error"><TextBlock Text="Deployment failed" /></Border>
<ProgressBar Classes="success" Value="100" />
<Border Classes="layer-card"> ... </Border>
<Border Classes="skeleton-line" Width="240" />
```

Available presets: buttons (`primary`, `danger`, `ghost`),
progress (`success`, `danger`), badges (14 variants), banners (`info`,
`warning`, `error`, `secondary`), toasts (4 status variants), `layer-card`,
`skeleton-line`, checkbox/radio `error`. See `docs/COVERAGE.md` for the
control-by-control inventory (test-enforced, so it is exact).

Real `Kumo*` composition controls live in `Kumo.Avalonia.Controls` namespace:
`KumoBadge` (Variant), `KumoAvatar`/`KumoAvatarGroup`, `KumoEmptyState`,
`KumoToolbar`, `KumoTabs` (native sliding indicator), `KumoTable`,
`KumoCalendarDatePicker` (text-completion validation), `ToastDeck`.

## Repo layout

- `src/Kumo.Avalonia` — theme library (palette + tokens + per-control ControlThemes
  in `Themes/Controls/*`; `Controls.axaml` is just the include registry)
- `src/Kumo.Demo` — showcase app with light/dark toggle
- `tests/Kumo.Avalonia.Tests` — Avalonia.Headless tests (xunit.v3; run via
  `dotnet run`, not `dotnet test`)
- `design/kumo/` — upstream CSS token source of truth from `@cloudflare/kumo` v2.13.1
- `scripts/generate_tokens.py` — CSS (oklch/light-dark/color-mix) -> Avalonia XAML
- `docs/` — token + coverage reference

## Maintenance commands

- Build everything: `dotnet build`
- Run demo app: `dotnet run --project src/Kumo.Demo`
- Run headless tests (no display needed): `dotnet run --project tests/Kumo.Avalonia.Tests`
- Regenerate tokens from `design/kumo/theme-kumo.css` into
  `src/Kumo.Avalonia/Themes/Palette.axaml` and `docs/TOKENS.md`:
  `python3 scripts/generate_tokens.py` (`--check` exits 1 when stale; CI-style)
- Regenerate reference component specs (needs the kumo npm tarball at
  `/tmp/opencode/kumo-pkg`): `python3 scripts/extract_component_specs.py`
  writes `design/specs/*.json`; `--check` verifies staleness. The headless
  SpecTests diff resolved control properties against those specs; accepted
  deviations live in `SpecTests.cs` `Accepted`.

## Design rules (from https://kumo-ui.com/skill.md)

- 14px base content text, sentence case, Medium/SemiBold (never `bold`)
- No color transitions on hover
- Ring instead of border + shadow; concentric radii
- Status = solid + tint pairs; tokens only, never hardcoded colors
