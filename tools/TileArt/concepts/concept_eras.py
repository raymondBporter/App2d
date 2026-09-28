"""Throwaway: the chosen 1b style (ink + flat, a little more colour) across three eras.

Same level layout for every era. 'D' cells are the era's natural ground, 'S'/'G' its built material.
"""
import math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageFont
import concept as C
from concept import U, T, W, H, GW, GH, GRID, SOLID, Scene, rgb, seed_of, solid, cell, draw_figure

ST = dict(C.STYLES["1-ink-flat"])
C.PAL.clear(); C.PAL.update(C.BASE_PAL); C.PAL.update(C.COLOUR)  # figure body colour etc.

ERAS = {
    "prehistoric": dict(
        sky_top="#94c8df", sky_bot="#f6e7c4", cloud="#fbf8ee", far="#bdb6cf", far2="#aca3c2", smoke="#c9c3c4",
        lava="#e0763a", mid="#9cc67e", tree="#7fb266", frond="#86b95f", trunk="#9a7c58",
        dirt="#9b5e35", peb="#c28a5a", grass="#6fb238", built="#c6a57a", crevice="#6e5540",
        wood="#9a6a3c", bark="#7a4f2a", endgrain="#e2bd84", vine="#4d7a2a", flint="#9aa6b3", bone="#efe6cc",
        ivy="#4f9a38",
    ),
    "medieval": None,
    "wasteland": dict(
        sky_top="#9fbcc2", sky_bot="#f1d6a8", cloud="#efe0c6", far="#bcc3c6", far2="#b0b8bd", window="#9aa3aa", paint="#d4923a",
        mid="#c7aa7c", tree="#6f5d4a", trunk="#6f5d4a", car="#b0643a", car_dark="#6f4a36",
        dirt="#8c6c4f", peb="#b4ada0", grass="#b3a64f", built="#b9b5aa", stain="#6b665c",
        steel="#8a979e", steel_dark="#4a5359", rust="#b5612f", rust_dark="#7c3d1f", wire="#3d4247",
        ivy="#6f9a3a",
    ),
}
TITLES = {"prehistoric": "Prehistoric", "medieval": "Medieval", "wasteland": "Post-apocalyptic"}


def rrect(x0, y0, x1, y1, r, rg=None, n=4):
    pts = []
    for (cx, cy, a0) in [(x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)]:
        for k in range(n + 1):
            a = math.radians(a0 + k * 90 / n)
            j = rg.uniform(-0.4, 0.4) * U if rg else 0
            pts.append((cx + (r + j) * math.cos(a), cy + (r + j) * math.sin(a)))
    return pts


def clipped(sc, poly, color, seed, clip, weight=0.55, layer="detail", shade=None, alpha=1.0):
    m, ox, oy = sc.mask_of(polys=[poly], pad=U)
    m = ImageChops.multiply(m, clip.crop((ox, oy, ox + m.width, oy + m.height)))
    sh = shade[oy:oy + m.height, ox:ox + m.width] if shade is not None else None
    sc.paint(m, ox, oy, color, seed, shade=sh, alpha=alpha)
    if weight:
        sc.ink(poly, seed, weight, closed=True, layer=layer)


# ------------------------------------------------------------------ backgrounds
def sky(sc, P):
    top, bot = np.array(rgb(P["sky_top"]), np.float32), np.array(rgb(P["sky_bot"]), np.float32)
    t = np.linspace(0, 1, H)[:, None, None]
    s = np.broadcast_to(top * (1 - t) + bot * t, (H, W, 3))
    sc.canvas = Image.fromarray(np.dstack([s, np.full((H, W), 255.0)]).astype(np.uint8), "RGBA")


def clouds(sc, P, spots):
    for i, (x, y, s) in enumerate(spots):
        for j in range(3):
            bx = (x + j * 0.9 * s) * T
            by = (y + (0.25 if j == 1 else 0.45)) * T - (0.35 * T if j == 1 else 0)
            sc.blob((bx, by, bx + 1.5 * s * T, by + 0.8 * s * T), P["cloud"], seed_of("c", i, j), 0.7, "far", alpha=0.85)


def hill(y, a, f1, f2, ph=0):
    return [(0, H)] + [(x * U, y * T + math.sin(x / f1 + ph) * a * T + math.sin(x / f2) * 0.13 * T)
                       for x in range(0, W // U + 1, 16)] + [(W, H)]


def hill_y(xw, y, a, f1, f2, ph=0):
    x = xw / U
    return y * T + math.sin(x / f1 + ph) * a * T + math.sin(x / f2) * 0.13 * T


def bg_prehistoric(sc, P):
    sky(sc, P)
    clouds(sc, P, [(2.5, 1.2, 0.9), (9.5, 0.7, 0.7)])
    # volcano + smoke
    vx, vy = 17.2 * T, 2.3 * T
    for k, (dx, dy, s) in enumerate([(0, -0.6, 0.7), (0.5, -1.3, 0.9), (1.3, -1.9, 1.1)]):
        x, y = vx + dx * T, vy + dy * T
        sc.blob((x - 0.6 * s * T, y - 0.45 * s * T, x + 0.6 * s * T, y + 0.45 * s * T), P["smoke"], seed_of("sm", k), 0.7, "far", alpha=0.9)
    sc.shape([(13.0 * T, 6.2 * T), (vx - 0.55 * T, vy), (vx + 0.55 * T, vy), (21.8 * T, 6.2 * T)], P["far2"], 201, 0.7, "far")
    sc.shape([(vx - 0.3 * T, vy + 0.05 * T), (vx - 0.05 * T, vy + 1.2 * T), (vx + 0.15 * T, vy + 0.6 * T), (vx + 0.35 * T, vy + 1.6 * T),
              (vx + 0.42 * T, vy + 0.05 * T)], P["lava"], 202, 0.6, "far")
    sc.shape(hill(5.7, 0.45, 230, 80), P["far"], 203, 0.7, "far", closed=False)
    # long-neck
    dx, dy = 2.2 * T, 5.0 * T
    body = [(dx - 1.1 * T, dy + 0.1 * T), (dx - 0.6 * T, dy - 0.45 * T), (dx + 0.5 * T, dy - 0.5 * T), (dx + 0.95 * T, dy - 0.2 * T),
            (dx + 1.2 * T, dy - 1.4 * T), (dx + 1.45 * T, dy - 1.75 * T), (dx + 1.75 * T, dy - 1.72 * T), (dx + 1.6 * T, dy - 1.55 * T),
            (dx + 1.35 * T, dy - 1.2 * T), (dx + 1.05 * T, dy + 0.1 * T), (dx + 0.8 * T, dy + 0.75 * T), (dx + 0.6 * T, dy + 0.75 * T),
            (dx + 0.5 * T, dy + 0.25 * T), (dx - 0.35 * T, dy + 0.3 * T), (dx - 0.45 * T, dy + 0.75 * T), (dx - 0.65 * T, dy + 0.75 * T),
            (dx - 0.7 * T, dy + 0.25 * T), (dx - 1.8 * T, dy + 0.35 * T)]
    sc.shape(body, P["far2"], 204, 0.7, "far")
    # jungle
    mid = hill(7.5, 0.5, 170, 60, 2)
    sc.shape(mid, P["mid"], 205, 0.8, "mid", closed=False)
    for k, tx in enumerate([0.8, 6.2, 10.4, 13.3]):
        x = tx * T
        yb = hill_y(x, 7.5, 0.5, 170, 60, 2) + 0.3 * T
        lean = (0.25 if k % 2 else -0.2) * T
        top = (x + lean, yb - (2.0 + 0.3 * (k % 2)) * T)
        sc.shape([(x - 0.08 * T, yb), (top[0] - 0.05 * T, top[1]), (top[0] + 0.05 * T, top[1]), (x + 0.08 * T, yb)], P["trunk"], 210 + k, 0.7, "mid")
        for j in range(6):
            a = math.radians(-160 + j * 28)
            L = 0.8 * T
            tip = (top[0] + math.cos(a) * L, top[1] + math.sin(a) * L * 0.8 + 0.25 * T)
            mx, my = (top[0] + tip[0]) / 2, (top[1] + tip[1]) / 2 - 0.15 * T
            nx, ny = -(tip[1] - top[1]), (tip[0] - top[0])
            nl = math.hypot(nx, ny) or 1
            w = 0.12 * T
            sc.shape([top, (mx + nx / nl * w, my + ny / nl * w), tip, (mx - nx / nl * w, my - ny / nl * w)], P["frond"], 220 + k * 10 + j, 0.6, "mid")
    for k, tx in enumerate([3.4, 8.6, 12.0]):
        x = tx * T
        yb = hill_y(x, 7.5, 0.5, 170, 60, 2) + 0.2 * T
        sc.blob((x - 0.7 * T, yb - 0.7 * T, x + 0.7 * T, yb + 0.2 * T), P["tree"], 260 + k, 0.7, "mid")


def bg_wasteland(sc, P):
    sky(sc, P)
    clouds(sc, P, [(4.0, 1.5, 1.1), (15.0, 0.8, 0.8)])
    rg = random.Random(7)
    # ruined skyline
    for k, (bx, bw, bh) in enumerate([(-0.3, 1.6, 3.4), (1.6, 1.1, 4.6), (3.1, 1.8, 2.8), (5.3, 1.2, 4.0), (7.0, 2.0, 3.1),
                                      (9.6, 1.3, 4.9), (11.4, 1.7, 2.6), (13.6, 1.2, 3.8), (15.2, 2.2, 4.4), (18.0, 1.4, 3.0), (19.8, 2.4, 3.9)]):
        x0, x1 = bx * T, (bx + bw) * T
        yb, yt = 6.4 * T, (6.4 - bh) * T
        cuts = [(x1, yt + rg.uniform(0.1, 0.7) * T)]
        n = 3
        for j in range(1, n):
            cuts.append((x1 - (x1 - x0) * j / n, yt + rg.uniform(0, 0.9) * T))
        poly = [(x0, yb), (x0, yt + rg.uniform(0, 0.4) * T)] + sorted(cuts) + [(x1, yb)]
        col = P["far"] if k % 2 else P["far2"]
        sc.shape(poly, col, 300 + k, 0.7, "far")
        wl = sc.layer("win")
        d = ImageDraw.Draw(wl)
        for wy in np.arange(yt + 0.9 * T, yb - 0.2 * T, 0.45 * T):
            for wx in np.arange(x0 + 0.18 * T, x1 - 0.2 * T, 0.35 * T):
                if rg.random() < 0.75:
                    d.rectangle((wx, wy, wx + 0.13 * T, wy + 0.18 * T), fill=rgb(P["window"]) + (255,))
        sc.flush("win", 0.55)
    sc.shape(hill(6.2, 0.2, 260, 70), P["far2"], 320, 0.7, "far", closed=False)
    # rubble dunes, dead trees, pylon, wreck
    mid = hill(7.6, 0.45, 150, 55, 1)
    # pylon (behind dune)
    px, py = 13.6 * T, hill_y(13.6 * T, 7.6, 0.45, 150, 55, 1)
    lean = 0.35 * T
    top = (px + lean, py - 3.0 * T)
    for a, b in [((px - 0.5 * T, py), (top[0] - 0.12 * T, top[1])), ((px + 0.5 * T, py), (top[0] + 0.12 * T, top[1]))]:
        sc.ink([a, b], seed_of("py", a), 1.1, layer="mid", color=P["steel_dark"])
    for j in range(5):
        t0, t1 = j / 5, (j + 1) / 5
        la = (px - 0.5 * T + (top[0] - 0.12 * T - px + 0.5 * T) * t0, py + (top[1] - py) * t0)
        rb = (px + 0.5 * T + (top[0] + 0.12 * T - px - 0.5 * T) * t1, py + (top[1] - py) * t1)
        lb = (px - 0.5 * T + (top[0] - 0.12 * T - px + 0.5 * T) * t1, py + (top[1] - py) * t1)
        ra = (px + 0.5 * T + (top[0] + 0.12 * T - px - 0.5 * T) * t0, py + (top[1] - py) * t0)
        sc.ink([la, rb], seed_of("pyx", j), 0.6, layer="mid", color=P["steel_dark"])
        sc.ink([ra, lb], seed_of("pyy", j), 0.6, layer="mid", color=P["steel_dark"])
    sc.ink([(top[0] - 0.7 * T, top[1] + 0.3 * T), (top[0] + 0.6 * T, top[1] + 0.15 * T)], 331, 1.0, layer="mid", color=P["steel_dark"])
    sc.ink([(top[0] + 0.6 * T, top[1] + 0.15 * T), (top[0] + 1.4 * T, top[1] + 1.9 * T)], 332, 0.5, layer="mid", color=P["wire"])
    sc.flush("mid", 0.8)
    sc.shape(mid, P["mid"], 330, 0.8, "mid", closed=False)
    for k, tx in enumerate([1.2, 6.7, 10.6]):
        x = tx * T
        yb = hill_y(x, 7.6, 0.45, 150, 55, 1) + 0.25 * T
        trunk = [(x, yb), (x + 0.1 * T, yb - 1.1 * T), (x - 0.05 * T, yb - 1.9 * T)]
        sc.ink(trunk, seed_of("dt", k), 2.2, layer="mid", color=P["trunk"])
        for j, (fy, dxb, dyb) in enumerate([(0.45, 0.55, -0.4), (0.62, -0.5, -0.35), (0.85, 0.35, -0.3)]):
            bx, by = x + 0.1 * T * fy, yb - 1.9 * T * fy
            sc.ink([(bx, by), (bx + dxb * T, by + dyb * T)], seed_of("db", k, j), 1.1, layer="mid", color=P["trunk"])
            sc.ink([(bx + dxb * T * 0.6, by + dyb * T * 0.6), (bx + dxb * T * 0.8, by + dyb * T * 1.4)], seed_of("dbb", k, j), 0.8, layer="mid", color=P["trunk"])
    cx, cy = 3.6 * T, hill_y(4.2 * T, 7.6, 0.45, 150, 55, 1) + 0.15 * T
    car = [(cx, cy), (cx + 0.1 * T, cy - 0.45 * T), (cx + 0.55 * T, cy - 0.55 * T), (cx + 0.85 * T, cy - 0.95 * T), (cx + 1.65 * T, cy - 0.95 * T),
           (cx + 1.95 * T, cy - 0.55 * T), (cx + 2.4 * T, cy - 0.45 * T), (cx + 2.45 * T, cy)]
    sc.shape(car, P["car"], 340, 0.8, "mid")
    sc.shape([(cx + 0.95 * T, cy - 0.85 * T), (cx + 1.25 * T, cy - 0.85 * T), (cx + 1.25 * T, cy - 0.55 * T), (cx + 0.75 * T, cy - 0.55 * T)],
             P["car_dark"], 341, 0.5, "mid")
    sc.shape([(cx + 1.35 * T, cy - 0.85 * T), (cx + 1.6 * T, cy - 0.85 * T), (cx + 1.8 * T, cy - 0.55 * T), (cx + 1.35 * T, cy - 0.55 * T)],
             P["car_dark"], 342, 0.5, "mid")
    sc.blob((cx + 1.8 * T, cy - 0.25 * T, cx + 2.2 * T, cy + 0.15 * T), P["car_dark"], 343, 0.6, "mid")
    sc.flush("mid", 0.4)


# ------------------------------------------------------------------ terrain
def terrain(sc, P, era):
    st = sc.st
    r = 5 * U
    masks = {m: Image.new("L", (W, H), 0) for m in "DS"}
    topband = Image.new("L", (W, H), 0)
    tb = ImageDraw.Draw(topband)
    for cy in range(GH):
        for cx in range(GW):
            ch = GRID[cy][cx]
            if ch not in SOLID:
                continue
            d = ImageDraw.Draw(masks["S" if ch in "SG" else "D"])
            x0, y0 = cx * T, cy * T
            d.rectangle((x0, y0, x0 + T - 1, y0 + T - 1), fill=255)
            ex = {k: not solid(cx + dx, cy + dy) for k, (dx, dy) in dict(t=(0, -1), b=(0, 1), l=(-1, 0), r=(1, 0)).items()}
            for (a, b_, qx, qy) in [("t", "l", 0, 0), ("t", "r", 1, 0), ("b", "l", 0, 1), ("b", "r", 1, 1)]:
                if ex[a] and ex[b_]:
                    sx, sy = x0 + qx * (T - r), y0 + qy * (T - r)
                    d.rectangle((sx, sy, sx + r, sy + r), fill=0)
                    ccx, ccy = x0 + (T - r if qx else r), y0 + (T - r if qy else r)
                    d.ellipse((ccx - r, ccy - r, ccx + r, ccy + r), fill=255)
            if ex["t"]:
                tb.rectangle((x0, y0, x0 + T, y0 + 12 * U), fill=255)
    shade = 1 - np.asarray(topband.filter(ImageFilter.GaussianBlur(1 * U))).astype(np.float32) / 255
    sc.paint(masks["D"], 0, 0, P["dirt"], 101, shade=shade)
    sc.paint(masks["S"], 0, 0, P["crevice"] if era == "prehistoric" else P["built"], 102, shade=shade)
    late = []  # things drawn above the outline (rebar stubs)

    for cy in range(GH):
        for cx in range(GW):
            ch = GRID[cy][cx]
            if ch not in SOLID:
                continue
            x0, y0 = cx * T, cy * T
            rg = random.Random(seed_of(cx, cy, era))
            topx = not solid(cx, cy - 1)
            ymin = y0 + (12 * U if topx else 3 * U)
            ex = {k: not solid(cx + dx, cy + dy) for k, (dx, dy) in dict(t=(0, -1), b=(0, 1), l=(-1, 0), r=(1, 0)).items()}
            if ch == "D":
                if era == "prehistoric":
                    for k in range(rg.choice((1, 2, 2))):
                        pw, ph = rg.uniform(4, 8) * U, rg.uniform(3, 5) * U
                        px = rg.uniform(x0 + 3 * U, x0 + T - 3 * U - pw)
                        py = rg.uniform(ymin, y0 + T - 3 * U - ph)
                        sc.blob((px, py, px + pw, py + ph), P["peb"], seed_of(cx, cy, k), 0.55, "detail", n=9)
                    if topx:
                        for k in range(rg.choice((0, 1, 2))):
                            rx = rg.uniform(x0 + 4 * U, x0 + T - 4 * U)
                            L = rg.uniform(6, 13) * U
                            sc.ink([(rx, y0 + 6 * U), (rx + rg.uniform(-2, 2) * U, y0 + 6 * U + L * 0.5), (rx + rg.uniform(-3, 3) * U, y0 + 6 * U + L)],
                                   seed_of(cx, cy, k, "root"), 0.5, layer="detail", pinned=False)
                    if rg.random() < 0.12 and not topx:
                        bx, by = rg.uniform(x0 + 6 * U, x0 + T - 14 * U), rg.uniform(y0 + 8 * U, y0 + T - 8 * U)
                        sc.shape([(bx, by - 0.9 * U), (bx + 8 * U, by - 0.9 * U), (bx + 8 * U, by + 0.9 * U), (bx, by + 0.9 * U)], P["bone"], seed_of(cx, cy, "bone"), 0.5, "detail")
                        for ex_ in (bx, bx + 8 * U):
                            for oy_ in (-1.2, 1.2):
                                sc.blob((ex_ - 1.4 * U, by + oy_ * U - 1.4 * U, ex_ + 1.4 * U, by + oy_ * U + 1.4 * U), P["bone"], seed_of(cx, cy, ex_, oy_), 0.5, "detail", n=8)
                else:
                    for k in range(rg.choice((1, 2, 2))):
                        px = rg.uniform(x0 + 4 * U, x0 + T - 10 * U)
                        py = rg.uniform(ymin, y0 + T - 8 * U)
                        s = rg.uniform(3, 6) * U
                        poly = [(px, py + s * 0.6), (px + s * 0.3, py), (px + s * 1.1, py + s * 0.15), (px + s * 1.2, py + s * 0.8), (px + s * 0.5, py + s)]
                        sc.shape(poly, P["peb"], seed_of(cx, cy, k, "rub"), 0.55, "detail")
                    if rg.random() < 0.3:
                        px, py = rg.uniform(x0 + 4 * U, x0 + T - 12 * U), rg.uniform(ymin + 2 * U, y0 + T - 5 * U)
                        sc.ink([(px, py), (px + 7 * U, py - 2 * U)], seed_of(cx, cy, "rod"), 0.9, layer="detail", color=P["rust"])
                    if rg.random() < 0.5:
                        px, py = rg.uniform(x0 + 4 * U, x0 + T - 12 * U), rg.uniform(ymin + 2 * U, y0 + T - 4 * U)
                        sc.ink([(px, py), (px + rg.uniform(4, 8) * U, py + rg.uniform(-1, 1) * U)], seed_of(cx, cy, 9), 0.5, layer="detail")
                if cell(cx + 1, cy) in "SG":
                    sc.ink([(x0 + T, y0), (x0 + T, y0 + T)], seed_of(cx, cy, "seam"), 0.6, layer="detail")
            elif era == "prehistoric":
                # dry-stone boulders; crevice colour shows between them
                ins = {k: (0 if ex[k] else 1.1 * U) for k in "tblr"}
                X0, Y0, X1, Y1 = x0 + ins["l"], y0 + ins["t"], x0 + T - ins["r"], y0 + T - ins["b"]
                kind = rg.choice(("one", "h", "v", "h"))
                if kind == "one":
                    boxes = [(X0, Y0, X1, Y1)]
                elif kind == "h":
                    sy = y0 + rg.uniform(12, 20) * U
                    boxes = [(X0, Y0, X1, sy - 1.1 * U), (X0, sy + 1.1 * U, X1, Y1)]
                else:
                    sx = x0 + rg.uniform(12, 20) * U
                    boxes = [(X0, Y0, sx - 1.1 * U, Y1), (sx + 1.1 * U, Y0, X1, Y1)]
                for k, b in enumerate(boxes):
                    rr = min(5 * U, (b[2] - b[0]) / 2.2, (b[3] - b[1]) / 2.2)
                    tone = P["built"] if rg.random() < 0.7 else C.darken(P["built"], 0.93)[:7]
                    clipped(sc, rrect(*b, rr, rg), tone, seed_of(cx, cy, k, "stone"), masks["S"], 0.6, shade=shade)
                    if rg.random() < 0.3:
                        px, py = rg.uniform(b[0] + 3 * U, b[2] - 6 * U), rg.uniform(max(b[1] + 3 * U, ymin), b[3] - 4 * U)
                        sc.ink([(px, py), (px + 3 * U, py + 1.5 * U)], seed_of(cx, cy, k, "chip"), 0.45, layer="detail")
            else:
                # cracked concrete slabs, 2x2 tiles per slab
                if cx % 2 == 0 and cell(cx - 1, cy) in "SG":
                    sc.ink([(x0, y0 + (3 * U if topx else 0)), (x0, y0 + T)], seed_of(cx, cy, "sv"), 0.6, layer="detail")
                if cy % 2 == 0 and cell(cx, cy - 1) in "SG":
                    sc.ink([(x0, y0), (x0 + T, y0)], seed_of(cx, cy, "sh"), 0.6, layer="detail")
                    for k in range(rg.choice((0, 1, 1))):
                        sx = rg.uniform(x0 + 3 * U, x0 + T - 8 * U)
                        L = rg.uniform(8, 20) * U
                        sm, ox, oy = sc.mask_of(polys=[[(sx, y0), (sx + 5 * U, y0), (sx + 4 * U, y0 + L), (sx + 1.5 * U, y0 + L * 0.8)]], pad=U)
                        sm = ImageChops.multiply(sm, masks["S"].crop((ox, oy, ox + sm.width, oy + sm.height)))
                        sc.paint(sm, ox, oy, P["stain"], 0, alpha=0.22)
                if rg.random() < 0.4:
                    px, py = rg.uniform(x0 + 4 * U, x0 + T - 16 * U), rg.uniform(ymin + 1 * U, y0 + T - 12 * U)
                    a = (px + rg.uniform(4, 7) * U, py + rg.uniform(3, 6) * U)
                    sc.ink([(px, py), a, (a[0] + rg.uniform(2, 6) * U, a[1] + rg.uniform(4, 7) * U)], seed_of(cx, cy, "cr"), 0.5, layer="detail", pinned=False)
                    sc.ink([a, (a[0] + 5 * U, a[1] - 1.5 * U)], seed_of(cx, cy, "cr2"), 0.4, layer="detail", pinned=False)
                for k in range(rg.randint(2, 5)):
                    px, py = rg.uniform(x0 + 2 * U, x0 + T - 2 * U), rg.uniform(ymin, y0 + T - 2 * U)
                    sc.blob((px - 0.5 * U, py - 0.5 * U, px + 0.5 * U, py + 0.5 * U), P["stain"], seed_of(cx, cy, k, "agg"), 0, n=6, alpha=0.5)
                for side in "lrb":
                    if ex[side] and rg.random() < 0.25:
                        t_ = rg.uniform(8, 24) * U
                        if side == "l":
                            late.append(((x0 + 2 * U, y0 + t_), (x0 - 3.5 * U, y0 + t_ + rg.uniform(-2, 2) * U)))
                        elif side == "r":
                            late.append(((x0 + T - 2 * U, y0 + t_), (x0 + T + 3.5 * U, y0 + t_ + rg.uniform(-2, 2) * U)))
                        else:
                            late.append(((x0 + t_, y0 + T - 2 * U), (x0 + t_ + rg.uniform(-2, 2) * U, y0 + T + 3.5 * U)))
    sc.flush("detail", 0.55)

    # top cover
    for cy in range(GH):
        for cx in range(GW):
            if not solid(cx, cy) or solid(cx, cy - 1):
                continue
            x0, y0 = cx * T, cy * T
            built = GRID[cy][cx] in "SG"
            rg = random.Random(seed_of(cx, cy, "tuft", era))
            band = not (era == "wasteland" and built)
            if band:
                depth = (5 if era == "wasteland" else 8) * U
                lower = sc.wobble((x0 + T, y0 + depth), (x0, y0 + depth), seed_of(cx, cy, "g"), 1.3 * U)
                poly = [(x0, y0), (x0 + T, y0)] + lower
                m, ox, oy = sc.mask_of(polys=[poly], pad=2 * U)
                m = ImageChops.multiply(m, masks["S" if built else "D"].crop((ox, oy, ox + m.width, oy + m.height)))
                sc.paint(m, ox, oy, P["grass"], seed_of(cx, cy, "gp"))
                lw = [(x, y) for x, y in lower if x0 + (4 * U if not solid(cx - 1, cy) else 0) <= x <= x0 + T - (4 * U if not solid(cx + 1, cy) else 0)]
                if len(lw) > 1:
                    sc.ink(lw, seed_of(cx, cy, "gl"), 0.5, pinned=False)
            if era == "prehistoric":
                n, hmin, hmax = rg.randint(3, 5), 3, 7
            elif band:
                n, hmin, hmax = rg.randint(1, 2), 2, 4
            else:
                n, hmin, hmax = (1 if rg.random() < 0.35 else 0), 2, 4
            for k in range(n):
                tx = rg.uniform(x0 + 3 * U, x0 + T - 3 * U)
                hgt = rg.uniform(hmin, hmax) * U
                lean = rg.uniform(-2, 2) * U + (1.5 * U if era == "wasteland" else 0)
                sc.ink([(tx, y0 + U), (tx + lean, y0 - hgt)], seed_of(cx, cy, k, "tf"), 0.6, wobble=0.3)
                sc.ink([(tx + 1.5 * U, y0 + U), (tx + 3 * U + lean * 0.3, y0 - hgt * 0.6)], seed_of(cx, cy, k, "tf2"), 0.55, wobble=0.3)
            if era == "prehistoric" and rg.random() < 0.3:
                fx = rg.uniform(x0 + 6 * U, x0 + T - 6 * U)
                s = -1 if rg.random() < 0.5 else 1
                stem = [(fx, y0 + U)] + [(fx + s * (math.sin(t * 1.9) * 6) * U, y0 - (t * 7) * U) for t in (0.3, 0.6, 0.85, 1.0)]
                sc.ink(stem, seed_of(cx, cy, "fern"), 0.6, pinned=False, wobble=0.2)
                for j, (px, py) in enumerate(stem[1:-1]):
                    for side in (-1, 1):
                        sc.ink([(px, py), (px + side * 2.5 * U, py - 1.2 * U)], seed_of(cx, cy, j, side, "leaflet"), 0.5, wobble=0)

    # outlines
    for cy in range(GH):
        for cx in range(GW):
            if not solid(cx, cy):
                continue
            x0, y0, x1, y1 = cx * T, cy * T, cx * T + T, cy * T + T
            ex = {k: not solid(cx + dx, cy + dy) for k, (dx, dy) in dict(t=(0, -1), b=(0, 1), l=(-1, 0), r=(1, 0)).items()}
            oc = lambda a, b: ex[a] and ex[b]
            segs = []
            if ex["t"]: segs.append(((x0 + (r if oc("t", "l") else 0), y0), (x1 - (r if oc("t", "r") else 0), y0)))
            if ex["b"]: segs.append(((x0 + (r if oc("b", "l") else 0), y1), (x1 - (r if oc("b", "r") else 0), y1)))
            if ex["l"]: segs.append(((x0, y0 + (r if oc("t", "l") else 0)), (x0, y1 - (r if oc("b", "l") else 0))))
            if ex["r"]: segs.append(((x1, y0 + (r if oc("t", "r") else 0)), (x1, y1 - (r if oc("b", "r") else 0))))
            for i, (a, b) in enumerate(segs):
                sc.ink([a, b], seed_of(cx, cy, i, "edge"), 1.0)
            for (a, b_, ccx, ccy, ang) in [("t", "l", x0 + r, y0 + r, 180), ("t", "r", x1 - r, y0 + r, 270),
                                            ("b", "r", x1 - r, y1 - r, 0), ("b", "l", x0 + r, y1 - r, 90)]:
                if oc(a, b_):
                    arc = [(ccx + r * math.cos(math.radians(ang + k * 15)), ccy + r * math.sin(math.radians(ang + k * 15))) for k in range(7)]
                    sc.ink(arc, seed_of(cx, cy, ang), 1.0, wobble=0.0)
    sc.flush("main")
    for i, (a, b) in enumerate(late):
        sc.ink([a, b], seed_of("rebar", i), 1.5, pinned=False, wobble=0.2)
        sc.ink([a, b], seed_of("rebar", i), 0.8, pinned=False, wobble=0.2, color=P["rust"])
    sc.flush("main")


# ------------------------------------------------------------------ props
def props_prehistoric(sc, P):
    row = 6
    for c in [c for c in range(GW) if GRID[row][c] == "="]:
        x0, y0 = c * T, row * T
        left, right = GRID[row][c - 1] != "=", GRID[row][c + 1] != "="
        xa, xb = x0 + (3 * U if left else 0), x0 + T - (3 * U if right else 0)
        th = 9 * U
        sc.shape([(xa, y0), (xb, y0), (xb, y0 + th), (xa, y0 + th)], P["wood"], seed_of(c, "log"), 1.0)
        rg = random.Random(seed_of(c, "bark"))
        for k in range(2):
            yy = y0 + (3 + k * 3.5) * U
            a = rg.uniform(xa + 2 * U, xb - 14 * U)
            sc.ink([(a, yy), (a + rg.uniform(7, 12) * U, yy + rg.uniform(-0.6, 0.6) * U)], seed_of(c, k, "bk"), 0.45, color=P["bark"])
        for ex_ in ([xa] if left else []) + ([xb] if right else []):
            sc.blob((ex_ - 3 * U, y0 - 0.2 * U, ex_ + 3 * U, y0 + th + 0.2 * U), P["endgrain"], seed_of(c, ex_, "end"), 0.9, n=12)
            sc.blob((ex_ - 1.2 * U, y0 + th / 2 - 1.8 * U, ex_ + 1.2 * U, y0 + th / 2 + 1.8 * U), P["endgrain"], seed_of(c, ex_, "ring"), 0.45, n=8)
        if not left:
            for k in range(3):
                sc.ink([(x0 - 1.5 * U + k * 1.5 * U, y0 - 0.5 * U), (x0 + k * 1.5 * U, y0 + th + 0.5 * U)], seed_of(c, k, "lash"), 0.8, color=P["vine"])
    for cy in range(GH):
        if GRID[cy][8] != "H":
            continue
        x0, y0 = 8 * T, cy * T
        topc = GRID[cy - 1][8] != "H"
        for k in range(3):
            yy = y0 + (5 + k * 11) * U
            sc.shape([(x0 + 5 * U, yy - 1.3 * U), (x0 + 27 * U, yy - 0.9 * U), (x0 + 27 * U, yy + 1.5 * U), (x0 + 5 * U, yy + 1.2 * U)], P["wood"], seed_of(cy, k, "rung"), 0.7)
            for rx in (7.3, 25.5):
                sc.ink([(x0 + (rx - 1.8) * U, yy - 2 * U), (x0 + (rx + 1.8) * U, yy + 2 * U)], seed_of(cy, k, rx, "x1"), 0.6, color=P["vine"], wobble=0)
                sc.ink([(x0 + (rx + 1.8) * U, yy - 2 * U), (x0 + (rx - 1.8) * U, yy + 2 * U)], seed_of(cy, k, rx, "x2"), 0.6, color=P["vine"], wobble=0)
        for rx, bend in ((6, 1.2), (24, -1.0)):
            ya = y0 - (6 * U if topc else 0)
            mx = bend * U * math.sin(cy * 1.3)
            sc.shape([(x0 + rx * U, ya), (x0 + (rx + 3) * U, ya), (x0 + (rx + 3) * U + mx, y0 + T / 2), (x0 + (rx + 3) * U, y0 + T),
                      (x0 + rx * U, y0 + T), (x0 + rx * U + mx, y0 + T / 2)], P["wood"], seed_of(cy, rx, "rail"), 0.9)
    for c in range(GW):
        if GRID[10][c] != "^":
            continue
        x0, y0 = c * T, 10 * T
        rg = random.Random(seed_of(c, "flint"))
        for k in range(4):
            bx = x0 + (1 + k * 7.8) * U
            w = rg.uniform(6, 8) * U
            tip = (bx + w / 2 + rg.uniform(-2, 2) * U, y0 + rg.uniform(5, 12) * U)
            col = P["bone"] if k == 2 else P["flint"]
            sc.shape([(bx, y0 + T), (bx + w * 0.2, y0 + 20 * U), tip, (bx + w * 0.85, y0 + 18 * U), (bx + w, y0 + T)], col, seed_of(c, k, "sh"), 0.9)
            sc.ink([tip, (bx + w * 0.55, y0 + T - 2 * U)], seed_of(c, k, "facet"), 0.45)
    for cy in range(GH):
        for cx in range(GW):
            if GRID[cy][cx] != "G":
                continue
            x0, y0 = cx * T, cy * T
            rg = random.Random(seed_of(cx, cy, "hv"))
            for vx, ph in ((2.5, 0), (7, 1.7)):
                pts = [(x0 + (vx + 1.5 * math.sin(ph + t * 3)) * U, y0 + t * T) for t in (0, 0.25, 0.5, 0.75, 1.0)]
                sc.ink(pts, seed_of(cx, cy, vx, "vine"), 1.3, color=P["vine"], pinned=False)
                sc.ink(pts, seed_of(cx, cy, vx, "vine2"), 0.4, pinned=False)
            for k in range(4):
                ly = y0 + (4 + k * 8) * U
                lx = x0 + (2.5 if k % 2 else 7) * U
                tipx = lx + (-1 if k % 2 else 1) * rg.uniform(5, 7) * U
                sc.shape([(lx, ly), ((lx + tipx) / 2, ly - 2.4 * U), (tipx, ly + 0.5 * U), ((lx + tipx) / 2, ly + 2.4 * U)], P["ivy"], seed_of(cx, cy, k, "lf"), 0.6)
    sc.flush("main")


def props_wasteland(sc, P):
    row = 6
    for c in [c for c in range(GW) if GRID[row][c] == "="]:
        x0, y0 = c * T, row * T
        left, right = GRID[row][c - 1] != "=", GRID[row][c + 1] != "="
        xa, xb = x0 + (1 * U if left else 0), x0 + T - (1 * U if right else 0)
        th = 9 * U
        if right:
            poly = [(xa, y0), (xb, y0), (xb - 2 * U, y0 + 3 * U), (xb + 1 * U, y0 + 5 * U), (xb - 1 * U, y0 + th), (xa, y0 + th)]
        else:
            poly = [(xa, y0), (xb, y0), (xb, y0 + th), (xa, y0 + th)]
        sc.shape(poly, P["paint"], seed_of(c, "gd"), 1.0)
        rg = random.Random(seed_of(c, "gdr"))
        if rg.random() < 0.8:
            rx = rg.uniform(xa + 2 * U, xb - 12 * U)
            sc.blob((rx, y0 + 1 * U, rx + rg.uniform(7, 12) * U, y0 + th - 1 * U), P["rust"], seed_of(c, "rp"), 0, alpha=0.55)
        for yy in (2.2, 6.8):
            sc.ink([(xa + 1 * U, y0 + yy * U), (xb - 1 * U, y0 + yy * U)], seed_of(c, yy, "fl"), 0.45)
        hx = x0 + 16 * U
        sc.blob((hx - 2.3 * U, y0 + 2.4 * U, hx + 2.3 * U, y0 + 6.6 * U), P["steel_dark"], seed_of(c, "hole"), 0.5, n=12)
        for rx in (xa + 3 * U, xb - 3 * U):
            for ry in (1.2, 7.8):
                sc.blob((rx - 0.7 * U, y0 + ry * U - 0.7 * U, rx + 0.7 * U, y0 + ry * U + 0.7 * U), P["steel_dark"], seed_of(c, rx, ry), 0, n=6)
    for cy in range(GH):
        if GRID[cy][8] != "H":
            continue
        x0, y0 = 8 * T, cy * T
        topc = GRID[cy - 1][8] != "H"
        for k in range(4):
            yy = y0 + (4 + k * 8) * U
            sc.shape([(x0 + 8 * U, yy - 1 * U), (x0 + 24 * U, yy - 1 * U), (x0 + 24 * U, yy + 1 * U), (x0 + 8 * U, yy + 1 * U)], P["paint"], seed_of(cy, k, "rung"), 0.7)
        for rx in (6, 24):
            ya = y0 - (6 * U if topc else 0)
            sc.shape([(x0 + rx * U, ya), (x0 + (rx + 2.5) * U, ya), (x0 + (rx + 2.5) * U, y0 + T), (x0 + rx * U, y0 + T)], P["paint"], seed_of(cy, rx, "rail"), 0.9)
            rg = random.Random(seed_of(cy, rx, "rs"))
            ry = y0 + rg.uniform(4, 24) * U
            sc.blob((x0 + rx * U, ry, x0 + (rx + 2.5) * U, ry + rg.uniform(3, 6) * U), P["rust"], seed_of(cy, rx, "rsb"), 0, alpha=0.7)
        if topc:
            for rx in (6, 24):
                sc.ink([(x0 + (rx + 1.25) * U, y0 - 6 * U), (x0 + (rx + 1.25 + (-3 if rx == 6 else 3)) * U, y0 - 9 * U)], seed_of(cy, rx, "hook"), 1.0)
    for c in range(GW):
        if GRID[10][c] != "^":
            continue
        x0, y0 = c * T, 10 * T
        rg = random.Random(seed_of(c, "rb"))
        for k in range(3):
            bx = x0 + (5 + k * 11) * U
            tip = (bx + rg.uniform(-4, 4) * U, y0 + rg.uniform(4, 10) * U)
            sc.ink([(bx, y0 + T), tip], seed_of(c, k, "rbo"), 2.0, pinned=False, wobble=0.2)
            sc.ink([(bx, y0 + T), tip], seed_of(c, k, "rbi"), 1.0, pinned=False, wobble=0.2, color=P["rust"])
        pts = []
        for i in range(49):
            t = i / 48
            th_ = t * 3 * 6.283
            pts.append((x0 + t * T + math.sin(th_) * 3.5 * U, y0 + 20 * U - math.cos(th_) * 7 * U))
        sc.ink(pts, seed_of(c, "coil"), 0.7, pinned=False, wobble=0, color=P["wire"])
        for k in range(6):
            bx, by = x0 + (3 + k * 5.2) * U, y0 + (13 + (k % 2) * 14) * U
            sc.ink([(bx - 1.3 * U, by - 1.3 * U), (bx + 1.3 * U, by + 1.3 * U)], seed_of(c, k, "b1"), 0.55, wobble=0, color=P["wire"])
            sc.ink([(bx + 1.3 * U, by - 1.3 * U), (bx - 1.3 * U, by + 1.3 * U)], seed_of(c, k, "b2"), 0.55, wobble=0, color=P["wire"])
    sc.flush("main")
    mesh = Image.new("L", (W, H), 0)
    md = ImageDraw.Draw(mesh)
    for cy in range(GH):
        for cx in range(GW):
            if GRID[cy][cx] != "G":
                continue
            x0, y0 = cx * T, cy * T
            md.rectangle((x0 - 1 * U, y0, x0 + 13 * U, y0 + T), fill=255)
            s = 4 * U
            for k in range(-4, 12):
                sc.ink([(x0 - 2 * U + k * s, y0 - 2 * U), (x0 - 2 * U + k * s + T + 4 * U, y0 + T + 2 * U)], seed_of(cx, cy, k, "m1"), 0.4, layer="mesh", color=P["steel_dark"], wobble=0.3)
                sc.ink([(x0 - 2 * U + k * s, y0 + T + 2 * U), (x0 - 2 * U + k * s + T + 4 * U, y0 - 2 * U)], seed_of(cx, cy, k, "m2"), 0.4, layer="mesh", color=P["steel_dark"], wobble=0.3)
    lay = sc.layer("mesh")
    lay.putalpha(ImageChops.multiply(lay.getchannel("A"), mesh))
    sc.flush("mesh", 0.9)
    for cy in range(GH):
        for cx in range(GW):
            if GRID[cy][cx] == "G":
                x0, y0 = cx * T, cy * T
                sc.shape([(x0 + 12 * U, y0), (x0 + 14 * U, y0), (x0 + 14 * U, y0 + T), (x0 + 12 * U, y0 + T)], P["steel"], seed_of(cx, cy, "post"), 0.7)
    sc.flush("main")


def render_era(era):
    if era == "medieval":
        return C.render("1-ink-flat", {})
    P = ERAS[era]
    sc = Scene(dict(ST))
    (bg_prehistoric if era == "prehistoric" else bg_wasteland)(sc, P)
    sc.flush("far", 0.28)
    sc.flush("mid", 0.4)
    terrain(sc, P, era)
    (props_prehistoric if era == "prehistoric" else props_wasteland)(sc, P)
    draw_figure(sc, 2.4 * T, 9 * T)
    return sc.canvas.convert("RGB")


def sheet(title, img):
    font = ImageFont.truetype("arial.ttf", 22)
    small = ImageFont.truetype("arial.ttf", 16)
    full = img.resize((int(W * 1.35 / U), int(H * 1.35 / U)), Image.LANCZOS)
    crops = [(1 * T, 5 * T, 11 * T, 12 * T), (9 * T, 3 * T, 19 * T, 10 * T)]
    zooms = [img.crop(c).resize((int((c[2] - c[0]) * 2.7 / U / 1.6), int((c[3] - c[1]) * 2.7 / U / 1.6)), Image.LANCZOS) for c in crops]
    pad = 20
    sw = pad * 2 + full.width
    sh = pad * 4 + 34 + full.height + zooms[0].height + 24
    out = Image.new("RGB", (max(sw, pad * 3 + zooms[0].width * 2), sh), (32, 34, 38))
    d = ImageDraw.Draw(out)
    d.text((pad, pad), title, font=font, fill=(235, 235, 235))
    y = pad * 2 + 30
    out.paste(full, (pad, y))
    d.text((pad, y + full.height + 4), "in-game size at 1080p (zoom 1.35)", font=small, fill=(170, 170, 170))
    y2 = y + full.height + pad + 14
    for i, z in enumerate(zooms):
        out.paste(z, (pad + i * (z.width + pad), y2))
    d.text((pad, y2 + zooms[0].height + 4), "close-ups (~1.7x)", font=small, fill=(170, 170, 170))
    return out, full


if __name__ == "__main__":
    os.makedirs(C.OUT, exist_ok=True)
    eras = sys.argv[1:] or ["prehistoric", "medieval", "wasteland"]
    fulls = []
    for e in eras:
        img = render_era(e)
        s, full = sheet(TITLES[e], img)
        s.save(os.path.join(C.OUT, f"era-{e}.png"))
        fulls.append((TITLES[e], full))
        print("wrote", e)
    if len(fulls) == 3:
        font = ImageFont.truetype("arial.ttf", 20)
        fw, fh = fulls[0][1].size
        ov = Image.new("RGB", (fw + 40, (fh + 50) * 3 + 10), (32, 34, 38))
        d = ImageDraw.Draw(ov)
        for i, (t, f) in enumerate(fulls):
            y = 20 + i * (fh + 50)
            d.text((20, y), t, font=font, fill=(235, 235, 235))
            ov.paste(f, (20, y + 28))
        ov.save(os.path.join(C.OUT, "eras-overview.png"))
