#!/usr/bin/env python3
"""Kumo -> Avalonia token pipeline.

Reads the Kumo theme CSS (source of truth, downloaded from the
@cloudflare/kumo npm package) and generates:

  1. src/Kumo.Avalonia/Themes/Palette.axaml  - Light/Dark ThemeDictionaries
     with Color + SolidColorBrush resources for every semantic token.
  2. docs/TOKENS.md - human-readable token reference (CSS name, light, dark,
     Avalonia resource keys).

The CSS uses Tailwind v4 features Avalonia cannot parse directly:
  - oklch() colors (e.g. oklch(0.5772 0.2324 260))
  - light-dark(light, dark) mode pairs
  - color-mix(in oklch, <color>, black 10%)
  - var(--name, <fallback>) references
All values are resolved here and emitted as #RRGGBB / #AARRGGBB hex.

Usage:
    python3 scripts/generate_tokens.py            # generate + write files
    python3 scripts/generate_tokens.py --check    # fail if outputs are stale
"""

from __future__ import annotations

import math
import re
import sys
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "design" / "kumo" / "theme-kumo.css"
# Fixed palette shades referenced by upstream component classes (e.g. the
# switch track colors) live in the compiled CSS; exposed for control styles.
PRIMITIVES_SRC = ROOT / "design" / "kumo" / "kumo-standalone.css"
OUT_AXAML = ROOT / "src" / "Kumo.Avalonia" / "Themes" / "Palette.axaml"
OUT_MD = ROOT / "docs" / "TOKENS.md"
KUMO_VERSION = "2.13.1"

RGBA = tuple[float, float, float, float]  # r,g,b,a each 0..1


# --------------------------------------------------------------------------
# oklch -> sRGB (Bjorn Ottosson's Oklab, reference implementation)
# --------------------------------------------------------------------------

def _oklab_to_linear_srgb(L: float, a: float, b: float) -> tuple[float, float, float]:
    l_ = L + 0.3963377774 * a + 0.2158037573 * b
    m_ = L - 0.1055613458 * a - 0.0638541728 * b
    s_ = L - 0.0894841775 * a - 1.2914855480 * b
    l, m, s = l_ * l_ * l_, m_ * m_ * m_, s_ * s_ * s_
    return (
        +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
        -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
        -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s,
    )


def _gamma(c: float) -> float:
    c = min(1.0, max(0.0, c))
    return 12.92 * c if c <= 0.0031308 else 1.055 * math.pow(c, 1 / 2.4) - 0.055


def _oklch_to_rgba(L: float, C: float, H: float, alpha: float) -> RGBA:
    h = math.radians(H)
    r, g, b = _oklab_to_linear_srgb(L, C * math.cos(h), C * math.sin(h))
    return (_gamma(r), _gamma(g), _gamma(b), alpha)


def _rgba_to_hex(c: RGBA) -> str:
    r, g, b, a = (round(v * 255) for v in c)
    return f"#{a:02X}{r:02X}{g:02X}{b:02X}" if a < 255 else f"#{r:02X}{g:02X}{b:02X}"


# --------------------------------------------------------------------------
# CSS value parsing
# --------------------------------------------------------------------------

def _split_top_level(s: str, sep: str = ",") -> list[str]:
    parts, depth, cur = [], 0, []
    for ch in s:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        if ch == sep and depth == 0:
            parts.append("".join(cur).strip())
            cur = []
        else:
            cur.append(ch)
    parts.append("".join(cur).strip())
    return parts


_NUM = r"-?\d*\.?\d+"


def _srgb_to_oklab(r: float, g: float, b: float) -> tuple[float, float, float]:
    def lin(c: float) -> float:
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = lin(r), lin(g), lin(b)
    l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b
    m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b
    s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b
    l_, m_, s_ = l ** (1 / 3), m ** (1 / 3), s ** (1 / 3)
    return (
        0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
        1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
        0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_,
    )


def _parse_color(expr: str) -> RGBA:
    s = expr.strip()
    if s == "transparent":
        return (0.0, 0.0, 0.0, 0.0)
    named = {"black": (0, 0, 0), "white": (255, 255, 255)}
    if s.lower() in named:
        return (*(v / 255 for v in named[s.lower()]), 1.0)
    if s.startswith("#"):
        h = s[1:]
        if len(h) in (3, 4):
            h = "".join(ch * 2 for ch in h)
        r, g, b = (int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
        a = int(h[6:8], 16) / 255 if len(h) == 8 else 1.0
        return (r, g, b, a)
    if s.startswith("color-mix("):
        inner = s[len("color-mix("):-1].strip()
        parts = _split_top_level(inner)
        # e.g. "in oklch, oklch(...) , black 10%"
        rest = parts[1:]
        if len(rest) == 1:  # comma after "in oklch" only; re-split
            rest = _split_top_level(",".join(rest))
        c1 = _parse_color(rest[0])
        second = rest[1].split()
        if len(second) == 2:  # "<color> <pct>%"
            pct = float(second[1].rstrip("%")) / 100
            c2, w2 = _parse_color(second[0]), pct
        else:
            c2, w2 = _parse_color(rest[1]), 0.5
        w1 = 1.0 - w2
        space = parts[0].strip().lower()
        if space in ("in oklch", "in oklab"):  # premultiplied-alpha Oklab mix
            a1, a2 = c1[3], c2[3]
            lab1, lab2 = _srgb_to_oklab(*c1[:3]), _srgb_to_oklab(*c2[:3])
            alpha = w1 * a1 + w2 * a2
            if alpha <= 0:
                return (0.0, 0.0, 0.0, 0.0)
            L = (w1 * lab1[0] * a1 + w2 * lab2[0] * a2) / alpha
            a = (w1 * lab1[1] * a1 + w2 * lab2[1] * a2) / alpha
            b = (w1 * lab1[2] * a1 + w2 * lab2[2] * a2) / alpha
            r, g, bb = _oklab_to_linear_srgb(L, a, b)
            return (_gamma(r), _gamma(g), _gamma(bb), alpha)
        return (w1 * c1[0] + w2 * c2[0], w1 * c1[1] + w2 * c2[1],
                w1 * c1[2] + w2 * c2[2], w1 * c1[3] + w2 * c2[3])
    m = re.fullmatch(rf"oklch\(\s*({_NUM})(%)?\s+({_NUM})\s+({_NUM})(?:\s*/\s*({_NUM})(%)?)?\s*\)", s)
    if m:
        L = float(m.group(1)) / 100 if m.group(2) else float(m.group(1))
        C, H = float(m.group(3)), float(m.group(4))
        alpha = float(m.group(5)) if m.group(5) else 1.0
        if m.group(6):
            alpha /= 100
        return _oklch_to_rgba(L, C, H, alpha)
    m = re.fullmatch(rf"rgb\(\s*{_NUM}%?\s+{_NUM}%?\s+{_NUM}%?(?:\s*/\s*{_NUM}%?)?\s*\)", s)
    if m:
        vals = [float(v) / 100 if "%" in seg else float(v)
                for v, seg in zip(m.group(1, 2, 3), re.split(r"\s+", s.strip("rgb() ")))]
        alpha = float(m.group(4)) if m.group(4) else 1.0
        return (vals[0], vals[1], vals[2], alpha)
    raise ValueError(f"Unsupported color value: {expr!r}")


def _resolve(expr: str) -> RGBA:
    s = expr.strip()
    if s.startswith("var("):
        inner = s[len("var("):-1].strip()
        parts = _split_top_level(inner)
        name = parts[0].strip().lstrip("-")
        # defined vars win: the inline fallbacks in the compiled CSS are stale
        # duplicates (e.g. --color-neutral-900's fallback predates the theme).
        if name in _PRIMITIVES_BY_CSS_NAME:
            return _PRIMITIVES_BY_CSS_NAME[name]
        if len(parts) > 1:
            return _resolve(",".join(parts[1:]))  # fallback holds the real value
        raise ValueError(f"var() without fallback: {expr!r}")
    return _parse_color(s)


# CSS var name -> RGBA, filled in by parse_primitives before token resolution.
_PRIMITIVES_BY_CSS_NAME: dict[str, RGBA] = {}


def _mix_oklab(c1: RGBA, c2: RGBA, w2: float) -> RGBA:
    """Mix c1 with w2 of c2 in Oklab (matches CSS color-mix in oklch)."""
    lab1, lab2 = _srgb_to_oklab(*c1[:3]), _srgb_to_oklab(*c2[:3])
    w1 = 1.0 - w2
    L = w1 * lab1[0] + w2 * lab2[0]
    a = w1 * lab1[1] + w2 * lab2[1]
    b = w1 * lab1[2] + w2 * lab2[2]
    r, g, bb = _oklab_to_linear_srgb(L, a, b)
    return (_gamma(r), _gamma(g), _gamma(bb), w1 * c1[3] + w2 * c2[3])


WHITE: RGBA = (1.0, 1.0, 1.0, 1.0)
BLACK: RGBA = (0.0, 0.0, 0.0, 1.0)


def _emphasis(base: RGBA) -> dict[str, RGBA]:
    """Upstream emphasis button recipe (getEmphasisStyle in the button chunk):
    ring = token+black 10%, bg = token+white 30%, sheen = token+white 15% -> token."""
    return {
        "ring": _mix_oklab(base, BLACK, 0.10),
        "bg": _mix_oklab(base, WHITE, 0.30),
        "sheen": _mix_oklab(base, WHITE, 0.15),
        "end": base,
    }


def _gradient_brush(key: str, from_hex: str, to_hex: str) -> str:
    return (f'            <LinearGradientBrush x:Key="{key}" StartPoint="0,0" EndPoint="0,1">\n'
            f'                <GradientStop Color="{from_hex}" Offset="0" />\n'
            f'                <GradientStop Color="{to_hex}" Offset="1" />\n'
            f'            </LinearGradientBrush>\n')


def parse_primitives(css: str) -> dict[str, RGBA]:
    """Parse fixed `--color-<family>-<shade>` component base colors (no variant).

    All `--color-*` names (including kumo-scoped ones like --color-kumo-neutral-975)
    are registered for var() resolution; only non-kumo families are exported as
    KumoColor* resources.
    """
    out: dict[str, RGBA] = {}
    body = "\n".join(re.findall(r"@layer\s+theme\s*\{(.*?)\n?\}", css, re.S))
    for m in re.finditer(r"--color-([a-z0-9-]+)\s*:\s*([^;]+)", body):
        family, value = m.group(1), m.group(2).strip()
        if "light-dark(" in value:
            continue  # semantic tokens are handled by parse_tokens
        _PRIMITIVES_BY_CSS_NAME[f"color-{family}"] = _resolve(value)
        if not family.startswith("kumo"):
            out[_pascal(family)] = _PRIMITIVES_BY_CSS_NAME[f"color-{family}"]
    return out


@dataclass
class Token:
    css: str          # full CSS custom property name
    kind: str         # "color" | "text-color"
    name: str         # kumo suffix, e.g. "canvas", "text-default"
    light: RGBA
    dark: RGBA
    color_key: str
    brush_key: str


def _pascal(name: str) -> str:
    return "".join(p.capitalize() for p in re.split(r"-", name) if p)


def parse_tokens(css: str) -> list[Token]:
    tokens: list[Token] = []
    bodies = re.findall(r"@theme\s*\{(.*?)\n\}", css, re.S)
    if not bodies:
        raise ValueError("no @theme blocks found")
    body = "\n".join(bodies)
    pattern = re.compile(r"(--(color|text-color)-kumo-([a-z0-9-]+)):\s*light-dark\(", re.M)
    for m in pattern.finditer(body):
        start = m.end() - 1
        depth, i = 0, start
        while True:
            if body[i] == "(":
                depth += 1
            elif body[i] == ")":
                depth -= 1
                if depth == 0:
                    break
            i += 1
        light_expr, dark_expr = _split_top_level(body[start + 1:i])
        kind, name = m.group(2), m.group(3)
        pascal = _pascal(name)
        color_key = f"KumoColor{'Text' if kind == 'text-color' else ''}{pascal}"
        tokens.append(Token(m.group(1), kind, name,
                            _resolve(light_expr), _resolve(dark_expr),
                            color_key, color_key.replace("Color", "Brush", 1)))
    return tokens


# --------------------------------------------------------------------------
# Emitters
# --------------------------------------------------------------------------

HEADER = "Generated by scripts/generate_tokens.py from design/kumo/theme-kumo.css (@cloudflare/kumo v{v}). DO NOT EDIT."


def emit_axaml(tokens: list[Token], primitives: dict[str, RGBA]) -> str:
    by_name = {t.name: t for t in tokens}
    emphasis = {}
    for variant, token_name in (("Primary", "brand"), ("Danger", "danger")):
        t = by_name[token_name]
        emphasis[variant] = {"light": _emphasis(t.light), "dark": _emphasis(t.dark)}
    lines = [
        '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
        f"    <!-- {HEADER.format(v=KUMO_VERSION)} -->",
        "    <!-- Component base colors used by control styles (variant-independent) -->",
    ]
    for name in sorted(primitives):
        hex_val = _rgba_to_hex(primitives[name])
        lines.append(f'    <Color x:Key="KumoColor{name}">{hex_val}</Color>')
    for name in sorted(primitives):
        hex_val = _rgba_to_hex(primitives[name])
        lines.append(f'    <SolidColorBrush x:Key="KumoBrush{name}" Color="{hex_val}" />')
    lines += [
        "    <ResourceDictionary.ThemeDictionaries>",
    ]
    for variant in ("Light", "Dark"):
        lines.append(f'        <ResourceDictionary x:Key="{variant}">')
        for t in tokens:
            hex_val = _rgba_to_hex(t.light if variant == "Light" else t.dark)
            lines.append(f'            <Color x:Key="{t.color_key}">{hex_val}</Color>')
        lines.append("            <!-- Brushes -->")
        for t in tokens:
            hex_val = _rgba_to_hex(t.light if variant == "Light" else t.dark)
            lines.append(f'            <SolidColorBrush x:Key="{t.brush_key}" Color="{hex_val}" />')
        mode = variant.lower()
        for label in ("Primary", "Danger"):
            e = emphasis[label][mode]
            lines.append(_gradient_brush(f"KumoBrushButton{label}Gradient",
                                         _rgba_to_hex(e["sheen"]), _rgba_to_hex(e["end"])))
            lines.append(_gradient_brush(f"KumoBrushButton{label}GradientHover",
                                         _rgba_to_hex(e["bg"]), _rgba_to_hex(e["end"])))
            lines.append(f'            <SolidColorBrush x:Key="KumoBrushButton{label}Ring" '
                         f'Color="{_rgba_to_hex(e["ring"])}" />')
        lines.append("        </ResourceDictionary>")
    lines += ["    </ResourceDictionary.ThemeDictionaries>", "</ResourceDictionary>", ""]
    return "\n".join(lines)


def emit_md(tokens: list[Token], primitives: dict[str, RGBA]) -> str:
    out = [
        "# Kumo color tokens (Avalonia resource reference)",
        "",
        f"Source: `design/kumo/theme-kumo.css` from `@cloudflare/kumo` v{KUMO_VERSION},",
        "converted by `scripts/generate_tokens.py`. Values are sRGB hex,",
        "`#AARRGGBB` when the token carries alpha.",
        "",
        f"{len(tokens)} semantic tokens. Set `RequestedThemeVariant` (Light/Dark) to switch.",
        "",
        "| CSS token | Light | Dark | Color key | Brush key |",
        "| --- | --- | --- | --- | --- |",
    ]
    for t in tokens:
        out.append(f"| `{t.css}` | `{_rgba_to_hex(t.light)}` | `{_rgba_to_hex(t.dark)}` "
                   f"| `{t.color_key}` | `{t.brush_key}` |")
    out += [
        "",
        f"## Component base colors ({len(primitives)})",
        "",
        "Fixed shades referenced by upstream component classes (switch tracks,",
        "checkbox fills, badge colors), parsed from `design/kumo/kumo-standalone.css`.",
        "Variant-independent (`KumoColor*` / `KumoBrush*`). Prefer semantic tokens in app UI.",
        "",
        "CSS var | Hex | Color key |",
        "| --- | --- | --- |",
    ]
    for name in sorted(primitives):
        out.append(f"| `--color-{name.lower()}` | `{_rgba_to_hex(primitives[name])}` | `KumoColor{name}` |")
    out.append("")
    return "\n".join(out)


def main() -> int:
    primitives = parse_primitives(PRIMITIVES_SRC.read_text())
    tokens = parse_tokens(SRC.read_text())
    if not tokens:
        print("error: no tokens parsed", file=sys.stderr)
        return 1
    primitives = parse_primitives(PRIMITIVES_SRC.read_text())
    axaml, md = emit_axaml(tokens, primitives), emit_md(tokens, primitives)
    if "--check" in sys.argv:
        stale = (OUT_AXAML.exists() and OUT_AXAML.read_text() != axaml) or \
                (OUT_MD.exists() and OUT_MD.read_text() != md)
        print("stale" if stale else "up to date")
        return 1 if stale else 0
    OUT_AXAML.parent.mkdir(parents=True, exist_ok=True)
    OUT_MD.parent.mkdir(parents=True, exist_ok=True)
    OUT_AXAML.write_text(axaml)
    OUT_MD.write_text(md)
    print(f"wrote {OUT_AXAML.relative_to(ROOT)} and {OUT_MD.relative_to(ROOT)} ({len(tokens)} tokens)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
