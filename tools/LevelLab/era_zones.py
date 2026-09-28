"""Split the cavern test map into three era zones and paint each with its ink tilesets.

Run from the repository root: python tools/LevelLab/era_zones.py
Idempotent. It:
  * writes zones.json with three equal thirds: prehistoric, medieval, wasteland (left to right);
  * adds a few era landmarks on open ground, refusing to overwrite existing terrain;
  * retiles every cell: natural terrain uses the era's ground set, while anything standing well
    above the local ground (pillars, arches, towers) uses its wall set and is grippable, so
    built things read, and play, as climbable;
  * repaints the moving lifts in the warm orange the tilesets use for interactive props.
"""
from pathlib import Path
import json
import sqlite3

ROOT = Path(__file__).resolve().parents[2]
LEVEL = ROOT / "Assets/Static/levels/cavern/level.db"
ZONES = ROOT / "Assets/Static/levels/cavern/zones.json"
ORIGIN_X, ORIGIN_Y, TILE = -512, -640, 32
SOLID, ONE_WAY, GRIP, SPIKES = 1, 2, 4, 8
LADDER = GRIP
ERAS = [  # (zone id, name, ground tileset index, wall tileset index), matching the level's tileset catalog
    ("prehistoric", "Fern Hollow", 2, 3),
    ("medieval", "Kingsroad", 0, 1),
    ("wasteland", "The Rust", 4, 5),
]
CATALOG = ["ink-medieval-ground", "ink-medieval-wall", "ink-prehistoric-ground", "ink-prehistoric-wall",
           "ink-wasteland-ground", "ink-wasteland-wall", "dark-cave", "mossy-cavern"]
WALL_RISE = 2      # cells more than this far above the surrounding ground become wall
BASE_WINDOW = 5    # columns either side used to find the surrounding ground


def main():
    with sqlite3.connect(f"file:{LEVEL.as_posix()}?mode=rw", uri=True) as db:
        meta = dict(db.execute("SELECT key, value FROM meta"))
        width, height, size = (int(meta[k]) for k in ("width", "height", "chunk_size"))
        assert [meta.get(f"tileset_{i}") for i in range(len(CATALOG))] == CATALOG, "Unexpected tileset catalog."
        originals = dict(((cx, cy), blob) for cx, cy, blob in db.execute("SELECT cx, cy, tiles FROM chunks"))
        chunks = {}
        for key, blob in originals.items():
            cells = []
            for i in range(0, len(blob), 2):
                cells.extend([blob[i]] * blob[i + 1])
            chunks[key] = cells

        def get(x, y):
            if not (0 <= x < width and 0 <= y < height):
                return 0
            return chunks.get((x // size, y // size), [0] * (size * size))[y % size * size + x % size]

        def set_(x, y, value):
            chunks.setdefault((x // size, y // size), [0] * (size * size))[y % size * size + x % size] = value

        def kind(x, y):
            return get(x, y) & 0x0F

        def put(x, y, k):
            assert kind(x, y) in (0, k), f"Existing terrain at {x}, {y}; refusing to overwrite."
            set_(x, y, k)

        thirds = [0, width // 3, 2 * width // 3, width]

        # ---- landmarks (kinds only; tilesets are assigned below)
        def column(x, top, k=SOLID | GRIP):
            y = 0
            while kind(x, y) & SOLID:
                y += 1
            for yy in range(y, top + 1):
                put(x, yy, k)

        # Fern Hollow: a stepped boulder mound (clear of the drop tower) and a lone stone spire.
        for x, rise in zip(range(33, 39), (1, 2, 3, 3, 2, 1)):
            column(x, 12 + rise)
        for x in range(153, 156):
            column(x, 17 + (x == 154))
        # Kingsroad: a keep tower with crenellations, beside the existing ledge at 305-307.
        for x in range(308, 313):
            column(x, 20 + (x % 2 == 0))
        # The Rust: a ruined building shell whose girder floor spans the spike trap at 509-511.
        for x, top in ((505, 19), (506, 18), (514, 20), (515, 17)):
            column(x, top)
        for x in range(507, 514):
            put(x, 16, ONE_WAY)

        # ---- retile
        def run_from_bottom(x):
            y = 0
            while kind(x, y) & SOLID:
                y += 1
            return y  # first empty row above the ground mass

        runs = [run_from_bottom(x) for x in range(width)]
        base = []
        for x in range(width):
            near = [runs[i] for i in range(max(0, x - BASE_WINDOW), min(width, x + BASE_WINDOW + 1)) if runs[i] > 0]
            base.append(min(near) if near else 0)
        walls = 0
        for x in range(width):
            era = next(i for i in range(3) if thirds[i] <= x < thirds[i + 1])
            _, _, ground, wall = ERAS[era]
            for y in range(height):
                k = kind(x, y)
                if not k:
                    continue
                if k & SOLID:
                    # Columns rising well above the surrounding ground are structures: wall from their foot up.
                    # Shallow terrace lips stay ground.
                    raised = runs[x] - base[x] >= WALL_RISE + 2 and y >= base[x]
                    is_wall = bool(k & GRIP) or raised or y >= runs[x]
                    if is_wall:
                        k = (k | GRIP) if not (k & SPIKES) else k
                        walls += 1
                    set_(x, y, ((wall if is_wall else ground) << 4) | k)
                else:
                    set_(x, y, (ground << 4) | k)

        changed = 0
        for (cx, cy), cells in chunks.items():
            encoded = bytearray()
            for value in cells:
                if encoded and encoded[-2] == value and encoded[-1] < 255:
                    encoded[-1] += 1
                else:
                    encoded.extend((value, 1))
            if bytes(encoded) != originals.get((cx, cy)):
                db.execute("INSERT INTO chunks(cx,cy,tiles) VALUES(?,?,?) ON CONFLICT(cx,cy) DO UPDATE SET tiles=excluded.tiles",
                           (cx, cy, bytes(encoded)))
                changed += 1

        lift = db.execute("SELECT definition_id FROM thing_definitions WHERE type_key = 'moving-platform'").fetchone()[0]
        db.execute("UPDATE thing_definitions SET name = 'Lift' WHERE definition_id = ?", (lift,))
        db.execute("UPDATE solid_color_art SET color_argb = ? WHERE definition_id = ?", (0xFFD4923A - (1 << 32), lift))
        assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"

    top = ORIGIN_Y + height * TILE
    zones = [{"id": zid, "name": name, "minX": ORIGIN_X + thirds[i] * TILE, "minY": ORIGIN_Y,
              "maxX": ORIGIN_X + thirds[i + 1] * TILE, "maxY": top, "priority": 0}
             for i, (zid, name, _, _) in enumerate(ERAS)]
    lines = ",\n".join("    " + json.dumps(z) for z in zones)
    ZONES.write_text('{\n  "version": 1,\n  "zones": [\n' + lines + "\n  ]\n}\n", encoding="utf-8")
    print(f"Era zones: {changed} chunks updated, {walls} wall cells; zone edges at world X "
          f"{', '.join(str(ORIGIN_X + t * TILE) for t in thirds)}.")


if __name__ == "__main__":
    main()
