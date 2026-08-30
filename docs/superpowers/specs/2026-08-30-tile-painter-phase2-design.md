# In-Game Tile Painter — Phase 2 Design

**Date:** 2026-08-30
**Status:** Approved in chat
**Supersedes:** the "Phase 2 — the tile painter (sketch)" section of
`docs/superpowers/specs/2026-08-30-tile-map-editor-design.md`, which this expands into a
buildable design. Phase 1 of that spec is implemented and merged.

## Goal

Paint tiles in the running game and see the result immediately. A key toggles editor mode,
the simulation freezes, the camera detaches, the mouse paints single tiles, and each stroke
commits to the level file.

The aim of this phase is **clean seams, not a rich toolset**. Painting one tile and erasing
one tile is enough to prove the whole loop — edit, invalidate, rebuild, persist, undo. Brush
sizes and fills are cheap to add afterwards and are deliberately excluded.

## Decisions

- **The editor owns a paused flag the game loop reads.** `SideScrollerGame.Update` checks the
  editor's mode and returns before the simulation block. No gameplay type gains a `Paused`
  property, and `Person2D`, enemies and physics are untouched. The seam is one branch.
- **Dirty chunks coalesce and rebuild once per frame**, not per change. Painting mutates the
  map and records dirt; rebuilding happens at a single flush point.
- **Whole chunks rebuild. No incremental rectangle updates.** Editing is not a 120 fps
  workload and visual fidelity is worth more than frame rate here.
- **Undo replays through the same mutation path** as painting, so invalidation and persistence
  cannot drift between the two.
- **Loading opens the level read-only.** Only baking and editing open it read-write.
- **Tools this phase: single-tile paint and erase.** No brush size, no rectangle fill, no flood
  fill, no entity editing, no ground-height recompute.

## Context findings (verified 2026-08-30)

- `SideScrollerGame` keeps its `EditableTileMap2D` as a **local variable** and exposes only
  `_level.TileMap`, typed `IChunkedTileMap2D` — which has no `SetTileKind` and no
  `ChunkChanged`. The editor needs the concrete map.
- `LevelBootstrap2D.LoadOrBake` **disposes** its `LevelDatabase2D` before returning. Write-through
  needs a database that outlives the load.
- `SideScrollerChunkStreamer2D` caches per-chunk physics bodies and Skia visuals in
  `_loadedChunks` and only loads or unloads in response to camera movement. There is **no
  invalidation path** for a chunk whose tiles changed.
- `EditableTileMap2D.SetTileKind` already raises `ChunkChanged` once per distinct chunk touching
  the painted tile's 3x3 neighbourhood, clamped to the map and deduplicated — 1 event for an
  interior tile, 2 on a chunk edge, 4 at a chunk corner. This exists because terrain visuals
  sample `x±1`/`y±1` across chunk borders (`GetExposedSurfaces`, `GetCorners`, `GetOneWayPart`,
  `GetSpikePart`).
- `LevelDatabase2D.SaveChunks(map, ReadOnlySpan<TileChunk2D>)` writes many chunks in one
  transaction; `SaveChunk` delegates to it.
- `LevelDatabase2D.Open` uses `SqliteOpenMode.ReadWriteCreate` and runs `CREATE TABLE IF NOT
  EXISTS`, which bumps SQLite's file change counter. Merely launching the game therefore dirties
  the committed `level.db` in git at bytes 28 and 96 with no content change.
- `InputState.SetSuppressed` already exists and is how the developer console stops gameplay from
  seeing input. `DeveloperConsole` opens on `Keys.Oemtilde`, handled in `GameHost.OnWindowKeyDown`.
- `SideScrollerCamera2D` follows the player every frame via `Update(position, velocity, grounded,
  dt)` and clamps to `_level.TileMap.WorldBounds` and `GetCameraFloorY`.

## Module placement

| Where | What | New dependency |
| --- | --- | --- |
| `App2d.Levels` | `LevelDatabase2D.OpenRead` | none |
| `App2d.Gameplay/World` | `SideScrollerChunkStreamer2D.Invalidate`; dirty-set + `FlushDirtyChunks` on `SideScrollerLevel2D` | none |
| `App2d/Editor/TileEditor2D.cs` | mode, input, tool state, undo stack, write-through | none |
| `App2d/Editor/TileEditorView2D.cs` | cursor, grid and status overlay | none |

The editor lives in the host because input types (`Keys`, `MouseButtons`, `InputState`) do.
`App2d.Gameplay` still must not reference `App2d.Levels` — the host owns the database and hands
the editor both it and the concrete map.

## Storage: read-only loading

`LevelDatabase2D` gains `OpenRead(string path)` using `SqliteOpenMode.ReadOnly` and skipping the
`CREATE TABLE` DDL, for the load path. `Open` keeps `ReadWriteCreate` for baking and editing.
`LevelBootstrap2D.LoadOrBake` uses `OpenRead` once the file exists, so launching the game stops
dirtying the committed asset.

Editing needs a writable handle that outlives the load, so `LevelBootstrap2D` also exposes
`OpenForEditing()` returning a read-write `LevelDatabase2D` for the level at `CavernLevelPath`.
The host owns its lifetime and disposes it in `SideScrollerGame.Dispose`.

## Invalidation and rebuild

`SideScrollerLevel2D` subscribes to the map's `ChunkChanged` and accumulates chunk coordinates in
a `HashSet<TileChunk2D>`. Nothing rebuilds at mutation time.

`FlushDirtyChunks()` drains that set, calling `SideScrollerChunkStreamer2D.Invalidate(chunk)` for
each. `Invalidate` unloads and reloads the chunk if it is currently loaded, and does nothing
otherwise — an edit to an unloaded chunk needs no work, because loading it later reads the
current map.

The editor branch of `SideScrollerGame.Update` calls `FlushDirtyChunks()` once per frame. A drag
across many tiles therefore causes at most one rebuild per affected chunk per frame, no matter
how many `ChunkChanged` events fired.

Whole chunks rebuild, including all neighbours the 3x3 rule marks dirty. Rebuilding a chunk
destroys and recreates its physics bodies and Skia visuals; that cost is accepted.

## Editor mode

`TileEditor2D` owns: whether the mode is active, the selected `TileKind2D`, the camera offset and
zoom while detached, the in-progress stroke, and the undo stack.

Entering: the mode toggles on a key, and the camera stops following. Leaving: the camera returns
to following, and play resumes with the player exactly where it stood — the simulation was frozen,
not reset.

`SideScrollerGame.Update` becomes:

```
_editor.Update(input, Camera);
if (_editor.IsActive)
{
    _level.FlushDirtyChunks();
    return;
}
...existing gameplay update...
```

`_level.UpdateStreaming` still runs before that branch so the free camera can stream new chunks —
but in editor mode it must be fed the **camera's** focus, not the frozen player's position, or
panning away from the player would paint into chunks that never load. The existing call site passes
`_player.Position`; the editor branch passes the editor's camera focus instead.

Bindings: `F1` toggles the mode. `LMB` paints the selected kind, `RMB` erases to `Empty`.
`1`-`5` select Empty / Solid / OneWay / Solid|Grippable / Spikes. Middle-drag pans, the wheel
zooms, `Ctrl+Z` undoes. `F1` is chosen because the developer console already owns `Oemtilde` and
the gameplay bindings documented in `WindowTitle` already claim the letter keys.

A drag paints a continuous stroke: the editor interpolates tiles between the previous and current
mouse position so fast movement leaves no gaps.

## Undo and persistence

A stroke records every `(x, y, before, after)` it changed, ignoring no-op writes. Mouse-up ends
the stroke, pushes it onto an in-memory stack, and commits.

Committing derives the distinct chunks the stroke touched and calls
`LevelDatabase2D.SaveChunks(map, chunks)` — one transaction per stroke.

Undo pops a stroke and re-applies its `before` values **through `SetTileKind`**, so it raises
`ChunkChanged` exactly as painting does, dirties the same chunks, and commits the same way. There
is no second persistence path to keep correct. Undo history is session-only; discarding
experiments is `git checkout level.db`.

## Overlay

`TileEditorView2D` draws, only while the mode is active: a highlight on the hovered tile, a light
grid over nearby tiles, and a status line showing the selected kind and the hovered tile
coordinate. It draws after the scene, in the same `Render` pass as the existing HUD.

## Testing

Most of this is input and rendering, which is not unit-testable. The parts that are:

- **Dirty-set coalescing** — painting N tiles across a chunk boundary yields the expected distinct
  chunk set; flushing drains it; flushing twice does nothing the second time.
- **Stroke and undo** — a stroke records only tiles it actually changed; undo restores exactly the
  previous kinds; undo of a stroke that crossed a chunk boundary dirties the same chunks painting
  did; the undo stack empties.
- **Read-only load** — `OpenRead` on an existing file loads correctly, and opening a level for
  reading does not modify the file's bytes. This is the regression test for the change-counter
  dirtying.
- **`Invalidate`** — invalidating a loaded chunk rebuilds it; invalidating an unloaded chunk is a
  no-op; a rebuilt chunk's collision rectangles reflect the edited tiles.

Then a playtest, which is the only thing that can confirm painting actually feels right: toggle in,
paint a ledge, toggle out, stand on it.

## Deliberately excluded

Brush size, rectangle fill, flood fill, eyedropper, tileset selection, entity placement,
ground-height recompute after painting (repainted terrain will not move the camera floor or spawn
until phase 3 replaces ground height entirely), multi-level undo persistence, and any release
binary level format.
