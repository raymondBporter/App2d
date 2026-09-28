"""Add the authored drop-test tower beside the player spawn, without rewriting other level content.

Run from the repository root: python tools/LevelLab/add_drop_tower.py
The operation is idempotent and refuses to replace existing nonempty terrain.
"""
from pathlib import Path
import sqlite3


LEVEL = Path(__file__).resolve().parents[2] / "Assets/Static/levels/cavern/level.db"
# Higher boards reach farther right so stepping off clears every lower board.
BOARDS = [(4, 6), (12, 8), (24, 10), (48, 12), (80, 14)]


def main():
    with sqlite3.connect(f"file:{LEVEL.as_posix()}?mode=rw", uri=True) as db:
        meta = dict(db.execute("SELECT key, value FROM meta"))
        width, height, size = (int(meta[k]) for k in ("width", "height", "chunk_size"))
        assert (width, height, size) == (640, 96, 32), "Tower layout requires the authored cavern dimensions."
        assert meta["tileset_0"] == "ink-medieval-ground"  # catalog order; cells may use other entries
        originals = dict(((cx, cy), blob) for cx, cy, blob in db.execute("SELECT cx, cy, tiles FROM chunks"))
        chunks = {}
        for key, blob in originals.items():
            cells = []
            for i in range(0, len(blob), 2):
                cells.extend([blob[i]] * blob[i + 1])
            assert len(cells) == size * size
            chunks[key] = cells

        def cell(x, y):
            return chunks.get((x // size, y // size), [0] * (size * size))[y % size * size + x % size]

        def put(x, y, value):
            assert cell(x, y) in (0, value), f"Existing terrain at {x}, {y}; refusing to overwrite."
            chunks.setdefault((x // size, y // size), [0] * (size * size))[y % size * size + x % size] = value

        # Existing flat ground ends at row 11. Keep it and all world things unchanged.
        for x in range(2, 17):
            assert cell(x, 11) & 1
        for y in range(12, 95):
            put(2, y, 4)  # Ladder, extended above the highest board for an easy exit.
        for drop, end_x in BOARDS:
            for x in range(3, end_x + 1):
                put(x, 11 + drop, 2)  # One-way board, reachable from the ladder.

        changed = 0
        for (cx, cy), cells in chunks.items():
            encoded = bytearray()
            for value in cells:
                if encoded and encoded[-2] == value and encoded[-1] < 255:
                    encoded[-1] += 1
                else:
                    encoded.extend((value, 1))
            if bytes(encoded) != originals.get((cx, cy)):
                db.execute("INSERT INTO chunks(cx,cy,tiles) VALUES(?,?,?) ON CONFLICT(cx,cy) DO UPDATE SET tiles=excluded.tiles", (cx, cy, bytes(encoded)))
                changed += 1
        assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
        print(f"Drop tower: {changed} chunks updated; boards at 4, 12, 24, 48, 80 tiles above ground.")
        print("Ladder world X = -432; head left from the player spawn, hold Up, then exit right.")


if __name__ == "__main__":
    main()
