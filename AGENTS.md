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
- Run headless theme tests (no display needed): `dotnet run --project tests/Kumo.Avalonia.Tests`

## Layout

- `src/Kumo.Avalonia` - the theme library (palette + tokens + control styles)
- `src/Kumo.Demo` - showcase app with light/dark toggle
- `tests/Kumo.Avalonia.Tests` - Avalonia.Headless tests (xunit.v3; run via `dotnet run`, not `dotnet test`)
- `design/kumo/` - upstream CSS token source of truth from `@cloudflare/kumo` v2.13.1
- `scripts/generate_tokens.py` - CSS (oklch/light-dark/color-mix) -> Avalonia XAML converter
- `docs/` - token reference and theme completeness audit
