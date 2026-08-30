# Collision Cleanup & Similarity2D Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deduplicate the 2D collision layer, unify narrow-phase dispatch, fix real allocation offenders, split tiles into their own project, then migrate collision to a validated `Similarity2D` transform.

**Architecture:** Source files stay in the `App2d` folder; sibling csprojs compile them via links. Phase 1 is behavior-preserving refactors (except the documented halfspace contact-point improvement) guarded by characterization tests. Phase 2 changes narrow-phase/ray signatures from `Transform2D`/`Matrix3x2` to `Similarity2D` extracted and cached on `SpatialObject2D`.

**Tech Stack:** .NET 10, System.Numerics, xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-08-29-collision-cleanup-design.md`

## Global Constraints

- Namespaces stay `App2d.Engine.*` even where assembly membership changes.
- Every task ends with `dotnet build App2d.slnx --nologo -v q` and `dotnet test App2d.Tests --nologo -v q` green (baseline 14/14; grows as tasks add tests).
- All builds must stay at 0 warnings (`AnalysisModePerformance=All` is on).
- Code style: file-scoped namespaces, expression bodies where the codebase already uses them, `var`, guard helpers (`ArgGuard`/`StateGuard`).
- Commit after each task with a conventional message; the user's pre-existing working-tree tweaks (CollisionSystem2D.cs + provider files) fold into the first commit that touches those files.

---

### Task 1: Characterization tests for the seams about to change

**Files:**
- Test: `App2d.Tests\Collision\ShapeContactCharacterizationTests.cs` (create)

**Interfaces:**
- Consumes: `ShapeCollisionContactProvider2D.TryGetContact`, `SpatialObject2D`, shapes.
- Produces: pinned expectations for normals/depths that later tasks must keep green.

- [ ] **Step 1: Write the tests** (they should PASS against current code — they pin behavior)

```csharp
using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Geometry;

namespace App2d.Tests.Collision;

public sealed class ShapeContactCharacterizationTests
{
    private static readonly ShapeCollisionContactProvider2D Provider = new();

    private static SpatialObject2D At(IShape2D shape, Vector2 position, float rotation = 0f)
    {
        var worldObject = new SpatialObject2D(shape);
        worldObject.Transform.Position = position;
        worldObject.Transform.Rotation = rotation;
        return worldObject;
    }

    [Fact]
    public void RectangleVsHalfSpaceReportsUpNormalAndDepth()
    {
        var box = At(Rectangle2D.FromSize(new Vector2(10f, 10f)), new Vector2(0f, -3f));
        var ground = At(new HalfSpace2D(Vector2.UnitY, 0f), Vector2.Zero);

        Assert.True(Provider.TryGetContact(box, ground, out var contact));
        Assert.Equal(1f, contact.Normal.Y, 3);
        Assert.Equal(8f, contact.PenetrationDepth, 3);

        Assert.True(Provider.TryGetContact(ground, box, out var flipped));
        Assert.Equal(-1f, flipped.Normal.Y, 3);
        Assert.Equal(8f, flipped.PenetrationDepth, 3);
    }

    [Fact]
    public void CapsuleVsHalfSpaceReportsDepthFromDeepestPoint()
    {
        var capsule = At(new Capsule2D(new Vector2(0f, -2f), new Vector2(0f, 2f), 1f), new Vector2(0f, 1f));
        var ground = At(new HalfSpace2D(Vector2.UnitY, 0f), Vector2.Zero);

        Assert.True(Provider.TryGetContact(capsule, ground, out var contact));
        Assert.Equal(1f, contact.Normal.Y, 3);
        Assert.Equal(2f, contact.PenetrationDepth, 3);
    }

    [Fact]
    public void CircleVsHalfSpaceContactPointLiesOnTheBoundary()
    {
        var circle = At(new Circle2D(2f), new Vector2(5f, 1f));
        var ground = At(new HalfSpace2D(Vector2.UnitY, 0f), Vector2.Zero);

        Assert.True(Provider.TryGetContact(circle, ground, out var contact));
        Assert.Equal(0f, contact.Point.Y, 3);
        Assert.Equal(1f, contact.PenetrationDepth, 3);
    }

    [Fact]
    public void OverlappingCapsulesResolveAlongTheShortestAxis()
    {
        var first = At(new Capsule2D(new Vector2(-2f, 0f), new Vector2(2f, 0f), 1f), Vector2.Zero);
        var second = At(new Capsule2D(new Vector2(-2f, 0f), new Vector2(2f, 0f), 1f), new Vector2(0f, 1.5f));

        Assert.True(Provider.TryGetContact(first, second, out var contact));
        Assert.Equal(0f, contact.Normal.X, 3);
        Assert.Equal(-1f, contact.Normal.Y, 3);
        Assert.Equal(0.5f, contact.PenetrationDepth, 3);
    }

    [Fact]
    public void RotatedRectangleVsRectangleFindsMtv()
    {
        var first = At(Rectangle2D.FromSize(new Vector2(4f, 4f)), Vector2.Zero);
        var second = At(Rectangle2D.FromSize(new Vector2(4f, 4f)), new Vector2(3.5f, 0f), MathF.PI / 4f);

        Assert.True(Provider.TryGetContact(first, second, out var contact));
        Assert.True(contact.PenetrationDepth > 0f);
        Assert.True(MathF.Abs(contact.Normal.Length() - 1f) < 0.001f);
    }
}
```

- [ ] **Step 2: Also pin the tile meshers.** Create `App2d.Tests\Tiles\TileMeshingCharacterizationTests.cs`:

```csharp
using System.Numerics;
using App2d.Engine.Tiles;

namespace App2d.Tests.Tiles;

public sealed class TileMeshingCharacterizationTests
{
    [Fact]
    public void TileMapMergesSolidBlocksGreedily()
    {
        var map = new TileMap2D(4, 3, 2f);
        map.Fill(0, 0, 4, 2);
        map.SetSolid(0, 2);

        var rectangles = map.CollisionRectangles;

        Assert.Equal(2, rectangles.Count);
        Assert.Contains(rectangles, r => r.Min == Vector2.Zero && r.Max == new Vector2(8f, 4f));
        Assert.Contains(rectangles, r => r.Min == new Vector2(0f, 4f) && r.Max == new Vector2(2f, 6f));
    }

    [Fact]
    public void ProceduralMapKeepsOneWayRowsOneTileTall()
    {
        // 3x3 chunk: bottom row solid, middle row one-way, top empty.
        var map = new ProceduralTileMap2D(3, 3, 1f, 3, (x, y) => y switch
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
}
```

- [ ] **Step 3: Run the new tests — they must PASS against current code.** If an expectation fails, fix the expectation (these pin behavior; they don't change it). `dotnet test App2d.Tests --nologo -v q`
- [ ] **Step 4: Commit** — `test: pin collision contact and tile meshing behavior`

---

### Task 2: Perp helpers

**Files:**
- Modify: `App2d\Engine\Mathematics\Vector2Extensions.cs`
- Modify: `App2d\Engine\Collision\Contacts\ShapeCollision2D.Capsules.cs:29,30,88`
- Modify: `App2d\Engine\Collision\Contacts\ShapeCollision2D.Circles.cs:66`
- Modify: `App2d\Engine\Collision\Contacts\ShapeCollision2D.Polygons.cs:81-82,93`
- Modify: `App2d\Engine\Collision\CollisionMath2D.cs:57,65`
- Modify: `App2d\Engine\Collision\Queries\RayIntersection2D.cs:168,180-181,265,393-394`

**Interfaces:**
- Produces: `Vector2 PerpCcw(this Vector2 v)` = `(-v.Y, v.X)`; `Vector2 PerpCw(this Vector2 v)` = `(v.Y, -v.X)`.

- [ ] **Step 1: Add the helpers**

```csharp
public static class Vector2Extensions
{
    public static float Cross(this Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;

    /// <summary>Rotates 90° counter-clockwise (in +Y-up orientation).</summary>
    public static Vector2 PerpCcw(this Vector2 value) => new(-value.Y, value.X);

    /// <summary>Rotates 90° clockwise (in +Y-up orientation).</summary>
    public static Vector2 PerpCw(this Vector2 value) => new(value.Y, -value.X);
}
```

- [ ] **Step 2: Replace every hand-rolled perp.** `new Vector2(-d.Y, d.X)` → `d.PerpCcw()`; `new Vector2(d.Y, -d.X)` → `d.PerpCw()` at the file:line list above. In `RayIntersection2D`, delete the private `Cross` (line 393) and call the `Vector2Extensions.Cross` extension instead (add `using App2d.Engine.Mathematics;`). `CollisionMath2D` needs no new using (already has it).
- [ ] **Step 3: Build + test green.**
- [ ] **Step 4: Commit** — `refactor: add PerpCcw/PerpCw and remove hand-rolled perpendiculars`

---

### Task 3: Rectangle corners + transpose-direction helper

**Files:**
- Modify: `App2d\Engine\Geometry\Rectangle2D.cs`
- Create: `App2d\Engine\Mathematics\Matrix3x2Extensions.cs`
- Modify: `App2d\Engine\Collision\Contacts\ShapeCollision2D.Utilities.cs` (delete `WriteLocalRectangleVertices`, rewire `WriteWorldRectangleVertices`)
- Modify: `App2d\Engine\Collision\Contacts\ShapeCollision2D.Circles.cs:12`
- Modify: `App2d\Engine\Collision\Queries\RayIntersection2D.cs:93-99,387-391`
- Modify: `App2d\Engine\Collision\Contacts\HalfSpaceCollision2D.cs:19`

**Interfaces:**
- Produces: `void Rectangle2D.WriteCorners(Span<Vector2> corners)` (4 corners, CCW from Min); `Vector2 Matrix3x2Extensions.TransposeTransformDirection(this Matrix3x2 m, Vector2 d)`.

- [ ] **Step 1: Add `WriteCorners` to `Rectangle2D`**

```csharp
/// <summary>Writes the four local-space corners counter-clockwise from Min.</summary>
public void WriteCorners(Span<Vector2> corners)
{
    corners[0] = Min;
    corners[1] = new Vector2(Max.X, Min.Y);
    corners[2] = Max;
    corners[3] = new Vector2(Min.X, Max.Y);
}
```

- [ ] **Step 2: Add `Matrix3x2Extensions`**

```csharp
using System.Numerics;

namespace App2d.Engine.Mathematics;

public static class Matrix3x2Extensions
{
    /// <summary>
    /// Multiplies a direction by the transpose of the linear part (row-vector
    /// convention). Maps world support/normal directions into the space the
    /// matrix transforms from.
    /// </summary>
    public static Vector2 TransposeTransformDirection(this Matrix3x2 matrix, Vector2 direction) => new(
        matrix.M11 * direction.X + matrix.M12 * direction.Y,
        matrix.M21 * direction.X + matrix.M22 * direction.Y);
}
```

- [ ] **Step 3: Rewire call sites.** Delete `WriteLocalRectangleVertices`; `WriteWorldRectangleVertices` and `CircleVsRectangle` call `rectangle.WriteCorners(vertices)`. In `RayIntersection2D` replace the inline corner span (lines 93-99) with `WriteCorners` into a `stackalloc Vector2[4]`, and replace `TransformNormalToWorld(localHit.Normal, worldToLocal)` with `worldToLocal.TransposeTransformDirection(localHit.Normal)` (delete the private method). In `HalfSpaceCollision2D` line 19 becomes `var localDirection = objectToWorld.TransposeTransformDirection(worldNormal);`.
- [ ] **Step 4: Build + test green.**
- [ ] **Step 5: Commit** — `refactor: rectangle corner writer and transpose-direction helper`

---

### Task 4: Fold halfspace into the dispatch table

**Files:**
- Create: `App2d\Engine\Collision\Contacts\ShapeCollision2D.HalfSpaces.cs`
- Modify: `App2d\Engine\Collision\Contacts\ShapeCollision2D.cs` (add dispatch row)
- Modify: `App2d\Engine\Collision\Contacts\HalfSpaceCollision2D.cs` (delegate)
- Modify: `App2d\Engine\Collision\ShapeCollisionContactProvider2D.cs` (one-liner)

**Interfaces:**
- Produces: `internal static bool ShapeCollision2D.TryGetConvexHalfSpacePenetration(IConvexShape2D convex, Transform2D convexTransform, HalfSpace2D halfSpace, Transform2D halfSpaceTransform, out Vector2 worldNormal, out float penetration, out Vector2 deepestPoint)`.
- Behavior change (intended): rect/capsule/polygon-vs-halfspace contact `Point` becomes the deepest support point projected onto the boundary (was `Transform.Position`). Normals/depths unchanged — Task 1 tests must stay green.

- [ ] **Step 1: New partial file**

```csharp
using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult HalfSpaceAgainst(HalfSpace2D halfSpace, Transform2D halfSpaceTransform, IShape2D other, Transform2D otherTransform) =>
        other switch
        {
            Circle2D circle => CircleVsHalfSpace(circle, otherTransform, halfSpace, halfSpaceTransform).Flipped(),
            IConvexShape2D convex => ConvexVsHalfSpace(convex, otherTransform, halfSpace, halfSpaceTransform).Flipped(),
            _ => CollisionResult.None
        };

    private static CollisionResult ConvexVsHalfSpace(IConvexShape2D convex, Transform2D convexTransform, HalfSpace2D halfSpace, Transform2D halfSpaceTransform)
    {
        if (!TryGetConvexHalfSpacePenetration(convex, convexTransform, halfSpace, halfSpaceTransform, out var normal, out var penetration, out var deepestPoint))
            return CollisionResult.None;

        // Report the deepest point projected onto the boundary, matching the circle row.
        return CollisionResult.From(new CollisionContact2D(deepestPoint + normal * penetration, normal, penetration));
    }

    internal static bool TryGetConvexHalfSpacePenetration(IConvexShape2D convex, Transform2D convexTransform, HalfSpace2D halfSpace, Transform2D halfSpaceTransform, out Vector2 worldNormal, out float penetration, out Vector2 deepestPoint)
    {
        (worldNormal, var worldOffset) = CollisionMath2D.GetWorldPlane(halfSpace, halfSpaceTransform);
        var objectToWorld = convexTransform.LocalToWorldMatrix;
        var localDirection = objectToWorld.TransposeTransformDirection(worldNormal);
        deepestPoint = Vector2.Transform(convex.GetSupportPoint(-localDirection), objectToWorld);
        penetration = worldOffset - Vector2.Dot(deepestPoint, worldNormal);
        return penetration > 0f;
    }
}
```

- [ ] **Step 2: Add the dispatch row** in `ShapeCollision2D.Dispatch`, after the `Rectangle2D` arm: `HalfSpace2D halfSpace => HalfSpaceAgainst(halfSpace, firstTransform, second, secondTransform),`
- [ ] **Step 3: `HalfSpaceCollision2D.TryGetContact` delegates** to `ShapeCollision2D.TryGetConvexHalfSpacePenetration` (same guards, builds `HalfSpaceContact2D(worldNormal, penetration)`); `ConstrainOutside` unchanged. Remove its now-unused matrix code and stale usings.
- [ ] **Step 4: Provider becomes**

```csharp
public sealed class ShapeCollisionContactProvider2D : ICollisionContactProvider2D
{
    public bool TryGetContact(SpatialObject2D first, SpatialObject2D second, out CollisionContact2D contact) =>
        ShapeCollision2D.TryGetContact(first, second, out contact);
}
```

- [ ] **Step 5: Build + test green** (Task 1 halfspace tests prove normals/depths held).
- [ ] **Step 6: Commit** — `refactor: dispatch convex-vs-halfspace through ShapeCollision2D`

---

### Task 5: Shared polygon/segment geometry

**Files:**
- Create: `App2d\Engine\Geometry\PolygonGeometry2D.cs`
- Move: `App2d\Engine\Collision\ClosestPoint2D.cs` → `App2d\Engine\Geometry\ClosestPoint2D.cs` (namespace → `App2d.Engine.Geometry`) via `git mv`
- Modify: `App2d\Engine\Geometry\ConvexPolygon2D.cs`, `App2d\Engine\Geometry\Capsule2D.cs`
- Modify: `App2d\Engine\Collision\Contacts\ShapeCollision2D.Polygons.cs`, `...Capsules.cs`, `...Circles.cs`
- Modify: `App2d\Engine\Collision\Queries\RayIntersection2D.cs`

**Interfaces:**
- Produces (all on `public static class PolygonGeometry2D`, namespace `App2d.Engine.Geometry`):
  - `float SignedAreaTwice(ReadOnlySpan<Vector2> vertices)`
  - `bool ContainsPoint(ReadOnlySpan<Vector2> vertices, Vector2 point, float collinearEpsilon = 0.0001f)` (winding test)
  - `Vector2 GetSupportPoint(ReadOnlySpan<Vector2> vertices, Vector2 direction)`
  - `Vector2 ClosestPointOnPerimeter(Vector2 point, ReadOnlySpan<Vector2> vertices, out int edgeIndex)`
  - `Vector2 GetOutwardEdgeNormal(ReadOnlySpan<Vector2> vertices, int edgeIndex)` (normalized; `UnitY` fallback for degenerate edges)

- [ ] **Step 1: Create `PolygonGeometry2D`** — move the bodies of `ShapeCollision2D.Polygons.ClosestPointOnPolygon`, `ContainsPoint`, `GetOutwardEdgeNormal`, `GetPolygonSupportPoint` verbatim (perp calls from Task 2), plus `SignedAreaTwice` extracted from `GetOutwardEdgeNormal`. `GetOutwardEdgeNormal` keeps computing the area internally.
- [ ] **Step 2: Delegate the shapes.** `ConvexPolygon2D.ContainsPoint` → `PolygonGeometry2D.ContainsPoint(_vertices, localPoint, Epsilon)`; `ConvexPolygon2D.GetSupportPoint` → `PolygonGeometry2D.GetSupportPoint(_vertices, localDirection)`. Move `ClosestPoint2D.cs` and update its namespace; `Capsule2D.ContainsPoint` body becomes `Vector2.DistanceSquared(localPoint, ClosestPoint2D.OnSegment(localPoint, Start, End)) <= Radius * Radius`.
- [ ] **Step 3: Delete the four private copies** from `ShapeCollision2D.Polygons.cs` and point all `ShapeCollision2D` call sites at `PolygonGeometry2D`. In `CapsuleVsCapsule`, replace the inline min/normal selection loop (lines 48-70 of `Capsules.cs`) with `TryUpdateMtv` calls and an early `return CollisionResult.None` when it reports separation. In `RayIntersection2D.TryConvexPolygon`, keep the local slab loop but compute `outward` as `signedAreaTwice >= 0f ? edge.PerpCw() : edge.PerpCcw()` with `PolygonGeometry2D.SignedAreaTwice`.
- [ ] **Step 4: Fix usings.** Callers in `App2d.Engine.Collision.*` already import `App2d.Engine.Geometry`. Check `ClosestPoint2D` consumers (`ShapeCollision2D.*`, tests) still compile — the old namespace resolved via parent-namespace lookup, the new one via the existing `using`.
- [ ] **Step 5: Build + test green.**
- [ ] **Step 6: Commit** — `refactor: extract PolygonGeometry2D and move ClosestPoint2D into geometry`

---

### Task 6: One tile mesher

**Files:**
- Create: `App2d\Engine\Tiles\TileRectangleMesher2D.cs`
- Modify: `App2d\Engine\Tiles\TileMap2D.cs`, `App2d\Engine\Tiles\ProceduralTileMap2D.cs`

**Interfaces:**
- Produces: `readonly record struct TileCellRectangle2D(int X, int Y, int Width, int Height, TileKind2D Kind)`; `delegate TileKind2D TileRectangleMesher2D.KindAt(int x, int y)`; `void TileRectangleMesher2D.Mesh(int width, int height, KindAt getKind, List<TileCellRectangle2D> results)`.
- Public APIs of both tile maps unchanged.

- [ ] **Step 1: Create the mesher**

```csharp
namespace App2d.Engine.Tiles;

public readonly record struct TileCellRectangle2D(int X, int Y, int Width, int Height, TileKind2D Kind);

public static class TileRectangleMesher2D
{
    public delegate TileKind2D KindAt(int x, int y);

    /// <summary>
    /// Greedy rectangle merge in cell space: horizontal runs of one kind, grown
    /// vertically only for solid kinds (one-way surfaces stay one tile tall so
    /// their walkable tops are preserved).
    /// </summary>
    public static void Mesh(int width, int height, KindAt getKind, List<TileCellRectangle2D> results)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNull(getKind);
        ArgGuard.ThrowIfNull(results);

        var consumed = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var kind = getKind(x, y);
                if (!kind.IsCollidable() || consumed[index])
                    continue;

                var rectangleWidth = 1;
                while (x + rectangleWidth < width &&
                       !consumed[index + rectangleWidth] &&
                       getKind(x + rectangleWidth, y) == kind)
                {
                    rectangleWidth++;
                }

                var rectangleHeight = 1;
                while (kind.IsSolid() &&
                       y + rectangleHeight < height &&
                       IsUnconsumedRun(getKind, consumed, width, x, y + rectangleHeight, rectangleWidth, kind))
                {
                    rectangleHeight++;
                }

                for (var row = y; row < y + rectangleHeight; row++)
                    consumed.AsSpan(row * width + x, rectangleWidth).Fill(true);

                results.Add(new TileCellRectangle2D(x, y, rectangleWidth, rectangleHeight, kind));
            }
        }
    }

    private static bool IsUnconsumedRun(KindAt getKind, ReadOnlySpan<bool> consumed, int rowWidth, int x, int y, int width, TileKind2D kind)
    {
        var start = y * rowWidth + x;
        for (var offset = 0; offset < width; offset++)
        {
            if (consumed[start + offset] || getKind(x + offset, y) != kind)
                return false;
        }

        return true;
    }
}
```

- [ ] **Step 2: `TileMap2D` delegates.** Add fields `private readonly List<TileCellRectangle2D> _meshBuffer = [];` and a cached `private readonly TileRectangleMesher2D.KindAt _kindAt;` initialized in the constructor to `(x, y) => _solidTiles[y * Width + x] ? TileKind2D.Solid : TileKind2D.Empty`. `RebuildCollisionRectangles` becomes: clear both lists, `TileRectangleMesher2D.Mesh(Width, Height, _kindAt, _meshBuffer)`, convert each cell to `Bounds2D` (`min = Origin + new Vector2(cell.X, cell.Y) * TileSize`, `max = min + new Vector2(cell.Width, cell.Height) * TileSize`). Delete `IsUnconsumedSolidRun`.
- [ ] **Step 3: `ProceduralTileMap2D` delegates.** Keep the `tiles` prefetch (the kind callback may be expensive user code). Replace the merge loops with `Mesh(width, height, (x, y) => tiles[y * width + x], cells)` into a local `List<TileCellRectangle2D>`, then convert with the `startX/startY` offset into `TileCollisionRectangle2D`s. Delete `IsTileRun`.
- [ ] **Step 4: Build + test green** (Task 1 tile tests prove equivalence).
- [ ] **Step 5: Commit** — `refactor: single greedy tile mesher shared by both tile maps`

---

### Task 7: Allocation fixes

**Files:**
- Modify: `App2d\Gameplay\SideScrollerChunkStreamer2D.cs:38`
- Modify: `App2d\Engine\Physics\PhysicsWorld2D.cs:96`
- Modify: `App2d\Engine\Collision\CollisionSystem2D.cs` (`RebuildIndex`)

**Interfaces:** none new; internals only.

- [ ] **Step 1: Streamer.** Add `private readonly List<TileChunk2D> _unloadBuffer = [];`. Replace `foreach (var chunk in _loadedChunks.Keys.ToArray())` with: clear the buffer, collect out-of-range chunks into it while iterating `_loadedChunks.Keys`, then `foreach (var chunk in _unloadBuffer) Unload(chunk);`.
- [ ] **Step 2: `IsTouching`.** Replace the LINQ `Any` with a `foreach` over `_lastContacts` returning true on the first match (also drop the now-unused `System.Linq` using if nothing else needs it).
- [ ] **Step 3: Bucket pruning.** In `CollisionSystem2D`, add `private const int MaximumRetainedCells = 1_024;` and `private readonly List<GridCell> _staleCells = [];`. At the end of `RebuildIndex`, when `cells.Count > MaximumRetainedCells`, collect keys with empty buckets into `_staleCells` and remove them from `cells`.
- [ ] **Step 4: Build + test green.**
- [ ] **Step 5: Commit** — `perf: remove per-frame allocations in streaming, touch queries, and grid retention` (folds in the user's pending working-tree tweaks to `CollisionSystem2D.cs`/providers if still uncommitted).

---

### Task 8: Project re-wiring — tiles out of Collision

**Files:**
- Modify: `App2d.Core\App2d.Core.csproj`, `App2d.Collision\App2d.Collision.csproj`, `App2d.Gameplay\App2d.Gameplay.csproj`, `App2d\App2d.csproj`, `App2d.Tests\App2d.Tests.csproj`, `App2d.Rendering\App2d.Rendering.csproj`, `App2d.slnx`
- Create: `App2d.Tiles\App2d.Tiles.csproj`

**Interfaces:** assembly membership only; namespaces unchanged.

- [ ] **Step 1: Core picks up the shape layer.** Add to `App2d.Core.csproj`'s ItemGroup:

```xml
<Compile Include="..\App2d\Engine\SpatialObject2D.cs" Link="Engine\SpatialObject2D.cs" />
<Compile Include="..\App2d\Engine\Geometry\**\*.cs" Link="Engine\Geometry\%(RecursiveDir)%(Filename)%(Extension)" />
```

- [ ] **Step 2: New `App2d.Tiles\App2d.Tiles.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisModePerformance>All</AnalysisModePerformance>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\App2d.Core\App2d.Core.csproj" />
    <Compile Include="..\App2d\Engine\Tiles\**\*.cs"
             Link="Engine\Tiles\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Slim `App2d.Collision.csproj`.** Delete the `SpatialObject2D`, `Geometry`, and `Tiles` links (the whole `Shapes\` group); keep the five Collision links; change the Queries link prefix from `Raycasts\` to `Queries\` so the VS view matches the folder.
- [ ] **Step 4: Consumers.** Add `<ProjectReference Include="..\App2d.Tiles\App2d.Tiles.csproj" />` to `App2d.Gameplay.csproj`, `App2d.csproj`, and `App2d.Tests.csproj`. Add `<Project Path="App2d.Tiles/App2d.Tiles.csproj" />` to `App2d.slnx`.
- [ ] **Step 5: Try dropping `App2d.Rendering`'s Collision reference** (its sources likely only need Geometry, now in Core). If the build fails, restore it and note why in the commit message.
- [ ] **Step 6: Full clean build + test green:** `dotnet build App2d.slnx --nologo -v q` then tests.
- [ ] **Step 7: Commit** — `build: move shapes to Core, split App2d.Tiles out of App2d.Collision`

---

### Task 9: Similarity2D (Phase 2 begins)

**Files:**
- Create: `App2d\Engine\Mathematics\Similarity2D.cs`
- Test: `App2d.Tests\Mathematics\Similarity2DTests.cs` (create)

**Interfaces:**
- Produces (`readonly struct Similarity2D`, namespace `App2d.Engine.Mathematics`):
  - Properties `Vector2 XAxis`, `Vector2 YAxis`, `Vector2 Translation`, `float Scale`
  - `static bool TryFromMatrix(Matrix3x2 matrix, out Similarity2D similarity)`
  - `Vector2 TransformPoint(Vector2 p)`, `Vector2 TransformDirection(Vector2 d)`, `Vector2 TransposeTransformDirection(Vector2 d)`, `Vector2 InverseTransformPoint(Vector2 p)`, `Vector2 InverseTransformDirection(Vector2 d)`

- [ ] **Step 1: Write failing tests**

```csharp
using System.Numerics;
using App2d.Engine.Mathematics;

namespace App2d.Tests.Mathematics;

public sealed class Similarity2DTests
{
    private static Matrix3x2 Trs(Vector2 scale, float rotation, Vector2 translation) =>
        Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(translation);

    [Fact]
    public void RoundTripsRotationScaleTranslation()
    {
        var matrix = Trs(new Vector2(2f, 2f), 0.7f, new Vector2(3f, -4f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));

        var point = new Vector2(1.5f, -2.5f);
        AssertClose(Vector2.Transform(point, matrix), pose.TransformPoint(point));
        AssertClose(point, pose.InverseTransformPoint(pose.TransformPoint(point)));
        Assert.Equal(2f, pose.Scale, 3);
    }

    [Fact]
    public void SupportsMirroring()
    {
        var matrix = Trs(new Vector2(-1f, 1f), 0.3f, new Vector2(5f, 0f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));

        var point = new Vector2(2f, 1f);
        AssertClose(Vector2.Transform(point, matrix), pose.TransformPoint(point));
        AssertClose(point, pose.InverseTransformPoint(pose.TransformPoint(point)));
    }

    [Fact]
    public void DirectionsIgnoreTranslation()
    {
        var matrix = Trs(new Vector2(3f, 3f), 1.1f, new Vector2(100f, 100f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));
        AssertClose(Vector2.TransformNormal(Vector2.UnitX, matrix), pose.TransformDirection(Vector2.UnitX));
    }

    [Fact]
    public void RejectsNonUniformScale() =>
        Assert.False(Similarity2D.TryFromMatrix(Trs(new Vector2(2f, 1f), 0f, Vector2.Zero), out _));

    [Fact]
    public void RejectsDegenerateScale() =>
        Assert.False(Similarity2D.TryFromMatrix(Trs(Vector2.Zero, 0f, Vector2.Zero), out _));

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }
}
```

- [ ] **Step 2: Run — expect compile failure (type missing).**
- [ ] **Step 3: Implement**

```csharp
using System.Numerics;

namespace App2d.Engine.Mathematics;

/// <summary>
/// Rotation + uniform scale + optional mirror + translation — the exact family
/// of transforms 2D collision supports. Row-vector convention matching
/// <see cref="Matrix3x2"/>: XAxis/YAxis are the images of the local axes.
/// </summary>
public readonly struct Similarity2D
{
    private Similarity2D(Vector2 xAxis, Vector2 yAxis, Vector2 translation, float scale)
    {
        XAxis = xAxis;
        YAxis = yAxis;
        Translation = translation;
        Scale = scale;
    }

    public Vector2 XAxis { get; }
    public Vector2 YAxis { get; }
    public Vector2 Translation { get; }
    public float Scale { get; }

    public static bool TryFromMatrix(Matrix3x2 matrix, out Similarity2D similarity)
    {
        var xAxis = new Vector2(matrix.M11, matrix.M12);
        var yAxis = new Vector2(matrix.M21, matrix.M22);
        var xLength = xAxis.Length();
        var yLength = yAxis.Length();
        var largest = Math.Max(xLength, yLength);
        if (largest <= float.Epsilon || MathF.Abs(xLength - yLength) > largest * 0.001f)
        {
            similarity = default;
            return false;
        }

        similarity = new Similarity2D(xAxis, yAxis, matrix.Translation, (xLength + yLength) / 2f);
        return true;
    }

    public Vector2 TransformPoint(Vector2 point) => Translation + XAxis * point.X + YAxis * point.Y;

    public Vector2 TransformDirection(Vector2 direction) => XAxis * direction.X + YAxis * direction.Y;

    /// <summary>
    /// Multiplies by the transpose of the linear part: maps a world support or
    /// normal direction into local space (direction-preserving up to Scale).
    /// </summary>
    public Vector2 TransposeTransformDirection(Vector2 direction) =>
        new(Vector2.Dot(XAxis, direction), Vector2.Dot(YAxis, direction));

    public Vector2 InverseTransformPoint(Vector2 point)
    {
        var relative = point - Translation;
        return new Vector2(Vector2.Dot(XAxis, relative), Vector2.Dot(YAxis, relative)) / (Scale * Scale);
    }

    public Vector2 InverseTransformDirection(Vector2 direction) =>
        TransposeTransformDirection(direction) / (Scale * Scale);
}
```

- [ ] **Step 4: Run tests — all pass.**
- [ ] **Step 5: Commit** — `feat: Similarity2D validated collision transform`

---

### Task 10: CollisionPose and narrow-phase migration

**Files:**
- Modify: `App2d\Engine\SpatialObject2D.cs`
- Modify: `App2d\Engine\Collision\CollisionMath2D.cs` (rewrite)
- Modify: all `App2d\Engine\Collision\Contacts\ShapeCollision2D.*.cs`, `HalfSpaceCollision2D.cs`
- Test: `App2d.Tests\Collision\ShapeContactCharacterizationTests.cs` (extend)

**Interfaces:**
- Produces: `Similarity2D SpatialObject2D.CollisionPose` (cached by `Transform.Version`; throws `InvalidOperationException` via `StateGuard` on non-uniform/degenerate scale).
- Changes: every private `ShapeCollision2D` method and `CollisionMath2D` member takes `Similarity2D pose` instead of `Transform2D transform`. `CollisionMath2D` becomes:
  - `(Vector2 Center, float Radius) GetWorldCircle(Circle2D circle, Similarity2D pose)`
  - `(Vector2 Start, Vector2 End, float Radius) GetWorldCapsule(Capsule2D capsule, Similarity2D pose)`
  - `(Vector2 Normal, float Offset) GetWorldPlane(HalfSpace2D halfSpace, Similarity2D pose)`
  - `TryGetUniformScale` deleted.
- `TryGetConvexHalfSpacePenetration` signature: transforms become `Similarity2D` poses.

- [ ] **Step 1: Add failing tests first** (extend the characterization file):

```csharp
[Fact]
public void MirroredCapsuleStillCollides()
{
    var capsule = At(new Capsule2D(new Vector2(-1f, -2f), new Vector2(-1f, 2f), 1f), Vector2.Zero);
    capsule.Transform.Scale = new Vector2(-1f, 1f); // player facing flip
    var wall = At(Rectangle2D.FromSize(new Vector2(2f, 10f)), new Vector2(2.5f, 0f));

    Assert.True(Provider.TryGetContact(capsule, wall, out var contact));
    Assert.True(contact.PenetrationDepth > 0.4f);
}

[Fact]
public void UniformlyScaledCircleUsesScaledRadius()
{
    var circle = At(new Circle2D(1f), Vector2.Zero);
    circle.Transform.Scale = new Vector2(3f, 3f);
    var other = At(new Circle2D(1f), new Vector2(3.5f, 0f));

    Assert.True(Provider.TryGetContact(circle, other, out var contact));
    Assert.Equal(0.5f, contact.PenetrationDepth, 3);
}

[Fact]
public void NonUniformScaleOnACollidableThrows()
{
    var squashed = At(new Circle2D(1f), Vector2.Zero);
    squashed.Transform.Scale = new Vector2(2f, 1f);
    var other = At(new Circle2D(1f), new Vector2(1f, 0f));

    Assert.Throws<InvalidOperationException>(() => Provider.TryGetContact(squashed, other, out _));
}
```

(The mirrored/scaled tests should pass before AND after; the throws test fails before — currently the ellipse fallback answers — and passes after.)
- [ ] **Step 2: Add `CollisionPose`**

```csharp
private Similarity2D _collisionPose;
private int _collisionPoseVersion = -1;

/// <summary>
/// The validated pose (rotation, uniform scale, mirror, translation) that
/// collision consumes. Collidable objects must not use non-uniform scale;
/// render-only objects may, as long as nothing queries them for collision.
/// </summary>
public Similarity2D CollisionPose
{
    get
    {
        if (_collisionPoseVersion == Transform.Version)
            return _collisionPose;

        StateGuard.ThrowIf(
            !Similarity2D.TryFromMatrix(Transform.LocalToWorldMatrix, out _collisionPose),
            "Collision requires a uniform, non-zero scale on the transform.");
        _collisionPoseVersion = Transform.Version;
        return _collisionPose;
    }
}
```

- [ ] **Step 3: Rewrite `CollisionMath2D`**

```csharp
using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision;

internal static class CollisionMath2D
{
    public static (Vector2 Center, float Radius) GetWorldCircle(Circle2D circle, Similarity2D pose) =>
        (pose.TransformPoint(circle.Center), circle.Radius * pose.Scale);

    public static (Vector2 Start, Vector2 End, float Radius) GetWorldCapsule(Capsule2D capsule, Similarity2D pose) =>
        (pose.TransformPoint(capsule.Start), pose.TransformPoint(capsule.End), capsule.Radius * pose.Scale);

    public static (Vector2 Normal, float Offset) GetWorldPlane(HalfSpace2D halfSpace, Similarity2D pose)
    {
        // Normals transform by (A⁻¹)ᵀ = A / Scale² for this family, so the
        // direct direction transform is exact (mirror included) after normalizing.
        var worldNormal = Vector2.Normalize(pose.TransformDirection(halfSpace.Normal));
        var worldBoundary = pose.TransformPoint(halfSpace.Normal * halfSpace.Offset);
        return (worldNormal, Vector2.Dot(worldBoundary, worldNormal));
    }
}
```

- [ ] **Step 4: Migrate `ShapeCollision2D`.** `TryGetContact` reads `first.CollisionPose`/`second.CollisionPose` once and passes poses down; every `Transform2D` parameter becomes `Similarity2D pose`. `WriteWorldRectangleVertices` and `CircleVsPolygon` transform vertices with `pose.TransformPoint`. The `TryGetWorldCircle`/`TryGetWorldCapsule` failure branches disappear (tuples now): delete the ellipse fallback in `CircleVsCircle`, delete `WriteCircleBoundary`. `TryGetConvexHalfSpacePenetration` uses `pose.TransposeTransformDirection(worldNormal)` and `pose.TransformPoint(support)`.
- [ ] **Step 5: Migrate `HalfSpaceCollision2D`** — extract poses from the two `SpatialObject2D`s, pass to the shared helper. `ConstrainOutside` unchanged.
- [ ] **Step 6: Build + all tests green** (including the three new ones).
- [ ] **Step 7: Commit** — `feat: narrow phase consumes Similarity2D; non-uniform collidables throw`

---

### Task 11: Ray migration, docs, wrap-up

**Files:**
- Modify: `App2d\Engine\Collision\Queries\RayIntersection2D.cs`
- Modify: `README.md` (collision/halfspace sections)
- Test: `App2d.Tests\Collision\CollisionQueries2DTests.cs` (extend)

**Interfaces:**
- `RayIntersection2D.TryIntersect` keeps its public signature; internals swap `Matrix3x2.Invert` for `CollisionPose`. Ray *parameter* semantics are preserved: local origin/direction come from `InverseTransformPoint`/`InverseTransformDirection` (the ray parameter is affine-invariant), hit normals return via `pose.TransformDirection` + normalize.

- [ ] **Step 1: Add a failing-order test first** (rotated rectangle raycast):

```csharp
[Fact]
public void RaycastHitsARotatedRectangle()
{
    var box = new SpatialObject2D(Rectangle2D.FromSize(new Vector2(4f, 4f)));
    box.Transform.Position = new Vector2(10f, 0f);
    box.Transform.Rotation = MathF.PI / 4f;

    var found = new[] { box }.Raycast(
        new Ray2D(Vector2.Zero, Vector2.UnitX), 20f, out var hit);

    Assert.True(found);
    // The rotated box's near corner sits at x = 10 - 2√2.
    Assert.Equal(10f - 2f * MathF.Sqrt(2f), hit.Distance, 3);
    Assert.Equal(-1f, hit.Normal.X, 3);
}
```

(Should pass before AND after — it guards the migration.)
- [ ] **Step 2: Migrate `TryIntersect`.** Replace the `Matrix3x2.Invert` block with `var pose = worldObject.CollisionPose;`, `var localOrigin = pose.InverseTransformPoint(ray.Origin);`, `var localDirection = pose.InverseTransformDirection(ray.Direction);`; world normal becomes `pose.TransformDirection(localHit.Normal)` then the existing normalize-or-reject logic.
- [ ] **Step 3: Update `README.md`** — the halfspace section now describes the dispatch row (support mapping, boundary-projected contact point) and mention `Similarity2D`/`CollisionPose` and the `App2d.Tiles` project in the architecture notes. Read the README fully before editing.
- [ ] **Step 4: Full build + test green; run the game once** (`dotnet run --project App2d` briefly) to sanity-check the player still walks/collides.
- [ ] **Step 5: Commit** — `feat: ray queries consume Similarity2D; docs updated`

---

## Self-review notes

- Spec coverage: Phase 1 items 1-7 map to Tasks 2,3,4,5,6,7,8 (+Task 1 tests); Phase 2 maps to Tasks 9,10,11. ✓
- Type consistency: `TileCellRectangle2D`, `Similarity2D`, `PolygonGeometry2D`, `TryGetConvexHalfSpacePenetration` names match across tasks. ✓
- Ordering: ClosestPoint2D's file move (Task 5) lands under the Geometry glob whether Task 8 has run or not — both csproj states compile. ✓
