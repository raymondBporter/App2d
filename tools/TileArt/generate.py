#!/usr/bin/env python3
"""Draw App2d's ink terrain tilesets: ground + wall for each era.

The style ("1b": dark ink, gentle wobble, flat colour) was chosen from the concept renders in
concepts/. Every piece is drawn procedurally so it can be re-tuned and regenerated:

    python tools/TileArt/generate.py                 # writes Assets/Static/environments/tilesets/ink-*
    python tools/TileArt/generate.py --also-runtime  # and mirrors into Assets/Runtime for a quick look

Judge the result through the real renderer, not these PNGs:

    dotnet run --project App2d -- --tileset-smoke <dir> ink-medieval-ground ink-medieval-wall

Pieces follow SideScrollerTerrainTileset2D: a world-anchored fill that repeats every FILL_PERIOD,
edge strips SURFACE px deep that overlay the fill, and sprites for one-ways, spikes and ladders.
Strip art must repeat seamlessly per tile, so every wobble is pinned at tile edges.
"""
from __future__ import annotations

import argparse
import json
import math
import random
import shutil
import zlib
from pathlib import Path

from PIL import Image, ImageDraw

TILE = 32
SURFACE = 12
FILL_PERIOD = 128  # four tiles: long enough that distinctive details do not visibly repeat
CELLS = FILL_PERIOD // TILE
ONE_WAY_HEIGHT = 20
SPIKE_HEIGHT = 26
CORNER = 6
OUT_SCALE = 2          # texels per world px in the written PNGs
SUPER = 4              # supersampling factor while drawing
K = OUT_SCALE * SUPER  # drawing px per world px

INK = "#222b32"
LINE = 1.7   # world px, silhouette ink
AMP = 0.45   # world px, wobble amplitude
PRESS = 0.10
FILL_SHADE = 0.9  # fill sits darker than the lighter lip right under a walkable surface


def rgb(h: str, a: int = 255) -> tuple[int, int, int, int]:
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def shade(h: str, k: float) -> str:
    r, g, b, _ = rgb(h)
    return "#%02x%02x%02x" % tuple(max(0, min(255, int(c * k))) for c in (r, g, b))


def stable(key) -> int:
    """Deterministic across runs, unlike hash() on strings."""
    return zlib.crc32(repr(key).encode())


def seeded(*key) -> random.Random:
    return random.Random(repr(key))


class Canvas:
    """World-px drawing surface. With wrap set, every primitive is repeated so the image tiles."""

    def __init__(self, width: float, height: float, wrap: bool = False):
        self.w, self.h = width, height
        self.image = Image.new("RGBA", (round(width * K), round(height * K)), (0, 0, 0, 0))
        self.detail = Image.new("RGBA", self.image.size, (0, 0, 0, 0))
        self.wrap = wrap

    def _offsets(self):
        if not self.wrap:
            return [(0.0, 0.0)]
        return [(dx * self.w, dy * self.h) for dx in (-1, 0, 1) for dy in (-1, 0, 1)]

    def _draw(self, layer: str) -> ImageDraw.ImageDraw:
        return ImageDraw.Draw(self.detail if layer == "detail" else self.image)

    def rect(self, x0, y0, x1, y1, color, alpha=255):
        d = ImageDraw.Draw(self.image)
        for ox, oy in self._offsets():
            d.rectangle(((x0 + ox) * K, (y0 + oy) * K, (x1 + ox) * K - 1, (y1 + oy) * K - 1), fill=rgb(color, alpha))

    def poly(self, pts, color, alpha=255):
        layer = Image.new("RGBA", self.image.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        for ox, oy in self._offsets():
            d.polygon([((x + ox) * K, (y + oy) * K) for x, y in pts], fill=rgb(color, 255))
        if alpha < 255:
            layer.putalpha(layer.getchannel("A").point(lambda v: v * alpha // 255))
        self.image.alpha_composite(layer)

    def wobble(self, p0, p1, seed, amp, pinned=True):
        (x0, y0), (x1, y1) = p0, p1
        length = math.hypot(x1 - x0, y1 - y0)
        if length < 1e-6:
            return [p0, p1]
        n = max(2, int(length / 1.25))
        rng = random.Random(seed)
        waves = [(f * rng.uniform(0.7, 1.3), rng.uniform(0, 6.283), a) for f, a in ((0.03, 1.0), (0.07, 0.5), (0.16, 0.25))]
        nx, ny = -(y1 - y0) / length, (x1 - x0) / length

        def o(t):
            return sum(a * math.sin(f * t * length * 6.283 + ph) for f, ph, a in waves) / 1.2

        o0, o1 = o(0), o(1)
        out = []
        for i in range(n + 1):
            t = i / n
            off = (o(t) - ((1 - t) * o0 + t * o1) if pinned else o(t)) * amp
            out.append((x0 + (x1 - x0) * t + nx * off, y0 + (y1 - y0) * t + ny * off))
        return out

    def ink(self, pts, seed, weight=1.0, closed=False, color=INK, pinned=True, wobble=1.0, layer="main"):
        pts = list(pts) + ([pts[0]] if closed else [])
        path = []
        for i in range(len(pts) - 1):
            seg = self.wobble(pts[i], pts[i + 1], stable((seed, i)), AMP * wobble, pinned)
            path += seg if not path else seg[1:]
        rng = random.Random(seed)
        w0 = LINE * weight
        ph = rng.uniform(0, 6.28)
        d = self._draw(layer)
        col = rgb(color)
        for ox, oy in self._offsets():
            for i in range(len(path) - 1):
                w = max(0.2, w0 * (1 + PRESS * math.sin(i * 0.7 + ph))) * K
                a = ((path[i][0] + ox) * K, (path[i][1] + oy) * K)
                b = ((path[i + 1][0] + ox) * K, (path[i + 1][1] + oy) * K)
                d.line([a, b], fill=col, width=max(1, round(w)))
                r = w / 2
                d.ellipse((b[0] - r, b[1] - r, b[0] + r, b[1] + r), fill=col)
            x, y = (path[0][0] + ox) * K, (path[0][1] + oy) * K
            r = w0 * K / 2
            d.ellipse((x - r, y - r, x + r, y + r), fill=col)

    def shape(self, pts, color, seed, weight=1.0, layer="main", alpha=255):
        self.poly(pts, color, alpha)
        if weight:
            self.ink(pts, seed, weight, closed=True, layer=layer)

    def blob(self, box, color, seed, weight=0.55, layer="detail", n=10, alpha=255):
        x0, y0, x1, y1 = box
        cx, cy, rx, ry = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
        rng = random.Random(seed)
        pts = [(cx + rx * math.cos(a) * rng.uniform(0.9, 1.05), cy + ry * math.sin(a) * rng.uniform(0.9, 1.05))
               for a in [i / n * 6.283 for i in range(n)]]
        self.shape(pts, color, seed, weight, layer, alpha)

    def finish(self, detail_opacity=0.55) -> Image.Image:
        a = self.detail.getchannel("A").point(lambda v: int(v * detail_opacity))
        self.detail.putalpha(a)
        self.image.alpha_composite(self.detail)
        self.detail = Image.new("RGBA", self.image.size, (0, 0, 0, 0))
        size = (round(self.w * OUT_SCALE), round(self.h * OUT_SCALE))
        return self.image.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


# ---------------------------------------------------------------------------- palettes
ERAS = {
    "medieval": dict(
        dirt="#95653c", peb="#b88a5a", grass="#78b33c", wall="#b3aa98",
        wood="#c98a45", wood_dark="#86552a", tip="#a8392a", ivy="#4f9a38", vine="#3f5a2a", root="#5e3b22",
    ),
    "prehistoric": dict(
        dirt="#9b5e35", peb="#c28a5a", grass="#6fb238", wall="#c6a57a", crevice="#6e5540",
        wood="#9a6a3c", bark="#7a4f2a", endgrain="#e2bd84", vine="#4d7a2a", flint="#9aa6b3", bone="#efe6cc",
        ivy="#4f9a38", root="#5e3b22",
    ),
    "wasteland": dict(
        dirt="#8c6c4f", peb="#b4ada0", grass="#b3a64f", wall="#b9b5aa", stain="#6b665c",
        paint="#d4923a", steel_dark="#4a5359", rust="#b5612f", rust_dark="#7c3d1f", wire="#3d4247", root="#5a4a3a",
    ),
}


# ---------------------------------------------------------------------------- fills
def fill_ground(era: str, P: dict) -> Image.Image:
    c = Canvas(FILL_PERIOD, FILL_PERIOD, wrap=True)
    c.rect(0, 0, FILL_PERIOD, FILL_PERIOD, shade(P["dirt"], FILL_SHADE))
    rg = seeded(era, "ground-fill")
    for cy in range(CELLS):
        for cx in range(CELLS):
            x0, y0 = cx * TILE, cy * TILE
            for k in range(rg.choice((1, 2, 2, 3))):
                pw, ph = rg.uniform(4, 8), rg.uniform(3, 5)
                px, py = rg.uniform(x0 + 1, x0 + TILE - 1 - pw), rg.uniform(y0 + 1, y0 + TILE - 1 - ph)
                if era == "wasteland":
                    s = pw * 0.8
                    c.shape([(px, py + s * 0.6), (px + s * 0.3, py), (px + s * 1.1, py + s * 0.15), (px + s * 1.2, py + s * 0.8), (px + s * 0.5, py + s)],
                            P["peb"], stable((era, cx, cy, k)), 0.55, "detail")
                else:
                    c.blob((px, py, px + pw, py + ph), P["peb"], stable((era, cx, cy, k)))
            if rg.random() < 0.6:
                px, py = rg.uniform(x0 + 2, x0 + TILE - 10), rg.uniform(y0 + 3, y0 + TILE - 3)
                c.ink([(px, py), (px + rg.uniform(4, 8), py + rg.uniform(-1, 1))], stable((era, cx, cy, "dash")), 0.5, layer="detail")
            if era == "wasteland" and rg.random() < 0.4:
                px, py = rg.uniform(x0 + 2, x0 + TILE - 10), rg.uniform(y0 + 3, y0 + TILE - 4)
                c.ink([(px, py), (px + 7, py - 2)], stable((era, cx, cy, "rod")), 0.9, color=P["rust"], layer="detail")
            if era == "prehistoric" and rg.random() < 0.08:
                bx, by = rg.uniform(x0 + 4, x0 + TILE - 12), rg.uniform(y0 + 5, y0 + TILE - 5)
                c.shape([(bx, by - 0.9), (bx + 8, by - 0.9), (bx + 8, by + 0.9), (bx, by + 0.9)], P["bone"], stable((cx, cy, "bone")), 0.5, "detail")
                for ex in (bx, bx + 8):
                    for oy in (-1.2, 1.2):
                        c.blob((ex - 1.4, by + oy - 1.4, ex + 1.4, by + oy + 1.4), P["bone"], stable((cx, cy, ex, oy)), 0.5, n=8)
    return c.finish()


def fill_wall(era: str, P: dict) -> Image.Image:
    c = Canvas(FILL_PERIOD, FILL_PERIOD, wrap=True)
    rg = seeded(era, "wall-fill")
    base = shade(P["wall"], FILL_SHADE)
    if era == "medieval":
        c.rect(0, 0, FILL_PERIOD, FILL_PERIOD, base)
        for row in range(FILL_PERIOD // 16):
            y = row * 16
            off = 16 if row % 2 else 0
            for j in range(CELLS + 1):
                xa = off + (j - 1) * 32
                v = rg.uniform(-1, 1) * 0.04
                c.rect(xa + 0.6, y + 0.6, xa + 32 - 0.6, y + 16 - 0.6, shade(base, 1 + v))
            c.ink([(0, y), (FILL_PERIOD, y)], stable((era, row, "h")), 0.55, layer="detail")
            for j in range(CELLS + 1):
                x = off + j * 32
                c.ink([(x, y), (x, y + 16)], stable((era, row, j, "v")), 0.55, layer="detail")
        for k in range(10):
            px, py = rg.uniform(4, FILL_PERIOD - 8), rg.uniform(3, FILL_PERIOD - 6)
            c.ink([(px, py), (px + 3, py + 2), (px + 4, py + 5)], stable((era, k, "crack")), 0.45, layer="detail")
    elif era == "prehistoric":
        c.rect(0, 0, FILL_PERIOD, FILL_PERIOD, shade(P["crevice"], FILL_SHADE))
        gap = 1.1
        for cy in range(CELLS):
            for cx in range(CELLS):
                x0, y0 = cx * TILE, cy * TILE
                kind = rg.choice(("one", "h", "v", "h"))
                if kind == "one":
                    boxes = [(x0 + gap, y0 + gap, x0 + TILE - gap, y0 + TILE - gap)]
                elif kind == "h":
                    sy = y0 + rg.uniform(12, 20)
                    boxes = [(x0 + gap, y0 + gap, x0 + TILE - gap, sy - gap), (x0 + gap, sy + gap, x0 + TILE - gap, y0 + TILE - gap)]
                else:
                    sx = x0 + rg.uniform(12, 20)
                    boxes = [(x0 + gap, y0 + gap, sx - gap, y0 + TILE - gap), (sx + gap, y0 + gap, x0 + TILE - gap, y0 + TILE - gap)]
                for k, b in enumerate(boxes):
                    r = min(5, (b[2] - b[0]) / 2.2, (b[3] - b[1]) / 2.2)
                    tone = base if rg.random() < 0.7 else shade(base, 0.93)
                    c.shape(rounded(*b, r, rg), tone, stable((cx, cy, k, "stone")), 0.6, "detail")
                    if rg.random() < 0.3:
                        px, py = rg.uniform(b[0] + 3, b[2] - 6), rg.uniform(b[1] + 3, b[3] - 4)
                        c.ink([(px, py), (px + 3, py + 1.5)], stable((cx, cy, k, "chip")), 0.45, layer="detail")
    else:
        c.rect(0, 0, FILL_PERIOD, FILL_PERIOD, base)
        for j in range(0, FILL_PERIOD, 2 * TILE):
            c.ink([(0, j), (FILL_PERIOD, j)], stable((era, j, "sh")), 0.6, layer="detail")
            c.ink([(j, 0), (j, FILL_PERIOD)], stable((era, j, "sv")), 0.6, layer="detail")
        for k in range(8):
            sx = rg.uniform(4, FILL_PERIOD - 10)
            sy = rg.choice(range(0, FILL_PERIOD, 2 * TILE))
            length = rg.uniform(10, 22)
            c.poly([(sx, sy), (sx + 5, sy), (sx + 4, sy + length), (sx + 1.5, sy + length * 0.8)], P["stain"], alpha=56)
        for k in range(10):
            px, py = rg.uniform(4, FILL_PERIOD - 16), rg.uniform(4, FILL_PERIOD - 14)
            a = (px + rg.uniform(4, 7), py + rg.uniform(3, 6))
            c.ink([(px, py), a, (a[0] + rg.uniform(2, 6), a[1] + rg.uniform(4, 7))], stable((era, k, "cr")), 0.5, pinned=False, layer="detail")
            c.ink([a, (a[0] + 5, a[1] - 1.5)], stable((era, k, "cr2")), 0.4, pinned=False, layer="detail")
        for k in range(96):
            px, py = rg.uniform(0, FILL_PERIOD), rg.uniform(0, FILL_PERIOD)
            c.blob((px - 0.5, py - 0.5, px + 0.5, py + 0.5), P["stain"], stable((k, "agg")), 0, n=6, alpha=128)
    return c.finish()


def rounded(x0, y0, x1, y1, r, rg, n=4):
    pts = []
    for cx, cy, a0 in [(x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)]:
        for k in range(n + 1):
            a = math.radians(a0 + k * 90 / n)
            j = rg.uniform(-0.4, 0.4)
            pts.append((cx + (r + j) * math.cos(a), cy + (r + j) * math.sin(a)))
    return pts


# ---------------------------------------------------------------------------- surfaces
EDGE = LINE * 0.62  # centre line of the outline, measured in from the exposed edge


def top_strip(era: str, material: str, P: dict) -> Image.Image:
    c = Canvas(TILE, SURFACE)
    lip = P["dirt"] if material == "ground" else P["wall"]
    c.rect(0, 0, TILE, SURFACE, lip)
    grassy = not (era == "wasteland" and material == "wall")
    if grassy:
        depth = 5 if era == "wasteland" else 7.5
        lower = c.wobble((TILE, depth), (0, depth), stable((era, material, "g")), 1.3)
        c.poly([(0, 0), (TILE, 0)] + lower, P["grass"])
        c.ink(lower[2:-2], stable((era, material, "gl")), 0.5, pinned=False)
    else:
        rg = seeded(era, material, "dust")
        for k in range(5):
            px = rg.uniform(2, TILE - 3)
            c.blob((px, 2.2, px + rg.uniform(1.5, 3), 3.6), P["stain"], stable((k, "dust")), 0, n=6, alpha=90)
    c.ink([(0, EDGE), (TILE, EDGE)], stable((era, material, "top")), 1.0)
    return c.finish()


def bottom_strip(era: str, material: str, P: dict) -> Image.Image:
    c = Canvas(TILE, SURFACE)
    c.poly([(0, SURFACE - 4), (TILE, SURFACE - 4), (TILE, SURFACE), (0, SURFACE)], "#000000", alpha=22)
    c.ink([(0, SURFACE - EDGE), (TILE, SURFACE - EDGE)], stable((era, material, "bottom")), 1.0)
    return c.finish()


def side_strip(era: str, material: str, P: dict, grip: bool) -> Image.Image:
    """The left-hand strip; the right one is its mirror."""
    c = Canvas(SURFACE, TILE)
    c.ink([(EDGE, 0), (EDGE, TILE)], stable((era, material, "side")), 1.0)
    if grip:
        draw_grip(c, era, material, P)
    return c.finish()


def draw_grip(c: Canvas, era: str, material: str, P: dict):
    rg = seeded(era, material, "grip")
    if material == "wall" and era in ("medieval", "prehistoric"):
        vines = [(3.0, 0.0)] + ([(6.5, 1.7)] if era == "prehistoric" else [])
        for vx, ph in vines:
            pts = [(vx + 1.3 * math.sin(ph + t * 6.283), t * TILE) for t in (0, 0.25, 0.5, 0.75, 1.0)]
            c.ink(pts, stable((era, vx, "vine")), 1.1 if era == "prehistoric" else 0.8, color=P["vine"], wobble=0.4)
        for k in range(4):
            ly = 4 + k * 8
            lx = 3.3
            tip = lx + (7.5 if k % 2 == 0 else 5.5) + rg.uniform(0, 1.2)
            c.shape([(lx, ly), ((lx + tip) / 2, ly - 3.0), (tip, ly + 0.6), ((lx + tip) / 2, ly + 3.0)], P["ivy"], stable((era, k, "leaf")), 0.65)
    elif material == "wall":
        for y in (8, 24):
            staple = [(1.2, y - 3), (8.5, y - 3), (8.5, y + 1)]
            c.ink(staple, stable((y, "staple")), 1.5, wobble=0.2, pinned=False)
            c.ink(staple, stable((y, "staple")), 0.8, wobble=0.2, pinned=False, color=P["rust"])
    else:
        for k, y in enumerate((6, 17, 27)):
            c.blob((0.5, y - 1.8, 5.5 + rg.uniform(0, 1.5), y + 1.6), P["peb"], stable((era, k, "hold")), 0.6, layer="main")
        c.ink([(1.5, 9), (4.5, 11.5), (4, 14)], stable((era, "root1")), 0.7, color=P["root"], pinned=False)
        c.ink([(1.5, 21), (5, 22.5), (6.5, 25)], stable((era, "root2")), 0.7, color=P["root"], pinned=False)


def inner_corner() -> Image.Image:
    c = Canvas(CORNER, CORNER)
    r = LINE * 0.55
    m = CORNER / 2
    c.blob((m - r, m - r, m + r, m + r), INK, 1, 0, layer="main", n=10)
    return c.finish()


def outer_corner() -> Image.Image:
    # The two edge outlines already meet; one image serves all four corners, so keep it empty.
    return Canvas(CORNER, CORNER).finish()


# ---------------------------------------------------------------------------- props
def one_way(era: str, P: dict, part: str) -> Image.Image:
    c = Canvas(TILE, ONE_WAY_HEIGHT)
    left = part in ("left", "standalone")
    right = part in ("right", "standalone")
    if era == "medieval":
        xa, xb, th = (2 if left else 0), TILE - (2 if right else 0), 8
        c.shape([(xa, 0.6), (xb, 0.6), (xb, th), (xa, th)], P["wood"], stable((part, "plank")), 1.0)
        c.ink([(xa + 4, 4), (xb - 9, 4.5)], stable((part, "grain")), 0.45)
        for nx in (xa + 3, xb - 3):
            c.blob((nx - 0.9, 5.3, nx + 0.9, 7.1), P["wood_dark"], stable((part, nx)), 0, layer="main")
        for end, s in ((left, 1), (right, -1)):
            if end:
                bx = xa + 5 if s == 1 else xb - 5
                c.shape([(bx - s * 1.5, th), (bx + s * 9, th), (bx, th + 11)], P["wood_dark"], stable((part, s, "br")), 0.9)
    elif era == "prehistoric":
        xa, xb, th = (3 if left else 0), TILE - (3 if right else 0), 9
        c.shape([(xa, 0.6), (xb, 0.6), (xb, th), (xa, th)], P["wood"], stable((part, "log")), 1.0)
        rg = seeded(part, "bark")
        for k in range(2):
            y = 3 + k * 3.5
            a = rg.uniform(xa + 2, xb - 14)
            c.ink([(a, y), (a + rg.uniform(7, 12), y + rg.uniform(-0.6, 0.6))], stable((part, k, "bk")), 0.45, color=P["bark"])
        for end, ex in ((left, xa), (right, xb)):
            if end:
                c.blob((ex - 3, 0.4, ex + 3, th + 0.2), P["endgrain"], stable((part, ex, "end")), 0.9, "main", n=12)
                c.blob((ex - 1.2, th / 2 - 1.6, ex + 1.2, th / 2 + 1.8), P["endgrain"], stable((part, ex, "ring")), 0.45, "main", n=8)
        if not left:
            for k in range(3):
                c.ink([(-0.5 + k * 1.5, 0.2), (1 + k * 1.5, th + 0.4)], stable((part, k, "lash")), 0.8, color=P["vine"])
    else:
        xa, xb, th = (1 if left else 0), TILE - (1 if right else 0), 9
        if right:
            pts = [(xa, 0.6), (xb, 0.6), (xb - 2, 3), (xb + 0.5, 5), (xb - 1, th), (xa, th)]
        else:
            pts = [(xa, 0.6), (xb, 0.6), (xb, th), (xa, th)]
        c.shape(pts, P["paint"], stable((part, "gd")), 1.0)
        rg = seeded(part, "rust")
        rx = rg.uniform(xa + 2, xb - 12)
        c.blob((rx, 1.2, rx + rg.uniform(7, 12), th - 1), P["rust"], stable((part, "rp")), 0, "main", alpha=140)
        for y in (2.4, 6.8):
            c.ink([(xa + 1, y), (xb - 1, y)], stable((part, y, "fl")), 0.45)
        c.blob((TILE / 2 - 2.3, 2.5, TILE / 2 + 2.3, 6.6), P["steel_dark"], stable((part, "hole")), 0.5, "main", n=12)
        for x in (xa + 3, xb - 3):
            for y in (1.5, 7.8):
                c.blob((x - 0.7, y - 0.7, x + 0.7, y + 0.7), P["steel_dark"], stable((part, x, y)), 0, "main", n=6)
    return c.finish(1.0)


def spikes(era: str, P: dict) -> Image.Image:
    c = Canvas(TILE, SPIKE_HEIGHT)
    top = TILE - SPIKE_HEIGHT  # spike art is drawn in tile coordinates, then shifted up into the sprite
    rg = seeded(era, "spikes")
    def T(pts):
        return [(x, y - top) for x, y in pts]
    if era == "medieval":
        for k in range(3):
            bx = 2 + k * 10.5
            lean = rg.uniform(-1.5, 1.5)
            tip = (bx + 4.5 + lean, rg.uniform(7, 10))
            c.shape(T([(bx, TILE), (bx + 0.5, 20), tip, (bx + 8.5, 20), (bx + 9, TILE)]), P["wood"], stable((k, "st")), 0.9)
            c.shape(T([(bx + 1.7 + lean * 0.5, 15), tip, (bx + 7.3 + lean * 0.5, 15)]), P["tip"], stable((k, "tip")), 0.9)
        c.ink(T([(0, 25), (TILE, 25.5)]), stable("lash"), 0.8)
    elif era == "prehistoric":
        for k in range(4):
            bx = 1 + k * 7.8
            w = rg.uniform(6, 8)
            tip = (bx + w / 2 + rg.uniform(-2, 2), rg.uniform(7, 12))
            col = P["bone"] if k == 2 else P["flint"]
            c.shape(T([(bx, TILE), (bx + w * 0.2, 20), tip, (bx + w * 0.85, 18), (bx + w, TILE)]), col, stable((k, "sh")), 0.9)
            c.ink(T([tip, (bx + w * 0.55, TILE - 2)]), stable((k, "facet")), 0.45)
    else:
        for k in range(3):
            bx = 5 + k * 11
            tip = (bx + rg.uniform(-4, 4), rg.uniform(7, 11))
            c.ink(T([(bx, TILE), tip]), stable((k, "rbo")), 2.0, pinned=False, wobble=0.2)
            c.ink(T([(bx, TILE), tip]), stable((k, "rbi")), 1.0, pinned=False, wobble=0.2, color=P["rust"])
        pts = []
        for i in range(49):
            t = i / 48
            a = t * 3 * 6.283
            pts.append((t * TILE + math.sin(a) * 3.5, 21 - math.cos(a) * 6.5 - top))
        c.ink(pts, stable("coil"), 0.7, pinned=False, wobble=0, color=P["wire"])
        for k in range(6):
            bx, by = 3 + k * 5.2, (15 + (k % 2) * 12) - top
            c.ink([(bx - 1.3, by - 1.3), (bx + 1.3, by + 1.3)], stable((k, "b1")), 0.55, wobble=0, color=P["wire"])
            c.ink([(bx + 1.3, by - 1.3), (bx - 1.3, by + 1.3)], stable((k, "b2")), 0.55, wobble=0, color=P["wire"])
    return c.finish(1.0)


def ladder(era: str, P: dict, is_top: bool) -> Image.Image:
    c = Canvas(TILE, TILE)
    y0 = 2.5 if is_top else 0
    if era == "prehistoric":
        for rx, bend in ((6, 1.2), (24, -1.0)):
            c.shape([(rx, y0), (rx + 3, y0), (rx + 3 + bend, TILE / 2), (rx + 3, TILE), (rx, TILE), (rx + bend, TILE / 2)], P["wood"], stable((rx, "rail")), 0.9)
        for k in range(3):
            y = 5 + k * 11
            c.shape([(5, y - 1.3), (27, y - 0.9), (27, y + 1.5), (5, y + 1.2)], P["wood"], stable((k, "rung")), 0.7)
            for rx in (7.3, 25.5):
                c.ink([(rx - 1.8, y - 2), (rx + 1.8, y + 2)], stable((k, rx, "x1")), 0.6, color=P["vine"], wobble=0)
                c.ink([(rx + 1.8, y - 2), (rx - 1.8, y + 2)], stable((k, rx, "x2")), 0.6, color=P["vine"], wobble=0)
    else:
        colour = P["wood"] if era == "medieval" else P["paint"]
        for k in range(4):
            y = 4 + k * 8
            c.shape([(8, y - 1.1), (24, y - 1.1), (24, y + 1.1), (8, y + 1.1)], colour, stable((k, "rung")), 0.7)
        for rx in (6, 24):
            c.shape([(rx, y0), (rx + 2.5, y0), (rx + 2.5, TILE), (rx, TILE)], colour, stable((rx, "rail")), 0.9)
            if era == "wasteland":
                rg = seeded(rx, "rs")
                ry = rg.uniform(6, 24)
                c.blob((rx, ry, rx + 2.5, ry + rg.uniform(3, 6)), P["rust"], stable((rx, "rsb")), 0, "main", alpha=180)
    return c.finish(1.0)


# ---------------------------------------------------------------------------- output
def save(image: Image.Image, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)


def write_tileset(root: Path, era: str, material: str):
    P = ERAS[era]
    tileset_id = f"ink-{era}-{material}"
    out = root / tileset_id
    if out.exists():
        shutil.rmtree(out)
    save(fill_ground(era, P) if material == "ground" else fill_wall(era, P), out / "fill.png")
    save(top_strip(era, material, P), out / "surfaces/top.png")
    save(bottom_strip(era, material, P), out / "surfaces/bottom.png")
    for grip in (False, True):
        left = side_strip(era, material, P, grip)
        suffix = "-grip" if grip else ""
        save(left, out / f"surfaces/left{suffix}.png")
        save(left.transpose(Image.FLIP_LEFT_RIGHT), out / f"surfaces/right{suffix}.png")
    save(outer_corner(), out / "corners/outer.png")
    save(inner_corner(), out / "corners/inner.png")
    for part in ("standalone", "left", "middle", "right"):
        save(one_way(era, P, part), out / f"one-way/{part}.png")
        save(spikes(era, P), out / f"hazards/spikes/{part}.png")
    save(ladder(era, P, True), out / "ladder/top.png")
    save(ladder(era, P, False), out / "ladder/middle.png")
    manifest = {
        "id": tileset_id, "tileSize": TILE, "fillPeriod": FILL_PERIOD, "surfaceThickness": SURFACE,
        "outerCornerSize": CORNER, "innerCornerSize": CORNER,
        "oneWayVisualHeight": ONE_WAY_HEIGHT, "spikeVisualHeight": SPIKE_HEIGHT,
    }
    (out / "tileset.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--content-root", type=Path, help="Defaults to Assets/Static.")
    parser.add_argument("--also-runtime", action="store_true", help="Mirror the output into Assets/Runtime.")
    parser.add_argument("eras", nargs="*", default=list(ERAS))
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[2]
    roots = [(args.content_root or repository / "Assets/Static") / "environments/tilesets"]
    if args.also_runtime:
        roots.append(repository / "Assets/Runtime/environments/tilesets")
    for era in args.eras:
        for material in ("ground", "wall"):
            for root in roots:
                out = write_tileset(root, era, material)
            print("wrote", out.name)


if __name__ == "__main__":
    main()
