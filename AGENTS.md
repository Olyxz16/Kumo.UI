# Resources

## Docs

https://kumo-ui.com/

## Colors

https://kumo-ui.com/colors/

## Skill reference

https://kumo-ui.com/skill.md

## Commands

- Build everything: `dotnet build`
- Run demo app: `dotnet run --project src/Kumo.Demo`
- Regenerate tokens from `design/kumo/theme-kumo.css` into `src/Kumo.Avalonia/Themes/Palette.axaml` and `docs/TOKENS.md`: `python3 scripts/generate_tokens.py`
- Check generated tokens are up to date (CI-style): `python3 scripts/generate_tokens.py --check`
- Regenerate reference component specs (needs the kumo npm tarball at /tmp/opencode/kumo-pkg): `python3 scripts/extract_component_specs.py` (writes `design/specs/*.json`; `--check` verifies staleness)
- The headless SpecTests diff resolved control properties against those specs; accepted deviations live in SpecTests.cs `Accepted`
- Run headless theme tests (no display needed): `dotnet run --project tests/Kumo.Avalonia.Tests`

## Layout

- `src/Kumo.Avalonia` - the theme library (palette + tokens + control styles)
- `src/Kumo.Demo` - showcase app with light/dark toggle
- `tests/Kumo.Avalonia.Tests` - Avalonia.Headless tests (xunit.v3; run via `dotnet run`, not `dotnet test`)
- `design/kumo/` - upstream CSS token source of truth from `@cloudflare/kumo` v2.13.1
- `scripts/generate_tokens.py` - CSS (oklch/light-dark/color-mix) -> Avalonia XAML converter
- `docs/` - token reference and theme completeness audit

# NEW PLAN

# Kumo.Avalonia — Self-Contained Theme Migration Plan

## Goal
Replace all Fluent-dependent styling with self-owned ControlThemes + real `Kumo*` controls, keeping Fluent only as a transitional fallback until coverage completes. Repo, pipeline, and tests carry over unchanged.

## Target structure

```
src/Kumo.Avalonia/
  KumoXamlStatics.cs            (unchanged, grows per new controls)
  TabSlide.cs                   (dies in slice 7 — replaced by KumoTabs)
  Themes/
    Palette.axaml               (generated — untouched)
    Tokens.axaml                (generated — untouched)
    KumoTheme.axaml             (derived brushes — grows)
    Controls.axaml              (shrinks to: include registry for Controls/*)
    Controls/                   ← NEW: self-contained per-control ControlThemes
      Shared.axaml              ring borders, glyphs, state-helper styles
      Button.axaml   TextBox.axaml   ComboBox.axaml ...
  Controls/                     ← NEW: Kumo control classes
      KumoTabs.cs KumoToolbar.cs KumoBadge.cs KumoTable.cs KumoAvatar.cs KumoEmptyState.cs
      (each with sibling .axaml ControlTheme)
```

Consumer UX stays stable: `KumoTheme.axaml` + `Controls.axaml` remain the two entry points; the *contents* of `Controls.axaml` become pure own-theme includes.

## Slices (each ends green: build + tests + demo)

| # | Slice | Work | Files |
|---|---|---|---|
| 1 | **Buttons** | Full ControlTheme: layered template (root Border + inset top-highlight Border for emphasis gradient), pseudo-classes per upstream spec; port all classes (`primary/danger/ghost/icon*/breadcrumb/page*`); delete Fluent-overlay button styles | `Controls/Button.axaml`, edit `Controls.axaml`, test, audit |
| 2 | **TextBox** | Full template (multiline/password/watermark/reveal/selection), override Fluent's TextBox context-menu resources | + `TextBox.axaml` |
| 3 | **ComboBox + AutoCompleteBox** | Trigger, chevron, popup presenter, item ControlThemes | + 2 files |
| 4 | **Scroll + popup chrome** | ScrollViewer/ScrollBar (thin Kumo), ToolTip, FlyoutPresenter, MenuFlyout*, MenuItem, ContextMenu, Separator; DataValidationErrors | + several files |
| 5 | **Misc templates** | Slider, Expander, TabControl temp template, TransitionsTabItem?, Calendar*/DatePicker (audit §3) | + several files |
| 6 | **Switch sizes + variants polish** | Switch 32/36/40 tracks, remaining audit §3 size variants | edits |
| 7 | **Kumo* controls** | TemplatedControls with own classes: Tabs (sliding indicator native), Toolbar, Badge, Table (real selection semantics), Avatar, EmptyState | `Controls/*.cs+axaml` |
| 8 | **Fluent removal** | Coverage checklist, `<FluentTheme />` deleted from demo/README, FedRAMP generator pass, final audit sweep | README, Demo, audit |

## Constraints & gotchas (bake into every slice)

- Tokens only — no hardcoded colors; generated palette never hand-edited
- Pseudo-class parity driven by `design/specs/*.json`; one spec-diff headless test per converted control
- Kumo design rules stay enforced: no hover color transitions, ring-not-border shadows, Medium/SemiBold only
- Deleting a slice's old styles is part of that slice — no double-definition drift
- Demo stays runnable at every commit; Fluent fallback removed only in slice 8

## Exit criteria
- Zero Fluent references (`rg -i fluent src/` clean), all 48-component audit matrix mapped, tests green, demo visually matching kumo-ui.com in light + dark.
- Slice 1 estimated ~a day; total slices 1–8 ≈ 1–2 weeks.

Loose ends resolved during implementation (2026-09-10): DataGrid not wanted —
`KumoTable` was skipped; static table composition presets cover tables (a real
grid needs `Avalonia.Controls.DataGrid`, deliberately not bundled now).
`TabSlide` stays as an attached behavior consumed by both `TabControl` (test
locked) and the new `KumoTabs` control; it now drives the template-native
`PART_Indicator` pill (slide + scaleX morph between source/target tabs), so
the "template-native indicator" follow-on is done. Migration executed to completion:
all 8 slices landed — `Themes/Controls/*.axaml` ControlThemes, `Kumo*`
composition controls, and `<FluentTheme />` removed from demo + csproj, zero
`rg -i fluent src/` hits, 88 headless tests green.
