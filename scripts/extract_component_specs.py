#!/usr/bin/env python3
"""Extract normalized component specs from the Kumo reference CSS.

Source of truth: the vendored CSS in design/kumo/ (committed copies of
@cloudflare/kumo v2.13.1 dist styles). No npm tarball needed.

Pipeline:
  1. Parse kumo-standalone.css (compiled Tailwind, ~1400 utility rules) into
     a class-token index: token -> [(order, state, {prop: value})].
  2. Parse every custom property (theme-kumo.css + standalone :root) into a
     var table. Values resolve recursively with light/dark branches,
     color-mix(in oklch/oklab), oklch, hex, rem/px.
  3. Resolve each curated fixture's class tokens (per UI state) against the
     index with tailwind-merge semantics (last class wins per property,
     !important beats order), producing a normalized IR in px/hex.
  4. Emit design/specs/<fixture>.json for the headless Avalonia snapshotter
     (tests/Kumo.Avalonia.Tests/SpecTests.cs) to diff against.

Usage:
    python3 scripts/extract_component_specs.py            # write specs
    python3 scripts/extract_component_specs.py --check    # exit 1 if stale
    python3 scripts/extract_component_specs.py -v button-primary
"""

from __future__ import annotations

import json
import math
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PKG = ROOT / "design" / "kumo"
STANDALONE = PKG / "kumo-standalone.css"
THEME = PKG / "theme-kumo.css"
OUT_DIR = ROOT / "design" / "specs"
KUMO_VERSION = "2.13.1"

sys.path.insert(0, str(ROOT / "scripts"))
from generate_tokens import _oklab_to_linear_srgb, _gamma, _srgb_to_oklab  # noqa: E402

RGBA = tuple[float, float, float, float]

# ---------------------------------------------------------------------------
# CSS parsing (minified, nested at-rules)
# ---------------------------------------------------------------------------


@dataclass
class Rule:
    order: int          # source order (cascade tiebreak)
    token: str          # class token as written in markup, e.g. "hover:bg-kumo-tint"
    state: str          # pseudo/attr condition, e.g. "hover" or "" for base
    decls: dict[str, str]
    media: str = ""     # media condition ("" = unconditional)


def parse_rules(css: str) -> list[Rule]:
    rules: list[Rule] = []
    counter = [0]

    def parse_block(body: str, media: str) -> None:
        i, n = 0, len(body)
        sel: list[str] = []
        while i < n:
            c = body[i]
            if c == "{":
                s = "".join(sel).strip().replace("\n", " ")
                sel = []
                j, d = i + 1, 1
                while j < n and d:
                    if body[j] == "{":
                        d += 1
                    elif body[j] == "}":
                        d -= 1
                    j += 1
                inner = body[i + 1:j - 1]
                if s.startswith("@"):
                    if re.match(r"@media\b", s):
                        parse_block(inner, s)
                    elif re.match(r"@(supports|container)\b", s):
                        parse_block(inner, "supports")
                    elif re.match(r"@layer\b", s):
                        parse_block(inner, media)
                    # @keyframes/@property/@font-face/@custom-variant/@utility: skipped
                else:
                    decls: dict[str, str] = {}
                    for part in split_top(inner, ";"):
                        if ":" in part:
                            k, _, v = part.partition(":")
                            if k.strip() and v.strip() and not k.strip().startswith("--tw"):
                                decls[k.strip()] = v.strip()
                            elif k.strip().startswith("--tw"):
                                decls[k.strip()] = v.strip()
                    if decls:
                        counter[0] += 1
                        for sel_part in split_top(s, ","):
                            r = classify_selector(sel_part.strip())
                            if r is not None:
                                rules.append(Rule(counter[0], r[0], r[1], dict(decls), media))
                i = j
            else:
                sel.append(c)
                i += 1

    parse_block(css, "")
    return rules


def split_top(s: str, sep: str) -> list[str]:
    parts, depth, cur = [], 0, []
    for ch in s:
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        if ch == sep and depth == 0:
            parts.append("".join(cur).strip())
            cur = []
        else:
            cur.append(ch)
    if cur:
        parts.append("".join(cur).strip())
    return parts


KNOWN_PSEUDO_STATES = {"hover", "active", "focus", "focus-visible", "disabled", "checked",
                       "indeterminate", "nth-child", "hover-visited"}


def classify_selector(sel: str) -> tuple[str, str] | None:
    """`.hover\\:bg-x:hover` -> ("hover:bg-x", "hover")"""
    sel = sel.replace("\\:", ":").replace("\\[", "[").replace("\\]", "]")
    sel = sel.replace("\\(", "(").replace("\\)", ")").replace("\\.", ".").replace("\\/", "/")
    sel = sel.replace("\\,", ",").replace("\\!", "!")
    # group-hover compiles to a descendant selector: unwrap it
    m = re.match(r"\.group-hover:(.+?):is\(:where\(\.group\):hover \*\)|\.group-hover:(.+?):where\(\.group:hover \*\)", sel)
    if m:
        sel = f".{m.group(1) or m.group(2)}:hover"
    if sel.startswith((":where", ":is", "::", "*", "html", "[data-mode", ".dark")):
        return None
    if not sel.startswith("."):
        return None
    if " " in sel or ">" in sel:
        return None  # descendant/nested selectors skipped in v1
    body = sel[1:].replace(":not(#\\9)", "").replace(":not(#\x09)", "").replace(":not(:disabled)", "")
    cond = ""
    while True:
        m2 = re.search(r"(:[a-z-]+(?:\([^)]*\))?|\[[^\]]+\])$", body)
        if not m2:
            break
        tail = m2.group(1)
        if tail.startswith("["):
            inner = tail[1:-1]
            if not inner.startswith(("data-", "aria-")):
                break  # arbitrary value like [5px] — part of the class token
            cond = {"data-checked": "checked", "data-indeterminate": "indeterminate",
                    "data-highlighted": "highlighted"}.get(inner, inner)
            body = body[:m2.start()]
            break
        word = tail[1:]
        if word in KNOWN_PSEUDO_STATES:
            cond = word
            body = body[:m2.start()]
            break
        break  # pseudo-function (not/is/...) or unknown: keep in token
    if not body:
        return None
    return body, cond


# ---------------------------------------------------------------------------
# Variable resolution (light/dark, color-mix, oklch, rem)
# ---------------------------------------------------------------------------

var_table: dict[str, str] = {}
_resolve_stack: set[str] = set()


def collect_vars(css: str) -> None:
    for m in re.finditer(r"(--[A-Za-z0-9-]+)\s*:\s*([^;{}]+)", css):
        name, val = m.group(1), m.group(2).strip()
        if name.startswith("--tw"):
            continue
        var_table.setdefault(name, val)


def _res_color(expr: str, mode: str) -> RGBA:
    s = expr.strip()
    if s.startswith("var("):
        inner = s[len("var("):-1].strip()
        parts = split_top(inner, ",")
        name = parts[0].strip()
        if name in var_table and name not in _resolve_stack:
            try:
                _resolve_stack.add(name)
                return res_color(var_table[name], mode)
            finally:
                _resolve_stack.discard(name)
        if len(parts) > 1:
            return res_color(",".join(parts[1:]), mode)
        raise ValueError(f"unresolvable var {name}")
    if s.startswith("light-dark("):
        args = split_top(s[len("light-dark("):-1], ",")
        return res_color(args[0] if mode == "light" else args[-1], mode)
    if s.startswith("color-mix("):
        args = split_top(s[len("color-mix("):-1], ",")
        space = args[0].strip().lower()
        colors = [a.strip() for a in args[1:]]
        if len(colors) == 1:
            colors = split_top(colors[0], ",")
        first = colors[0].strip()
        w1_pct = None
        m_pct = re.search(r"\)\s*([\d.]+)%$", first)
        if m_pct:
            w1_pct = float(m_pct.group(1)) / 100
            colors[0] = first[:m_pct.start()]
        c1 = res_color(colors[0], mode)
        second = colors[1].split()
        if len(second) == 2 and second[1].endswith("%"):
            c2, w2 = res_color(second[0], mode), float(second[1][:-1]) / 100
        else:
            c2, w2 = res_color(colors[1], mode), 0.5
        if w1_pct is not None:
            w2 = 1.0 - w1_pct
        w1 = 1.0 - w2
        if space in ("in oklch", "in oklab"):
            # premultiplied alpha (CSS): transparent stays the other color
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
        # sRGB interpolation is also premultiplied-alpha in CSS
        a1, a2 = c1[3], c2[3]
        alpha = w1 * a1 + w2 * a2
        if alpha <= 0:
            return (0.0, 0.0, 0.0, 0.0)
        return ((w1 * c1[0] * a1 + w2 * c2[0] * a2) / alpha,
                (w1 * c1[1] * a1 + w2 * c2[1] * a2) / alpha,
                (w1 * c1[2] * a1 + w2 * c2[2] * a2) / alpha, alpha)
    if s.startswith("oklch("):
        m = re.fullmatch(r"oklch\(\s*([\d.]+)(%?)\s+([\d.]+)\s+(-?[\d.]+)(?:\s*/\s*([\d.]+)(%?))?\s*\)", s)
        if not m:
            raise ValueError(s)
        L = float(m.group(1)) / 100 if m.group(2) else float(m.group(1))
        C, H = float(m.group(3)), float(m.group(4))
        alpha = (float(m.group(5)) / 100 if m.group(6) else float(m.group(5))) if m.group(5) else 1.0
        h = math.radians(H)
        r, g, b = _oklab_to_linear_srgb(L, C * math.cos(h), C * math.sin(h))
        return (_gamma(r), _gamma(g), _gamma(b), alpha)
    if s == "transparent" or s == "#0000" or s == "#00000000":
        return (0.0, 0.0, 0.0, 0.0)
    named = {"black": (0, 0, 0), "white": (255, 255, 255)}
    if s.lower() in named:
        return (*(v / 255 for v in named[s.lower()]), 1.0)
    if s.startswith("#"):
        h = s[1:]
        if len(h) in (3, 4):
            h = "".join(ch * 2 for ch in h)
        vals = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        a = int(h[6:8], 16) / 255 if len(h) == 8 else 1.0
        return (*vals, a)
    if s.startswith("rgb("):
        nums = re.findall(r"-?[\d.]+%?", s)
        vals = [float(v) / 100 if v.endswith("%") else float(v) / 255 for v in nums[:3]]
        a = float(nums[3]) / 100 if len(nums) > 3 and "%" in nums[3] else (float(nums[3]) if len(nums) > 3 else 1.0)
        return (*vals, a)
    if s == "currentColor":
        raise ValueError("currentColor needs context")
    raise ValueError(f"unsupported color: {expr!r}")


def res_color(expr: str, mode: str = "light") -> RGBA:
    return _res_color(expr, mode)


def hex_of(c: RGBA) -> str:
    r, g, b, a = (round(v * 255) for v in c)
    return f"#{r:02X}{g:02X}{b:02X}{a:02X}" if a < 255 else f"#{r:02X}{g:02X}{b:02X}"


def to_px(v: str) -> float:
    if v.endswith("rem"):
        return float(v[:-3]) * 16
    if v.endswith("px"):
        return float(v[:-2])
    return float(v)


def resolve_px(expr: str, mode: str) -> float:
    """calc(var(--spacing)*N) / var(--radius-lg) / 1px / .875rem -> px"""
    s = expr.strip()
    m = re.fullmatch(r"calc\((.*)\)", s)
    if m:
        inner = m.group(1)
        # substitute var(...) then evaluate simple +-*/ chain
        def sub(mm: re.Match) -> str:
            return str(resolve_px(var_lookup(mm.group(1), mode), mode))
        inner = re.sub(r"var\(([^,)]+)(?:,[^)]*)?\)", sub, inner)
        inner = re.sub(r"(\d*\.?\d+)px", lambda mm: mm.group(1), inner)
        try:
            return float(eval(inner, {"__builtins__": {}}))
        except Exception:
            return float(inner)
    if s.endswith("rem") or s.endswith("px"):
        return to_px(s)
    return to_px(f"{s}px")


def var_lookup(name: str, mode: str) -> str:
    if name not in var_table:
        raise ValueError(f"unknown var {name}")
    return var_table[name]


def resolve_value(expr: str, mode: str) -> str:
    s = expr.strip()
    if s.startswith("var(") or s.startswith("light-dark(") or s.startswith("color-mix(") or s.startswith("oklch(") or s.startswith("#") or s.startswith("rgb(") or s in ("transparent", "#0000", "currentColor"):
        try:
            return hex_of(res_color(s, mode))
        except Exception:
            # var() whose value is not a color (radius, size, ...): recurse
            m = re.fullmatch(r"var\(\s*(--[A-Za-z0-9-]+)\s*(?:,[^)]*)?\)", s)
            if m and m.group(1) in var_table and m.group(1) not in _resolve_stack:
                try:
                    _resolve_stack.add(m.group(1))
                    return resolve_value(var_table[m.group(1)], mode)
                finally:
                    _resolve_stack.discard(m.group(1))
            return s
    if re.fullmatch(r"-?[\d.]+(px|rem)?", s):
        try:
            v = resolve_px(s, mode)
            return f"{v:g}px"
        except Exception:
            return s
    if s.startswith("calc("):
        return f"{resolve_px(s, mode):g}px"
    return s


# ---------------------------------------------------------------------------
# IR interpretation: token decls -> normalized keys
# ---------------------------------------------------------------------------

def _resolve_shadow(raw: str, mode: str) -> str | None:
    """`0 1px 2px 0 var(--tw-shadow-color,oklch(...))` -> '0 1px 2px 0 #rrggbb'"""
    if not raw or raw == "0 0 #0000" or set(raw) <= set("0 "):
        return None if raw and "1px" not in raw and "rem" not in raw else raw
    out = []
    for part in split_top(raw.replace("var(--tw-shadow-color,", "var(").replace("var(--tw-ring-offset-width,0px)", "0"), ","):
        part = part.strip()
        mm = re.match(r"(inset )?((?:[-\d.]+(?:px|rem|%)?\s*){3,4})\s*(.*)", part)
        if not mm:
            return None
        inset, geom, col = mm.group(1) or "", mm.group(2).strip(), mm.group(3).strip()
        if col.startswith("var("):
            inner = col[len("var("):-1]
            parts2 = split_top(inner, ",")
            col = parts2[-1].strip()
        if col.startswith("--"):
            col = f"var({col})"
        try:
            colhex = hex_of(res_color(col, mode)) if col else ""
        except Exception:
            return None
        out.append(f"{inset}{geom} {colhex}".strip())
    return " | ".join(out)


WEIGHTS = {"font-thin": 100, "font-light": 300, "font-normal": 400,
           "font-medium": 500, "font-semibold": 600, "font-bold": 700}

PAD_AXIS = {
    "padding": ("padding-top", "padding-right", "padding-bottom", "padding-left"),
    "padding-inline": ("padding-left", "padding-right"),
    "padding-block": ("padding-top", "padding-bottom"),
    "padding-inline-start": ("padding-left",), "padding-inline-end": ("padding-right",),
    "padding-block-start": ("padding-top",), "padding-block-end": ("padding-bottom",),
    "padding-top": ("padding-top",), "padding-right": ("padding-right",),
    "padding-bottom": ("padding-bottom",), "padding-left": ("padding-left",),
}


def interpret(token: str, decls: dict[str, str], mode: str) -> dict[str, str]:
    out: dict[str, str] = {}

    def setp(key: str, raw: str) -> None:
        try:
            out[key] = resolve_value(raw, mode)
        except Exception as e:
            out[f"raw:{key}"] = raw

    for prop, raw in decls.items():
        if prop in ("background-color", "color", "border-color", "border-top-color",
                    "border-bottom-color", "fill", "outline-color"):
            key = {"background-color": "background-color", "color": "color",
                   "border-color": "border-color", "border-top-color": "border-top-color",
                   "border-bottom-color": "border-bottom-color", "fill": "fill",
                   "outline-color": "outline-color"}[prop]
            setp(key, raw)
        elif prop == "border-radius":
            setp("border-radius", raw)
            if "border-radius" in out:
                try:
                    if float(out["border-radius"].rstrip("px")) > 1e6:
                        out["border-radius"] = "9999px"  # calc(infinity*1px) = rounded-full
                except ValueError:
                    pass
        elif prop in PAD_AXIS:
            v = resolve_px(raw, mode) if not raw.startswith("var") else raw
            for target in PAD_AXIS[prop]:
                if isinstance(v, float):
                    out[target] = f"{v:g}px"
                else:
                    setp(target, v)
        elif prop == "opacity":
            out["opacity"] = raw
        elif prop == "height":
            setp("height", raw)
        elif prop == "width":
            setp("width", raw)
        elif prop == "min-height":
            setp("min-height", raw)
        elif prop == "min-width":
            setp("min-width", raw)
        elif prop == "max-width":
            setp("max-width", raw)
        elif prop == "gap":
            setp("gap", raw)
        elif prop == "border-width":
            setp("border-width", raw)
        elif prop == "border-top-width":
            setp("border-top-width", raw)
        elif prop == "border-bottom-width":
            setp("border-bottom-width", raw)
        elif prop == "line-height":
            if raw.startswith("calc"):
                try:
                    out["line-height"] = f"{resolve_px(raw, mode):g}px"
                except Exception:
                    pass
            else:
                out["line-height"] = raw
        elif prop == "font-size":
            setp("font-size", raw)
        elif prop == "font-weight":
            wm = re.search(r"--font-weight-([a-z]+)", raw)
            if wm and wm.group(1) in ("thin", "light", "normal", "medium", "semibold", "bold"):
                out["font-weight"] = str(WEIGHTS[f"font-{wm.group(1)}"])
            elif re.fullmatch(r"\d+", raw):
                out["font-weight"] = raw
        elif prop == "left":
            setp("left", raw)
        elif prop == "inset":
            pass
        elif prop == "border-style":
            if "var(--tw" not in raw:
                out["border-style"] = raw
        elif prop == "--tw-shadow" or prop == "box-shadow":
            v = _resolve_shadow(raw, mode)
            if v:
                out["shadow"] = v
        elif prop == "--tw-inset-shadow":
            v = _resolve_shadow(raw, mode)
            if v:
                out["inset-shadow"] = v
        elif prop == "--tw-gradient-from":
            first = split_top(raw, ",")[0].split(" var(--tw-gradient")[0].strip()
            setp("gradient-from", first)
        elif prop == "--tw-gradient-to":
            first = split_top(raw, ",")[0].split(" var(--tw-gradient")[0].strip()
            setp("gradient-to", first)
        elif prop == "--tw-ring-color":
            setp("ring-color", raw)
        elif prop == "--tw-ring-inset":
            out["ring-inset"] = raw if raw == "true" else "false"
        elif prop == "--tw-shadow-color":
            setp("shadow-color", raw)
        elif prop == "text-decoration-line":
            out["text-decoration"] = raw
        elif prop == "background-image":
            if "var(--tw-gradient" not in raw:
                out["background-image"] = raw
        # other --tw-* composite vars and passthrough vars are noise: dropped
    return out


def token_ring_width(token: str) -> str | None:
    base = token.split(":")[-1]
    if base == "ring":
        return "1px"
    m = re.fullmatch(r"ring-(\d+(?:\.\d+)?)", base)
    if m:  # ring-<number> = <number>px in Tailwind v4 (not the spacing scale)
        return f"{float(m.group(1)):g}px"
    m = re.fullmatch(r"ring-\[([\d.]+)px\]", base)
    if m:
        return f"{m.group(1)}px"
    if base == "ring-inset":
        return None
    return None


def token_box_shadow(token: str, decls: dict[str, str], mode: str) -> str | None:
    base = token.split(":")[-1]
    if base.startswith("shadow-") and not base.startswith("shadow-color"):
        raw = decls.get("--tw-shadow")
        if raw:
            try:
                mm = re.match(r"(inset )?((?:-?[\d.]+(?:px|rem) ){3}-?[\d.]+(?:px|rem)) (.*)", raw)
                if mm:
                    inset, geom, col = mm.group(1) or "", mm.group(2), mm.group(3)
                    col = re.sub(r"var\([^)]*\)", lambda m2: hex_of(res_color(m2.group(0), mode)) if not m2.group(0).startswith("--tw") else m2.group(0), col)
                    return f"{inset}{geom} {col}"
            except Exception:
                pass
            return raw
    if base.startswith("shadow-["):
        raw = decls.get("--tw-shadow", "")
        return raw if raw else None
    return None


# ---------------------------------------------------------------------------
# Fixture curation (class lists hand-evaluated from the component chunks)
# ---------------------------------------------------------------------------

BUTTON_BASE = ("group flex w-max shrink-0 items-center font-medium select-none",
               "border-0 shadow-xs", "cursor-pointer")
BUTTON_FOCUS = "focus:ring-kumo-focus/50 focus:outline-none focus-visible:ring-2 focus-visible:ring-kumo-brand"
BUTTON_DISABLED = "disabled:cursor-not-allowed disabled:text-kumo-subtle"
SIZE_BASE = "h-9 gap-1.5 rounded-lg px-3 text-base"
SIZE_SM = "h-6.5 gap-1 rounded-md px-2 text-xs"
SIZE_LG = "h-10 gap-2 rounded-lg px-4 text-base"

INPUT_BASE = "border-0 bg-kumo-control text-kumo-default ring ring-kumo-line outline-none focus:outline-none"
INPUT_FOCUS = "focus:ring-[1.5px] focus:ring-kumo-focus/50"
INPUT_ERROR = "!ring-kumo-danger focus:ring-kumo-danger/50 focus:ring-[1.5px]"

CHECKBOX_BOX = "relative flex h-4 w-4 shrink-0 items-center justify-center rounded-sm border-0 bg-kumo-base ring ring-kumo-hairline after:absolute after:-inset-x-3 after:-inset-y-2"
CHECKBOX_FOCUS = "focus:ring-2 focus:ring-kumo-focus focus-visible:ring-2 focus-visible:ring-kumo-brand"
CHECKBOX_CHECKED = "data-[checked]:bg-kumo-contrast data-[checked]:ring-kumo-contrast"
CHECKBOX_INDICATOR = "flex items-center justify-center text-kumo-inverse"

SWITCH_SQUIRCLE = "rounded-[5px]"
SWITCH_TRACK_OFF = "bg-neutral-200 ring-neutral-300"
SWITCH_TRACK_ON = "bg-blue-500 ring-blue-600"
SWITCH_TRACK_BASE = "relative inline-flex cursor-pointer items-center border-none p-0 ring"
SWITCH_THUMB = "absolute top-0 bottom-0 shadow-[0_0_1px_0.5px_var(--color-kumo-shadow-edge),0_1px_2px_var(--color-kumo-shadow-drop)] bg-kumo-base"
SWITCH_OFF_POS, SWITCH_ON_POS = "left-0", "left-4.5"

BADGE_BASE = "inline-flex w-fit flex-none shrink-0 items-center justify-self-start gap-1 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap"

TABS_LIST = "rounded-lg bg-kumo-recessed px-0.5"
TABS_INDICATOR = "top-(--active-tab-top) h-(--active-tab-height) bg-kumo-base shadow-sm ring ring-kumo-line"
TABS_TAB_BASE = "relative z-2 flex items-center rounded bg-transparent whitespace-nowrap"
TABS_TAB_CONTENT = "text-kumo-subtle hover:bg-kumo-tint hover:text-kumo-default aria-selected:font-medium aria-selected:text-kumo-default aria-selected:hover:bg-kumo-tint"

BANNER_INFO = "bg-kumo-info-tint text-kumo-info"
BANNER_ALERT = "bg-kumo-warning-tint text-kumo-warning"
BANNER_ERROR = "bg-kumo-danger-tint text-kumo-danger"
BANNER_SECONDARY = "bg-kumo-contrast/5 text-kumo-default/70"
BANNER_BASE_SIZE = "items-start gap-3 rounded-lg px-4 py-3 text-base"
BANNER_SM_SIZE = "items-center gap-2 rounded-md px-3 py-2 text-sm"

TOAST_CARD = "rounded-lg border border-kumo-fill bg-kumo-control p-4 shadow-lg text-kumo-default"
TOAST_SUCCESS = "ring-[0.3px] ring-kumo-success"
TOAST_INFO = "ring-[0.3px] ring-kumo-info"

TABLE_TH = "[&_th]:border-b [&_th]:border-kumo-fill [&_th]:p-3 [&_th]:text-base [&_th]:font-semibold"
TABLE_ROW = "bg-(--kumo-table-row-bg) [--kumo-table-row-bg:var(--color-kumo-base)]"
TABLE_ROW_EVEN = "even:bg-kumo-elevated [--kumo-table-row-bg:var(--color-kumo-elevated)]"
TABLE_ROW_SELECTED = "bg-kumo-tint [--kumo-table-row-bg:var(--color-kumo-tint)]"


def table_row_state(which: str) -> list[str]:
    """Row state token list; even/selected override the runtime row-bg var."""
    base = TABLE_ROW
    if which == "even":
        return [base, "even:bg-kumo-elevated", "[--kumo-table-row-bg:var(--color-kumo-elevated)]"]
    if which == "selected":
        return ["[--kumo-table-row-bg:var(--color-kumo-tint)]", "bg-(--kumo-table-row-bg)"]
    return [base]

MENU_ITEM = "relative flex cursor-default items-center rounded-md px-2 py-1.5 text-base outline-hidden select-none"
MENU_ITEM_HL = "data-highlighted:bg-kumo-overlay"

METER_TRACK = "relative h-2 w-full overflow-hidden rounded-full bg-kumo-fill"
METER_FILL = "absolute inset-y-0 left-0 rounded-full bg-linear-to-r from-kumo-brand via-kumo-brand to-kumo-brand"

DIALOG_SURFACE = "shadow-m ring ring-kumo-line rounded-xl bg-kumo-base text-kumo-default"
TOOLTIP_CONTENT = "flex flex-col rounded-md bg-kumo-base px-2.5 py-1.5 text-sm text-kumo-default"

BUTTON_EMPHASIS_VARS = {
    "--kumo-button-emphasis-ring": "color-mix(in oklch, {token}, black 10%)",
    "--kumo-button-emphasis-bg": "color-mix(in oklch, {token}, white 30%)",
    "--kumo-button-emphasis-gradient-start": "color-mix(in oklch, {token}, white 15%)",
    "--kumo-button-emphasis-gradient-end": "{token}",
}


def fixture(name: str, description: str, states: dict[str, list[str]],
            vars_: dict[str, str] | None = None, keys: list[str] | None = None) -> dict:
    return {"fixture": name, "description": description, "states": states,
            "vars": vars_ or {}, "keys": keys}


def emphasis_fixture(name: str, token: str, description: str) -> dict:
    vars_ = {k: v.replace("{token}", token) for k, v in BUTTON_EMPHASIS_VARS.items()}
    surface = "relative overflow-hidden bg-(--kumo-button-emphasis-bg) !text-white ring ring-(--kumo-button-emphasis-ring)"
    states = {
        "default": [*BUTTON_BASE, SIZE_BASE, surface],
        "hover": [*BUTTON_BASE, SIZE_BASE, surface],
        "focus-visible": [*BUTTON_BASE, SIZE_BASE, BUTTON_FOCUS, surface, "focus-visible:ring-(--kumo-button-emphasis-ring)"],
        "disabled": [*BUTTON_BASE, SIZE_BASE, BUTTON_DISABLED, "disabled:opacity-50", surface],
    }
    return fixture(name, description, states, vars_,
                   keys=["background-color", "color", "ring-color", "ring-width",
                         "border-radius", "padding-left", "font-size", "font-weight",
                         "opacity", "height"])


def emphasis_overlay_fixture(name: str, token: str, description: str) -> dict:
    vars_ = {k: v.replace("{token}", token) for k, v in BUTTON_EMPHASIS_VARS.items()}
    overlay = ("absolute inset-0 rounded-[inherit] bg-linear-to-b from-(--kumo-button-emphasis-gradient-start)"
               " to-(--kumo-button-emphasis-gradient-end) shadow-[inset_0_1px_0_0_var(--kumo-button-emphasis-bg)]")
    states = {
        "default": [overlay],
        "hover": [overlay, "group-hover:from-(--kumo-button-emphasis-bg)"],
    }
    return fixture(name, description, states, vars_,
                   keys=["gradient-from", "gradient-to", "inset-shadow"])


FIXTURES: list[dict] = [
    fixture("button-secondary", "Button variant=secondary size=base (kumo default button)", {
        "default": [*BUTTON_BASE, SIZE_BASE, "bg-kumo-base !text-kumo-default ring ring-kumo-line"],
        "hover": [*BUTTON_BASE, SIZE_BASE, "bg-kumo-base !text-kumo-default ring ring-kumo-line not-disabled:hover:bg-kumo-tint"],
        "focus-visible": [*BUTTON_BASE, SIZE_BASE, BUTTON_FOCUS, "bg-kumo-base !text-kumo-default ring ring-kumo-line focus-visible:ring-2 focus-visible:ring-kumo-brand"],
        "disabled": [*BUTTON_BASE, SIZE_BASE, BUTTON_DISABLED, "bg-kumo-base !text-kumo-default ring ring-kumo-line disabled:bg-kumo-base/50 disabled:!text-kumo-default/70"],
    }, keys=["background-color", "color", "ring-width", "ring-color", "border-radius", "padding-left", "height", "font-size", "font-weight", "shadow"]),

    emphasis_fixture("button-primary", "var(--color-kumo-brand)", "Button variant=primary size=base"),
    emphasis_overlay_fixture("button-primary-overlay", "var(--color-kumo-brand)", "Primary button gradient overlay span"),
    emphasis_fixture("button-destructive", "var(--color-kumo-danger)", "Button variant=destructive size=base"),
    emphasis_overlay_fixture("button-destructive-overlay", "var(--color-kumo-danger)", "Destructive button gradient overlay span"),

    fixture("button-ghost", "Button variant=ghost size=base", {
        "default": [*BUTTON_BASE, SIZE_BASE, "text-kumo-default hover:bg-kumo-tint shadow-none bg-inherit"],
        "hover": [*BUTTON_BASE, SIZE_BASE, "text-kumo-default hover:bg-kumo-tint shadow-none bg-inherit"],
    }, keys=["background-color", "color", "box-shadow", "border-radius", "padding-left", "font-size"]),

    fixture("button-outline", "Button variant=outline size=base", {
        "default": [*BUTTON_BASE, SIZE_BASE, "bg-transparent text-kumo-default ring ring-kumo-line"],
        "hover": [*BUTTON_BASE, SIZE_BASE, "bg-transparent text-kumo-default ring ring-kumo-line not-disabled:hover:text-kumo-strong not-disabled:hover:ring-kumo-focus/25"],
    }, keys=["background-color", "color", "ring-width", "ring-color", "border-radius"]),

    fixture("button-secondary-destructive", "Button variant=secondary-destructive size=base", {
        "default": [*BUTTON_BASE, SIZE_BASE, "bg-kumo-base !text-kumo-danger ring ring-kumo-line"],
        "hover": [*BUTTON_BASE, SIZE_BASE, "bg-kumo-base !text-kumo-danger ring ring-kumo-line not-disabled:hover:!text-kumo-danger not-disabled:hover:ring-kumo-danger/30"],
    }, keys=["background-color", "color", "ring-color", "border-radius"]),

    fixture("button-sm", "Button secondary size=sm", {
        "default": [*BUTTON_BASE, SIZE_SM, "bg-kumo-base !text-kumo-default ring ring-kumo-line"],
    }, keys=["height", "padding-left", "padding-right", "font-size", "border-radius"]),

    fixture("input-base", "Input size=base focusIndicator=true", {
        "default": [INPUT_BASE, SIZE_BASE],
        "focus": [INPUT_BASE, SIZE_BASE, INPUT_FOCUS],
        "error": [INPUT_BASE, SIZE_BASE, INPUT_ERROR],
        "error-focus": [INPUT_BASE, SIZE_BASE, INPUT_ERROR, "focus:ring-[1.5px] focus:ring-kumo-danger/50"],
    }, keys=["background-color", "color", "ring-width", "ring-color", "border-radius", "padding-left", "height", "font-size"]),

    fixture("checkbox-unchecked", "Checkbox unchecked (16px box)", {
        "default": [CHECKBOX_BOX],
        "hover": [CHECKBOX_BOX, "hover:ring-kumo-hairline"],
        "focus": [CHECKBOX_BOX, CHECKBOX_FOCUS],
        "checked": [CHECKBOX_BOX, CHECKBOX_CHECKED, CHECKBOX_INDICATOR],
    }, keys=["background-color", "ring-width", "ring-color", "border-radius", "height", "width"]),

    fixture("switch-base-track", "Switch track size=base default variant, off/checked", {
        "default": [SWITCH_TRACK_BASE, "h-4.5 w-9", SWITCH_SQUIRCLE, SWITCH_TRACK_OFF],
        "checked": [SWITCH_TRACK_BASE, "h-4.5 w-9", SWITCH_SQUIRCLE, SWITCH_TRACK_ON],
    }, keys=["background-color", "ring-color", "border-radius", "height", "width"]),
    fixture("switch-base-thumb", "Switch thumb (18px, base bg, edge shadow)", {
        "default": [SWITCH_THUMB, SWITCH_OFF_POS, "w-4.5", SWITCH_SQUIRCLE],
        "checked": [SWITCH_THUMB, SWITCH_ON_POS, "w-4.5", SWITCH_SQUIRCLE],
    }, keys=["background-color", "width", "border-radius", "box-shadow"]),

    fixture("badge-primary", "Badge variant=primary", {
        "default": [BADGE_BASE, "bg-kumo-badge-inverted text-kumo-badge-inverted"],
    }, keys=["background-color", "color", "border-radius", "padding-left", "padding-top", "font-size", "font-weight"]),
    fixture("badge-secondary", "Badge variant=secondary", {
        "default": [BADGE_BASE, "bg-kumo-fill text-kumo-badge-neutral-subtle"],
    }, keys=["background-color", "color"]),
    fixture("badge-error", "Badge variant=error (danger tint)", {
        "default": [BADGE_BASE, "bg-kumo-danger-tint text-kumo-danger"],
    }, keys=["background-color", "color"]),
    fixture("badge-info", "Badge variant=info", {
        "default": [BADGE_BASE, "bg-kumo-info-tint text-kumo-info"],
    }, keys=["background-color", "color"]),
    fixture("badge-beta", "Badge variant=beta (dashed brand)", {
        "default": [BADGE_BASE, "border border-dashed border-kumo-brand bg-transparent text-kumo-link"],
    }, keys=["background-color", "color", "border-color", "border-style"]),
    fixture("badge-outline", "Badge variant=outline", {
        "default": [BADGE_BASE, "border border-kumo-fill bg-kumo-base text-kumo-default"],
    }, keys=["background-color", "color", "border-color"]),
    fixture("badge-blue", "Badge variant=blue (solid color)", {
        "default": [BADGE_BASE, "bg-kumo-badge-blue text-white"],
    }, keys=["background-color", "color"]),

    fixture("tabs-list", "Tabs list container (boxed/segmented)", {
        "default": [TABS_LIST],
    }, keys=["background-color", "border-radius", "padding-left"]),
    fixture("tabs-tab", "Tab (boxed) idle vs selected", {
        "default": [TABS_TAB_BASE, TABS_TAB_CONTENT],
        "selected": [TABS_TAB_BASE, TABS_TAB_CONTENT],
    }, keys=["color", "font-weight", "background-color"]),
    fixture("tabs-indicator", "Active tab indicator (floating pill)", {
        "default": [TABS_INDICATOR],
    }, keys=["background-color", "ring-width", "ring-color", "shadow"]),

    fixture("banner-info", "Banner variant=default size=base", {
        "default": [BANNER_BASE_SIZE, BANNER_INFO],
    }, keys=["background-color", "color", "border-radius", "padding-left", "padding-top", "font-size"]),
    fixture("banner-alert", "Banner variant=alert size=base", {
        "default": [BANNER_BASE_SIZE, BANNER_ALERT],
    }, keys=["background-color", "color"]),
    fixture("banner-error", "Banner variant=error size=base", {
        "default": [BANNER_BASE_SIZE, BANNER_ERROR],
    }, keys=["background-color", "color"]),
    fixture("banner-secondary", "Banner variant=secondary size=base", {
        "default": [BANNER_BASE_SIZE, BANNER_SECONDARY],
    }, keys=["background-color", "color"]),
    fixture("banner-info-sm", "Banner info size=sm", {
        "default": [BANNER_SM_SIZE, BANNER_INFO],
    }, keys=["border-radius", "padding-left", "padding-top", "font-size"]),

    fixture("toast-card", "NotificationCard default", {
        "default": [TOAST_CARD],
    }, keys=["background-color", "color", "border-color", "border-radius", "padding", "shadow"]),
    fixture("toast-success", "NotificationCard success accent", {
        "default": [TOAST_CARD, TOAST_SUCCESS],
    }, keys=["ring-color"]),
    fixture("toast-info", "NotificationCard info accent", {
        "default": [TOAST_CARD, TOAST_INFO],
    }, keys=["ring-color"]),

    fixture("table-header", "Table header cell", {
        "default": ["text-left text-base text-kumo-default", "border-b border-kumo-fill", "p-3", "font-semibold"],
    }, keys=["color", "border-bottom-width", "border-bottom-color", "padding", "font-weight", "font-size"]),
    fixture("table-row", "Table row (odd) / even / selected", {
        "default": table_row_state("default"),
        "even": table_row_state("even"),
        "selected": table_row_state("selected"),
    }, keys=["background-color"]),
    fixture("table-td", "Table cell padding", {
        "default": ["p-3"],
    }, keys=["padding"]),

    fixture("pagination-page", "Pagination page button (ghost + square + hairline ring)", {
        "default": [*BUTTON_BASE, "h-6.5 gap-1 rounded-none px-2 text-xs", "text-center",
                    "text-kumo-default hover:bg-kumo-tint shadow-none bg-inherit ring ring-kumo-hairline"],
    }, keys=["border-radius", "ring-color", "height", "font-size"]),

    fixture("menu-item", "Menu item (dropdown)", {
        "default": [MENU_ITEM],
        "highlighted": [MENU_ITEM, MENU_ITEM_HL],
    }, keys=["padding-left", "padding-top", "border-radius", "font-size", "background-color"]),

    fixture("meter-track", "Meter track", {
        "default": [METER_TRACK],
    }, keys=["background-color", "border-radius", "height"]),
    fixture("meter-fill", "Meter fill (brand gradient)", {
        "default": [METER_FILL],
    }, keys=["background-image"]),

    fixture("dialog-surface", "Dialog surface", {
        "default": [DIALOG_SURFACE],
    }, keys=["background-color", "color", "border-radius", "ring-width", "ring-color", "shadow"]),

    fixture("tooltip-content", "Tooltip content", {
        "default": [TOOLTIP_CONTENT],
    }, keys=["background-color", "color", "border-radius", "padding-left", "padding-top", "font-size"]),
]

# hand-curated fixture classes containing bare pseudo-conditions in the token
# (aria-selected: etc.) are split by resolve_state below.


STATE_OF_PREFIX = {"aria-selected": "selected", "data-[checked]": "checked",
                   "data-[indeterminate]": "indeterminate", "data-highlighted": "highlighted",
                   "group-hover": "hover", "hover": "hover", "focus": "focus",
                   "focus-visible": "focus-visible", "active": "active", "disabled": "disabled",
                   "not-disabled:hover": "hover"}
WANT_COND = {"hover": "hover", "focus": "focus", "focus-visible": "focus-visible",
             "active": "active", "disabled": "disabled", "checked": "checked",
             "indeterminate": "indeterminate", "highlighted": "highlighted",
             "even": "nth-child", "selected": "", "default": "", "error-focus": "focus"}


def resolve_state(tokens_in: list[str], state: str, mode: str, index: dict,
                  vars_: dict[str, str]) -> tuple[dict[str, str], list[str]]:
    """Merge class tokens (tailwind-merge: later wins, !important wins) for a UI state."""
    global var_table
    saved = dict(var_table)
    var_table.update(vars_)

    notes: list[str] = []
    # tokens like [--kumo-table-row-bg:var(--color-kumo-base)] define runtime vars
    for raw in tokens_in:
        for tok in raw.split():
            m = re.fullmatch(r"\[--([a-z0-9-]+):(.+)\]", tok)
            if m:
                var_table[f"--{m.group(1)}"] = m.group(2)

    merged: dict[str, str] = {}
    important: set[str] = set()
    want = WANT_COND.get(state, state)

    for raw in tokens_in:
        for tok in raw.split():
            # component-computed gradient/dot tokens: skip pseudo-element helpers
            if tok.startswith(("after:", "before:", "[&", "supports-", "dark:", "*:")):
                continue
            # split state prefix only when it matches the fixture state
            applies_state = state
            for prefix in ("aria-selected:", "data-[checked]:", "data-[indeterminate]:",
                           "data-highlighted:", "group-hover:"):
                if tok.startswith(prefix):
                    applies_state = STATE_OF_PREFIX[prefix[:-1]]
                    tok = tok[len(prefix):]
                    break
            if tok is None or applies_state != state:
                continue
            imp = tok.startswith("!")
            if imp:
                tok = tok[1:]
                if ":" in tok:  # "!text-white" important + variant handled below
                    variant, base = tok.split(":", 1)
                    if STATE_OF_PREFIX.get(variant, variant) != state:
                        # important + state variant, e.g. disabled:!text-kumo-default/70
                        if WANT_COND.get(variant, variant) != state:
                            continue
                        tok = base
                    else:
                        tok = base
            # ring/gradient widths derived from the effective token name
            base_tok = tok.split(":")[-1] if ":" in tok else tok
            leading = tok[:len(tok) - len(base_tok)] if ":" in tok else ""
            leading_state = STATE_OF_PREFIX.get(leading.rstrip(":"), leading.rstrip(":"))
            if ":" in tok and WANT_COND.get(leading_state, leading_state) != want:
                continue

            rules = index.get(tok)
            if not rules:
                if re.match(r"^(bg-|text-|ring|border|shadow|rounded|[ptbxy]?p-|h-|w-|size-|gap-|font-|opacity-|leading-|underline)", tok):
                    notes.append(f"unknown token: {tok}")
                continue
            if imp and ":" not in tok:
                pass  # `!text-white`: apply as base
            best = None
            for r in sorted(rules, key=lambda x: x.order):
                if r.media and "(hover:hover)" not in r.media:
                    continue
                if r.state == want:
                    best = r
                elif r.state == "" and best is None:
                    best = r
            if best is None:
                notes.append(f"token {tok} has no rule for state {state}")
                continue
            props = interpret(tok, best.decls, mode)
            for k, v in props.items():
                if imp:
                    merged[k] = v
                    important.add(k)
                elif k in important:
                    pass
                else:
                    merged[k] = v
            # derived: ring width from token name when this rule matches the state
            w = token_ring_width(tok)
            if w:
                merged["ring-width"] = w

    var_table.clear()
    var_table.update(saved)
    return merged, notes


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def build_index(rules: list[Rule]) -> dict[str, list[Rule]]:
    index: dict[str, list[Rule]] = {}
    for r in rules:
        index.setdefault(r.token, []).append(r)
    return index


def main() -> int:
    check = "--check" in sys.argv
    verbose = "-v" in sys.argv
    only = None
    if "-v" in sys.argv:
        i = sys.argv.index("-v")
        if i + 1 < len(sys.argv):
            only = sys.argv[i + 1]

    if not STANDALONE.exists():
        print(f"missing {STANDALONE}; vendored CSS should live in design/kumo/", file=sys.stderr)
        return 2

    standalone = STANDALONE.read_text()
    theme = THEME.read_text() if THEME.exists() else ""
    collect_vars(standalone)
    collect_vars(theme)

    rules = parse_rules(standalone)
    index = build_index(rules)
    print(f"indexed {len(index)} utility tokens from {len(rules)} rules")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for old in OUT_DIR.glob("*.json"):
        if old.stem not in {f["fixture"] for f in FIXTURES}:
            old.unlink()
    stale = False
    for spec in FIXTURES:
        if only and spec["fixture"] != only:
            continue
        states_out: dict[str, dict[str, str]] = {}
        all_notes: list[str] = []
        for state, tokens in spec["states"].items():
            merged, notes = resolve_state(tokens, state, "light", index, spec["vars"])
            states_out[state] = merged
            all_notes += [f"{state}: {n}" for n in notes]
        doc = {
            "fixture": spec["fixture"],
            "description": spec["description"],
            "kumo_version": KUMO_VERSION,
            "keys": spec["keys"],
            "vars": spec["vars"],
            "states": states_out,
        }
        path = OUT_DIR / f"{spec['fixture']}.json"
        data = json.dumps(doc, indent=2, sort_keys=False) + "\n"
        if check and path.exists() and path.read_text() != data:
            print(f"stale: {path}")
            stale = True
        if not check or not path.exists():
            path.write_text(data)
        if verbose:
            print(f"\n=== {spec['fixture']}")
            for st, props in states_out.items():
                print(f"  [{st}]")
                for k, v in sorted(props.items()):
                    print(f"    {k}: {v}")
            for n in all_notes:
                print(f"  note: {n}")
    if check:
        print("OK: specs up to date" if not stale else "specs stale")
        return 1 if stale else 0
    return 0


if __name__ == "__main__":
    sys.exit(main())
