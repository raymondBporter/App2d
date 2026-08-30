# Tile Map Levels — Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make an authored SQLite level file the source of truth for terrain, replacing runtime procedural generation.

**Architecture:** A new `EditableTileMap2D` (dense, mutable, kind-carrying) replaces the read-only `ProceduralTileMap2D` as the single `IChunkedTileMap2D` implementation. A new `App2d.Levels` project stores that map in SQLite — tiles as run-length-encoded per-chunk blobs. `SideScrollerLevel2D` stops constructing `JumpableWorldGenerator2D` and takes an injected map plus a ground-height lookup; the host bakes the generator's output to a `.db` once, then loads it forever after.

**Tech Stack:** C# / .NET 10, xunit 2.9.3, `Microsoft.Data.Sqlite`, SkiaSharp (host only).

**Spec:** `docs/superpowers/specs/2026-08-30-tile-map-editor-design.md`

## Global Constraints

- **Naming:** every public type ends in `2D`. This is universal in this codebase — follow it.
- **Validation:** use `App2d.Core.ArgGuard` for argument checks (`ThrowIfNotPositive`, `ThrowIfNull`, `RequireNotNull`, `ThrowIfNotFinite`, `ThrowOutOfRange`) and `StateGuard` for invalid-state checks. Do not hand-roll `throw new ArgumentException`.
- **Target frameworks:** `App2d.Core`, `App2d.Collision`, `App2d.Tiles`, `App2d.Physics`, `App2d.Rendering`, `App2d.Tests` are plain `net10.0`. `App2d.Gameplay`, `App2d.Gameplay.Tests`, `App2d` are `net10.0-windows10.0.19041.0`. **`App2d.Levels` must be plain `net10.0`.**
- **Dependency limits:** `Microsoft.Data.Sqlite` may be referenced **only** by `App2d.Levels` (and test projects). `App2d.Gameplay` must NOT reference `App2d.Levels`.
- **Nullable and implicit usings are enabled** in every project; `AnalysisModePerformance` is `All` in engine projects, so avoid obvious per-frame allocations.
- **Level file path:** `Assets/Static/levels/cavern/level.db`. Never write durable files into `Assets/Runtime` — it is generated and disposable.
- **World constants (must not change):** width 640, height 96, chunk size 32, origin `(-512, -640)`, seed `0xA2D_2026_0823UL`. Tile size comes from `TraversalMetrics2D.TileSize`.
- **Ground height is provisional.** Do not add pit-detection or fidelity logic. A clamp is the entire mitigation.
- **Build/test:** `dotnet build App2d.slnx` and `dotnet test App2d.slnx`.

---

### Task 1: `EditableTileMap2D` replaces `ProceduralTileMap2D`

A dense, mutable, kind-carrying `IChunkedTileMap2D`. `ProceduralTileMap2D` has exactly one production consumer (`SideScrollerLevel2D:38`), so this replaces it rather than joining it.

**Files:**
- Create: `App2d.Tiles/EditableTileMap2D.cs`
- Delete: `App2d.Tiles/ProceduralTileMap2D.cs` (keep `TileChunk2D` — move it, see Step 3)
- Modify: `App2d.Gameplay/World/SideScrollerLevel2D.cs:38-44`
- Test: `App2d.Tests/Tiles/EditableTileMap2DTests.cs` (renamed from `ProceduralTileMap2DTests.cs`)
- Modify: `App2d.Tests/Tiles/TileMeshingCharacterizationTests.cs:22-37`

**Interfaces:**
- Consumes: `IChunkedTileMap2D`, `TileKind2D`, `TileCollisionRectangle2D`, `TileRectangleMesher2D.Mesh`, `TileCellRectangle2D` (all existing in `App2d.Tiles`).
- Produces:
  - `EditableTileMap2D(int width, int height, float tileSize, int chunkSize, Vector2 origin = default)`
  - `void SetTileKind(int x, int y, TileKind2D kind)`
  - `void Fill(Func<int, int, TileKind2D> source)`
  - `TileChunk2D TileToChunk(int x, int y)`
  - `int ChunkWidth(int chunkX)` / `int ChunkHeight(int chunkY)` — clipped extents
  - `ReadOnlySpan<TileKind2D> GetChunkTiles(TileChunk2D chunk, Span<TileKind2D> destination)`
  - `void SetChunkTiles(TileChunk2D chunk, ReadOnlySpan<TileKind2D> source)`
  - `event Action<TileChunk2D>? ChunkChanged`
  - plus the full `IChunkedTileMap2D` surface.

- [ ] **Step 1: Write the failing tests**

Rename `App2d.Tests/Tiles/ProceduralTileMap2DTests.cs` to `EditableTileMap2DTests.cs` and replace its contents. The first three tests are the existing ones retargeted (same assertions, seeded via `Fill` instead of a constructor `Func`); the rest are new.

```csharp
using App2d.Tiles;
using System.Numerics;

namespace App2d.Tests.Tiles;

public sealed class EditableTileMap2DTests
{
    private static EditableTileMap2D Seed(
        int width,
        int height,
        float tileSize,
        int chunkSize,
        Func<int, int, TileKind2D> source)
    {
        var map = new EditableTileMap2D(width, height, tileSize, chunkSize);
        map.Fill(source);
        return map;
    }

    [Fact]
    public void GrippableFlagKeepsTileSolidAndReachesMergedCollision()
    {
        const TileKind2D grippableSolid = TileKind2D.Solid | TileKind2D.Grippable;
        var map = Seed(3, 1, 32f, 3, (x, _) => x < 2 ? grippableSolid : TileKind2D.Solid);

        Assert.True(map.IsSolid(0, 0));
        Assert.True(map.GetTileKind(0, 0).IsGrippable());

        var rectangles = map.BuildCollisionRectangles(new TileChunk2D(0, 0));
        Assert.Equal(2, rectangles.Count);
        var grippable = Assert.Single(rectangles, rectangle => rectangle.Kind.IsGrippable());
        Assert.Equal(grippableSolid, grippable.Kind);
        Assert.Equal(64f, grippable.Bounds.Size.X);
    }

    [Fact]
    public void ModifierFlagAloneDoesNotCreateCollision()
    {
        var map = Seed(1, 1, 32f, 1, (_, _) => TileKind2D.Grippable);

        Assert.False(map.IsSolid(0, 0));
        Assert.Empty(map.BuildCollisionRectangles(new TileChunk2D(0, 0)));
    }

    [Fact]
    public void SpikeTileIsHazardWithoutBecomingSolidCollision()
    {
        var map = Seed(1, 1, 32f, 1, (_, _) => TileKind2D.Spikes);

        Assert.True(map.GetTileKind(0, 0).IsSpikes());
        Assert.False(map.IsSolid(0, 0));
        Assert.Empty(map.BuildCollisionRectangles(new TileChunk2D(0, 0)));
    }

    [Fact]
    public void SetTileKindRoundTrips()
    {
        var map = new EditableTileMap2D(4, 4, 32f, 2);

        map.SetTileKind(2, 3, TileKind2D.OneWay);

        Assert.Equal(TileKind2D.OneWay, map.GetTileKind(2, 3));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(2, 2));
    }

    [Fact]
    public void OutOfBoundsReadsReturnEmptyInsteadOfThrowing()
    {
        var map = new EditableTileMap2D(4, 4, 32f, 2);
        map.Fill((_, _) => TileKind2D.Solid);

        Assert.Equal(TileKind2D.Empty, map.GetTileKind(-1, 0));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(0, -1));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(4, 0));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(0, 4));
        Assert.False(map.IsSolid(99, 99));
    }

    [Fact]
    public void SetTileKindOutsideMapThrows()
    {
        var map = new EditableTileMap2D(4, 4, 32f, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetTileKind(4, 0, TileKind2D.Solid));
    }

    [Fact]
    public void ChunkChangedFiresForTheOwningChunkOnlyWhenTheKindChanges()
    {
        var map = new EditableTileMap2D(8, 8, 32f, 4);
        var changed = new List<TileChunk2D>();
        map.ChunkChanged += chunk => changed.Add(chunk);

        map.SetTileKind(5, 6, TileKind2D.Solid);
        map.SetTileKind(5, 6, TileKind2D.Solid);

        Assert.Single(changed);
        Assert.Equal(new TileChunk2D(1, 1), changed[0]);
    }

    [Fact]
    public void ClippedChunkExtentsCoverPartialEdgeChunks()
    {
        var map = new EditableTileMap2D(10, 6, 32f, 4);

        Assert.Equal(3, map.ChunkColumns);
        Assert.Equal(2, map.ChunkRows);
        Assert.Equal(4, map.ChunkWidth(0));
        Assert.Equal(2, map.ChunkWidth(2));
        Assert.Equal(2, map.ChunkHeight(1));
    }

    [Fact]
    public void GetChunkTilesReadsTheChunkInRowMajorOrder()
    {
        var map = new EditableTileMap2D(4, 4, 32f, 2);
        map.SetTileKind(2, 2, TileKind2D.Solid);
        map.SetTileKind(3, 2, TileKind2D.Spikes);

        var buffer = new TileKind2D[4];
        var tiles = map.GetChunkTiles(new TileChunk2D(1, 1), buffer);

        Assert.Equal(TileKind2D.Solid, tiles[0]);
        Assert.Equal(TileKind2D.Spikes, tiles[1]);
        Assert.Equal(TileKind2D.Empty, tiles[2]);
        Assert.Equal(TileKind2D.Empty, tiles[3]);
    }

    [Fact]
    public void WorldToChunkClampsOutsideTheMap()
    {
        var map = new EditableTileMap2D(8, 8, 10f, 4, new Vector2(-40f, -40f));

        Assert.Equal(new TileChunk2D(0, 0), map.WorldToChunk(new Vector2(-1000f, -1000f)));
        Assert.Equal(new TileChunk2D(1, 1), map.WorldToChunk(new Vector2(1000f, 1000f)));
    }
}
```

Also retarget `App2d.Tests/Tiles/TileMeshingCharacterizationTests.cs`. Replace the body of `ProceduralMapKeepsOneWayRowsOneTileTall` (lines 22-37) — keep the test name and assertions, change only construction:

```csharp
    [Fact]
    public void ProceduralMapKeepsOneWayRowsOneTileTall()
    {
        // 3x3 chunk: bottom row solid, middle row one-way, top empty.
        var map = new EditableTileMap2D(3, 3, 1f, 3);
        map.Fill((x, y) => y switch
        {
            0 => TileKind2D.Solid,
            1 => TileKind2D.OneWay,
            _ => TileKind2D.Empty
        });

        var rectangles = map.BuildCollisionRectangles(new TileChunk2D(0, 0));

        Assert.Equal(2, rectangles.Count);
        Assert.Contains(rectangles, r => r.Kind == TileKind2D.Solid && r.Bounds.Max.Y == 1f);
        Assert.Contains(rectangles, r => r.Kind == TileKind2D.OneWay && r.Bounds.Min.Y == 1f && r.Bounds.Max.Y == 2f);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~EditableTileMap2D"`
Expected: FAIL — compile error, `EditableTileMap2D` does not exist.

- [ ] **Step 3: Write the implementation**

Create `App2d.Tiles/EditableTileMap2D.cs`. Note `TileChunk2D` currently lives at the bottom of `ProceduralTileMap2D.cs`, which is being deleted — it moves here.

```csharp
using App2d.Core;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tiles;

/// <summary>
/// The dense, mutable tile map that authored levels load into. It is the only
/// <see cref="IChunkedTileMap2D"/> implementation: terrain is data, not a generator.
/// </summary>
public sealed class EditableTileMap2D : IChunkedTileMap2D
{
    private readonly TileKind2D[] _tiles;
    private readonly List<TileCellRectangle2D> _meshBuffer = [];

    public EditableTileMap2D(
        int width,
        int height,
        float tileSize,
        int chunkSize,
        Vector2 origin = default)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNotPositive(tileSize);
        ArgGuard.ThrowIfNotPositive(chunkSize);
        ArgGuard.ThrowIfNotFinite(origin);

        Width = width;
        Height = height;
        TileSize = tileSize;
        ChunkSize = chunkSize;
        Origin = origin;
        _tiles = new TileKind2D[width * height];
    }

    /// <summary>Raised when a chunk's tiles changed. Phase 2's editor drives streamer reloads from this.</summary>
    public event Action<TileChunk2D>? ChunkChanged;

    public int Width { get; }
    public int Height { get; }
    public float TileSize { get; }
    public int ChunkSize { get; }
    public Vector2 Origin { get; }
    public int ChunkColumns => DivideRoundUp(Width, ChunkSize);
    public int ChunkRows => DivideRoundUp(Height, ChunkSize);
    public Bounds2D WorldBounds =>
        new(Origin, Origin + new Vector2(Width * TileSize, Height * TileSize));

    public TileKind2D GetTileKind(int x, int y) => IsInside(x, y)
        ? _tiles[y * Width + x]
        : TileKind2D.Empty;

    public bool IsSolid(int x, int y) => GetTileKind(x, y).IsSolid();

    public void SetTileKind(int x, int y, TileKind2D kind)
    {
        if (!IsInside(x, y))
            ArgGuard.ThrowOutOfRange(x, $"Tile ({x}, {y}) is outside the map.");

        var index = y * Width + x;
        if (_tiles[index] == kind)
            return;

        _tiles[index] = kind;
        ChunkChanged?.Invoke(TileToChunk(x, y));
    }

    /// <summary>Seeds every tile from <paramref name="source"/> without raising per-tile change events.</summary>
    public void Fill(Func<int, int, TileKind2D> source)
    {
        ArgGuard.ThrowIfNull(source);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
                _tiles[y * Width + x] = source(x, y);
        }
    }

    public TileChunk2D TileToChunk(int x, int y) => new(x / ChunkSize, y / ChunkSize);

    public int ChunkWidth(int chunkX) => Math.Min(ChunkSize, Width - chunkX * ChunkSize);

    public int ChunkHeight(int chunkY) => Math.Min(ChunkSize, Height - chunkY * ChunkSize);

    /// <summary>Copies a chunk's tiles into <paramref name="destination"/> in row-major order.</summary>
    public ReadOnlySpan<TileKind2D> GetChunkTiles(TileChunk2D chunk, Span<TileKind2D> destination)
    {
        ValidateChunk(chunk);
        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = ChunkWidth(chunk.X);
        var height = ChunkHeight(chunk.Y);
        ArgGuard.ThrowIfTooShort<TileKind2D>(destination, width * height);

        for (var y = 0; y < height; y++)
        {
            _tiles.AsSpan((startY + y) * Width + startX, width)
                .CopyTo(destination[(y * width)..]);
        }

        return destination[..(width * height)];
    }

    /// <summary>Writes a chunk's tiles from <paramref name="source"/> in row-major order.</summary>
    public void SetChunkTiles(TileChunk2D chunk, ReadOnlySpan<TileKind2D> source)
    {
        ValidateChunk(chunk);
        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = ChunkWidth(chunk.X);
        var height = ChunkHeight(chunk.Y);
        ArgGuard.ThrowIfTooShort<TileKind2D>(source, width * height);

        for (var y = 0; y < height; y++)
        {
            source.Slice(y * width, width)
                .CopyTo(_tiles.AsSpan((startY + y) * Width + startX, width));
        }

        ChunkChanged?.Invoke(chunk);
    }

    public TileChunk2D WorldToChunk(Vector2 worldPosition)
    {
        var tile = (worldPosition - Origin) / TileSize;
        return new TileChunk2D(
            Math.Clamp((int)MathF.Floor(tile.X / ChunkSize), 0, ChunkColumns - 1),
            Math.Clamp((int)MathF.Floor(tile.Y / ChunkSize), 0, ChunkRows - 1));
    }

    public IReadOnlyList<TileCollisionRectangle2D> BuildCollisionRectangles(TileChunk2D chunk)
    {
        ValidateChunk(chunk);

        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = ChunkWidth(chunk.X);
        var height = ChunkHeight(chunk.Y);

        _meshBuffer.Clear();
        TileRectangleMesher2D.Mesh(
            width,
            height,
            (x, y) => _tiles[(startY + y) * Width + startX + x],
            _meshBuffer);

        var rectangles = new List<TileCollisionRectangle2D>(_meshBuffer.Count);
        foreach (var cell in _meshBuffer)
        {
            var min = Origin + new Vector2(startX + cell.X, startY + cell.Y) * TileSize;
            var max = min + new Vector2(cell.Width, cell.Height) * TileSize;
            rectangles.Add(new TileCollisionRectangle2D(new Bounds2D(min, max), cell.Kind));
        }

        return rectangles;
    }

    private bool IsInside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    private void ValidateChunk(TileChunk2D chunk)
    {
        if (chunk.X < 0 || chunk.X >= ChunkColumns || chunk.Y < 0 || chunk.Y >= ChunkRows)
            ArgGuard.ThrowOutOfRange(chunk, "Chunk coordinates must be inside the map.");
    }

    private static int DivideRoundUp(int value, int divisor) => (value + divisor - 1) / divisor;
}

public readonly record struct TileChunk2D(int X, int Y);
```

Note the difference from `ProceduralTileMap2D`: it prefetched tiles into a temporary array so the mesher's repeated reads would not re-run generator code. A dense array needs no prefetch, so that allocation is gone.

- [ ] **Step 4: Delete `ProceduralTileMap2D` and update its consumer**

```bash
git rm App2d.Tiles/ProceduralTileMap2D.cs
```

In `App2d.Gameplay/World/SideScrollerLevel2D.cs`, replace the `TileMap = new ProceduralTileMap2D(...)` assignment at lines 38-44. This is a temporary shape — Task 5 replaces it with injection — but it must compile and behave identically now:

```csharp
        var tileMap = new EditableTileMap2D(
            WorldWidthTiles,
            WorldHeightTiles,
            _tileSize,
            ChunkSizeTiles,
            new Vector2(-512f, -640f));
        tileMap.Fill(_generator.GetTileKind);
        TileMap = tileMap;
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test App2d.slnx`
Expected: PASS — all tests including the retargeted meshing characterization test.

- [ ] **Step 6: Commit**

```bash
git add -A App2d.Tiles App2d.Tests App2d.Gameplay
git commit -m "Replace ProceduralTileMap2D with dense EditableTileMap2D"
```

---

### Task 2: `App2d.Levels` project and `TileRunCodec2D`

The RLE codec for one chunk's tiles. Pure and dependency-free apart from `App2d.Tiles`, so it is worth getting exactly right before any SQLite exists.

**Files:**
- Create: `App2d.Levels/App2d.Levels.csproj`
- Create: `App2d.Levels/TileRunCodec2D.cs`
- Modify: `App2d.slnx`
- Modify: `App2d.Tests/App2d.Tests.csproj`
- Test: `App2d.Tests/Levels/TileRunCodec2DTests.cs`

**Interfaces:**
- Consumes: `TileKind2D` from `App2d.Tiles`.
- Produces:
  - `static byte[] TileRunCodec2D.Encode(ReadOnlySpan<TileKind2D> tiles)`
  - `static void TileRunCodec2D.Decode(ReadOnlySpan<byte> encoded, Span<TileKind2D> tiles)`

Format: `[kind:u8][count:u8]` pairs, row-major, `count` in 1..255. Runs longer than 255 split across pairs.

- [ ] **Step 1: Create the project and wire it up**

Create `App2d.Levels/App2d.Levels.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisModePerformance>All</AnalysisModePerformance>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\App2d.Core\App2d.Core.csproj" />
    <ProjectReference Include="..\App2d.Tiles\App2d.Tiles.csproj" />
  </ItemGroup>
</Project>
```

If `Microsoft.Data.Sqlite` version `10.0.0` does not resolve, run `dotnet package search Microsoft.Data.Sqlite` and pin the latest stable version that targets `net10.0`. Do not use a floating version.

Add to `App2d.slnx` after the `App2d.Tiles` line:

```xml
  <Project Path="App2d.Levels/App2d.Levels.csproj" />
```

Add to `App2d.Tests/App2d.Tests.csproj`, inside the existing `ItemGroup` of `ProjectReference`s:

```xml
    <ProjectReference Include="..\App2d.Levels\App2d.Levels.csproj" />
```

- [ ] **Step 2: Write the failing tests**

Create `App2d.Tests/Levels/TileRunCodec2DTests.cs`:

```csharp
using App2d.Levels;
using App2d.Tiles;

namespace App2d.Tests.Levels;

public sealed class TileRunCodec2DTests
{
    private static void AssertRoundTrips(TileKind2D[] tiles)
    {
        var encoded = TileRunCodec2D.Encode(tiles);
        var decoded = new TileKind2D[tiles.Length];
        TileRunCodec2D.Decode(encoded, decoded);
        Assert.Equal(tiles, decoded);
    }

    [Fact]
    public void EmptyChunkEncodesToFivePairs()
    {
        var tiles = new TileKind2D[1024];

        var encoded = TileRunCodec2D.Encode(tiles);

        Assert.Equal(10, encoded.Length);
        AssertRoundTrips(tiles);
    }

    [Fact]
    public void SingleKindChunkRoundTrips()
    {
        var tiles = new TileKind2D[1024];
        Array.Fill(tiles, TileKind2D.Solid);

        AssertRoundTrips(tiles);
    }

    [Fact]
    public void AlternatingWorstCaseRoundTripsAtTwoBytesPerTile()
    {
        var tiles = new TileKind2D[1024];
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = i % 2 == 0 ? TileKind2D.Solid : TileKind2D.Empty;

        var encoded = TileRunCodec2D.Encode(tiles);

        Assert.Equal(2048, encoded.Length);
        AssertRoundTrips(tiles);
    }

    [Fact]
    public void RunsLongerThanTwoHundredFiftyFiveSplitAcrossPairs()
    {
        var tiles = new TileKind2D[300];
        Array.Fill(tiles, TileKind2D.OneWay);

        var encoded = TileRunCodec2D.Encode(tiles);

        Assert.Equal(4, encoded.Length);
        Assert.Equal(255, encoded[1]);
        Assert.Equal(45, encoded[3]);
        AssertRoundTrips(tiles);
    }

    [Fact]
    public void CombinedFlagKindsSurviveRoundTrip()
    {
        var tiles = new TileKind2D[8];
        Array.Fill(tiles, TileKind2D.Solid | TileKind2D.Grippable);
        tiles[3] = TileKind2D.Spikes;

        AssertRoundTrips(tiles);
    }

    [Fact]
    public void ClippedEdgeChunkExtentRoundTrips()
    {
        // A 2x6 edge chunk, not a full 32x32 one.
        var tiles = new TileKind2D[12];
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = i < 4 ? TileKind2D.Solid : TileKind2D.Empty;

        AssertRoundTrips(tiles);
    }

    [Fact]
    public void DecodeIntoWrongSizedDestinationThrows()
    {
        var encoded = TileRunCodec2D.Encode(new TileKind2D[16]);
        var tooSmall = new TileKind2D[8];

        Assert.Throws<ArgumentOutOfRangeException>(() => TileRunCodec2D.Decode(encoded, tooSmall));
    }

    [Fact]
    public void DecodeRejectsOddLengthPayload()
    {
        var tiles = new TileKind2D[4];

        Assert.Throws<ArgumentException>(() => TileRunCodec2D.Decode([1, 2, 3], tiles));
    }

    [Fact]
    public void EncodeRejectsEmptyInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TileRunCodec2D.Encode([]));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~TileRunCodec2D"`
Expected: FAIL — compile error, `TileRunCodec2D` does not exist.

- [ ] **Step 4: Write the implementation**

Create `App2d.Levels/TileRunCodec2D.cs`:

```csharp
using App2d.Core;
using App2d.Tiles;

namespace App2d.Levels;

/// <summary>
/// Run-length codec for one chunk's tiles, stored row-major as
/// <c>[kind:u8][count:u8]</c> pairs. Runs longer than 255 split across pairs.
/// </summary>
public static class TileRunCodec2D
{
    private const int MaximumRunLength = byte.MaxValue;

    public static byte[] Encode(ReadOnlySpan<TileKind2D> tiles)
    {
        ArgGuard.ThrowIfNotPositive(tiles.Length);

        var encoded = new List<byte>(16);
        var index = 0;
        while (index < tiles.Length)
        {
            var kind = tiles[index];
            var runLength = 1;
            while (index + runLength < tiles.Length &&
                   tiles[index + runLength] == kind &&
                   runLength < MaximumRunLength)
            {
                runLength++;
            }

            encoded.Add((byte)kind);
            encoded.Add((byte)runLength);
            index += runLength;
        }

        return [.. encoded];
    }

    public static void Decode(ReadOnlySpan<byte> encoded, Span<TileKind2D> tiles)
    {
        if (encoded.Length % 2 != 0)
            ArgGuard.ThrowInvalid("Encoded tile runs must be whole [kind][count] pairs.", nameof(encoded));

        var written = 0;
        for (var pair = 0; pair < encoded.Length; pair += 2)
        {
            var kind = (TileKind2D)encoded[pair];
            int count = encoded[pair + 1];
            if (count == 0)
                ArgGuard.ThrowInvalid("Encoded tile runs must have a positive count.", nameof(encoded));
            if (written + count > tiles.Length)
                ArgGuard.ThrowOutOfRange(count, "Encoded tile runs overflow the destination.");

            tiles.Slice(written, count).Fill(kind);
            written += count;
        }

        if (written != tiles.Length)
            ArgGuard.ThrowOutOfRange(written, "Encoded tile runs do not fill the destination.");
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~TileRunCodec2D"`
Expected: PASS — 9 tests.

- [ ] **Step 6: Commit**

```bash
git add -A App2d.Levels App2d.Tests App2d.slnx
git commit -m "Add App2d.Levels project with chunk run-length codec"
```

---

### Task 3: `LevelDatabase2D`

Schema, save, and load. Ends with a full map round-tripping through a real file on disk.

**Files:**
- Create: `App2d.Levels/LevelDatabase2D.cs`
- Test: `App2d.Tests/Levels/LevelDatabase2DTests.cs`

**Interfaces:**
- Consumes: `EditableTileMap2D` (Task 1), `TileRunCodec2D.Encode` / `Decode` (Task 2).
- Produces:
  - `static LevelDatabase2D LevelDatabase2D.Open(string path)` — creates the schema if absent
  - `void Save(EditableTileMap2D map, ulong sourceSeed)`
  - `EditableTileMap2D Load()`
  - `void SaveChunk(EditableTileMap2D map, TileChunk2D chunk)` — phase 2's write-through entry point
  - `int FormatVersion { get; }` and `const int CurrentFormatVersion = 1`
  - `int ChunkRowCount { get; }` — used by tests to assert empty chunks store no row
  - `void Dispose()`

- [ ] **Step 1: Write the failing tests**

Create `App2d.Tests/Levels/LevelDatabase2DTests.cs`:

```csharp
using App2d.Levels;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Tests.Levels;

public sealed class LevelDatabase2DTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "app2d-levels-" + Guid.NewGuid().ToString("N"));

    private string NewPath()
    {
        Directory.CreateDirectory(_directory);
        return Path.Combine(_directory, "level.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static EditableTileMap2D BuildMap()
    {
        var map = new EditableTileMap2D(10, 6, 32f, 4, new Vector2(-512f, -640f));
        map.Fill((x, y) => (x + y) switch
        {
            0 => TileKind2D.Solid | TileKind2D.Grippable,
            1 => TileKind2D.OneWay,
            2 => TileKind2D.Spikes,
            _ => y < 2 ? TileKind2D.Solid : TileKind2D.Empty
        });
        return map;
    }

    [Fact]
    public void SavedMapRoundTripsEveryTile()
    {
        var path = NewPath();
        var original = BuildMap();

        using (var database = LevelDatabase2D.Open(path))
            database.Save(original, sourceSeed: 0xA2D_2026_0823UL);

        using var reopened = LevelDatabase2D.Open(path);
        var loaded = reopened.Load();

        Assert.Equal(original.Width, loaded.Width);
        Assert.Equal(original.Height, loaded.Height);
        Assert.Equal(original.TileSize, loaded.TileSize);
        Assert.Equal(original.ChunkSize, loaded.ChunkSize);
        Assert.Equal(original.Origin, loaded.Origin);

        for (var y = 0; y < original.Height; y++)
        {
            for (var x = 0; x < original.Width; x++)
                Assert.Equal(original.GetTileKind(x, y), loaded.GetTileKind(x, y));
        }
    }

    [Fact]
    public void EmptyChunksAreNotStoredAsRows()
    {
        var path = NewPath();
        // 2x1 chunks; only the left chunk has any content.
        var map = new EditableTileMap2D(8, 4, 32f, 4);
        map.SetTileKind(1, 1, TileKind2D.Solid);

        using var database = LevelDatabase2D.Open(path);
        database.Save(map, sourceSeed: 0UL);

        Assert.Equal(1, database.ChunkRowCount);

        var loaded = database.Load();
        Assert.Equal(TileKind2D.Solid, loaded.GetTileKind(1, 1));
        Assert.Equal(TileKind2D.Empty, loaded.GetTileKind(5, 1));
    }

    [Fact]
    public void SaveChunkUpdatesOnlyThatChunk()
    {
        var path = NewPath();
        var map = BuildMap();

        using var database = LevelDatabase2D.Open(path);
        database.Save(map, sourceSeed: 0UL);

        map.SetTileKind(0, 0, TileKind2D.Spikes);
        database.SaveChunk(map, new TileChunk2D(0, 0));

        var loaded = database.Load();
        Assert.Equal(TileKind2D.Spikes, loaded.GetTileKind(0, 0));
        Assert.Equal(map.GetTileKind(9, 5), loaded.GetTileKind(9, 5));
    }

    [Fact]
    public void OpeningTwiceReusesTheExistingSchema()
    {
        var path = NewPath();

        using (var first = LevelDatabase2D.Open(path))
            first.Save(BuildMap(), sourceSeed: 0UL);

        using var second = LevelDatabase2D.Open(path);
        Assert.Equal(1, second.FormatVersion);
    }

    [Fact]
    public void LoadingBeforeAnySaveThrows()
    {
        var path = NewPath();
        using var database = LevelDatabase2D.Open(path);

        Assert.Throws<InvalidOperationException>(() => database.Load());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~LevelDatabase2D"`
Expected: FAIL — compile error, `LevelDatabase2D` does not exist.

- [ ] **Step 3: Write the implementation**

Create `App2d.Levels/LevelDatabase2D.cs`:

```csharp
using App2d.Core;
using App2d.Tiles;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Numerics;

namespace App2d.Levels;

/// <summary>
/// One authored level in one SQLite file. Tiles are run-length encoded per chunk so a
/// single edit rewrites a single row rather than the whole level.
/// </summary>
public sealed class LevelDatabase2D : IDisposable
{
    public const int CurrentFormatVersion = 1;

    private readonly SqliteConnection _connection;

    private LevelDatabase2D(SqliteConnection connection) => _connection = connection;

    public int FormatVersion => (int)ReadScalarLong("PRAGMA user_version;");

    public int ChunkRowCount => (int)ReadScalarLong("SELECT COUNT(*) FROM chunks;");

    public static LevelDatabase2D Open(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS meta(
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL) WITHOUT ROWID;
                CREATE TABLE IF NOT EXISTS chunks(
                    cx INTEGER NOT NULL,
                    cy INTEGER NOT NULL,
                    tiles BLOB NOT NULL,
                    PRIMARY KEY(cx, cy)) WITHOUT ROWID;
                PRAGMA user_version = {CurrentFormatVersion};
                """;
            command.ExecuteNonQuery();
        }

        return new LevelDatabase2D(connection);
    }

    public void Save(EditableTileMap2D map, ulong sourceSeed)
    {
        ArgGuard.ThrowIfNull(map);

        using var transaction = _connection.BeginTransaction();
        WriteMeta("width", map.Width);
        WriteMeta("height", map.Height);
        WriteMeta("tile_size", map.TileSize);
        WriteMeta("chunk_size", map.ChunkSize);
        WriteMeta("origin_x", map.Origin.X);
        WriteMeta("origin_y", map.Origin.Y);
        WriteMeta("source_seed", sourceSeed.ToString(CultureInfo.InvariantCulture));
        WriteMeta("generated_utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        using (var clear = _connection.CreateCommand())
        {
            clear.CommandText = "DELETE FROM chunks;";
            clear.ExecuteNonQuery();
        }

        var buffer = new TileKind2D[map.ChunkSize * map.ChunkSize];
        for (var cy = 0; cy < map.ChunkRows; cy++)
        {
            for (var cx = 0; cx < map.ChunkColumns; cx++)
                WriteChunk(map, new TileChunk2D(cx, cy), buffer);
        }

        transaction.Commit();
    }

    /// <summary>Commits one chunk. Phase 2's per-stroke write-through calls this inside its own transaction.</summary>
    public void SaveChunk(EditableTileMap2D map, TileChunk2D chunk)
    {
        ArgGuard.ThrowIfNull(map);
        var buffer = new TileKind2D[map.ChunkSize * map.ChunkSize];
        using var transaction = _connection.BeginTransaction();
        WriteChunk(map, chunk, buffer);
        transaction.Commit();
    }

    public EditableTileMap2D Load()
    {
        var width = (int)RequireMetaLong("width");
        var height = (int)RequireMetaLong("height");
        var chunkSize = (int)RequireMetaLong("chunk_size");
        var tileSize = RequireMetaFloat("tile_size");
        var origin = new Vector2(RequireMetaFloat("origin_x"), RequireMetaFloat("origin_y"));

        var map = new EditableTileMap2D(width, height, tileSize, chunkSize, origin);
        var buffer = new TileKind2D[chunkSize * chunkSize];

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT cx, cy, tiles FROM chunks;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var chunk = new TileChunk2D(reader.GetInt32(0), reader.GetInt32(1));
            var encoded = (byte[])reader["tiles"];
            var tileCount = map.ChunkWidth(chunk.X) * map.ChunkHeight(chunk.Y);
            var tiles = buffer.AsSpan(0, tileCount);
            TileRunCodec2D.Decode(encoded, tiles);
            map.SetChunkTiles(chunk, tiles);
        }

        return map;
    }

    public void Dispose() => _connection.Dispose();

    private void WriteChunk(EditableTileMap2D map, TileChunk2D chunk, TileKind2D[] buffer)
    {
        var tiles = map.GetChunkTiles(chunk, buffer);

        // A missing row means an entirely empty chunk, so empty sky costs no rows.
        var isEmpty = true;
        foreach (var tile in tiles)
        {
            if (tile == TileKind2D.Empty)
                continue;
            isEmpty = false;
            break;
        }

        using var command = _connection.CreateCommand();
        if (isEmpty)
        {
            command.CommandText = "DELETE FROM chunks WHERE cx = $cx AND cy = $cy;";
        }
        else
        {
            command.CommandText = """
                INSERT INTO chunks(cx, cy, tiles) VALUES($cx, $cy, $tiles)
                ON CONFLICT(cx, cy) DO UPDATE SET tiles = excluded.tiles;
                """;
            command.Parameters.AddWithValue("$tiles", TileRunCodec2D.Encode(tiles));
        }

        command.Parameters.AddWithValue("$cx", chunk.X);
        command.Parameters.AddWithValue("$cy", chunk.Y);
        command.ExecuteNonQuery();
    }

    private void WriteMeta(string key, int value) =>
        WriteMeta(key, value.ToString(CultureInfo.InvariantCulture));

    private void WriteMeta(string key, float value) =>
        WriteMeta(key, value.ToString("R", CultureInfo.InvariantCulture));

    private void WriteMeta(string key, string value)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO meta(key, value) VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private string RequireMeta(string key)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT value FROM meta WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return StateGuard.RequireNotNull(
            command.ExecuteScalar() as string,
            $"The level file is missing required metadata '{key}'.");
    }

    private long RequireMetaLong(string key) =>
        long.Parse(RequireMeta(key), CultureInfo.InvariantCulture);

    private float RequireMetaFloat(string key) =>
        float.Parse(RequireMeta(key), CultureInfo.InvariantCulture);

    private long ReadScalarLong(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
```

If `StateGuard.RequireNotNull` has a different signature than `(value, message)`, check `App2d.Core/StateGuard.cs` and match it — `SideScrollerLevel2D.RequireEnvironment` shows the intended usage.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~LevelDatabase2D"`
Expected: PASS — 5 tests.

- [ ] **Step 5: Commit**

```bash
git add -A App2d.Levels App2d.Tests
git commit -m "Add SQLite level database with per-chunk storage"
```

---

### Task 4: Derived ground heights

`TerrainHeight(x)` disappears with the generator. This replaces it with a column scan. **Provisional by design** — see the spec's "Ground height" section. Do not add pit detection.

**Files:**
- Create: `App2d.Tiles/TileGroundHeights2D.cs`
- Test: `App2d.Tests/Tiles/TileGroundHeights2DTests.cs`

**Interfaces:**
- Consumes: `ISolidTileMap2D` (existing).
- Produces: `static int[] TileGroundHeights2D.Derive(ISolidTileMap2D map)` — length `map.Width`, every entry ≥ 1.

- [ ] **Step 1: Write the failing tests**

Create `App2d.Tests/Tiles/TileGroundHeights2DTests.cs`:

```csharp
using App2d.Tiles;

namespace App2d.Tests.Tiles;

public sealed class TileGroundHeights2DTests
{
    [Fact]
    public void GroundHeightIsTheLowestNonSolidRow()
    {
        var map = new EditableTileMap2D(3, 8, 32f, 4);
        // Column 0 solid to row 2, column 1 solid to row 4, column 2 solid to row 1.
        map.Fill((x, y) => y < x switch { 0 => 3, 1 => 5, _ => 2 }
            ? TileKind2D.Solid
            : TileKind2D.Empty);

        var heights = TileGroundHeights2D.Derive(map);

        Assert.Equal(new[] { 3, 5, 2 }, heights);
    }

    [Fact]
    public void EmptyColumnClampsToOneSoCallersNeverIndexBelowTheWorld()
    {
        var map = new EditableTileMap2D(2, 8, 32f, 4);
        map.Fill((x, y) => x == 0 && y < 4 ? TileKind2D.Solid : TileKind2D.Empty);

        var heights = TileGroundHeights2D.Derive(map);

        Assert.Equal(4, heights[0]);
        // A pit column has no solid floor at all; the clamp keeps `height - 1` at 0.
        Assert.Equal(1, heights[1]);
    }

    [Fact]
    public void FullySolidColumnReportsTheMapHeight()
    {
        var map = new EditableTileMap2D(1, 5, 32f, 4);
        map.Fill((_, _) => TileKind2D.Solid);

        Assert.Equal(new[] { 5 }, TileGroundHeights2D.Derive(map));
    }

    [Fact]
    public void OneWayAndSpikeTilesAreNotGround()
    {
        var map = new EditableTileMap2D(1, 6, 32f, 4);
        map.Fill((_, y) => y switch
        {
            0 => TileKind2D.Solid,
            1 => TileKind2D.Spikes,
            2 => TileKind2D.OneWay,
            _ => TileKind2D.Empty
        });

        Assert.Equal(new[] { 1 }, TileGroundHeights2D.Derive(map));
    }

    [Fact]
    public void EveryDerivedHeightIsAtLeastOne()
    {
        var map = new EditableTileMap2D(16, 8, 32f, 4);

        Assert.All(TileGroundHeights2D.Derive(map), height => Assert.True(height >= 1));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~TileGroundHeights2D"`
Expected: FAIL — compile error, `TileGroundHeights2D` does not exist.

- [ ] **Step 3: Write the implementation**

Create `App2d.Tiles/TileGroundHeights2D.cs`:

```csharp
using App2d.Core;

namespace App2d.Tiles;

/// <summary>
/// Per-column ground rows derived from tile data, replacing the world generator's
/// <c>TerrainHeight</c>.
/// </summary>
/// <remarks>
/// Deliberately crude. Ground height feeds camera floor clamping, spawn and goal rows, and
/// enemy placement — getting it wrong degrades feel, not correctness. A single row per column
/// cannot describe a second-storey floor, so this is replaced by authored, multi-valued data
/// rather than refined in place. The clamp to 1 is the entire mitigation for pit columns:
/// callers use <c>height - 1</c>, which must never index below the world.
/// </remarks>
public static class TileGroundHeights2D
{
    public static int[] Derive(ISolidTileMap2D map)
    {
        ArgGuard.ThrowIfNull(map);

        var heights = new int[map.Width];
        for (var x = 0; x < map.Width; x++)
        {
            var y = 0;
            while (y < map.Height && map.IsSolid(x, y))
                y++;

            heights[x] = Math.Max(1, y);
        }

        return heights;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~TileGroundHeights2D"`
Expected: PASS — 5 tests.

- [ ] **Step 5: Commit**

```bash
git add -A App2d.Tiles App2d.Tests
git commit -m "Derive per-column ground heights from tile data"
```

---

### Task 5: Inject the map and ground heights into gameplay

Cut `SideScrollerLevel2D` and `SideScrollerEncounterSpawner2D` off `JumpableWorldGenerator2D`. **No database yet** — the host still builds the map from the generator. This isolates the coupling change from the storage change, so if the game misbehaves you know which half did it.

**Files:**
- Modify: `App2d.Gameplay/World/SideScrollerLevel2D.cs` (constructor, lines 20-56; `GetCameraFloorY` line 76; spawner construction line 215; `AddMovingPlatform` line 324)
- Modify: `App2d.Gameplay/World/SideScrollerEncounterSpawner2D.cs:18` and `:127`
- Modify: `App2d/SideScrollerGame.cs:52-53`

**Interfaces:**
- Consumes: `EditableTileMap2D` (Task 1), `TileGroundHeights2D.Derive` (Task 4), `JumpableWorldGenerator2D` (existing, host-side only from now on).
- Produces:
  - `SideScrollerLevel2D(TraversalMetrics2D traversal, IChunkedTileMap2D tileMap, Func<int, int> groundY)`
  - `SideScrollerLevel2D.WorldWidthTiles` / `WorldHeightTiles` / `ChunkSizeTiles` / `WorldSeed` / `WorldOrigin` become `public const` / `public static` so the host can build a matching map.

- [ ] **Step 1: Expose the world constants and change the constructor**

In `App2d.Gameplay/World/SideScrollerLevel2D.cs`, promote the private constants and replace the generator field:

```csharp
    public const int WorldWidthTiles = 640;
    public const int WorldHeightTiles = 96;
    public const int ChunkSizeTiles = 32;
    public const ulong WorldSeed = 0xA2D_2026_0823UL;
    public static Vector2 WorldOrigin { get; } = new(-512f, -640f);

    private readonly float _tileSize;
    private readonly Func<int, int> _groundY;
```

Delete the `private readonly JumpableWorldGenerator2D _generator;` field. Replace the constructor's opening through the `TileMap` assignment with:

```csharp
    public SideScrollerLevel2D(
        TraversalMetrics2D traversal,
        IChunkedTileMap2D tileMap,
        Func<int, int> groundY)
    {
        ArgGuard.ThrowIfNull(traversal);
        ArgGuard.ThrowIfNull(tileMap);
        ArgGuard.ThrowIfNull(groundY);
        _tileSize = traversal.TileSize;
        ArgGuard.ThrowIfNotPositive(_tileSize);
        _groundY = groundY;
        TileMap = tileMap;
```

Keep the rest of the constructor (spawn point, goal) unchanged apart from swapping the calls:

```csharp
        const int spawnTileX = 4;
        SpawnPoint = new Vector2(
            TileCenterX(spawnTileX),
            TileMap.Origin.Y + _groundY(spawnTileX) * _tileSize +
            traversal.PlayerColliderSize.Y / 2f + traversal.GroundProbeDistance);

        const int goalTileX = WorldWidthTiles - 5;
        GoalX = TileCenterX(goalTileX);
        GoalGroundY = TileMap.Origin.Y + _groundY(goalTileX) * _tileSize;
```

- [ ] **Step 2: Swap the remaining call sites**

`GetCameraFloorY` (line 76):

```csharp
        return TileMap.Origin.Y + _groundY(tileX) * _tileSize;
```

`AddMovingPlatform` (line 324):

```csharp
            terrainTileY = Math.Max(terrainTileY, _groundY(tileX));
```

`CreateMechanicsPlaygroundEnemies` (line 215) — pass the delegate instead of the generator:

```csharp
            _groundY,
```

In `App2d.Gameplay/World/SideScrollerEncounterSpawner2D.cs`, change the primary constructor parameter at line 18:

```csharp
    Func<int, int> groundY,
```

and line 127:

```csharp
            var surfaceY = groundY(x) - 1;
```

- [ ] **Step 3: Build the map in the host**

In `App2d/SideScrollerGame.cs`, replace `_level = new SideScrollerLevel2D(Traversal);` (line 52) with:

```csharp
        var generator = new JumpableWorldGenerator2D(
            SideScrollerLevel2D.WorldSeed,
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            Traversal);
        var tileMap = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            Traversal.TileSize,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
        tileMap.Fill(generator.GetTileKind);
        var groundHeights = TileGroundHeights2D.Derive(tileMap);

        _level = new SideScrollerLevel2D(
            Traversal,
            tileMap,
            x => groundHeights[Math.Clamp(x, 0, groundHeights.Length - 1)]);
```

Add `using App2d.Tiles;` to the file's usings if it is not already present.

- [ ] **Step 4: Build and run the full test suite**

Run: `dotnet build App2d.slnx` then `dotnet test App2d.slnx`
Expected: build succeeds, all tests pass. `JumpableWorldGenerator2D` should now have no references inside `SideScrollerLevel2D`.

Verify the decoupling actually happened:

Run: `git grep -n "TerrainHeight\|JumpableWorldGenerator2D" -- "App2d.Gameplay"`
Expected: matches only inside `App2d.Gameplay/World/JumpableWorldGenerator2D.cs` itself.

- [ ] **Step 5: Play the game and confirm nothing shifted**

Run the game. Confirm: the player spawns on the ground (not inside terrain or falling), enemies stand on ground rather than floating or sinking, moving platforms sit at their usual heights, the camera behaves normally including over pits, and the goal flag is reachable.

This is the checkpoint where a ground-height regression would show up. If something is off, fix it here — do not carry it into Task 6, where the database would get the blame.

- [ ] **Step 6: Commit**

```bash
git add -A App2d.Gameplay App2d
git commit -m "Inject tile map and ground heights into the side-scroller level"
```

---

### Task 6: Bake to disk and load from the level file

The host bakes the generator's output once when the file is missing, then loads from it. After this task the generator is dead weight.

**Files:**
- Create: `App2d/Levels/LevelBootstrap2D.cs`
- Modify: `App2d/App2d.csproj` (project reference + Static asset packaging)
- Modify: `App2d/SideScrollerGame.cs`
- Modify: `.gitignore` (only if `Assets/Static` is currently ignored — check first)

**Interfaces:**
- Consumes: `LevelDatabase2D.Open` / `Save` / `Load` (Task 3), `EditableTileMap2D` (Task 1), `JumpableWorldGenerator2D` (existing).
- Produces:
  - `static string LevelBootstrap2D.CavernLevelPath { get; }`
  - `static EditableTileMap2D LevelBootstrap2D.LoadOrBake(TraversalMetrics2D traversal)`
  - `static void LevelBootstrap2D.Bake(TraversalMetrics2D traversal)`

- [ ] **Step 1: Reference `App2d.Levels` from the host**

In `App2d/App2d.csproj`, add to the `ProjectReference` group:

```xml
    <ProjectReference Include="..\App2d.Levels\App2d.Levels.csproj" />
```

Levels are durable authored content under `Assets/Static`, which the Python pipeline copies into `Runtime`. For Release packaging the existing `Assets/Runtime/**` glob already covers the copied file, so no packaging change is needed — but confirm `Assets/Static` is not git-ignored:

Run: `git check-ignore -v Assets/Static/levels 2>&1; echo "exit=$?"`
Expected: `exit=1` (not ignored). If it IS ignored, add a negation to `.gitignore` so level files can be committed.

- [ ] **Step 2: Write the bootstrap**

Create `App2d/Levels/LevelBootstrap2D.cs`:

```csharp
using App2d.Gameplay;
using App2d.Levels;
using App2d.Tiles;

namespace App2d;

/// <summary>
/// Resolves the authored level file, baking it from the world generator the first time.
/// </summary>
/// <remarks>
/// The bake path is temporary scaffolding: <see cref="JumpableWorldGenerator2D"/> exists only
/// to produce a level to start editing from. Deleting the generator means deleting
/// <see cref="Bake"/> and the <c>level_rebake</c> command with it — <see cref="LoadOrBake"/>
/// becomes a plain load.
/// </remarks>
internal static class LevelBootstrap2D
{
    private const string LevelId = "cavern";

    /// <summary>
    /// Levels are durable authored content, so they live under <c>Assets/Static</c> and are
    /// read from there directly in Debug. <c>Assets/Runtime</c> is generated and disposable
    /// and must never be the only home for a hand-edited file.
    /// </summary>
    public static string CavernLevelPath { get; } = ResolveLevelPath();

    public static EditableTileMap2D LoadOrBake(TraversalMetrics2D traversal)
    {
        if (!File.Exists(CavernLevelPath))
            Bake(traversal);

        using var database = LevelDatabase2D.Open(CavernLevelPath);
        return database.Load();
    }

    public static void Bake(TraversalMetrics2D traversal)
    {
        var generator = new JumpableWorldGenerator2D(
            SideScrollerLevel2D.WorldSeed,
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal);
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal.TileSize,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
        map.Fill(generator.GetTileKind);

        using var database = LevelDatabase2D.Open(CavernLevelPath);
        database.Save(map, SideScrollerLevel2D.WorldSeed);
    }

    private static string ResolveLevelPath()
    {
#if DEBUG
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var staticRoot = Path.Combine(directory.FullName, "Assets", "Static");
            if (Directory.Exists(staticRoot))
                return Path.Combine(staticRoot, "levels", LevelId, "level.db");
        }
#endif

        return Path.Combine(AssetPaths.Root, "levels", LevelId, "level.db");
    }
}
```

- [ ] **Step 3: Load from the file in the game**

In `App2d/SideScrollerGame.cs`, replace the generator/map block added in Task 5 Step 3 with:

```csharp
        var tileMap = LevelBootstrap2D.LoadOrBake(Traversal);
        var groundHeights = TileGroundHeights2D.Derive(tileMap);

        _level = new SideScrollerLevel2D(
            Traversal,
            tileMap,
            x => groundHeights[Math.Clamp(x, 0, groundHeights.Length - 1)]);
```

Register a rebake command next to the existing console registrations (near lines 48-50):

```csharp
        DeveloperConsole.RegisterCommand(
            "level_rebake",
            "Regenerate the cavern level file from the world generator. Restart to see it.",
            _ =>
            {
                LevelBootstrap2D.Bake(Traversal);
                return ConsoleCommandResult.From($"baked {LevelBootstrap2D.CavernLevelPath}");
            });
```

Add `using App2d.Diagnostics;` if it is not already present.

- [ ] **Step 4: Build, run, and commit the baked level**

Run: `dotnet build App2d.slnx` then run the game once so the bake happens.

Confirm the file exists and is a real SQLite database:

Run: `ls -la Assets/Static/levels/cavern/ && head -c 16 Assets/Static/levels/cavern/level.db | xxd | head -1`
Expected: `level.db` exists; the first 16 bytes read `SQLite format 3`.

WAL mode leaves `level.db-wal` and `level.db-shm` beside the database. Those are transient and must not be committed — add to `.gitignore`:

```
Assets/Static/levels/**/*.db-wal
Assets/Static/levels/**/*.db-shm
```

- [ ] **Step 5: Play the game and confirm it is identical**

Run the game again (it now loads from the file rather than baking). Confirm the same checks as Task 5 Step 5: spawn, enemies, platforms, camera, goal. The terrain should be visually identical to before — same pits, same platforms, same spike patches.

- [ ] **Step 6: Commit**

```bash
git add -A App2d Assets/Static/levels .gitignore
git commit -m "Load terrain from the authored level file"
```

---

### Task 7: Bake characterization test

The test that proves the format before it is trusted: every tile in the file equals what the generator produces. It is deleted along with the generator.

**Files:**
- Modify: `App2d.Gameplay.Tests/App2d.Gameplay.Tests.csproj`
- Test: `App2d.Gameplay.Tests/World/LevelBakeCharacterizationTests.cs`

**Interfaces:**
- Consumes: `JumpableWorldGenerator2D`, `EditableTileMap2D`, `LevelDatabase2D`, `TraversalMetrics2D.FromPlayerAsset`, `SideScrollerLevel2D` world constants.
- Produces: nothing — this is a leaf.

- [ ] **Step 1: Reference `App2d.Levels` from the gameplay test project**

In `App2d.Gameplay.Tests/App2d.Gameplay.Tests.csproj`, add to the `ProjectReference` group:

```xml
    <ProjectReference Include="..\App2d.Levels\App2d.Levels.csproj" />
```

- [ ] **Step 2: Write the test**

Create `App2d.Gameplay.Tests/World/LevelBakeCharacterizationTests.cs`:

```csharp
using App2d.Gameplay;
using App2d.Levels;
using App2d.Tiles;

namespace App2d.Gameplay.Tests.World;

/// <summary>
/// Proves the level format reproduces the world generator exactly. Delete this file when
/// <see cref="JumpableWorldGenerator2D"/> is deleted — it characterizes scaffolding.
/// </summary>
public sealed class LevelBakeCharacterizationTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "app2d-bake-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void EveryBakedTileMatchesTheGenerator()
    {
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var generator = new JumpableWorldGenerator2D(
            SideScrollerLevel2D.WorldSeed,
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal);

        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal.TileSize,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
        map.Fill(generator.GetTileKind);

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "level.db");
        using (var database = LevelDatabase2D.Open(path))
            database.Save(map, SideScrollerLevel2D.WorldSeed);

        using var reopened = LevelDatabase2D.Open(path);
        var loaded = reopened.Load();

        var mismatches = 0;
        for (var y = 0; y < SideScrollerLevel2D.WorldHeightTiles; y++)
        {
            for (var x = 0; x < SideScrollerLevel2D.WorldWidthTiles; x++)
            {
                if (loaded.GetTileKind(x, y) != generator.GetTileKind(x, y))
                    mismatches++;
            }
        }

        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void GroundHeightsNeverIndexBelowTheWorld()
    {
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var generator = new JumpableWorldGenerator2D(
            SideScrollerLevel2D.WorldSeed,
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal);
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal.TileSize,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
        map.Fill(generator.GetTileKind);

        // Ground height is provisional and may differ from the generator's TerrainHeight.
        // The only contract is that `height - 1` stays inside the world.
        Assert.All(TileGroundHeights2D.Derive(map), height => Assert.True(height >= 1));
    }
}
```

`TraversalMetrics2D.FromPlayerAsset` needs the runtime asset root, and `App2d.Gameplay.Tests` has no helper for it (verified — the project has no asset-path code). Create `App2d.Gameplay.Tests/TestAssetPath.cs`, mirroring the walk-up in `App2d/AssetPaths.cs`:

```csharp
namespace App2d.Gameplay.Tests;

internal static class TestAssetPath
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var runtimeRoot = Path.Combine(directory.FullName, "Assets", "Runtime");
            if (Directory.Exists(runtimeRoot))
                return runtimeRoot;
        }

        throw new InvalidOperationException(
            "Assets/Runtime was not found. Run: python tools/ArtPipeline/build_runtime_assets.py");
    }
}
```

- [ ] **Step 3: Run the test**

Run: `dotnet test App2d.slnx --filter "FullyQualifiedName~LevelBakeCharacterization"`
Expected: PASS — 2 tests. A failure here means the codec or chunk clipping is wrong; fix that rather than the test.

- [ ] **Step 4: Run the whole suite and build**

Run: `dotnet build App2d.slnx` then `dotnet test App2d.slnx`
Expected: build succeeds with no warnings introduced; all tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A App2d.Gameplay.Tests
git commit -m "Characterize the baked level against the world generator"
```

---

### Task 8: Document the level format

The README describes every module; a new project and a new asset kind belong in it.

**Files:**
- Modify: `README.md`
- Modify: `Assets/README.md`

- [ ] **Step 1: Add `App2d.Levels` to the module list in `README.md`**

In the bulleted list describing the engine's grouping, after the `App2d.Tiles` mention, add:

```markdown
- `App2d.Levels` stores authored levels as SQLite files. Tiles are run-length encoded per
  chunk so a single edit rewrites a single row; a missing chunk row means an entirely empty
  chunk. It is the only project that references `Microsoft.Data.Sqlite`, and it never
  references gameplay — terrain is data, not a generator.
```

Also update the opening paragraph listing the projects to include `App2d.Levels`.

- [ ] **Step 2: Document the level asset in `Assets/README.md`**

After the paragraph describing runtime content organization, add:

```markdown
Levels live at `levels/<id>/level.db` — one SQLite file per level, holding the tile grid and
(later) its entities. They are durable authored content, so they are committed under
`Static` and read from there directly in Debug builds; the pipeline copies them into
`Runtime` like any other static asset. The `.db-wal` and `.db-shm` files SQLite leaves
alongside are transient and are not committed.
```

- [ ] **Step 3: Commit**

```bash
git add README.md Assets/README.md
git commit -m "Document the level format and App2d.Levels"
```

---

## Definition of done

- `dotnet build App2d.slnx` succeeds and `dotnet test App2d.slnx` passes.
- `git grep -n "JumpableWorldGenerator2D" -- App2d.Gameplay App2d` matches only the generator's own file, `LevelBootstrap2D`, and the characterization test.
- `Assets/Static/levels/cavern/level.db` is committed and the game loads terrain from it.
- The game plays as it did before: spawn on ground, reachable goal, enemies on ground, pits and spikes where they were.
- Deleting the generator later means deleting `JumpableWorldGenerator2D.cs`, `LevelBootstrap2D.Bake`, the `level_rebake` command, and `LevelBakeCharacterizationTests.cs` — nothing else.
