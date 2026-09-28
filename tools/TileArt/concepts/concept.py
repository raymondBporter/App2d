"""Throwaway tileset style concepts. Renders one medieval scene per candidate style.

Everything is built per 32px tile cell with seams pinned at cell corners, so what you see
is honest about what a tileable set can do. Internal scale U=4 (128px per tile) then downsampled.
"""
import math, random, sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageFont

U = 4
TS = 32
T = TS * U
GRID = [
    "......................",
    "......................",
    "......................",
    "......................",
    "...........SS.........",
    "......................",
    "....====H......SSSSSSS",
    "........H......GSSSSSS",
    "........H......GSSSSSS",
    "DDDDDDDDDD...DDSSSSSSS",
    "DDDDDDDDDD^^^DDSSSSSSS",
    "DDDDDDDDDDDDDDDSSSSSSS",
]
GW, GH = len(GRID[0]), len(GRID)
W, H = GW * T, GH * T
SOLID = set("DSG")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")

PAL = dict(
    sky_top="#b7cfd8", sky_bot="#ece9d6", cloud="#f6f5ee", far="#b9c6bd", castle="#a3afab",
    mid="#a6ba8f", tree="#8fa779", trunk="#8a7a62", dirt="#8b6b4c", peb="#a38566",
    grass="#7fa150", stone="#aaa396", wood="#b07c43", wood_dark="#7d5530", metal="#8f959a",
    ivy="#5f8a3e", body="#d8e9db", roof="#9a8f86", stake_tip="#5a4c40",
)
BASE_PAL = dict(PAL)

STYLES = {
    "1-ink-flat":   dict(title="1  Ink + flat fill",  line=1.7,  amp=0.45, fill="flat",   press=0.10, gaps=0.0,  double=False, ink="#222b32"),
    "2-ink-wash":   dict(title="2  Ink + wash",       line=1.25, amp=0.65, fill="wash",   press=0.25, gaps=0.07, double=False, ink="#2a2a30"),
    "3-marker":     dict(title="3  Loose marker",     line=2.6,  amp=0.9,  fill="marker", press=0.30, gaps=0.0,  double=False, ink="#1f2328"),
    "4-pencil":     dict(title="4  Pencil + hatch",   line=0.95, amp=0.8,  fill="pencil", press=0.35, gaps=0.03, double=True,  ink="#3b4048"),
}
COLOUR = dict(  # "a little more colourful"
    sky_top="#9cc6de", sky_bot="#eef0dc", cloud="#fbfbf6", far="#a9c3c6", castle="#9eb0bd",
    mid="#9cc47c", tree="#7cb35a", trunk="#8c6f4e", dirt="#95653c", peb="#b88a5a",
    grass="#78b33c", stone="#b3aa98", wood="#c98a45", wood_dark="#86552a", metal="#8f959a",
    ivy="#4f9a38", roof="#b8664f", stake_tip="#7a2f24",
)
VIVID = dict(  # pushed further
    sky_top="#7fbbe3", sky_bot="#f3f0d4", cloud="#ffffff", far="#98bfd0", castle="#93a9c4",
    mid="#8fcf6a", tree="#63b548", trunk="#8e6a45", dirt="#a0612f", peb="#c9925a",
    grass="#6cc232", stone="#bcae94", wood="#dc9140", wood_dark="#8e5424", metal="#8f959a",
    ivy="#3fa832", roof="#c85a3e", stake_tip="#9a2a1c",
)
R3 = {
    "1b-ref":      ("1-ink-flat", COLOUR, "1b  reference", {}),
    "1b-A-form":   ("1-ink-flat", COLOUR, "A  Soft form: light lip, depth shading, brick tones", dict(form=1.0, brick_var=0.05)),
    "1b-B-tinted": ("1-ink-flat", COLOUR, "B  Coloured lines: world inked in its own hues, no bg lines", dict(tint=0.42, bg_ink=0, line=1.5)),
    "1b-C-bold":   ("1-ink-flat", COLOUR, "C  Bold + simple: heavy silhouette, light interior",
                    dict(edge=1.45, detail=0.6, pebbles=(0, 1, 1), bg_ink=0, grass_depth=9, grass_amp=2.0, tufts=(1, 2), amp=0.35)),
    "1b-D-loose":  ("1-ink-flat", COLOUR, "D  Looser hand: more wobble, fill misses line, quick hatch",
                    dict(amp=0.95, press=0.32, misreg=1.2, hatch=True, tufts=(3, 6), gaps=0.04)),
}
VARIANTS = {
    "1b-ink-flat-colour": ("1-ink-flat", COLOUR, "1b  Ink + flat, a little more colour", {}),
    "1c-ink-flat-vivid":  ("1-ink-flat", VIVID,  "1c  Ink + flat, pushed further", {}),
    "2b-ink-wash-colour": ("2-ink-wash", COLOUR, "2b  Ink + wash, a little more colour", {}),
}
VARIANTS.update(R3)
PAPER = np.array((246, 242, 232), np.float32)


def rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def darken(h, k):
    r, g, b = rgb(h)
    m = (r + g + b) / 3
    r, g, b = [max(0, min(255, int(((c - m) * 1.25 + m) * k))) for c in (r, g, b)]
    return "#%02x%02x%02x" % (r, g, b)


def seed_of(*k):
    return hash(k) & 0x7FFFFFFF


def noise(w, h, scale, seed):
    rng = np.random.default_rng(seed)
    sw, sh = max(2, int(w / scale) + 2), max(2, int(h / scale) + 2)
    small = Image.fromarray((rng.random((sh, sw)) * 255).astype(np.uint8))
    return np.asarray(small.resize((w, h), Image.BICUBIC)).astype(np.float32) / 255


class Scene:
    def __init__(self, st):
        self.st = st
        self.canvas = Image.new("RGBA", (W, H), (0, 0, 0, 255))
        self.layers = {}

    # ---------- ink ----------
    def layer(self, name):
        if name not in self.layers:
            self.layers[name] = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        return self.layers[name]

    def flush(self, name, opacity=1.0):
        if name not in self.layers:
            return
        lay = self.layers.pop(name)
        if opacity < 1:
            a = lay.getchannel("A").point(lambda v: int(v * opacity))
            lay.putalpha(a)
        self.canvas.alpha_composite(lay)

    def wobble(self, p0, p1, seed, amp, pinned=True):
        (x0, y0), (x1, y1) = p0, p1
        L = math.hypot(x1 - x0, y1 - y0)
        if L < 1e-6:
            return [p0, p1]
        n = max(2, int(L / (5 * U)))
        rng = random.Random(seed)
        waves = [(f * rng.uniform(0.7, 1.3), rng.uniform(0, 6.283), a) for f, a in ((0.03, 1.0), (0.07, 0.5), (0.16, 0.25))]
        nx, ny = -(y1 - y0) / L, (x1 - x0) / L

        def o(t):
            d = t * L / U
            return sum(a * math.sin(f * d * 6.283 + ph) for f, ph, a in waves) / 1.2

        o0, o1 = o(0), o(1)
        pts = []
        for i in range(n + 1):
            t = i / n
            off = o(t) - ((1 - t) * o0 + t * o1) if pinned else o(t)
            off *= amp
            pts.append((x0 + (x1 - x0) * t + nx * off, y0 + (y1 - y0) * t + ny * off))
        return pts

    def ink(self, pts, seed, weight=1.0, closed=False, layer="main", color=None, pinned=True, wobble=1.0, tint=None, kind=None):
        st = self.st
        d = ImageDraw.Draw(self.layer(layer))
        if color is None and tint and st.get("tint"):
            color = darken(tint, st["tint"])
        if kind:
            weight *= st.get(kind, 1.0)
        col = rgb(color or st["ink"]) + (255,)
        if closed:
            pts = list(pts) + [pts[0]]
        for p in range(2 if st["double"] else 1):
            path = []
            for i in range(len(pts) - 1):
                seg = self.wobble(pts[i], pts[i + 1], seed * 31 + i + p * 977,
                                  st["amp"] * U * wobble * (1.5 if p else 1.0), pinned)
                path += seg if not path else seg[1:]
            rng = random.Random(seed + p)
            w0 = st["line"] * U * weight * (0.8 if p else 1.0)
            ph = rng.uniform(0, 6.28)
            for i in range(len(path) - 1):
                if st["gaps"] and rng.random() < st["gaps"]:
                    continue
                w = max(1.0, w0 * (1 + st["press"] * math.sin(i * 0.7 + ph)))
                d.line([path[i], path[i + 1]], fill=col, width=int(round(w)))
                r = w / 2
                x, y = path[i + 1]
                d.ellipse((x - r, y - r, x + r, y + r), fill=col)
            x, y = path[0]
            r = w0 / 2
            d.ellipse((x - r, y - r, x + r, y + r), fill=col)

    # ---------- fills ----------
    def mask_of(self, polys=(), ellipses=(), rects=(), pad=8 * U):
        xs, ys = [], []
        for poly in polys:
            xs += [p[0] for p in poly]; ys += [p[1] for p in poly]
        for e in list(ellipses) + list(rects):
            xs += [e[0], e[2]]; ys += [e[1], e[3]]
        x0, y0 = max(0, int(min(xs)) - pad), max(0, int(min(ys)) - pad)
        x1, y1 = min(W, int(max(xs)) + pad), min(H, int(max(ys)) + pad)
        m = Image.new("L", (x1 - x0, y1 - y0), 0)
        d = ImageDraw.Draw(m)
        for poly in polys:
            d.polygon([(x - x0, y - y0) for x, y in poly], fill=255)
        for e in ellipses:
            d.ellipse((e[0] - x0, e[1] - y0, e[2] - x0, e[3] - y0), fill=255)
        for r in rects:
            d.rectangle((r[0] - x0, r[1] - y0, r[2] - x0, r[3] - y0), fill=255)
        return m, x0, y0

    def paint(self, mask, ox, oy, color, seed, shade=None, alpha=1.0):
        mode = self.st["fill"]
        w, h = mask.size
        m = np.asarray(mask).astype(np.float32) / 255
        c = np.array(rgb(color), np.float32)
        col = np.ones((h, w, 3), np.float32) * c
        f = np.ones((h, w), np.float32)
        if mode == "wash":
            f *= 0.9 + 0.16 * noise(w, h, 16 * U, seed) + 0.06 * (noise(w, h, 2.5 * U, seed + 7) - 0.5)
            bl = np.asarray(mask.filter(ImageFilter.GaussianBlur(3 * U))).astype(np.float32) / 255
            f *= 1 - 0.3 * np.clip((m - bl) * 2.4, 0, 1)
            a = m * 0.94
        elif mode == "marker":
            mm = mask.filter(ImageFilter.MinFilter(2 * U + 1))
            sh = Image.new("L", mm.size, 0)
            sh.paste(mm, (int(1.5 * U), int(1.0 * U)))
            mm = sh
            m = np.asarray(mm).astype(np.float32) / 255
            yy, xx = np.mgrid[0:h, 0:w]
            s = 0.5 + 0.5 * np.sin((xx + ox + (yy + oy) * 0.4) / (2.0 * U) + noise(w, h, 12 * U, seed) * 2)
            f *= 0.93 + 0.08 * s
            a = m * 0.97
        elif mode == "pencil":
            col = col * 0.55 + PAPER * 0.45
            f *= 0.96 + 0.06 * np.random.default_rng(seed).random((h, w)).astype(np.float32)
            a = m
        else:
            if self.st.get("misreg"):
                sh = Image.new("L", mask.size, 0)
                sh.paste(mask, (int(self.st["misreg"] * U), int(self.st["misreg"] * 0.6 * U)))
                m = np.asarray(sh).astype(np.float32) / 255
            a = m
        if shade is not None:
            if mode == "pencil":
                yy, xx = np.mgrid[0:h, 0:w]
                jitter = noise(w, h, 6 * U, seed + 3) * 2.0
                hv = (0.5 + 0.5 * np.cos(6.283 * ((xx + ox) + (yy + oy)) / (3.2 * U) + jitter)) ** 5
                f *= 1 - 0.38 * hv * shade
            else:
                f *= 1 - {"flat": 0.1, "wash": 0.16, "marker": 0.12}[mode] * shade
        col = np.clip(col * f[..., None], 0, 255)
        out = np.dstack([col, np.clip(a * alpha * 255, 0, 255)]).astype(np.uint8)
        self.canvas.alpha_composite(Image.fromarray(out, "RGBA"), (ox, oy))

    BG = {"far": 0.28, "mid": 0.4}

    def shape(self, poly, color, seed, weight=1.0, layer="main", closed=True, shade=None, alpha=1.0, kind=None):
        m, ox, oy = self.mask_of(polys=[poly])
        self.paint(m, ox, oy, color, seed, shade=shade, alpha=alpha)
        if layer in self.BG and not self.st.get("bg_ink", 1):
            return
        if weight:
            self.ink(poly, seed, weight, closed=closed, layer=layer, tint=color, kind=kind)
            if layer in self.BG:
                self.flush(layer, self.BG[layer] * self.st.get("bg_ink", 1))

    def blob(self, box, color, seed, weight=1.0, layer="main", n=14, alpha=1.0):
        x0, y0, x1, y1 = box
        cx, cy, rx, ry = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
        rng = random.Random(seed)
        poly = [(cx + rx * math.cos(a) * rng.uniform(0.9, 1.05), cy + ry * math.sin(a) * rng.uniform(0.9, 1.05))
                for a in [i / n * 6.283 for i in range(n)]]
        self.shape(poly, color, seed, weight, layer, alpha=alpha)


def cell(cx, cy):
    if cx < 0 or cx >= GW or cy >= GH:
        return "D"
    if cy < 0:
        return "."
    return GRID[cy][cx]


def solid(cx, cy):
    return cell(cx, cy) in SOLID


def render(key, over=None):
    st = dict(STYLES[key], **(over or {}))
    sc = Scene(st)
    rnd = random.Random(1)

    # ---------- sky ----------
    top, bot = np.array(rgb(PAL["sky_top"]), np.float32), np.array(rgb(PAL["sky_bot"]), np.float32)
    t = np.linspace(0, 1, H)[:, None, None]
    sky = top * (1 - t) + bot * t
    if st["fill"] == "pencil":
        sky = sky * 0.5 + PAPER * 0.5
    sky = np.broadcast_to(sky, (H, W, 3))
    sc.canvas = Image.fromarray(np.dstack([sky, np.full((H, W), 255.0)]).astype(np.uint8), "RGBA")

    # clouds
    for i, (x, y, s) in enumerate([(3.5, 1.4, 1.0), (12.5, 0.9, 0.8), (18.8, 2.1, 1.1)]):
        for j in range(3):
            bx = (x + j * 0.9 * s) * T
            by = (y + (0.25 if j == 1 else 0.45)) * T - (0.35 * T if j == 1 else 0)
            sc.blob((bx, by, bx + 1.5 * s * T, by + 0.8 * s * T), PAL["cloud"], seed_of("c", i, j), 0.7, "far", alpha=0.85)
    sc.flush("far", 0.2 * st.get("bg_ink", 1))

    # far hills + castle
    far = [(0, H)] + [(x * U, (5.6 * T + math.sin(x / 260) * 0.5 * T + math.sin(x / 90) * 0.12 * T))
                      for x in range(0, W // U + 1, 16)] + [(W, H)]
    far = [(x, y) if i in (0, len(far) - 1) else (x, y) for i, (x, y) in enumerate(far)]
    sc.shape(far, PAL["far"], 11, 0.7, "far", closed=False)
    cx0, cy0 = 16.6 * T, 3.0 * T
    c = PAL["castle"]
    sc.shape([(cx0, cy0 + 3.4 * T), (cx0, cy0 + 1.0 * T), (cx0 + 3.2 * T, cy0 + 1.0 * T), (cx0 + 3.2 * T, cy0 + 3.4 * T)], c, 21, 0.7, "far")
    for k, (tx, th) in enumerate([(-0.2, 2.3), (1.3, 3.0), (2.8, 2.1)]):
        x, w = cx0 + tx * T, 0.7 * T
        yb, yt = cy0 + 3.4 * T, cy0 + (3.2 - th) * T
        sc.shape([(x, yb), (x, yt), (x + w, yt), (x + w, yb)], c, 30 + k, 0.7, "far")
        sc.shape([(x - 0.08 * T, yt), (x + w / 2, yt - 0.7 * T), (x + w + 0.08 * T, yt)], PAL["roof"], 40 + k, 0.7, "far")
    for k in range(6):
        x = cx0 + 0.45 * T + k * 0.42 * T
        sc.shape([(x, cy0 + T), (x, cy0 + 0.82 * T), (x + 0.22 * T, cy0 + 0.82 * T), (x + 0.22 * T, cy0 + T)], c, 60 + k, 0.5, "far")
    sc.flush("far", 0.28)

    # mid hills + trees
    mid = [(0, H)] + [(x * U, 7.4 * T + math.sin(x / 180 + 1) * 0.6 * T + math.sin(x / 70) * 0.15 * T)
                      for x in range(0, W // U + 1, 16)] + [(W, H)]
    sc.shape(mid, PAL["mid"], 12, 0.8, "mid", closed=False)
    for k, tx in enumerate([1.0, 5.6, 6.3, 10.2, 13.1]):
        x = tx * T
        ytop = 7.4 * T + math.sin(x / U / 180 + 1) * 0.6 * T - 0.2 * T
        sc.shape([(x - 0.07 * T, ytop + 0.6 * T), (x - 0.05 * T, ytop - 0.5 * T), (x + 0.05 * T, ytop - 0.5 * T), (x + 0.07 * T, ytop + 0.6 * T)],
                 PAL["trunk"], 80 + k, 0.7, "mid")
        s = 0.8 + (k % 3) * 0.2
        sc.blob((x - 0.55 * s * T, ytop - 1.5 * s * T, x + 0.55 * s * T, ytop - 0.3 * T), PAL["tree"], 90 + k, 0.8, "mid")
    sc.flush("mid", 0.4)

    # ---------- terrain ----------
    r = 5 * U
    masks = {m: Image.new("L", (W, H), 0) for m in "DS"}
    topband = Image.new("L", (W, H), 0)
    tb = ImageDraw.Draw(topband)
    for cy in range(GH):
        for cx in range(GW):
            ch = GRID[cy][cx]
            if ch not in SOLID:
                continue
            m = masks["S" if ch in "SG" else "D"]
            d = ImageDraw.Draw(m)
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
    tbn = np.asarray(topband).astype(np.float32) / 255
    shade_full = 1 - np.asarray(topband.filter(ImageFilter.GaussianBlur(1 * U if st["fill"] == "flat" else 5 * U))).astype(np.float32) / 255
    if st.get("form"):
        # deep gradient: darker the further from the walkable surface; plus a lighter lip under the grass
        deep = topband.resize((W // 8, H // 8))
        deep = Image.fromarray((np.asarray(deep) > 0).astype(np.uint8) * 255)
        for _ in range(3):
            deep = deep.filter(ImageFilter.MaxFilter(3))
        deep = deep.filter(ImageFilter.GaussianBlur(4)).resize((W, H), Image.BICUBIC)
        g = 1 - np.asarray(deep).astype(np.float32) / 255
        shade_full = shade_full * 0.4 + g * 1.4 * st["form"] - tbn * 0.6 * st["form"]
    sc.paint(masks["D"], 0, 0, PAL["dirt"], 101, shade=shade_full)
    sc.paint(masks["S"], 0, 0, PAL["stone"], 102, shade=shade_full)

    # details
    for cy in range(GH):
        for cx in range(GW):
            ch = GRID[cy][cx]
            if ch not in SOLID:
                continue
            x0, y0 = cx * T, cy * T
            rg = random.Random(seed_of(cx, cy))
            topx = not solid(cx, cy - 1)
            ymin = y0 + (12 * U if topx else 3 * U)
            if ch == "D":
                for k in range(rg.choice(st.get("pebbles", (1, 2, 2, 3)))):
                    pw, ph = rg.uniform(4, 8) * U, rg.uniform(3, 5) * U
                    px = rg.uniform(x0 + 3 * U, x0 + T - 3 * U - pw)
                    py = rg.uniform(ymin, y0 + T - 3 * U - ph)
                    sc.blob((px, py, px + pw, py + ph), PAL["peb"], seed_of(cx, cy, k), 0.55 * st.get("detail", 1), "detail", n=9)
                if rg.random() < 0.6:
                    px, py = rg.uniform(x0 + 4 * U, x0 + T - 12 * U), rg.uniform(ymin + 2 * U, y0 + T - 4 * U)
                    sc.ink([(px, py), (px + rg.uniform(4, 8) * U, py + rg.uniform(-1, 1) * U)], seed_of(cx, cy, 9), 0.5, layer="detail", tint=PAL["dirt"], kind="detail")
                if cell(cx + 1, cy) in "SG":
                    sc.ink([(x0 + T, y0), (x0 + T, y0 + T)], seed_of(cx, cy, "seam"), 0.6, layer="detail", tint=PAL["dirt"], kind="detail")
            else:
                for k in range(2):
                    row = cy * 2 + k
                    yy = y0 + k * 16 * U
                    if k == 1 or cell(cx, cy - 1) in "SG":
                        sc.ink([(x0, yy), (x0 + T, yy)], seed_of(cx, cy, k, "h"), 0.55, layer="detail", tint=PAL["stone"], kind="detail")
                    off = 16 * U if row % 2 else 0
                    jx = x0 + off
                    if off or cell(cx - 1, cy) in "SG":
                        ya = yy + (3 * U if (k == 0 and topx) else 0)
                        if not (k == 0 and topx):
                            sc.ink([(jx, ya), (jx, yy + 16 * U)], seed_of(cx, cy, k, "v"), 0.55, layer="detail", tint=PAL["stone"], kind="detail")
                if rg.random() < 0.35:
                    px, py = rg.uniform(x0 + 5 * U, x0 + T - 9 * U), rg.uniform(ymin + 2 * U, y0 + T - 6 * U)
                    sc.ink([(px, py), (px + 3 * U, py + 2 * U), (px + 4 * U, py + 5 * U)], seed_of(cx, cy, "crack"), 0.45, layer="detail", tint=PAL["stone"], kind="detail")
                if st.get("brick_var"):
                    for k in range(2):
                        yy = y0 + k * 16 * U
                        off = 16 * U if (cy * 2 + k) % 2 else 0
                        spans = [(x0, x0 + off), (x0 + off, x0 + T)] if off else [(x0, x0 + T)]
                        for j, (xa, xb) in enumerate(spans):
                            v = random.Random(seed_of(cx, cy, k, j, "bv")).uniform(-1, 1) * st["brick_var"]
                            bm, bx0, by0 = sc.mask_of(rects=[(xa, yy, xb, yy + 16 * U)], pad=0)
                            bm = ImageChops.multiply(bm, masks["S"].crop((bx0, by0, bx0 + bm.width, by0 + bm.height)))
                            sc.paint(bm, bx0, by0, "#ffffff" if v > 0 else "#000000", 0, alpha=abs(v))
            if st.get("hatch") and rg.random() < 0.3 and not topx:
                hx, hy = rg.uniform(x0 + 4 * U, x0 + T - 14 * U), rg.uniform(y0 + 6 * U, y0 + T - 12 * U)
                for k in range(4):
                    sc.ink([(hx + k * 2.5 * U, hy + 8 * U), (hx + k * 2.5 * U + 5 * U, hy)], seed_of(cx, cy, k, "hatch"), 0.45,
                           layer="detail", tint=PAL["dirt"] if ch == "D" else PAL["stone"], wobble=0.3)
    sc.flush("detail", 0.55)

    # grass bands + outlines
    for cy in range(GH):
        for cx in range(GW):
            if not solid(cx, cy) or solid(cx, cy - 1):
                continue
            x0, y0 = cx * T, cy * T
            depth = st.get("grass_depth", 7) * U
            lower = sc.wobble((x0 + T, y0 + depth), (x0, y0 + depth), seed_of(cx, cy, "g"), st.get("grass_amp", 1.3) * U)
            poly = [(x0, y0), (x0 + T, y0)] + lower
            m, ox, oy = sc.mask_of(polys=[poly], pad=2 * U)
            tm = masks["S" if GRID[cy][cx] in "SG" else "D"].crop((ox, oy, ox + m.width, oy + m.height))
            m = ImageChops.multiply(m, tm)
            sc.paint(m, ox, oy, PAL["grass"], seed_of(cx, cy, "gp"))
            lw = [p for p in lower]
            lw = [(x, y) for x, y in lw if x0 + (4 * U if not solid(cx - 1, cy) else 0) <= x <= x0 + T - (4 * U if not solid(cx + 1, cy) else 0)]
            if len(lw) > 1:
                sc.ink(lw, seed_of(cx, cy, "gl"), 0.5, layer="main", pinned=False, tint=PAL["grass"], kind="detail")
            rg = random.Random(seed_of(cx, cy, "tuft"))
            for k in range(rg.randint(*st.get("tufts", (2, 4)))):
                tx = rg.uniform(x0 + 3 * U, x0 + T - 3 * U)
                hgt = rg.uniform(2.5, 5) * U
                lean = rg.uniform(-2, 2) * U
                sc.ink([(tx, y0 + U), (tx + lean, y0 - hgt)], seed_of(cx, cy, k, "tf"), 0.6, wobble=0.3, tint=PAL["grass"])
                sc.ink([(tx + 1.5 * U, y0 + U), (tx + 1.5 * U + lean * 0.3 + 1.5 * U, y0 - hgt * 0.6)], seed_of(cx, cy, k, "tf2"), 0.55, wobble=0.3, tint=PAL["grass"])

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
            matc = PAL["stone"] if GRID[cy][cx] in "SG" else PAL["dirt"]
            for i, (a, b) in enumerate(segs):
                sc.ink([a, b], seed_of(cx, cy, i, "edge"), 1.0, kind="edge", tint=PAL["grass"] if a[1] == b[1] == y0 else matc)
            for (a, b_, ccx, ccy, ang) in [("t", "l", x0 + r, y0 + r, 180), ("t", "r", x1 - r, y0 + r, 270),
                                            ("b", "r", x1 - r, y1 - r, 0), ("b", "l", x0 + r, y1 - r, 90)]:
                if oc(a, b_):
                    arc = [(ccx + r * math.cos(math.radians(ang + k * 15)), ccy + r * math.sin(math.radians(ang + k * 15))) for k in range(7)]
                    sc.ink(arc, seed_of(cx, cy, ang), 1.0, wobble=0.0, kind="edge", tint=PAL["grass"] if a == "t" else matc)
    sc.flush("main")

    # ---------- props ----------
    # one-way planks
    row = 6
    cols = [c for c in range(GW) if GRID[row][c] == "="]
    for c in cols:
        x0, y0 = c * T, row * T
        left, right = GRID[row][c - 1] != "=", GRID[row][c + 1] != "="
        xa = x0 + (2 * U if left else 0)
        xb = x0 + T - (2 * U if right else 0)
        th = 8 * U
        plank = [(xa, y0), (xb, y0), (xb, y0 + th), (xa, y0 + th)]
        sc.shape(plank, PAL["wood"], seed_of(c, "pl"), 1.0, kind="edge")
        if st.get("form"):
            pm, px0, py0 = sc.mask_of(rects=[(xa, y0 + th - 2.8 * U, xb, y0 + th)], pad=0)
            sc.paint(pm, px0, py0, PAL["wood_dark"], 0, alpha=0.55)
            pm, px0, py0 = sc.mask_of(rects=[(xa, y0, xb, y0 + 1.6 * U)], pad=0)
            sc.paint(pm, px0, py0, "#ffffff", 0, alpha=0.25)
        sc.ink([(xa + 4 * U, y0 + 4 * U), (xb - 9 * U, y0 + 4.5 * U)], seed_of(c, "grain"), 0.45, tint=PAL["wood"], kind="detail")
        for nx in (xa + 3 * U, xb - 3 * U):
            sc.blob((nx - 0.9 * U, y0 + 5.5 * U, nx + 0.9 * U, y0 + 7.3 * U), PAL["wood_dark"], seed_of(c, nx), 0)
        if left or right:
            bx = xa + 5 * U if left else xb - 5 * U
            s = 1 if left else -1
            sc.shape([(bx - s * 1.5 * U, y0 + th), (bx + s * 9 * U, y0 + th), (bx, y0 + th + 11 * U)], PAL["wood_dark"], seed_of(c, "br"), 0.9)
    # ladder
    for cy in range(GH):
        if GRID[cy][8] != "H":
            continue
        x0, y0 = 8 * T, cy * T
        topc = GRID[cy - 1][8] != "H"
        for k in range(4):
            yy = y0 + (4 + k * 8) * U
            sc.shape([(x0 + 8 * U, yy - 1.2 * U), (x0 + 24 * U, yy - 1.2 * U), (x0 + 24 * U, yy + 1.2 * U), (x0 + 8 * U, yy + 1.2 * U)],
                     PAL["wood"], seed_of(cy, k, "rung"), 0.7)
        for rx in (6, 24):
            ya = y0 - (4 * U if topc else 0)
            sc.shape([(x0 + rx * U, ya), (x0 + (rx + 2.5) * U, ya), (x0 + (rx + 2.5) * U, y0 + T), (x0 + rx * U, y0 + T)],
                     PAL["wood"], seed_of(cy, rx, "rail"), 0.9)
    # spikes: palisade stakes
    for c in range(GW):
        if GRID[10][c] != "^":
            continue
        x0, y0 = c * T, 10 * T
        rg = random.Random(seed_of(c, "sp"))
        for k in range(3):
            bx = x0 + (2 + k * 10.5) * U
            lean = rg.uniform(-1.5, 1.5) * U
            tip = (bx + 4.5 * U + lean, y0 + rg.uniform(7, 11) * U)
            stake = [(bx, y0 + T), (bx + 0.5 * U, y0 + 20 * U), tip, (bx + 8.5 * U, y0 + 20 * U), (bx + 9 * U, y0 + T)]
            sc.shape(stake, PAL["wood"], seed_of(c, k, "st"), 0.9)
            t2 = [(bx + 1.7 * U + lean * 0.5, y0 + 15 * U), tip, (bx + 7.3 * U + lean * 0.5, y0 + 15 * U)]
            sc.shape(t2, PAL["stake_tip"], seed_of(c, k, "tip"), 0.9)
        sc.ink([(x0, y0 + 25 * U), (x0 + T, y0 + 25.5 * U)], seed_of(c, "lash"), 0.8, tint=PAL["wood"])
    # ivy on grippable face
    for cy in range(GH):
        for cx in range(GW):
            if GRID[cy][cx] != "G":
                continue
            x0, y0 = cx * T, cy * T
            rg = random.Random(seed_of(cx, cy, "ivy"))
            vine = [(x0 + 3 * U, y0), (x0 + 5 * U, y0 + 10 * U), (x0 + 2 * U, y0 + 20 * U), (x0 + 4.5 * U, y0 + T)]
            sc.ink(vine, seed_of(cx, cy, "vine"), 0.8, color="#3f5a2a")
            for k in range(5):
                ly = y0 + (3 + k * 6.5) * U
                side = -1 if k % 2 else 1
                lx = x0 + 3.5 * U
                tipx = lx + side * rg.uniform(5, 7) * U
                leaf = [(lx, ly), ((lx + tipx) / 2, ly - 2.4 * U), (tipx, ly + 0.5 * U), ((lx + tipx) / 2, ly + 2.4 * U)]
                sc.shape(leaf, PAL["ivy"], seed_of(cx, cy, k, "leaf"), 0.6)
    sc.flush("main")

    draw_figure(sc, 2.4 * T, 9 * T)
    return sc.canvas.convert("RGB")


def draw_figure(sc, fx, fy):
    P = 33 * U
    lay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    ink = rgb("#222b32") + (255,)
    lw = int(round(0.045 * P))
    pt = lambda x, y: (fx + x * P, fy - y * P)

    def limb(*pts):
        q = [pt(*p) for p in pts]
        d.line(q, fill=ink, width=lw, joint="curve")
        for x, y in (q[0], q[-1]):
            d.ellipse((x - lw / 2, y - lw / 2, x + lw / 2, y + lw / 2), fill=ink)

    limb((-0.1, 1.57), (-0.16, 1.25), (-0.12, 0.94))
    limb((-0.06, 1.0), (-0.02, 0.51), (-0.13, 0.03))
    limb((0.02, 1.65), (0.05, 2.0))
    bx0, by0 = pt(-0.165, 1.66)
    bx1, by1 = pt(0.195, 0.98)
    d.rounded_rectangle((bx0, by0, bx1, by1), radius=0.1 * P, fill=rgb(PAL["body"]) + (255,), outline=ink, width=lw)
    limb((0.06, 1.0), (0.16, 0.52), (0.21, 0.03))
    limb((0.08, 1.57), (0.24, 1.3), (0.36, 1.43))
    hx, hy = pt(0.06, 2.1)
    d.ellipse((hx - 0.28 * P, hy - 0.29 * P, hx + 0.28 * P, hy + 0.29 * P), fill=rgb("#eef3ea") + (255,), outline=ink, width=lw)
    for ex in (0.07, 0.17):
        x, y = pt(0.06 + ex, 2.13)
        d.ellipse((x - 0.025 * P, y - 0.035 * P, x + 0.025 * P, y + 0.035 * P), fill=ink)
    sc.canvas.alpha_composite(lay)


def sheet(key, img, title=None):
    font = ImageFont.truetype("arial.ttf", 22)
    small = ImageFont.truetype("arial.ttf", 16)
    full = img.resize((int(W * 1.35 / U), int(H * 1.35 / U)), Image.LANCZOS)
    cx0, cy0, cx1, cy1 = 1 * T, 5 * T, 11 * T, 12 * T
    zoom = img.crop((cx0, cy0, cx1, cy1))
    zoom = zoom.resize((int(zoom.width * 2.7 / U), int(zoom.height * 2.7 / U)), Image.LANCZOS)
    sq = img.resize((W // 16, H // 16), Image.LANCZOS).filter(ImageFilter.GaussianBlur(2)).convert("L")
    sq = sq.resize((400, int(400 * H / W)), Image.BICUBIC).convert("RGB")
    pad = 20
    sw = pad * 3 + full.width + max(sq.width, 360)
    sh = pad * 4 + 34 + full.height + zoom.height
    out = Image.new("RGB", (sw, sh), (32, 34, 38))
    d = ImageDraw.Draw(out)
    d.text((pad, pad), (title or STYLES[key]["title"]) + "   (medieval)", font=font, fill=(235, 235, 235))
    y = pad * 2 + 30
    out.paste(full, (pad, y))
    d.text((pad, y + full.height + 4), "in-game size at 1080p (zoom 1.35)", font=small, fill=(170, 170, 170))
    sx = pad * 2 + full.width
    out.paste(sq, (sx, y))
    d.text((sx, y + sq.height + 4), "squint test: blurred greyscale", font=small, fill=(170, 170, 170))
    y2 = y + full.height + pad + 14
    out.paste(zoom, (pad, y2))
    d.text((pad + zoom.width + pad, y2), "2x zoom of left area", font=small, fill=(170, 170, 170))
    return out, full


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    keys = sys.argv[1:] or list(R3)
    fulls = []
    for k in keys:
        if k in VARIANTS:
            base, pal, title, over = VARIANTS[k]
            PAL.clear(); PAL.update(BASE_PAL); PAL.update(pal)
        else:
            base, title, over = k, STYLES[k]["title"], {}
            PAL.clear(); PAL.update(BASE_PAL)
        img = render(base, over)
        s, full = sheet(base, img, title)
        s.save(os.path.join(OUT, f"{k}.png"))
        fulls.append((title, full))
        print("wrote", k)
    font = ImageFont.truetype("arial.ttf", 20)
    fw, fh = fulls[0][1].size
    n = len(fulls)
    cols = 2
    rows = (n + 1) // 2
    ov = Image.new("RGB", (fw * cols + 60, (fh + 50) * rows + 20), (32, 34, 38))
    d = ImageDraw.Draw(ov)
    for i, (t, f) in enumerate(fulls):
        x, y = 20 + (i % 2) * (fw + 20), 20 + (i // 2) * (fh + 50)
        d.text((x, y), t, font=font, fill=(235, 235, 235))
        ov.paste(f, (x, y + 28))
    ov.save(os.path.join(OUT, os.environ.get("OVERVIEW", "overview-r3.png")))
