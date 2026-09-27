# Grids

`App2d.Core.Grids` provides reusable grid coordinates, geometry, and storage. Tile maps,
wind fields, and spatial buckets can choose their own cell sizes and data while sharing
the same math. Everything here is independent of rendering and the shape hierarchy.

| Type | Purpose |
| --- | --- |
| `GridCell2D` | Signed integer `(X, Y)` coordinates; also a dictionary key |
| `GridSize2D` | Width, height, checked cell count, row-major indices, and partial-block counts |
| `GridCellRange2D` | Inclusive minimum/maximum cells, containment, intersection, and allocation-free iteration |
| `GridGeometry2D` | Cell size and origin; world/cell conversion, centers, rectangles, and rectangle-to-cell ranges |
| `Grid2D<T>` | One flat array with `[x, y]`, `[cell]`, and `[index]` access, plus row spans, fill, and clear |

Geometry and storage compose: use `Grid2D<Vector2>` for a dense wind field, or keep
`GridGeometry2D` alongside a dictionary for a sparse, potentially unbounded spatial index.
The coordinate, size, range, and geometry types are small value types. Cells do not allocate
individual objects. Existing storage can reuse `GridSize2D` and `GridGeometry2D` directly.

## Dense wind data

```csharp
using App2d.Core.Grids;
using System.Numerics;

var wind = new Grid2D<Vector2>(64, 32, cellSize: 128f, origin: new(-4096, -2048));
wind.Fill(new Vector2(12f, 0f));
wind[4, 2] = new Vector2(25f, 3f);

int index = wind.Size.GetIndex(4, 2); // 4 + 2 * 64
GridCell2D coordinates = wind.Size.GetCell(index);
var cellBounds = wind.GetCellBounds(coordinates); // Bounds2D also implements IRect2D.
var center = wind.Geometry.GetCellCenter(coordinates);

if (wind.TryGetCell(playerPosition, out var cell))
{
    Vector2 localWind = wind[cell];
    // Apply this wind sample in the consuming system.
}
```

Use a `Vector2` cell size for rectangular cells. Indexers return by reference, so
`wind[4, 2].X = 30f` also works. `AsSpan()` and `GetRowSpan(y)` expose the same storage.
Interpolation, evolution over time, and object response belong to the wind system.

## Sparse spatial buckets

```csharp
var geometry = new GridGeometry2D(256f);
var buckets = new Dictionary<GridCell2D, List<int>>();

if (geometry.TryGetCellRange(objectBounds, out var cells) && cells.CellCount <= 4096)
{
    foreach (var cell in cells)
    {
        if (!buckets.TryGetValue(cell, out var bucket))
            buckets.Add(cell, bucket = []);
        bucket.Add(objectId);
    }
}
else
{
    overflow.Add(objectId);
}
```

The existing `CollisionSystem2D` now uses these grid primitives for its static and dynamic
buckets. It still owns insertion, invalidation, pair filtering, candidate deduplication, and
overflow handling. A grid query provides candidates; precise geometry remains a separate step.

## Boundaries and validation

- Origin is the lower-left corner of cell `(0, 0)`, with Y increasing upward. Cell size must
  be positive and finite. World-to-cell uses floor: with size 10, X=-0.1 belongs to column -1.
- Point ownership is half-open: a cell includes its minimum edge and excludes its maximum.
  `Grid2D<T>.TryGetCell` returns false outside the finite grid; `WorldToCellClamped` explicitly
  selects the nearest edge cell instead. `GridGeometry2D` accepts signed cells independently
  of storage; the dense grid's indexers and `GetCellBounds` require an in-bounds cell.
- `GetCellRange(bounds)` floors both endpoints and includes the cell owning the maximum
  endpoint. This is conservative coverage for closed bounds: exact edge contacts must survive
  broad-phase lookup. A point-sized rectangle maps to one cell. Consequently, querying the
  world rectangle of a cell also includes its upper-edge neighbors.
- `GetCellRange(bounds, size)` clips to a finite grid before converting. Wholly disjoint
  rectangles return an empty range; touching the outer boundary includes the last cell.
  The viewport clips to the actual map rectangle first, so partial chunks do not enlarge it.
- `GridSize2D` permits empty dimensions and rejects counts above `int.MaxValue`. Index checks
  validate X and Y separately so X=width cannot alias the next row. `DivideRoundUp` counts
  partial chunks without overflowing the addition.
- A default range is empty. Range endpoints are inclusive; `CellCount` is a checked `long`
  contract. Iteration is row-major and safely terminates even at `int.MaxValue` coordinates.
- A default geometry is uninitialized. Try methods return false for uninitialized geometry,
  nonfinite coordinates, unrepresentable cell coordinates, or an oversized range. A spatial
  index must fall back on false, rather than discard that object's candidates.

`TileMap2D` and `EditableTileMap2D` store their data in `Grid2D<T>` while keeping tile-specific
events and collision meshing. `TileMapGridExtensions2D` adapts any existing tile-map interface
to `GridSize`, `GridGeometry`, `ChunkGridSize`, and `ChunkGridGeometry` without changing its storage.
`ViewportTerrainSource2D` uses this adapter for visible chunk ranges and cache eviction.
