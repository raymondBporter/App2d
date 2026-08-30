# In-Game Tile Map Editor Design

**Date:** 2026-08-30
**Status:** Approved in chat (phase 1 scoped for implementation; phases 2–3 sketched)

## Goal

Replace procedurally generated terrain with authored levels stored in a SQLite file, and
build an in-game tile painter on top of that store. `JumpableWorldGenerator2D` was scaffolding
— a way to have something to walk on before the real-map problem was solved. It becomes a
one-shot baking tool and is then deleted.

This spec scopes **phase 1** for implementation: the level format, the editable map, the
baker, and the swap of the game's read path. The editor itself is phase 2 and is sketched
here only enough to keep phase 1 from painting itself into a corner.

## Decisions

- **Bake once, file is truth.** The generator produces a level file; from then on the file
  is authoritative. No procedural base with an edit overlay, and no runtime generation.
- **One `.db` per level, holding everything.** Tiles as run-length-encoded chunk blobs;
  entities and metadata (phase 3) as tables in the same file. A level is one atomic artifact.
- **Write-through per stroke, in-memory undo** (phase 2). Each completed stroke commits its
  dirty chunks in one transaction. Undo is a session-only stack. Discarding an experiment is
  `git checkout level.db`.
- **Modal editor** (phase 2). A key toggles editor mode: player and enemies freeze, camera
  detaches for free pan/zoom, mouse paints, edits apply live and the touched chunks rebuild.
  Toggling out resumes play exactly where the player stood.
- **Phase 2 tools are single-tile paint and erase only.** Brush size, rectangle fill and
  flood fill are deliberately excluded from the first editor version.
- **Ground height is derived, provisional, and deliberately sloppy.** It drives camera
  tweaks; getting it wrong degrades feel, not correctness. See "Ground height" below.
- **No compatibility shims.** `ProceduralTileMap2D` is deleted, not deprecated; the generator
  leaves the runtime path in phase 1. Breaking things for a few hours mid-phase is acceptable.

## Context findings (verified 2026-08-30)

- `App2d.Tiles` has two map types. `TileMap2D` is dense but bool-only — it carries no
  `TileKind2D`. `ProceduralTileMap2D` carries kinds and chunking but is backed by a
  `Func<int, int, TileKind2D>`, so it is read-only by construction. Neither is an editable
  kind-carrying map.
- `ProceduralTileMap2D` has exactly **one** production consumer, `SideScrollerLevel2D:38`.
  Its other references are `App2d.Tests/Tiles/ProceduralTileMap2DTests.cs` and
  `TileMeshingCharacterizationTests.cs`. It is therefore replaced rather than duplicated.
- `SideScrollerLevel2D` builds a 640×96 world at seed `0xA2D_2026_0823`, tile size from
  `TraversalMetrics2D`, origin `(-512, -640)`, streamed in 32-tile chunks.
- `JumpableWorldGenerator2D.TerrainHeight(x)` has 5 call sites across 2 files:
  `SideScrollerLevel2D` lines 49 (spawn Y), 54 (goal Y), 76 (`GetCameraFloorY`) and 324
  (moving-platform height), plus `SideScrollerEncounterSpawner2D:127`
  (`TerrainHeight(x) - 1`, enemy placement). A sixth site, `SideScrollerLevel2D:215`, hands
  the generator instance itself to the encounter spawner.
- `SideScrollerChunkStreamer2D` caches per-chunk physics bodies and Skia visuals in
  `_loadedChunks`, loading and unloading only in response to camera movement. There is no
  invalidation path for a chunk whose tiles changed.
- **Terrain visuals sample across chunk borders.** `GetExposedSurfaces`, `GetCorners`,
  `GetOneWayPart` and `GetSpikePart` all read `x±1` / `y±1` with no clamping to the chunk.
  Painting a tile on a chunk edge therefore changes the appearance of the adjacent chunk.
- The solution is dependency-light: `SkiaSharp.Views.WindowsForms` in the host is the only
  `PackageReference`. Every engine project is pure `net10.0` with project references only.
- `Assets/README.md` states `Runtime` is generated and disposable and that durable or
  hand-edited files must never live only there. `AssetPaths.Root` resolves to
  `Assets/Runtime` in Debug via an `#if DEBUG` walk-up of parent directories.
- There is an established in-game tooling precedent: `App2d/Diagnostics/DeveloperConsole.cs`
  with `RegisterVariable` / `RegisterCommand`, already used for `sfx_volume` and
  `draw_traversal_metrics`.

## Module placement

The SQLite dependency is confined to one new project so the engine core stays free of it.

| Where | What | New dependency |
| --- | --- | --- |
| `App2d.Tiles` (existing) | `EditableTileMap2D` — dense, kind-carrying, mutable `IChunkedTileMap2D`; raises `ChunkChanged` | none |
| `App2d.Levels` (**new**, `net10.0`) | `LevelDatabase2D`, `TileRunCodec2D` | `Microsoft.Data.Sqlite` |
| `App2d/Levels/` (host) | `LevelBootstrap2D` — bake-if-missing, load, `level_rebake` console command | none |
| `App2d/Editor/` (host, phase 2) | `TileEditor2D`, `TileEditorView2D` — mirrors `App2d/Diagnostics/` | none |

Baking lives in the **host**, not `App2d.Levels`: it needs `JumpableWorldGenerator2D` from
`App2d.Gameplay` (`net10.0-windows`) *and* `LevelDatabase2D`, and the host is the only
project that references both. `App2d.Levels` staying `net10.0` and generator-free is what
lets the generator be deleted without touching the level format.

`App2d.Levels` references `App2d.Core` and `App2d.Tiles`. `App2d.Gameplay` does **not**
reference `App2d.Levels`: the host loads the map and injects it into `SideScrollerLevel2D`,
so gameplay stays SQLite-free. The editor lives in the host because input types
(`Keys`, `MouseButtons`, `InputState`) live there.

## Storage format

```sql
PRAGMA user_version = 1;
CREATE TABLE meta  (key TEXT PRIMARY KEY, value TEXT NOT NULL) WITHOUT ROWID;
CREATE TABLE chunks(cx INTEGER, cy INTEGER, tiles BLOB NOT NULL,
                    PRIMARY KEY(cx, cy)) WITHOUT ROWID;
```

`meta` keys for phase 1: `width`, `height`, `tile_size`, `chunk_size`, `origin_x`,
`origin_y`, `source_seed`, `generated_utc`. Key/value rather than fixed columns so that
provenance keys simply stop being written when the generator is deleted — no migration to
unwind a throwaway.

`tiles` is run-length encoded, row-major within the chunk, as `[kind:u8][count:u8]` pairs
with `count` in 1..255. Runs longer than 255 split across pairs. An all-empty chunk encodes
to 5 pairs (10 bytes); the alternating worst case is 2 KB. **A missing `chunks` row means the
chunk is entirely `TileKind2D.Empty`**, so empty sky costs no rows.

Edge chunks store their clipped extent — width `min(chunk_size, width - startX)`, height
likewise — and dimensions are derived from `meta`, so a partial chunk is never ambiguous.

Phase 1 deliberately omits `entities`, `tileset_regions` and `undo_log`. `user_version` is
the hook for adding them.

## Ground height

`TerrainHeight(x)` disappears with the generator, but 5 sites need the answer. Ground height
becomes **derived from tile data**: scan column `x` upward from `y = 0` and take the lowest
non-solid tile, clamped to a minimum of 1. Computed once at load into an `int[width]` and
exposed as `GroundY(x)`, which returns a **tile row index, not a world coordinate** — the
same units `TerrainHeight` returned, so callers keep their `* _tileSize` arithmetic unchanged.

The derived rule was chosen over baking a `columns(x, ground_y)` table because the table
would preserve a dependency on a component being deleted, and a hand-painted level has no
generator to ask.

**This is knowingly provisional and does not need to be good.** Ground height feeds camera
floor clamping, spawn/goal Y, and enemy placement rows. Getting it wrong degrades feel; it
does not break the game, and today's generator-derived version is itself approximate. The
column scan diverges from `TerrainHeight` at jumpable pits — a pit column is empty at
`y = 0`, so the scan reports the pit floor rather than the surrounding terrain. The clamp to
1 is what keeps `SideScrollerEncounterSpawner2D:127` (`GroundY(x) - 1`) from placing an enemy
below the world. That clamp is the entire mitigation; no pit-detection logic.

The real fix is a later redesign, not a better scan. A single ground row per column cannot
express a **second-storey floor** — an interior level above the terrain that the camera and
spawns should treat as ground. That needs ground height to become authored, multi-valued
data (phase 3), at which point the column scan is replaced outright. Building anything
elaborate here now would be work thrown away twice.

## Phase 1 — format, editable map, and the swap

1. **`EditableTileMap2D`** in `App2d.Tiles`: dense `TileKind2D[width * height]`, implements
   `IChunkedTileMap2D`, adds `SetTileKind(x, y, kind)`, a `Fill(Func<int, int, TileKind2D>)`
   seeding method, and a `ChunkChanged` event (unused in phase 1, consumed in phase 2).
2. **Delete `ProceduralTileMap2D`.** Retarget `ProceduralTileMap2DTests` and
   `TileMeshingCharacterizationTests` onto `EditableTileMap2D` via the `Fill` overload.
3. **`App2d.Levels` project** with `Microsoft.Data.Sqlite`, `TileRunCodec2D` (encode/decode a
   chunk), and `LevelDatabase2D` (create, open, read chunk, write chunk, read/write meta).
4. **`LevelBootstrap2D`** in the host — generator → `EditableTileMap2D.Fill` →
   `LevelDatabase2D` → `Assets/Static/levels/cavern/level.db`. Bakes automatically when the
   file is missing, so the one-time generation needs no remembered manual step, and exposes a
   `level_rebake` `DeveloperConsole` command to force it. The resulting `.db` is committed.
   Both the auto-bake and the command are deleted along with the generator.
5. **Swap the read path.** `SideScrollerLevel2D` takes an injected `IChunkedTileMap2D` and a
   `GroundY` source instead of constructing a generator. The 5 `TerrainHeight` sites become
   `GroundY(x)`, and the generator handoff at line 215 becomes a `GroundY` handoff.
6. **Level path resolution.** The loader resolves `Assets/Static/levels/...` directly in
   Debug — mirroring the `#if DEBUG` walk-up in `AssetPaths` — so authored levels are read
   from their durable home and `Runtime` stays disposable. Release reads the packaged copy.

At the end of phase 1 the generator has no runtime consumer and can be deleted whenever
convenient.

## Phase 2 — the tile painter (sketch)

A modal editor in `App2d/Editor/`, toggled by a key and registered as a `DeveloperConsole`
variable. Entering freezes the player and enemies and detaches the camera for free pan/zoom;
leaving resumes play at the same position. `LMB` paints the selected kind, `RMB` erases,
`1`–`5` select kind, drag paints a continuous stroke with line interpolation so fast mouse
movement leaves no gaps. `Ctrl+Z` undoes a stroke. An overlay draws the tile cursor, a grid,
and the selected kind.

The load-bearing piece is **chunk invalidation**. `SideScrollerChunkStreamer2D` gains an
`Invalidate(TileChunk2D)` that unloads and reloads a chunk if currently loaded;
`EditableTileMap2D.ChunkChanged` drives it. Because terrain visuals sample across chunk
borders, a painted tile invalidates every chunk touching its 3×3 tile neighbourhood — one
chunk for an interior tile, up to four at a chunk corner.

Phase 2 does not update ground height: painting changes collision and visuals but not
`GroundY`, so repainted terrain will not move the camera floor or spawn points until phase 3
replaces the whole notion. Accepted, consistent with ground height being provisional.

## Phase 3 — beyond tiles (sketch)

`entities` and `tileset_regions` tables; spawn point, goal, moving platforms and enemy
placements become authored data rather than code in `SideScrollerLevel2D`; ground height is
redesigned as authored, multi-valued data supporting second-storey floors, replacing the
column scan; a production bake step in
`tools/ArtPipeline/build_runtime_assets.py` compiling `.db` files into a read-optimized
`Runtime/levels/` artifact; additional tools (brush size, rectangle fill, flood fill).

## Testing

- **RLE round-trip** — random chunks, all-empty, all-one-kind, alternating worst case, runs
  longer than 255, and clipped edge-chunk extents.
- **Bake characterization** — every one of the 61,440 tiles read back from the `.db` equals
  `JumpableWorldGenerator2D.GetTileKind(x, y)`. This is the test that proves the format
  before the format is trusted, and it is deleted along with the generator.
- **Derived ground height** — a sanity test only, matching how provisional it is: `GroundY(x)`
  is always ≥ 1 and never indexes below the world. No assertion that it agrees with
  `TerrainHeight`; it is allowed to differ and will be replaced.
- **`EditableTileMap2D`** — `SetTileKind` round-trips, out-of-bounds reads return `Empty`,
  chunk math matches the retargeted `ProceduralTileMap2D` tests, `ChunkChanged` fires for the
  right chunk.
- **Retargeted** `ProceduralTileMap2DTests` and `TileMeshingCharacterizationTests`.
- **Playtest.** Spawn, run the level, cross a pit, reach the goal, confirm enemies stand on
  ground and nothing shifted. Tests cannot make the "nothing changed" claim on their own.
