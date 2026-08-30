# Tile Painter — Phase 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Paint and erase tiles in the running game, see the result immediately, and have each stroke persist to the level file.

**Architecture:** A modal editor freezes the simulation by making `SideScrollerGame.Update` return early. Painting mutates `EditableTileMap2D`, whose `ChunkChanged` event feeds a dirty-chunk tracker; the editor flushes that tracker once per frame, rebuilding whole chunks through a new `SideScrollerChunkStreamer2D.Invalidate`. The stroke/undo model lives in `App2d.Tiles` so it is unit-testable; only input, camera and rendering live in the host.

**Tech Stack:** C# / .NET 10, xunit 2.9.3, `Microsoft.Data.Sqlite` 10.0.11, SkiaSharp (host only), WinForms input.

**Spec:** `docs/superpowers/specs/2026-08-30-tile-painter-phase2-design.md`

## Global Constraints

- **Naming:** every public type ends in `2D`. Universal in this codebase.
- **Validation:** use `App2d.Core.ArgGuard` for arguments and `App2d.Core.StateGuard` for invalid state. Never hand-roll `throw new ArgumentException` / `InvalidOperationException`.
- **Target frameworks:** `App2d.Core`, `App2d.Collision`, `App2d.Tiles`, `App2d.Levels`, `App2d.Physics`, `App2d.Rendering`, `App2d.Tests` are plain `net10.0`. `App2d.Gameplay`, `App2d.Gameplay.Tests`, `App2d` are `net10.0-windows10.0.19041.0`.
- **Module boundaries — do not violate:** `App2d.Gameplay` must NOT reference `App2d.Levels`. `App2d.Tiles` must NOT reference `App2d.Levels` or `Microsoft.Data.Sqlite`. Only `App2d.Levels` (and test projects) reference SQLite. **The host is the only place that sees both the database and gameplay.**
- **`Microsoft.Data.Sqlite` stays pinned at exactly `10.0.11`.** Do not change it.
- **Do not re-enable WAL journal mode.** It was removed because pooled connections meant `Dispose()` never checkpointed, stranding committed data in a gitignored sidecar. A test pins this.
- **World constants unchanged:** width 640, height 96, chunk size 32, origin `(-512, -640)`, seed `0xA2D_2026_0823UL`.
- **The host (`App2d`) has no test project.** Anything that must be unit-tested belongs in `App2d.Tiles` or `App2d.Levels`.
- **Editing is not a 120 fps workload.** Rebuild whole chunks; do not write incremental rectangle-level updates.
- **Bindings:** `F1` toggles editor mode. `LMB` paints, `RMB` erases, `1`–`5` select kind, middle-drag pans, wheel zooms, `Ctrl+Z` undoes.
- **Build/test:** `dotnet build App2d.slnx`, `dotnet test App2d.slnx`. Close any running `App2d.exe` first — it locks the output DLLs and the build fails with MSB3021/MSB3027 file-lock errors that are NOT compile errors.

---

### Task 1: Read-only level loading

Loading currently opens `ReadWriteCreate` and runs `CREATE TABLE IF NOT EXISTS`, which bumps SQLite's file change counter — so merely launching the game dirties the committed `level.db` in git (bytes 28 and 96) with no content change. Editing also needs a writable handle that outlives the load.

**Files:**
- Modify: `App2d.Levels/LevelDatabase2D.cs`
- Modify: `App2d/LevelBootstrap2D.cs`
- Test: `App2d.Tests/Levels/LevelDatabase2DTests.cs`

**Interfaces:**
- Consumes: `LevelDatabase2D.Open(string path)`, `.Load()`, `.Save(EditableTileMap2D, ulong)`, `EditableTileMap2D`.
- Produces:
  - `static LevelDatabase2D LevelDatabase2D.OpenRead(string path)` — read-only, no DDL, throws if the file does not exist.
  - `static LevelDatabase2D LevelBootstrap2D.OpenForEditing()` — read-write handle for `CavernLevelPath`, caller disposes.

- [ ] **Step 1: Write the failing tests**

Add to `App2d.Tests/Levels/LevelDatabase2DTests.cs`:

```csharp
    [Fact]
    public void OpenReadLoadsAnExistingLevel()
    {
        var path = NewPath();
        var original = BuildMap();
        using (var database = LevelDatabase2D.Open(path))
            database.Save(original, sourceSeed: 0UL);

        using var reader = LevelDatabase2D.OpenRead(path);
        var loaded = reader.Load();

        Assert.Equal(original.Width, loaded.Width);
        for (var y = 0; y < original.Height; y++)
        {
            for (var x = 0; x < original.Width; x++)
                Assert.Equal(original.GetTileKind(x, y), loaded.GetTileKind(x, y));
        }
    }

    [Fact]
    public void OpenReadDoesNotModifyTheFile()
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path))
            database.Save(BuildMap(), sourceSeed: 0UL);

        var before = File.ReadAllBytes(path);

        using (var reader = LevelDatabase2D.OpenRead(path))
            reader.Load();

        var after = File.ReadAllBytes(path);

        // Opening read-write and running DDL bumps SQLite's file change counter
        // (bytes 28 and 96), which dirties the committed level asset in git on
        // every launch. The read path must leave the bytes untouched.
        Assert.Equal(before, after);
    }

    [Fact]
    public void OpenReadOnAMissingFileThrows()
    {
        Directory.CreateDirectory(_directory);
        var missing = Path.Combine(_directory, "does-not-exist.db");

        Assert.Throws<InvalidOperationException>(() => LevelDatabase2D.OpenRead(missing));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~OpenRead"`
Expected: FAIL — compile error, `OpenRead` does not exist.

- [ ] **Step 3: Implement `OpenRead`**

In `App2d.Levels/LevelDatabase2D.cs`, add alongside `Open`. Note it does NOT run the `CREATE TABLE` DDL and does NOT stamp `user_version`; it still validates the version is not newer than this code understands.

```csharp
    /// <summary>
    /// Opens an existing level for reading only. Loading must not write: opening
    /// read-write and running DDL bumps SQLite's file change counter, which dirties
    /// the committed level asset in git on every game launch.
    /// </summary>
    public static LevelDatabase2D OpenRead(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        StateGuard.ThrowIf(!File.Exists(path), $"The level file '{path}' does not exist.");

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        connection.Open();

        var database = new LevelDatabase2D(connection);
        var version = database.FormatVersion;
        if (version > CurrentFormatVersion)
        {
            connection.Dispose();
            StateGuard.Throw(
                $"The level file is format version {version}, newer than this build understands ({CurrentFormatVersion}).");
        }

        return database;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~OpenRead"`
Expected: PASS — 3 tests.

- [ ] **Step 5: Use it from the bootstrap and add the editing handle**

In `App2d/LevelBootstrap2D.cs`, change `LoadOrBake` and add `OpenForEditing`:

```csharp
    public static EditableTileMap2D LoadOrBake(TraversalMetrics2D traversal)
    {
        if (!File.Exists(CavernLevelPath))
            Bake(traversal);

        using var database = LevelDatabase2D.OpenRead(CavernLevelPath);
        return database.Load();
    }

    /// <summary>
    /// Opens the cavern level read-write for an editing session. The caller owns the
    /// returned database and must dispose it.
    /// </summary>
    public static LevelDatabase2D OpenForEditing() => LevelDatabase2D.Open(CavernLevelPath);
```

- [ ] **Step 6: Verify the game no longer dirties the level file**

Run: `dotnet build App2d.slnx`, then run the game once and close it.

Run: `git status --short -- Assets/Static/levels/cavern/level.db`
Expected: **no output** — the file is unchanged. Before this task it would show as modified.

If it still shows modified, run `git diff --stat` on it; a byte-identical-content change at offsets 28/96 means something is still opening it read-write.

- [ ] **Step 7: Commit**

```bash
git add App2d.Levels App2d.Tests App2d/LevelBootstrap2D.cs
git commit -m "Open levels read-only for loading"
```

---

### Task 2: Dirty-chunk tracking and invalidation

Painting mutates the map, but nothing rebuilds the affected chunks. `ChunkChanged` can fire up to 4 times per painted tile and constantly during a drag, so changes are accumulated and rebuilt once per frame rather than per event.

**Files:**
- Create: `App2d.Tiles/DirtyChunkTracker2D.cs`
- Modify: `App2d.Gameplay/World/SideScrollerChunkStreamer2D.cs`
- Modify: `App2d.Gameplay/World/SideScrollerLevel2D.cs`
- Test: `App2d.Tests/Tiles/DirtyChunkTracker2DTests.cs`

**Interfaces:**
- Consumes: `EditableTileMap2D.ChunkChanged` (event `Action<TileChunk2D>`), `TileChunk2D`.
- Produces:
  - `DirtyChunkTracker2D()` with `void Mark(TileChunk2D)`, `int Count`, `bool IsEmpty`, `void Flush(Action<TileChunk2D> rebuild)`
  - `void SideScrollerChunkStreamer2D.Invalidate(TileChunk2D chunk)`
  - `void SideScrollerLevel2D.FlushDirtyChunks()`

- [ ] **Step 1: Write the failing tests**

Create `App2d.Tests/Tiles/DirtyChunkTracker2DTests.cs`:

```csharp
using App2d.Tiles;

namespace App2d.Tests.Tiles;

public sealed class DirtyChunkTracker2DTests
{
    [Fact]
    public void MarkingTheSameChunkTwiceTracksItOnce()
    {
        var tracker = new DirtyChunkTracker2D();

        tracker.Mark(new TileChunk2D(1, 1));
        tracker.Mark(new TileChunk2D(1, 1));

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void FlushRebuildsEachMarkedChunkOnceThenEmpties()
    {
        var tracker = new DirtyChunkTracker2D();
        tracker.Mark(new TileChunk2D(0, 0));
        tracker.Mark(new TileChunk2D(1, 0));
        tracker.Mark(new TileChunk2D(0, 0));

        var rebuilt = new List<TileChunk2D>();
        tracker.Flush(rebuilt.Add);

        Assert.Equal(2, rebuilt.Count);
        Assert.Contains(new TileChunk2D(0, 0), rebuilt);
        Assert.Contains(new TileChunk2D(1, 0), rebuilt);
        Assert.True(tracker.IsEmpty);
    }

    [Fact]
    public void FlushingTwiceDoesNothingTheSecondTime()
    {
        var tracker = new DirtyChunkTracker2D();
        tracker.Mark(new TileChunk2D(2, 1));
        tracker.Flush(_ => { });

        var rebuilt = new List<TileChunk2D>();
        tracker.Flush(rebuilt.Add);

        Assert.Empty(rebuilt);
    }

    [Fact]
    public void PaintingAcrossAChunkBorderCoalescesIntoTheAffectedChunks()
    {
        // 8x8 tiles at chunk size 4 -> a 2x2 chunk grid. Painting tile (3,3) is a
        // chunk corner, so EditableTileMap2D raises ChunkChanged for all four chunks.
        var map = new EditableTileMap2D(8, 8, 32f, 4);
        var tracker = new DirtyChunkTracker2D();
        map.ChunkChanged += tracker.Mark;

        map.SetTileKind(3, 3, TileKind2D.Solid);
        map.SetTileKind(4, 3, TileKind2D.Solid);

        // Both tiles sit on the same chunk seam, so the dirty set stays at four
        // chunks no matter how many events fired.
        Assert.Equal(4, tracker.Count);
    }

    [Fact]
    public void FlushDuringRebuildDoesNotLoseChunksMarkedByTheRebuild()
    {
        var tracker = new DirtyChunkTracker2D();
        tracker.Mark(new TileChunk2D(0, 0));

        var rebuilt = new List<TileChunk2D>();
        tracker.Flush(chunk =>
        {
            rebuilt.Add(chunk);
            if (chunk == new TileChunk2D(0, 0))
                tracker.Mark(new TileChunk2D(5, 5));
        });

        Assert.Single(rebuilt);
        Assert.Equal(1, tracker.Count);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~DirtyChunkTracker2D"`
Expected: FAIL — compile error, `DirtyChunkTracker2D` does not exist.

- [ ] **Step 3: Implement the tracker**

Create `App2d.Tiles/DirtyChunkTracker2D.cs`. Note the flush drains into a buffer first, so a rebuild that marks new chunks does not mutate the set being iterated or lose that mark.

```csharp
using App2d.Core;

namespace App2d.Tiles;

/// <summary>
/// Accumulates chunks whose tiles or appearance changed, so a burst of edits rebuilds
/// each affected chunk at most once.
/// </summary>
/// <remarks>
/// Painting raises <see cref="EditableTileMap2D.ChunkChanged"/> up to four times per tile
/// (terrain visuals sample across chunk borders) and continuously while dragging. Rebuilding
/// per event would destroy and recreate a chunk's physics bodies and visuals many times in
/// one frame, so edits are coalesced and flushed once per frame instead.
/// </remarks>
public sealed class DirtyChunkTracker2D
{
    private readonly HashSet<TileChunk2D> _dirty = [];
    private readonly List<TileChunk2D> _flushBuffer = [];

    public int Count => _dirty.Count;
    public bool IsEmpty => _dirty.Count == 0;

    public void Mark(TileChunk2D chunk) => _dirty.Add(chunk);

    public void Flush(Action<TileChunk2D> rebuild)
    {
        ArgGuard.ThrowIfNull(rebuild);
        if (_dirty.Count == 0)
            return;

        // Drain before rebuilding: a rebuild may mark further chunks, and those must
        // survive for the next flush rather than corrupt this iteration.
        _flushBuffer.Clear();
        _flushBuffer.AddRange(_dirty);
        _dirty.Clear();

        foreach (var chunk in _flushBuffer)
            rebuild(chunk);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~DirtyChunkTracker2D"`
Expected: PASS — 5 tests.

- [ ] **Step 5: Add `Invalidate` to the streamer**

In `App2d.Gameplay/World/SideScrollerChunkStreamer2D.cs`, add after `IsChunkActive`:

```csharp
    /// <summary>
    /// Rebuilds a chunk whose tiles changed. Does nothing when the chunk is not loaded —
    /// loading it later reads the current map anyway.
    /// </summary>
    public void Invalidate(TileChunk2D chunk)
    {
        if (!_loadedChunks.ContainsKey(chunk))
            return;

        Unload(chunk);
        Load(chunk);
    }
```

- [ ] **Step 6: Wire the tracker into the level**

In `App2d.Gameplay/World/SideScrollerLevel2D.cs`, add the field next to the other private fields:

```csharp
    private readonly DirtyChunkTracker2D _dirtyChunks = new();
```

In the constructor, after `TileMap = tileMap;`, subscribe when the injected map is editable:

```csharp
        // Only an editable map can change under us. A read-only map never raises the event.
        if (tileMap is EditableTileMap2D editable)
            editable.ChunkChanged += _dirtyChunks.Mark;
```

Add the flush method next to `UpdateStreaming`:

```csharp
    /// <summary>
    /// Rebuilds every chunk marked dirty since the last call. Cheap when nothing changed.
    /// </summary>
    public void FlushDirtyChunks()
    {
        if (_dirtyChunks.IsEmpty)
            return;

        var environment = RequireEnvironment();
        _dirtyChunks.Flush(environment.Streamer.Invalidate);
    }
```

Add `using App2d.Tiles;` if it is not already present.

- [ ] **Step 7: Build and run the full suite**

Run: `dotnet build App2d.slnx` then `dotnet test App2d.slnx`
Expected: build clean, all tests pass. Nothing calls `FlushDirtyChunks` yet — that is Task 4.

- [ ] **Step 8: Commit**

```bash
git add App2d.Tiles App2d.Tests App2d.Gameplay
git commit -m "Coalesce dirty chunks and rebuild them on demand"
```

---

### Task 3: The stroke and undo model

The editable core of the editor, kept in `App2d.Tiles` because the host has no test project. It knows about tiles and chunks; it knows nothing about input, cameras, rendering or SQLite.

**Files:**
- Create: `App2d.Tiles/TileEditSession2D.cs`
- Test: `App2d.Tests/Tiles/TileEditSession2DTests.cs`

**Interfaces:**
- Consumes: `EditableTileMap2D` (`SetTileKind`, `GetTileKind`, `TileToChunk`, `Width`, `Height`), `TileKind2D`, `TileChunk2D`.
- Produces:
  - `TileEditSession2D(EditableTileMap2D map)`
  - `bool IsStrokeActive { get; }`
  - `int UndoCount { get; }`
  - `void BeginStroke()`
  - `void Paint(int x, int y, TileKind2D kind)` — ignores out-of-bounds and no-op writes
  - `void PaintLine(int fromX, int fromY, int toX, int toY, TileKind2D kind)`
  - `IReadOnlyCollection<TileChunk2D> EndStroke()` — the chunks whose **data** changed, empty if nothing changed
  - `IReadOnlyCollection<TileChunk2D> Undo()` — replays the previous values through `Paint`

- [ ] **Step 1: Write the failing tests**

Create `App2d.Tests/Tiles/TileEditSession2DTests.cs`:

```csharp
using App2d.Tiles;

namespace App2d.Tests.Tiles;

public sealed class TileEditSession2DTests
{
    private static EditableTileMap2D NewMap() => new(16, 16, 32f, 4);

    [Fact]
    public void PaintingInsideAStrokeChangesTheMap()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(2, 3, TileKind2D.Solid);
        var chunks = session.EndStroke();

        Assert.Equal(TileKind2D.Solid, map.GetTileKind(2, 3));
        Assert.Equal(new TileChunk2D(0, 0), Assert.Single(chunks));
    }

    [Fact]
    public void EndStrokeReportsOnlyChunksWhoseDataChanged()
    {
        // Tile (3,3) is a chunk corner, so the map raises ChunkChanged for 4 chunks —
        // but only chunk (0,0) actually owns the changed tile. Persistence must write
        // one chunk, not four; the extra events exist for visual invalidation only.
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(3, 3, TileKind2D.Solid);
        var chunks = session.EndStroke();

        Assert.Equal(new TileChunk2D(0, 0), Assert.Single(chunks));
    }

    [Fact]
    public void PaintingTheSameKindIsNotRecorded()
    {
        var map = NewMap();
        map.SetTileKind(1, 1, TileKind2D.Solid);
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(1, 1, TileKind2D.Solid);
        var chunks = session.EndStroke();

        Assert.Empty(chunks);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact]
    public void OutOfBoundsPaintingIsIgnored()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(-1, 0, TileKind2D.Solid);
        session.Paint(0, 99, TileKind2D.Solid);
        var chunks = session.EndStroke();

        Assert.Empty(chunks);
    }

    [Fact]
    public void UndoRestoresEveryTileTheStrokeChanged()
    {
        var map = NewMap();
        map.SetTileKind(5, 5, TileKind2D.OneWay);
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(5, 5, TileKind2D.Solid);
        session.Paint(6, 5, TileKind2D.Solid);
        session.EndStroke();

        session.Undo();

        Assert.Equal(TileKind2D.OneWay, map.GetTileKind(5, 5));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(6, 5));
        Assert.Equal(0, session.UndoCount);
    }

    [Fact]
    public void UndoReturnsTheSameChunksThePaintingDirtied()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(3, 3, TileKind2D.Solid);
        session.Paint(4, 4, TileKind2D.Solid);
        var painted = session.EndStroke();

        var undone = session.Undo();

        Assert.Equal(painted.OrderBy(c => c.X).ThenBy(c => c.Y), undone.OrderBy(c => c.X).ThenBy(c => c.Y));
    }

    [Fact]
    public void UndoWithNothingRecordedIsANoOp()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        Assert.Empty(session.Undo());
    }

    [Fact]
    public void UndoUnwindsStrokesInReverseOrder()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(1, 1, TileKind2D.Solid);
        session.EndStroke();

        session.BeginStroke();
        session.Paint(1, 1, TileKind2D.Spikes);
        session.EndStroke();

        session.Undo();
        Assert.Equal(TileKind2D.Solid, map.GetTileKind(1, 1));

        session.Undo();
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(1, 1));
    }

    [Fact]
    public void PaintLineFillsEveryTileBetweenTheEndpoints()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.PaintLine(0, 0, 4, 0, TileKind2D.Solid);
        session.EndStroke();

        for (var x = 0; x <= 4; x++)
            Assert.Equal(TileKind2D.Solid, map.GetTileKind(x, 0));
    }

    [Fact]
    public void PaintLineHandlesDiagonalsWithoutGaps()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.PaintLine(0, 0, 3, 3, TileKind2D.Solid);
        session.EndStroke();

        Assert.Equal(TileKind2D.Solid, map.GetTileKind(0, 0));
        Assert.Equal(TileKind2D.Solid, map.GetTileKind(3, 3));
        // Every step is orthogonally or diagonally adjacent: no skipped tiles.
        Assert.Equal(TileKind2D.Solid, map.GetTileKind(1, 1));
        Assert.Equal(TileKind2D.Solid, map.GetTileKind(2, 2));
    }

    [Fact]
    public void PaintOutsideAStrokeThrows()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        Assert.Throws<InvalidOperationException>(() => session.Paint(0, 0, TileKind2D.Solid));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~TileEditSession2D"`
Expected: FAIL — compile error, `TileEditSession2D` does not exist.

- [ ] **Step 3: Implement the session**

Create `App2d.Tiles/TileEditSession2D.cs`:

```csharp
using App2d.Core;

namespace App2d.Tiles;

/// <summary>
/// The editable core of the tile painter: strokes, undo, and which chunks a stroke's data
/// touched. Knows nothing about input, cameras, rendering or storage.
/// </summary>
public sealed class TileEditSession2D
{
    private readonly EditableTileMap2D _map;
    private readonly List<TileEdit2D> _currentStroke = [];
    private readonly List<TileEdit2D[]> _undoStack = [];

    public TileEditSession2D(EditableTileMap2D map) => _map = ArgGuard.RequireNotNull(map);

    public bool IsStrokeActive { get; private set; }
    public int UndoCount => _undoStack.Count;

    public void BeginStroke()
    {
        _currentStroke.Clear();
        IsStrokeActive = true;
    }

    /// <summary>
    /// Paints one tile. Out-of-bounds coordinates and writes that would not change the
    /// tile are ignored, so a drag over unchanged tiles records nothing to undo.
    /// </summary>
    public void Paint(int x, int y, TileKind2D kind)
    {
        StateGuard.ThrowIf(!IsStrokeActive, "Begin a stroke before painting.");

        if (x < 0 || x >= _map.Width || y < 0 || y >= _map.Height)
            return;

        var before = _map.GetTileKind(x, y);
        if (before == kind)
            return;

        _map.SetTileKind(x, y, kind);
        _currentStroke.Add(new TileEdit2D(x, y, before, kind));
    }

    /// <summary>
    /// Paints every tile on the line between two tile coordinates, so a fast mouse drag
    /// leaves no gaps between sampled positions.
    /// </summary>
    public void PaintLine(int fromX, int fromY, int toX, int toY, TileKind2D kind)
    {
        StateGuard.ThrowIf(!IsStrokeActive, "Begin a stroke before painting.");

        var deltaX = Math.Abs(toX - fromX);
        var deltaY = Math.Abs(toY - fromY);
        var stepX = fromX < toX ? 1 : -1;
        var stepY = fromY < toY ? 1 : -1;
        var error = deltaX - deltaY;

        var x = fromX;
        var y = fromY;
        while (true)
        {
            Paint(x, y, kind);
            if (x == toX && y == toY)
                return;

            var doubledError = error * 2;
            if (doubledError > -deltaY)
            {
                error -= deltaY;
                x += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                y += stepY;
            }
        }
    }

    /// <summary>
    /// Ends the stroke and returns the chunks whose tile DATA changed — the chunks that
    /// must be persisted. This is deliberately narrower than the chunks whose appearance
    /// changed: painting near a chunk border dirties neighbours visually, but their stored
    /// tiles are untouched.
    /// </summary>
    public IReadOnlyCollection<TileChunk2D> EndStroke()
    {
        StateGuard.ThrowIf(!IsStrokeActive, "No stroke is in progress.");
        IsStrokeActive = false;

        if (_currentStroke.Count == 0)
            return [];

        _undoStack.Add([.. _currentStroke]);
        var chunks = CollectChunks(_currentStroke);
        _currentStroke.Clear();
        return chunks;
    }

    /// <summary>
    /// Reverts the most recent stroke by replaying its previous values through the same
    /// paint path, so invalidation and persistence behave identically to painting.
    /// </summary>
    public IReadOnlyCollection<TileChunk2D> Undo()
    {
        StateGuard.ThrowIf(IsStrokeActive, "Finish the stroke before undoing.");
        if (_undoStack.Count == 0)
            return [];

        var stroke = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        BeginStroke();
        for (var index = stroke.Length - 1; index >= 0; index--)
        {
            var edit = stroke[index];
            Paint(edit.X, edit.Y, edit.Before);
        }

        IsStrokeActive = false;
        var chunks = CollectChunks(_currentStroke);
        _currentStroke.Clear();
        return chunks;
    }

    private HashSet<TileChunk2D> CollectChunks(List<TileEdit2D> edits)
    {
        var chunks = new HashSet<TileChunk2D>();
        foreach (var edit in edits)
            chunks.Add(_map.TileToChunk(edit.X, edit.Y));
        return chunks;
    }

    private readonly record struct TileEdit2D(int X, int Y, TileKind2D Before, TileKind2D After);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~TileEditSession2D"`
Expected: PASS — 11 tests.

- [ ] **Step 5: Commit**

```bash
git add App2d.Tiles App2d.Tests
git commit -m "Add tile edit session with strokes and undo"
```

---

### Task 4: Editor mode, input, and the frozen game loop

The host half: mode toggle, mouse and key handling, free camera, write-through, and the branch that freezes the simulation.

**Files:**
- Create: `App2d/Editor/TileEditor2D.cs`
- Modify: `App2d/SideScrollerGame.cs`

**Interfaces:**
- Consumes: `TileEditSession2D` (Task 3), `LevelBootstrap2D.OpenForEditing()` (Task 1), `SideScrollerLevel2D.FlushDirtyChunks()` (Task 2), `LevelDatabase2D.SaveChunks(EditableTileMap2D, ReadOnlySpan<TileChunk2D>)`, `Camera2D` (`Position`, `Zoom`, `DeviceToWorld`), `InputState`.
- Produces:
  - `TileEditor2D(EditableTileMap2D map, LevelDatabase2D database, Camera2D camera, Vector2 origin, float tileSize)`
  - `bool IsActive { get; }`
  - `TileKind2D SelectedKind { get; }`
  - `Vector2 CameraFocus { get; }`
  - `bool TryGetHoveredTile(out int x, out int y)`
  - `void Update(InputState input)`
  - `void Dispose()`

- [ ] **Step 1: Write the editor**

Create `App2d/Editor/TileEditor2D.cs`. Namespace is `App2d.Editor` (the host uses folder-matching namespaces — `App2d/Diagnostics` declares `namespace App2d.Diagnostics`).

```csharp
using App2d.Levels;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Editor;

/// <summary>
/// The in-game tile painter. Owns editor mode, tool state, the free camera, and
/// write-through persistence; delegates tile mutation and undo to <see cref="TileEditSession2D"/>.
/// </summary>
internal sealed class TileEditor2D : IDisposable
{
    private const float ZoomStep = 1.1f;

    private static readonly TileKind2D[] SelectableKinds =
    [
        TileKind2D.Empty,
        TileKind2D.Solid,
        TileKind2D.OneWay,
        TileKind2D.Solid | TileKind2D.Grippable,
        TileKind2D.Spikes
    ];

    private readonly EditableTileMap2D _map;
    private readonly LevelDatabase2D _database;
    private readonly Camera2D _camera;
    private readonly TileEditSession2D _session;
    private readonly Vector2 _origin;
    private readonly float _tileSize;

    private Vector2 _cameraFocus;
    private Vector2 _panAnchorDevice;
    private Vector2 _panAnchorFocus;
    private bool _isPanning;
    private Vector2 _lastMouseDevice;
    private int _lastPaintedX;
    private int _lastPaintedY;
    private bool _hasLastPainted;

    public TileEditor2D(
        EditableTileMap2D map,
        LevelDatabase2D database,
        Camera2D camera,
        Vector2 origin,
        float tileSize)
    {
        _map = map;
        _database = database;
        _camera = camera;
        _origin = origin;
        _tileSize = tileSize;
        _session = new TileEditSession2D(map);
        SelectedKind = TileKind2D.Solid;
    }

    public bool IsActive { get; private set; }
    public TileKind2D SelectedKind { get; private set; }
    public Vector2 CameraFocus => _cameraFocus;

    public bool TryGetHoveredTile(out int x, out int y)
    {
        var world = _camera.DeviceToWorld(_lastMouseDevice);
        x = (int)MathF.Floor((world.X - _origin.X) / _tileSize);
        y = (int)MathF.Floor((world.Y - _origin.Y) / _tileSize);
        return x >= 0 && x < _map.Width && y >= 0 && y < _map.Height;
    }

    public void Update(InputState input)
    {
        if (input.WasKeyPressed(Keys.F1))
        {
            IsActive = !IsActive;
            if (IsActive)
                _cameraFocus = _camera.Position;
            else
                EndStrokeIfActive();
        }

        if (!IsActive)
            return;

        _lastMouseDevice = input.MousePositionDevice;
        UpdateKindSelection(input);
        UpdateCamera(input);
        UpdatePainting(input);

        if (input.IsControlDown && input.WasKeyPressed(Keys.Z))
            CommitChunks(_session.Undo());

        _camera.Position = _cameraFocus;
    }

    private void UpdateKindSelection(InputState input)
    {
        for (var index = 0; index < SelectableKinds.Length; index++)
        {
            if (input.WasKeyPressed(Keys.D1 + index))
                SelectedKind = SelectableKinds[index];
        }
    }

    private void UpdateCamera(InputState input)
    {
        if (input.WasMousePressed(MouseButtons.Middle))
        {
            _isPanning = true;
            _panAnchorDevice = input.MousePositionDevice;
            _panAnchorFocus = _cameraFocus;
        }

        if (input.WasMouseReleased(MouseButtons.Middle))
            _isPanning = false;

        if (_isPanning)
        {
            var deviceDelta = input.MousePositionDevice - _panAnchorDevice;
            _cameraFocus = _panAnchorFocus - deviceDelta / _camera.Zoom;
        }

        if (input.MouseWheelDelta != 0f)
        {
            // Camera2D.Zoom clamps to its own MinZoom/MaxZoom on assignment; do not
            // add a second clamp here with different bounds.
            var factor = input.MouseWheelDelta > 0f ? ZoomStep : 1f / ZoomStep;
            _camera.Zoom *= factor;
        }
    }

    private void UpdatePainting(InputState input)
    {
        var isPainting = input.IsMouseDown(MouseButtons.Left);
        var isErasing = input.IsMouseDown(MouseButtons.Right);

        if (input.WasMousePressed(MouseButtons.Left) || input.WasMousePressed(MouseButtons.Right))
        {
            _session.BeginStroke();
            _hasLastPainted = false;
        }

        if ((isPainting || isErasing) && _session.IsStrokeActive && TryGetHoveredTile(out var x, out var y))
        {
            var kind = isErasing ? TileKind2D.Empty : SelectedKind;
            if (_hasLastPainted)
                _session.PaintLine(_lastPaintedX, _lastPaintedY, x, y, kind);
            else
                _session.Paint(x, y, kind);

            _lastPaintedX = x;
            _lastPaintedY = y;
            _hasLastPainted = true;
        }

        if (input.WasMouseReleased(MouseButtons.Left) || input.WasMouseReleased(MouseButtons.Right))
            EndStrokeIfActive();
    }

    private void EndStrokeIfActive()
    {
        if (!_session.IsStrokeActive)
            return;

        CommitChunks(_session.EndStroke());
        _hasLastPainted = false;
    }

    /// <summary>Writes a stroke's changed chunks in one transaction.</summary>
    private void CommitChunks(IReadOnlyCollection<TileChunk2D> chunks)
    {
        if (chunks.Count == 0)
            return;

        _database.SaveChunks(_map, chunks.ToArray());
    }

    public void Dispose() => _database.Dispose();
}
```

- [ ] **Step 2: Wire it into the game**

In `App2d/SideScrollerGame.cs`, add `using App2d.Editor;` and a field beside the other readonly fields:

```csharp
    private readonly TileEditor2D _editor;
```

In the constructor, replace the `var tileMap = LevelBootstrap2D.LoadOrBake(Traversal);` line's block so the concrete map is kept, and construct the editor after `_cameraController`:

```csharp
        var tileMap = LevelBootstrap2D.LoadOrBake(Traversal);
        var groundHeights = TileGroundHeights2D.Derive(tileMap);

        _level = new SideScrollerLevel2D(
            Traversal,
            tileMap,
            x => groundHeights[Math.Clamp(x, 0, groundHeights.Length - 1)]);
        _cameraController = new SideScrollerCamera2D(Scene, Camera, _level.TileMap.WorldBounds, _level.SpawnPoint, _level.GetCameraFloorY);
        _editor = new TileEditor2D(
            tileMap,
            LevelBootstrap2D.OpenForEditing(),
            Camera,
            tileMap.Origin,
            Traversal.TileSize);
```

Replace the opening of `Update` (currently `_level.UpdateStreaming(_player.Position);`) with the editor branch:

```csharp
    public override void Update(FrameTime time, InputState input)
    {
        var dt = time.DeltaSeconds;

        _editor.Update(input);
        if (_editor.IsActive)
        {
            // Stream around the free camera, not the frozen player, or panning away
            // would paint into chunks that never load.
            _level.UpdateStreaming(_editor.CameraFocus);
            _level.FlushDirtyChunks();
            return;
        }

        _level.UpdateStreaming(_player.Position);
        _level.UpdateMovingPlatforms(dt);
```

Leave the rest of `Update` exactly as it is.

Add the editor to `Dispose`:

```csharp
    public override void Dispose()
    {
        _editor.Dispose();
        _playerPresentation.Dispose();
        _sounds.Dispose();
        base.Dispose();
    }
```

- [ ] **Step 3: Build and run the suite**

Run: `dotnet build App2d.slnx` then `dotnet test App2d.slnx`
Expected: build clean, all tests pass (this task adds no tests; the host has no test project).

- [ ] **Step 4: Playtest — this is the real verification**

Close any running instance first, then run the game and check each of these:

1. Press `F1`. The player stops moving and animating; the game is frozen.
2. Left-click on empty space near the ground. A solid tile appears **immediately**, with correct edges against its neighbours.
3. Drag quickly across several tiles. A continuous line appears with no gaps.
4. Paint a tile exactly on a chunk seam (chunks are 32 tiles; a seam is at world tile x = 32, 64, ...). The neighbouring chunk's edge art updates too — no stale seam.
5. Right-click a solid tile. It erases.
6. Press `1`–`5` and paint. The kind changes (Empty, Solid, OneWay, Grippable, Spikes).
7. Middle-drag to pan away from the player, then paint. Terrain there loads and paints correctly.
8. Wheel zooms in and out.
9. `Ctrl+Z` undoes the last stroke, restoring the previous tiles.
10. Press `F1` again. The player resumes **exactly where it stood**, and can stand on a ledge you painted.
11. Close the game, relaunch, and confirm your edits are still there.

Record which of these pass. If any fail, fix before committing.

- [ ] **Step 5: Commit**

```bash
git add App2d
git commit -m "Add the in-game tile painter"
```

---

### Task 5: The editor overlay

Without a cursor you are painting blind. This draws what is about to happen.

**Files:**
- Create: `App2d/Editor/TileEditorView2D.cs`
- Modify: `App2d/SideScrollerGame.cs` (the `Render` method)

**Interfaces:**
- Consumes: `TileEditor2D` (`IsActive`, `SelectedKind`, `TryGetHoveredTile`), `Renderer2D` (`DrawWorldPolyline`, `DrawScreenLabel`), `TileKind2D`.
- Produces: `static void TileEditorView2D.Draw(Renderer2D renderer, TileEditor2D editor, Vector2 origin, float tileSize)`

- [ ] **Step 1: Write the overlay**

Create `App2d/Editor/TileEditorView2D.cs`:

```csharp
using App2d.Rendering;
using App2d.Tiles;
using SkiaSharp;
using System.Numerics;

namespace App2d.Editor;

/// <summary>Draws the tile cursor and editor status while editor mode is active.</summary>
internal static class TileEditorView2D
{
    private static readonly SKColor CursorColor = new(255, 214, 64);

    public static void Draw(Renderer2D renderer, TileEditor2D editor, Vector2 origin, float tileSize)
    {
        if (!editor.IsActive)
            return;

        var hasTile = editor.TryGetHoveredTile(out var tileX, out var tileY);
        if (hasTile)
        {
            var min = origin + new Vector2(tileX, tileY) * tileSize;
            var max = min + new Vector2(tileSize);
            Span<Vector2> outline =
            [
                new(min.X, min.Y),
                new(max.X, min.Y),
                new(max.X, max.Y),
                new(min.X, max.Y),
                new(min.X, min.Y)
            ];
            renderer.DrawWorldPolyline(outline, CursorColor, strokeWidth: 2f);
        }

        var position = hasTile ? $"{tileX}, {tileY}" : "outside";
        renderer.DrawScreenLabel(
            $"EDIT  kind: {Describe(editor.SelectedKind)}  tile: {position}  [F1] play  [1-5] kind  [RMB] erase  [Ctrl+Z] undo",
            new Vector2(16f, 16f));
    }

    private static string Describe(TileKind2D kind) => kind switch
    {
        TileKind2D.Empty => "empty",
        TileKind2D.Solid => "solid",
        TileKind2D.OneWay => "one-way",
        TileKind2D.Spikes => "spikes",
        _ when kind.IsGrippable() => "grippable",
        _ => kind.ToString()
    };
}
```

- [ ] **Step 2: Draw it**

In `App2d/SideScrollerGame.cs`, add to the end of `Render`:

```csharp
        TileEditorView2D.Draw(renderer, _editor, _level.TileMap.Origin, _level.TileMap.TileSize);
```

- [ ] **Step 3: Build and playtest**

Run: `dotnet build App2d.slnx`, then run the game.

Confirm: pressing `F1` shows the status line and a yellow outline that tracks the hovered tile; the outline lands on the tile that actually gets painted when you click; the kind name changes with `1`–`5`; the overlay disappears when you leave editor mode.

- [ ] **Step 4: Commit**

```bash
git add App2d
git commit -m "Draw the tile editor cursor and status"
```

---

### Task 6: Document the editor

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-08-30-tile-map-editor-design.md`

- [ ] **Step 1: Document the editor in `README.md`**

Find the section describing the tile modules (it mentions `EditableTileMap2D` and chunk streaming) and add after it:

```markdown
Editor mode is part of the game rather than a separate tool. `F1` freezes the simulation,
detaches the camera, and paints tiles with the mouse; `1`-`5` pick the tile kind, the right
button erases, and `Ctrl+Z` undoes a stroke. Painting mutates the loaded `EditableTileMap2D`,
whose `ChunkChanged` event feeds a `DirtyChunkTracker2D`; the editor flushes that tracker once
per frame so a drag rebuilds each affected chunk at most once instead of once per event. Each
stroke commits its changed chunks to the level file in a single transaction. `TileEditSession2D`
holds the strokes and undo history and lives in `App2d.Tiles`, so the editable core is testable
without input or storage.
```

- [ ] **Step 2: Mark phase 2 done in the original spec**

In `docs/superpowers/specs/2026-08-30-tile-map-editor-design.md`, replace the heading line `## Phase 2 — the tile painter (sketch)` with:

```markdown
## Phase 2 — the tile painter (implemented)

**Superseded by `docs/superpowers/specs/2026-08-30-tile-painter-phase2-design.md`**, which
expanded this sketch into the built design. The sketch below is kept for the reasoning.
```

- [ ] **Step 3: Commit**

```bash
git add README.md docs
git commit -m "Document the in-game tile painter"
```

---

## Definition of done

- `dotnet build App2d.slnx` clean and `dotnet test App2d.slnx` green.
- `git status --short -- Assets/Static/levels/cavern/level.db` is empty after launching and closing the game.
- `F1` toggles a frozen editor; painting, erasing, kind selection, panning, zooming and undo all work.
- Painting on a chunk seam updates the neighbouring chunk's art with no stale edge.
- Edits survive a restart.
- `App2d.Gameplay` still has no reference to `App2d.Levels`; `App2d.Tiles` still has no SQLite reference.
