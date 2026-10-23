# Oriented Shapes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Rectangle2D`, `RoundedRectangle2D` and `Ellipse2D` carry a unit axis so they rotate in local space, JSON reads both spellings, and `ShapeTransforms2D.Posed` maps any built-in shape through a `Similarity2D`.

**Architecture:** One small frame primitive (`OrientedFrame2D`: center plus unit axis) does all projection. Each oriented shape stores center, extents and axis, and every query projects into the frame, calls the raw axis-aligned operation that already exists, and maps back. The per-shape switch tables gain explicit cases where `IRect2D` used to catch `Rectangle2D`. `AxisAlignedRectangle2D` keeps min/max and refuses rotating poses.

**Tech Stack:** .NET 10, C# records, `System.Numerics.Vector2`/`Matrix3x2`, System.Text.Json custom converter, xUnit, the APP2D001 shape-construction analyzer.

**Spec:** `docs/superpowers/specs/2026-10-22-oriented-shapes-design.md`

## Global Constraints

- Single-line method signatures and parameter lists, however long.
- Every file written with the Write tool or sed is LF; normalize to CRLF afterwards with PowerShell: `[IO.File]::WriteAllText($f, ([IO.File]::ReadAllText($f) -replace "`r?`n","`r`n"))`.
- No Bash heredocs containing code. Use Write/Edit.
- Before editing any file this plan touches, run `git -C C:/Users/eonei/source/App2d status --short -- <path>`; if it shows `M`, copy the main-checkout version into the worktree first, normalize CRLF, and build on it. Known overlaps: `App2d.Core/Geometry/Operations/Bounds/ShapeBounds2D.cs`, `App2d.Tests/Geometry/Rect2DTests.cs`, `App2d.Tests/Geometry/GeometryValidation2DTests.cs`, `App2d.Tests/Geometry/ShapeBoundsPrecisionTests.cs`.
- Builder aliases (`CreateFromSize`, `CreateFromMinAndSize`, `FromDimensions`, `FromMinAndSize`) are not touched in any task. Raymond is pruning them separately.
- Raw constructors take the axis as a required trailing parameter; no two-argument rectangle, three-argument rounded, or one/two-argument ellipse raw constructor survives. Object initializers (`new Ellipse2D { ... }`) remain legal.
- An unset axis reads as `Vector2.UnitX`.
- Axis validation tolerance: length within `0.001` of one. Pose axis-parallel tolerance in `Posed`: `0.001f * pose.Scale`.
- JSON member names: `center`, `size`, `radius`, `radii`, `angleRadians`; alternate rectangle spelling `min`, `max`. Written through the existing `W(...)` helper so the naming policy applies.
- Commits land on the worktree branch only. Never fast-forward `main`, never push; Raymond merges after testing in the app.
- Test commands: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~<ClassName>"` per task; full gate `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName!~GameUsesConfiguredWidthForTheActualCollidableTerrain"` plus `dotnet test App2d.Analyzers.Tests/App2d.Analyzers.Tests.csproj`. Five `tileset manifest not found` failures are pre-existing in a worktree unless the gitignored dark-cave tileset is copied from the main checkout.
- Commit trailer: `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

- A rectangle JSON object carrying both `min`/`max` and `center`/`size` must be rejected, not silently prefer one. Pinned in Task 3 (`MixedRectangleSpellingsAreRejected`).
- A `min`/`max` rectangle that also carries `angleRadians` must rotate about the box center. Pinned in Task 3 (`MinMaxSpellingAcceptsAnAngle`).
- A shape built with `with { Axis = new(2, 0) }` must fail `Validate`, since queries trust the axis. Pinned in Task 2 (`NonUnitAxisFailsValidation`).
- A rounded rectangle whose radius equals the smaller half size collapses to a capsule-like core; rotated, its core count, support and outline must stay finite. Pinned in Task 4 (`CollapsedRoundedRectangleRotatesLikeACapsule`).
- `Posed` with a zero-scale or non-finite pose must throw an argument exception rather than return a degenerate shape that only fails later. Pinned in Task 7 (`PosedRejectsDegeneratePoses`).

---

### Task 1: OrientedFrame2D primitive

**Files:**
- Create: `App2d.Core/Geometry/Primitives/OrientedFrame2D.cs`
- Create: `App2d.Tests/Geometry/VectorComparers.cs`
- Test: `App2d.Tests/Geometry/OrientedFrame2DTests.cs`

**Interfaces:**
- Produces, namespace `App2d.Core.Geometry.Primitives`, `public readonly record struct OrientedFrame2D(Vector2 Center, Vector2 Axis)`:
  - `Vector2 Perp => Axis.PerpCcw`
  - `bool IsAxisAligned => Axis == Vector2.UnitX`
  - `float AngleRadians => Polar2D.AngleOf(Axis)`
  - `Vector2 ToFrame(Vector2 point)`, `Vector2 FromFrame(Vector2 point)`
  - `Vector2 ToFrameDirection(Vector2 direction)`, `Vector2 FromFrameDirection(Vector2 direction)`
  - `void MapFromFrame(Span<Vector2> points)` (in place)
  - `Matrix3x2 Matrix` (frame to local, row-vector: `new(Axis.X, Axis.Y, Perp.X, Perp.Y, Center.X, Center.Y)`)
  - `Rect2D BoxBounds(Vector2 halfSize, float inflate = 0f)`: half extent x `= halfSize.X * |Axis.X| + halfSize.Y * |Perp.X| + inflate`, y likewise, around `Center`
  - `Rect2D EllipseBounds(Vector2 radii)`: half extent x `= sqrt((radii.X * Axis.X)^2 + (radii.Y * Perp.X)^2)`, y likewise
  - `static Vector2 AxisOrUnitX(Vector2 axis)` (`axis == default ? Vector2.UnitX : axis`)
  - `static Vector2 AxisFromAngle(float angleRadians)` (`Polar2D.Direction`)
  - `static void ValidateAxis(Vector2 axis, string name)`: finite and `|axis.Length() - 1| <= 0.001f`, else `ArgumentException` via `ArgGuard.ThrowIf`
- The constructor does not validate; callers pass a unit axis.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class OrientedFrame2DTests
{
    private static readonly OrientedFrame2D Tilted = new(new(2, 1), OrientedFrame2D.AxisFromAngle(MathF.PI / 6));

    [Fact] public void RoundTripsPointsAndDirections()
    {
        var p = new Vector2(-3, 4);
        Assert.Equal(p, Tilted.FromFrame(Tilted.ToFrame(p)), Near);
        Assert.Equal(p, Tilted.FromFrameDirection(Tilted.ToFrameDirection(p)), Near);
        Assert.Equal(Vector2.Transform(new(1, 0), Tilted.Matrix), Tilted.FromFrame(new(1, 0)), Near);
    }
    [Fact] public void AxisPointsAlongLocalX() => Assert.Equal(Tilted.Center + Tilted.Axis, Tilted.FromFrame(Vector2.UnitX), Near);
    [Fact] public void BoxBoundsMatchCorners()
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        VertexGenerator2D.WriteRectangle(corners, new(-3, -1), new(3, 1));
        Tilted.MapFromFrame(corners);
        var expected = Rect2D.FromPoints(corners);
        var actual = Tilted.BoxBounds(new(3, 1));
        Assert.Equal(expected.Min, actual.Min, Near); Assert.Equal(expected.Max, actual.Max, Near);
    }
    [Fact] public void NinetyDegreesSwapsBoxExtentsExactly()
    {
        var frame = new OrientedFrame2D(Vector2.Zero, Vector2.UnitY);
        Assert.Equal(new Rect2D(new(-1, -3), new(1, 3)), frame.BoxBounds(new(3, 1)));
    }
    [Fact] public void EllipseBoundsMatchSampledOutline()
    {
        var vertices = new Vector2[256];
        VertexGenerator2D.WriteEllipse(vertices, Vector2.Zero, new(3, 1));
        Tilted.MapFromFrame(vertices);
        var sampled = Rect2D.FromPoints(vertices);
        var analytic = Tilted.EllipseBounds(new(3, 1));
        Assert.Equal(sampled.Min, analytic.Min, Loose); Assert.Equal(sampled.Max, analytic.Max, Loose);
    }
    [Fact] public void UnsetAxisReadsAsUnitX() => Assert.Equal(Vector2.UnitX, OrientedFrame2D.AxisOrUnitX(default));
    [Fact] public void ValidateAxisRejectsNonUnit()
    {
        Assert.Throws<ArgumentException>(() => OrientedFrame2D.ValidateAxis(new(2, 0), "axis"));
        Assert.Throws<ArgumentException>(() => OrientedFrame2D.ValidateAxis(new(float.NaN, 0), "axis"));
        OrientedFrame2D.ValidateAxis(OrientedFrame2D.AxisFromAngle(1.3f), "axis");
    }
}
```

`Near` and `Loose` are `IEqualityComparer<Vector2>` instances with absolute per-component tolerances `1e-5f` and `1e-3f`. The test project has none, so create `App2d.Tests/Geometry/VectorComparers.cs` holding `internal static class VectorComparers` with both as `public static readonly` fields, and give every test class in this plan `using static App2d.Tests.Geometry.VectorComparers;`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~OrientedFrame2DTests"`
Expected: build error, `OrientedFrame2D` does not exist.

- [ ] **Step 3: Implement `OrientedFrame2D` with the members listed in Interfaces**

Follow `Rect2D.cs` for file layout and doc-comment style. `ToFrame(p)` is `(dot(Axis, p - Center), dot(Perp, p - Center))`; `FromFrame(p)` is `Center + Axis * p.X + Perp * p.Y`; the direction forms drop `Center`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: the Step 2 command. Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add App2d.Core/Geometry/Primitives/OrientedFrame2D.cs App2d.Tests/Geometry/OrientedFrame2DTests.cs
git commit -m "Oriented shapes: OrientedFrame2D primitive"
```

---

### Task 2: Rectangle2D becomes oriented

**Files:**
- Modify: `App2d.Core/Geometry/Shapes/Rectangle2D.cs` (whole type)
- Modify: `App2d.Core/Geometry/Builders/Rectangle2D.Builder.cs`
- Modify: `App2d.Core/Geometry/Shapes/AxisAlignedRectangle2D.cs`, `App2d.Core/Geometry/Builders/AxisAlignedRectangle2D.Builder.cs`
- Modify: `App2d.Core/Geometry/Operations/Bounds/ShapeBounds2D.cs:33` (`Calculate`) and the `Rectangle2D` case in `CalculateBoundingCircle`
- Modify: `App2d.Core/Geometry/Operations/Distance/ShapeDistance2D.cs:37`
- Modify: `App2d.Core/Geometry/Operations/Vertices/ShapeVertices2D.cs:47,101,130,168`
- Modify: `App2d.Core/Geometry/Operations/MassProperties/ShapeMassProperties2D.cs:24`
- Modify: `App2d.Core/Geometry/Operations/Transforms/ShapeTransforms2D.cs:105`
- Modify: `App2d.Core/Geometry/Serialization/GeometryJsonConverterFactory.cs` rectangle read and write (minimal, Task 3 tests it)
- Modify tests that no longer compile: `App2d.Tests/Geometry/Rect2DTests.cs:19-20,74-80`, `App2d.Tests/Geometry/DistanceSquaredTests.cs:36`, `App2d.Tests/Authored/NativeRigTests.cs:43`, `App2d.Tests/Geometry/GeometryValidation2DTests.cs:20`, `App2d.Analyzers.Tests/ShapeConstructionTests.cs:28`
- Test: `App2d.Tests/Geometry/Rectangle2DTests.cs` (new)

**Interfaces:**
- Consumes: `OrientedFrame2D` from Task 1.
- Produces `Rectangle2D : IConvexShape2D` (no longer `IRect2D`):
  - raw `Rectangle2D(Vector2 center, Vector2 halfSize, Vector2 axis)`
  - `Vector2 Center { get; init; }`, `Vector2 HalfSize { get; init; }`, `Vector2 Axis { get => OrientedFrame2D.AxisOrUnitX(_axis); init => _axis = value; }`
  - `Vector2 Size`, `float Width`, `float Height`, `float AngleRadians`, `bool IsAxisAligned`, `OrientedFrame2D Frame => new(Center, Axis)`
  - `float Area => Area2D.Rectangle(-HalfSize, HalfSize)`, `float Diameter => Diameter2D.Rectangle(-HalfSize, HalfSize)`
  - `bool ContainsPoint(Vector2 localPoint)`, `Vector2 GetSupportPoint(Vector2 localDirection)`, `void WriteCorners(Span<Vector2> corners)` (counter-clockwise from the `(-,-)` frame corner, mapped)
  - `void Validate()`: `Center` finite, `HalfSize` finite and both components `> 0`, `OrientedFrame2D.ValidateAxis(Axis, "axis")`
- Produces `Rectangle2D.Builder`: `Create(Vector2 min, Vector2 max)` (ordered corners, +X), `Create(Vector2 center, Vector2 halfSize, Vector2 axis)` (axis normalized through `new Direction2D(axis).Vector`), `FromSize(Vector2 size, Vector2 center = default, float angleRadians = 0f)`, `FromSize(float width, float height, Vector2 center = default, float angleRadians = 0f)`, `Create(float width, float height, Vector2 center = default, float angleRadians = 0f)`; existing aliases unchanged and still forwarding.
- Produces `AxisAlignedRectangle2D` unchanged in surface; it now calls `Area2D.Rectangle`, `Diameter2D.Rectangle`, `Containment2D.Rectangle`, `SupportPoint2D.Rectangle` with `Min`/`Max` directly, and its `Validate` is `ArgGuard.LessThan(Min, Max)` (the former `Rectangle2D.ValidateBounds`).

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class Rectangle2DTests
{
    // 6 by 2 box at (2, 1), rotated 30 degrees.
    private static readonly Rectangle2D Tilted = Rectangle2D.Builder.FromSize(6, 2, new(2, 1), MathF.PI / 6);

    [Fact] public void BuildersWithoutAnAngleStayAxisAligned()
    {
        var box = Rectangle2D.Builder.Create(new(-1, -2), new(3, 1));
        Assert.Equal(new Vector2(1, -.5f), box.Center); Assert.Equal(new Vector2(4, 3), box.Size); Assert.Equal(Vector2.UnitX, box.Axis); Assert.True(box.IsAxisAligned);
        Assert.Equal(box, Rectangle2D.Builder.FromSize(4, 3, new(1, -.5f)));
        Assert.Equal(Vector2.UnitX, default(Rectangle2D).Axis);
    }
    [Fact] public void ContainsOnlyWhenRotated()
    {
        // (2 + 2.6, 1 + 1.5) lies on the rotated long axis, 3 units out; the unrotated box ends at x = 5.
        var onAxis = new Vector2(2 + 3 * MathF.Cos(MathF.PI / 6) * .99f, 1 + 3 * MathF.Sin(MathF.PI / 6) * .99f);
        Assert.True(Tilted.ContainsPoint(onAxis));
        Assert.False(Rectangle2D.Builder.FromSize(6, 2, new(2, 1)).ContainsPoint(onAxis));
        Assert.False(Tilted.ContainsPoint(new(5, 1)));
    }
    [Fact] public void CornersSupportAndBoundsAgree()
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        Tilted.WriteCorners(corners);
        var bounds = ShapeBounds2D.Calculate(Tilted);
        Assert.Equal(Rect2D.FromPoints(corners).Min, bounds.Min, Near); Assert.Equal(Rect2D.FromPoints(corners).Max, bounds.Max, Near);
        Assert.Equal(corners[2], Tilted.GetSupportPoint(Tilted.Axis + Tilted.Frame.Perp), Near);
        Assert.Equal(12f, Tilted.Area, 5); Assert.Equal(MathF.Sqrt(40), Tilted.Diameter, 5); Assert.Equal(MathF.PI / 6, Tilted.AngleRadians, 5);
    }
    [Fact] public void NinetyDegreesMatchesTheSwappedBox()
    {
        var turned = Rectangle2D.Builder.FromSize(6, 2, Vector2.Zero, MathF.PI / 2);
        var bounds = ShapeBounds2D.Calculate(turned);
        Assert.Equal(new Vector2(-1, -3), bounds.Min, Near); Assert.Equal(new Vector2(1, 3), bounds.Max, Near);
    }
    [Fact] public void SignedDistanceAndMassIgnoreRotation()
    {
        var flat = Rectangle2D.Builder.FromSize(6, 2, new(2, 1));
        Assert.Equal(ShapeDistance2D.SignedDistance(new Vector2(2, 1), flat), ShapeDistance2D.SignedDistance(new Vector2(2, 1), Tilted), 5);
        var a = ShapeMassProperties2D.Calculate(flat); var b = ShapeMassProperties2D.Calculate(Tilted);
        Assert.Equal(a.Mass, b.Mass, 5); Assert.Equal(a.MomentOfInertia, b.MomentOfInertia, 5); Assert.Equal(a.CenterOfMass.X, b.CenterOfMass.X, 5);
    }
    [Fact] public void ScaledKeepsTheAxis()
    {
        var scaled = Assert.IsType<Rectangle2D>(ShapeTransforms2D.Scaled(Tilted, 2));
        Assert.Equal(Tilted.Axis, scaled.Axis); Assert.Equal(Tilted.HalfSize * 2, scaled.HalfSize); Assert.Equal(Tilted.Center * 2, scaled.Center);
    }
    [Fact] public void NonUnitAxisFailsValidation()
    {
        Assert.ThrowsAny<ArgumentException>(() => (Tilted with { Axis = new(2, 0) }).Validate());
        Assert.ThrowsAny<ArgumentException>(() => Rectangle2D.Builder.FromSize(0, 2));
        Assert.Equal(Vector2.UnitY, Rectangle2D.Builder.Create(Vector2.Zero, Vector2.One, new(0, 5)).Axis);
    }
    [Fact] public void RotatedBoxCollidesLikeTheSamePolygon()
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        Tilted.WriteCorners(corners);
        var polygon = ConvexPolygon2D.Builder.Create(corners.ToArray());
        var probe = Circle2D.Builder.Create(.5f, new(5.2f, 2.7f));
        var hitBox = ShapeCollision2D.TryGetContact(new SpatialObject2D(Tilted), new SpatialObject2D(probe), out var boxContact);
        var hitPolygon = ShapeCollision2D.TryGetContact(new SpatialObject2D(polygon), new SpatialObject2D(probe), out var polygonContact);
        Assert.True(hitPolygon); Assert.Equal(hitPolygon, hitBox);
        Assert.Equal(polygonContact.PenetrationDepth, boxContact.PenetrationDepth, 4);
    }
}
```

The probe circle sits just outside the long edge of the tilted box; if it reads as no contact, move it 0.2 closer to the center along the box's `Frame.Perp` so both paths report a shallow contact.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~Rectangle2DTests"`
Expected: build errors (`FromSize` has no angle parameter, `Axis` missing).

- [ ] **Step 3: Rewrite `Rectangle2D` and its builder per Interfaces**

`ContainsPoint` is `Containment2D.Rectangle(Frame.ToFrame(p), -HalfSize, HalfSize)`. `GetSupportPoint` is `Frame.FromFrame(SupportPoint2D.Rectangle(Frame.ToFrameDirection(d), -HalfSize, HalfSize))`. `WriteCorners` is `VertexGenerator2D.WriteRectangle(corners, -HalfSize, HalfSize)` then `Frame.MapFromFrame(corners[..4])`.

- [ ] **Step 4: Decouple `AxisAlignedRectangle2D`**

Remove the private `Rectangle2D` property and the `APP2D001` pragma; call the raw operations listed in Interfaces. Move `ValidateBounds` into `AxisAlignedRectangle2D.Validate` as `ArgGuard.LessThan(Min, Max)`.

- [ ] **Step 5: Update the switch tables**

- `ShapeBounds2D.Calculate`: add `Rectangle2D rectangle => rectangle.Frame.BoxBounds(rectangle.HalfSize),` above the `IRect2D` line. `CalculateBoundingCircle`: the `Rectangle2D` case becomes `(rectangle.Center, rectangle.HalfSize.Length())`.
- `ShapeDistance2D`: add `Rectangle2D rectangle => Distance2D.SignedDistanceToRectangle(rectangle.Frame.ToFrame(point), -rectangle.HalfSize, rectangle.HalfSize),` above the `IRect2D` line.
- `ShapeVertices2D`: add `Rectangle2D => 4` above each `IRect2D => 4`, and `case Rectangle2D rectangle: rectangle.WriteCorners(vertices); break;` above each `case IRect2D` in `GetVerts` and `GetOutlineVerts`.
- `ShapeMassProperties2D`: `Rectangle2D rectangle => Rectangle(rectangle.Center - rectangle.HalfSize, rectangle.Center + rectangle.HalfSize, density),` (the helper's center and scalar inertia are rotation-invariant).
- `ShapeTransforms2D.Scaled`: `Rectangle2D rectangle => Rectangle2D.Builder.Create(rectangle.Center * scale, rectangle.HalfSize * scale, rectangle.Axis),`.
- `GeometryJsonConverterFactory`: read `ShapeKinds2D.Rectangle` as `new Rectangle2D(R<Vector2>("Center"), R<Vector2>("Size") / 2, OrientedFrame2D.AxisFromAngle(Optional("AngleRadians", 0f)))`; write `Center`, `Size`, `AngleRadians`. (Task 3 adds the `min`/`max` spelling and tests.)

- [ ] **Step 6: Fix the tests that stopped compiling**

- `Rect2DTests` 19 to 20: replace `.ToRect()` with `ShapeBounds2D.Calculate(...)`. Lines 74 to 80: keep `shape.Height`; replace `bounds.Intersects(shape)` and `shape.Contains(bounds)` with the `axisAligned` instance.
- `DistanceSquaredTests:36`: if `rectangle` is a `Rectangle2D`, read `rectangle.Center - rectangle.HalfSize` and `+ HalfSize` (it is axis-aligned there); otherwise leave it.
- `NativeRigTests:43`: `Rectangle2D.Builder.Create(new(-.5f, -.5f), new(.5f, .5f))`.
- `GeometryValidation2DTests:20`: the invalid sample becomes `new Rectangle2D(Vector2.Zero, new(-1, 1), Vector2.UnitX)` (non-positive half size).
- `ShapeConstructionTests:28`: the source string becomes `"new Rectangle2D(Vector2.Zero, Vector2.One, Vector2.UnitX)"`.
- `ShapeSerialization2DTests:35,42,97,144` compile as they are (builders); the line 144 "backwards" expectation is handled in Task 3.

- [ ] **Step 7: Run the task tests, then the full gate**

Run: the Step 2 command, then the full gate and the analyzer tests from Global Constraints.
Expected: all pass except the pre-existing tileset failures when the tileset is absent.

- [ ] **Step 8: Commit**

```bash
git add -A App2d.Core App2d.Tests App2d.Analyzers.Tests
git commit -m "Oriented shapes: Rectangle2D stores center, half size and axis"
```

---

### Task 3: Rectangle JSON, both spellings

**Files:**
- Modify: `App2d.Core/Geometry/Serialization/GeometryJsonConverterFactory.cs` (rectangle read)
- Modify: `App2d.Tests/Geometry/ShapeSerialization2DTests.cs:144` expectation if it changes
- Test: `App2d.Tests/Geometry/RectangleJsonTests.cs` (new)

**Interfaces:**
- Consumes `GeometryJson2D.ShapeFromJson(string)` and `IGeometryValue2D.ToJson()`.
- Produces: a private static helper in the converter, `static (Vector2 Center, Vector2 HalfSize) ReadRectangleExtents(Func<string, bool> has, Func<string, Vector2> read)`, used by the rectangle and (Task 4) rounded-rectangle cases: if `Min` or `Max` is present, require both and forbid `Center`/`Size`; else require `Center` and `Size`. A mix throws `JsonException("A rectangle is spelled with min and max or with center and size, not both.")`.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class RectangleJsonTests
{
    [Fact] public void WritesCenterSizeAndAngle()
    {
        var json = Rectangle2D.Builder.FromSize(4, 2, new(1, 1), .5f).ToJson();
        Assert.Contains("\"center\"", json); Assert.Contains("\"size\"", json); Assert.Contains("\"angleRadians\":0.5", json); Assert.DoesNotContain("\"min\"", json);
        var back = Assert.IsType<Rectangle2D>(GeometryJson2D.ShapeFromJson(json).Validated());
        Assert.Equal(.5f, back.AngleRadians, 5); Assert.Equal(new Vector2(4, 2), back.Size, Near);
    }
    [Fact] public void MinMaxSpellingReadsAxisAligned()
    {
        var box = Assert.IsType<Rectangle2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":-1,\"y\":-2},\"max\":{\"x\":3,\"y\":1}}").Validated());
        Assert.Equal(new Vector2(1, -.5f), box.Center); Assert.Equal(new Vector2(2, 1.5f), box.HalfSize); Assert.True(box.IsAxisAligned);
    }
    [Fact] public void MinMaxSpellingAcceptsAnAngle()
    {
        var box = Assert.IsType<Rectangle2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":-1,\"y\":-2},\"max\":{\"x\":3,\"y\":1},\"angleRadians\":1.5707963}").Validated());
        Assert.Equal(new Vector2(1, -.5f), box.Center); Assert.Equal(Vector2.UnitY, box.Axis, Near);
    }
    [Fact] public void MixedRectangleSpellingsAreRejected() =>
        Assert.Throws<JsonException>(() => GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":0,\"y\":0},\"max\":{\"x\":1,\"y\":1},\"center\":{\"x\":0,\"y\":0},\"size\":{\"x\":1,\"y\":1}}"));
    [Fact] public void BackwardsMinMaxStillFailsValidation() =>
        Assert.ThrowsAny<ArgumentException>(() => GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":1,\"y\":1},\"max\":{\"x\":0,\"y\":0}}").Validated());
    [Fact] public void AxisAlignedKindStillReadsMinMax() =>
        Assert.IsType<AxisAlignedRectangle2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"axis-aligned-rectangle\",\"min\":{\"x\":0,\"y\":0},\"max\":{\"x\":1,\"y\":1}}").Validated());
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~RectangleJsonTests"`
Expected: `MinMaxSpellingReadsAxisAligned` and the two `min`/`max` tests fail with `Missing geometry member 'center'`.

- [ ] **Step 3: Implement `ReadRectangleExtents` and use it in the rectangle case**

Both spellings mark their members read through the existing `R`/`Optional` helpers so `Disallow` still works. Check the existing line 144 test in `ShapeSerialization2DTests` still expresses "backwards min/max fails on validate"; align its exception type with `BackwardsMinMaxStillFailsValidation` if needed.

- [ ] **Step 4: Run the tests to verify they pass**

Run: the Step 2 command and `--filter "FullyQualifiedName~ShapeSerialization2DTests"`. Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add App2d.Core/Geometry/Serialization/GeometryJsonConverterFactory.cs App2d.Tests/Geometry/RectangleJsonTests.cs App2d.Tests/Geometry/ShapeSerialization2DTests.cs
git commit -m "Oriented shapes: rectangle JSON reads min/max or center/size, writes center/size/angle"
```

---

### Task 4: RoundedRectangle2D becomes oriented

**Files:**
- Modify: `App2d.Core/Geometry/Shapes/RoundedRectangle2D.cs`, `App2d.Core/Geometry/Builders/RoundedRectangle2D.Builder.cs`
- Modify: `App2d.Core/Geometry/Operations/Bounds/ShapeBounds2D.cs` (`Calculate` line 32, `CalculateBoundingCircle` rounded case)
- Modify: `App2d.Core/Geometry/Operations/Distance/ShapeDistance2D.cs:36`
- Modify: `App2d.Core/Geometry/Operations/Vertices/ShapeVertices2D.cs:44-46,84-99,165-167`
- Modify: `App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs:62-66`
- Modify: `App2d.Core/Geometry/Operations/MassProperties/ShapeMassProperties2D.cs:61-67`
- Modify: `App2d.Core/Geometry/Operations/Transforms/ShapeTransforms2D.cs:103`
- Modify: `App2d.Core/Geometry/Serialization/GeometryJsonConverterFactory.cs` rounded read and write
- Modify: `App2d.Noodle/Rigging/RigAuthoredBridge2D.cs:70-74`
- Modify tests: `App2d.Tests/Geometry/GeometryValidation2DTests.cs:22`, `App2d.Analyzers.Tests/ShapeConstructionTests.cs:30`
- Test: `App2d.Tests/Geometry/RoundedRectangle2DTests.cs` (extend)

**Interfaces:**
- Consumes `OrientedFrame2D`, `ReadRectangleExtents` (Task 3).
- Produces `RoundedRectangle2D : IConvexShape2D`:
  - raw `RoundedRectangle2D(Vector2 center, Vector2 halfSize, float radius, Vector2 axis)`
  - `Center`, `HalfSize`, `Radius`, `Axis` (same fallback getter), `Vector2 InnerHalfSize => HalfSize - new Vector2(Radius)`, `Size`, `AngleRadians`, `IsAxisAligned`, `Frame`
  - `Area => Area2D.RoundedRectangle(-HalfSize, HalfSize, Radius)`, `Diameter => Diameter2D.RoundedRectangle(-InnerHalfSize, InnerHalfSize, Radius)`
  - `ContainsPoint`, `GetSupportPoint`, `int WriteInnerCorners(Span<Vector2> corners)` (writes 1, 2 or 4 frame-mapped inner points by the existing collapse rules and returns the count)
  - `RoundedRectangle2D WithSize(Vector2 size)` keeps `Center` and `Axis`, clamps `Radius` as today
  - `Validate()`: center finite, half size positive, `Radius` in `[0, min(HalfSize)]` via `ArgGuard.InRange`, axis valid
  - `OuterRect` and `InnerRect` are removed.
- Produces builder: `Create(Vector2 min, Vector2 max, float radius)`, `Create(Vector2 center, Vector2 halfSize, float radius, Vector2 axis)`, `FromSize(Vector2 size, float radius, Vector2 center = default, float angleRadians = 0f)`, `FromSize(float width, float height, float radius, Vector2 center = default, float angleRadians = 0f)`, `Create(float width, float height, float radius, Vector2 center = default, float angleRadians = 0f)`; aliases untouched.
- JSON `rounded-rectangle`: writes `Center`, `Size`, `Radius`, `AngleRadians`; reads either rectangle spelling plus `Radius`.

- [ ] **Step 1: Add the failing tests to `RoundedRectangle2DTests`**

```csharp
private static readonly RoundedRectangle2D TiltedRounded = RoundedRectangle2D.Builder.FromSize(4, 2, .5f, new(1, 1), MathF.PI / 6);

[Fact] public void RotatedRoundedRectangleMatchesItsOutline()
{
    var count = TiltedRounded.GetOutlineVertCount(cornerSegments: 64);
    var outline = new Vector2[count];
    TiltedRounded.GetOutlineVerts(outline, cornerSegments: 64);
    var bounds = ShapeBounds2D.Calculate(TiltedRounded);
    Assert.Equal(Rect2D.FromPoints(outline).Min, bounds.Min, Loose); Assert.Equal(Rect2D.FromPoints(outline).Max, bounds.Max, Loose);
    foreach (var v in outline) Assert.InRange(ShapeDistance2D.SignedDistance(v, TiltedRounded), -1e-3f, 1e-3f);
    Assert.Equal(RoundedRectangle2D.Builder.FromSize(4, 2, .5f).Area, TiltedRounded.Area, 5);
}
[Fact] public void CoreVerticesAreTheMappedInnerCorners()
{
    Span<Vector2> core = stackalloc Vector2[4];
    var verts = TiltedRounded.GetVerts(core, out var radius);
    Assert.Equal(4, verts.Length); Assert.Equal(.5f, radius);
    Assert.Equal(TiltedRounded.Frame.FromFrame(-TiltedRounded.InnerHalfSize), verts[0], Near);
}
[Fact] public void CollapsedRoundedRectangleRotatesLikeACapsule()
{
    var pill = RoundedRectangle2D.Builder.FromSize(4, 2, 1f, Vector2.Zero, MathF.PI / 4);
    Span<Vector2> core = stackalloc Vector2[4];
    Assert.Equal(2, pill.GetVerts(core, out _).Length);
    Assert.True(pill.GetSupportPoint(Vector2.UnitX).IsFinite());
    Assert.Equal(MathF.Sqrt(2) / 2 + 1f, ShapeBounds2D.Calculate(pill).Max.X, 4);
}
[Fact] public void WithSizeKeepsCenterAndAxis()
{
    var resized = TiltedRounded.WithSize(new(8, 1));
    Assert.Equal(TiltedRounded.Center, resized.Center); Assert.Equal(TiltedRounded.Axis, resized.Axis); Assert.Equal(.5f, resized.Radius);
}
[Fact] public void RoundedJsonRoundTripsAndReadsMinMax()
{
    var back = Assert.IsType<RoundedRectangle2D>(GeometryJson2D.ShapeFromJson(TiltedRounded.ToJson()).Validated());
    Assert.Equal(TiltedRounded.AngleRadians, back.AngleRadians, 5); Assert.Equal(.5f, back.Radius);
    var legacy = Assert.IsType<RoundedRectangle2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"rounded-rectangle\",\"min\":{\"x\":-2,\"y\":-1},\"max\":{\"x\":2,\"y\":1},\"radius\":0.4}").Validated());
    Assert.Equal(Vector2.Zero, legacy.Center); Assert.Equal(new Vector2(2, 1), legacy.HalfSize);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~RoundedRectangle2DTests"`
Expected: build errors.

- [ ] **Step 3: Rewrite `RoundedRectangle2D` and its builder per Interfaces**

- [ ] **Step 4: Update the consumers**

- `ShapeBounds2D.Calculate`: `RoundedRectangle2D rounded => rounded.Frame.BoxBounds(rounded.InnerHalfSize, rounded.Radius),`. `CalculateBoundingCircle`: `(rounded.Center, rounded.InnerHalfSize.Length() + rounded.Radius)`.
- `ShapeDistance2D`: `Distance2D.SignedDistanceToRectangle(rounded.Frame.ToFrame(point), -rounded.InnerHalfSize, rounded.InnerHalfSize) - rounded.Radius`.
- `ShapeVertices2D`: the three `GetVertCount` rounded lines test `InnerHalfSize` components against zero; `GetVerts` calls `rounded.WriteInnerCorners(vertices)`; `GetOutlineVerts` writes `WriteRoundedRectangle(vertices, -HalfSize, HalfSize, Radius, cornerSegments)` then `rounded.Frame.MapFromFrame(vertices[..count])`.
- `ShapeArea2D:62-66`: same write-then-map.
- `ShapeMassProperties2D.RoundedRectangle`: `RoundedBox(inner.X, inner.Y, rounded.Radius, new Vector2d(rounded.Center), density)`.
- `ShapeTransforms2D.Scaled`: `RoundedRectangle2D.Builder.Create(rounded.Center * scale, rounded.HalfSize * scale, rounded.Radius * scale, rounded.Axis)`.
- JSON converter: rounded case via `ReadRectangleExtents` plus `R<float>("Radius")` and `Optional("AngleRadians", 0f)`; writer emits `Center`, `Size`, `Radius`, `AngleRadians`.
- `RigAuthoredBridge2D:70-74`: `part.ScaleX = rounded.Size.X; part.ScaleY = rounded.Size.Y;`.
- `GeometryValidation2DTests:22`: `new RoundedRectangle2D(Vector2.Zero, new(.5f, .5f), 1, Vector2.UnitX)` (radius too large). `ShapeConstructionTests:30`: `"new RoundedRectangle2D(Vector2.Zero, new Vector2(1), 0.5f, Vector2.UnitX)"`.

- [ ] **Step 5: Run the task tests, then the full gate**

Run: the Step 2 command, then the full gate and the analyzer tests. Expected: pass.

- [ ] **Step 6: Commit**

```bash
git add -A App2d.Core App2d.Noodle App2d.Tests App2d.Analyzers.Tests
git commit -m "Oriented shapes: RoundedRectangle2D stores center, half size, radius and axis"
```

---

### Task 5: Ellipse2D becomes oriented

**Files:**
- Modify: `App2d.Core/Geometry/Shapes/Ellipse2D.cs`, `App2d.Core/Geometry/Builders/Ellipse2D.Builder.cs`
- Modify: `App2d.Core/Geometry/Operations/Bounds/ShapeBounds2D.cs` (`Calculate` ellipse line, `TryCalculateTransformed` ellipse case)
- Modify: `App2d.Core/Geometry/Operations/Distance/ShapeDistance2D.cs:34`
- Modify: `App2d.Core/Geometry/Operations/Intersection/ShapeRaycast2D.cs:27-28`
- Modify: `App2d.Core/Geometry/Operations/Convex/ShapeConvexQuery2D.cs:73-87`
- Modify: `App2d.Core/Geometry/Operations/Vertices/ShapeVertices2D.cs` ellipse outline case
- Modify: `App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs:46-50`
- Modify: `App2d.Core/Geometry/Operations/Diameter/ShapeDiameter2D.cs:51-60,95-106`
- Modify: `App2d.Core/Geometry/Operations/Transforms/ShapeTransforms2D.cs` ellipse `Scaled` line
- Modify: `App2d.Core/Geometry/Serialization/GeometryJsonConverterFactory.cs` ellipse read and write
- Modify tests: `App2d.Tests/Geometry/GeometryValidation2DTests.cs:18`, `App2d.Analyzers.Tests/ShapeConstructionTests.cs:26`
- Test: `App2d.Tests/Geometry/Ellipse2DTests.cs` (create or extend if one exists)

**Interfaces:**
- Produces `Ellipse2D`: raw `Ellipse2D(Vector2 radii, Vector2 center, Vector2 axis)`; `Radii`, `Center`, `Axis` (fallback getter), `AngleRadians`, `IsAxisAligned`, `Frame`; `Area`, `Diameter` unchanged; `ContainsPoint` and `GetSupportPoint` through the frame; `WriteVertices` writes about the frame origin then maps.
- Builder: `Create(Vector2 radii, Vector2 center = default, float angleRadians = 0f)`, `Create(Vector2 radii, Vector2 center, Vector2 axis)`.
- `ShapeDiameter2D.Between`: exact `BetweenEllipses` only when both ellipses `IsAxisAligned`; otherwise `FarthestDistance2D.BetweenConvex(first, second)`. `FromEllipse`'s per-vertex loop becomes exact for any axis by projecting each point: `BetweenEllipses(Vector2.Zero, ellipse.Radii, ellipse.Frame.ToFrame(point), Vector2.Zero)`.
- `ShapeArea2D.Union`: the `Ellipse2D` analytic part only when `IsAxisAligned`; a rotated ellipse falls through to the sampled-outline branch.
- JSON `ellipse`: writes `Center`, `Radii`, `AngleRadians`; reads `Optional("AngleRadians", 0f)`.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class Ellipse2DTests
{
    private static readonly Ellipse2D Tilted = Ellipse2D.Builder.Create(new(3, 1), new(2, 1), MathF.PI / 6);

    [Fact] public void ContainsSupportDistanceAndBoundsAgreeWithTheOutline()
    {
        var outline = new Vector2[512];
        Tilted.WriteVertices(outline);
        var bounds = ShapeBounds2D.Calculate(Tilted);
        Assert.Equal(Rect2D.FromPoints(outline).Min, bounds.Min, Loose); Assert.Equal(Rect2D.FromPoints(outline).Max, bounds.Max, Loose);
        foreach (var v in outline) Assert.InRange(ShapeDistance2D.SignedDistance(v, Tilted), -1e-3f, 1e-3f);
        var tip = Tilted.Frame.FromFrame(new(3, 0));
        Assert.Equal(tip, Tilted.GetSupportPoint(Tilted.Axis), Near);
        Assert.True(Tilted.ContainsPoint(Tilted.Frame.FromFrame(new(2.9f, 0)))); Assert.False(Tilted.ContainsPoint(new(4.9f, 1)));
    }
    [Fact] public void TransformedBoundsComposeTheFrame()
    {
        var turnBack = Matrix3x2.CreateRotation(-MathF.PI / 6, new Vector2(2, 1));
        var bounds = ShapeBounds2D.CalculateTransformed(Tilted, turnBack);
        Assert.Equal(new Vector2(-1, 0), bounds.Min, Loose); Assert.Equal(new Vector2(5, 2), bounds.Max, Loose);
    }
    [Fact] public void RaycastHitsTheRotatedPerimeter()
    {
        var origin = Tilted.Frame.FromFrame(new(10, 0));
        Assert.True(ShapeRaycast2D.TryIntersect(origin, -Tilted.Axis, Tilted, 100, out var hit));
        Assert.Equal(Tilted.Frame.FromFrame(new(3, 0)), hit.Point, Loose); Assert.Equal(Tilted.Axis, hit.Normal, Loose); Assert.Equal(7f, hit.Distance, 3);
    }
    [Fact] public void CircleQueryAgainstARotatedEllipseMatchesTheUnrotatedOne()
    {
        var flat = Ellipse2D.Builder.Create(new(3, 1), new(2, 1));
        var pose = Similarity2D.FromAxis(Vector2.Zero, OrientedFrame2D.AxisFromAngle(MathF.PI / 6));
        var probe = Circle2D.Builder.Create(.5f, new(4.5f, 1.2f));
        var viaFlatPosed = ShapeConvexQuery2D.Query(probe, Similarity2D.Identity, flat, pose);
        var viaTilted = ShapeConvexQuery2D.Query(probe, Similarity2D.Identity, Ellipse2D.Builder.Create(new(3, 1), pose.TransformPoint(new(2, 1)), MathF.PI / 6), Similarity2D.Identity);
        Assert.Equal(viaFlatPosed.SignedDistance, viaTilted.SignedDistance, 4);
    }
    [Fact] public void PairSolversFallBackForRotatedEllipsesAndStayExactForAligned()
    {
        var a = Ellipse2D.Builder.Create(new(2, 1), new(-1, 0)); var b = Ellipse2D.Builder.Create(new(2, 1), new(2, 0));
        var composite = CompositeShape2D.Builder.Create([a, b], includeOverlap: false, areaOutlineSegments: 256);
        var turned = Similarity2D.FromAxis(Vector2.Zero, OrientedFrame2D.AxisFromAngle(.7f));
        var ar = Ellipse2D.Builder.Create(new(2, 1), turned.TransformPoint(new(-1, 0)), .7f); var br = Ellipse2D.Builder.Create(new(2, 1), turned.TransformPoint(new(2, 0)), .7f);
        var rotated = CompositeShape2D.Builder.Create([ar, br], includeOverlap: false, areaOutlineSegments: 256);
        Assert.Equal(composite.Area, rotated.Area, 2);
        Assert.Equal(composite.Diameter, rotated.Diameter, 2);
    }
    [Fact] public void EllipseJsonRoundTripsAndReadsWithoutAnAngle()
    {
        Assert.Equal(MathF.PI / 6, Assert.IsType<Ellipse2D>(GeometryJson2D.ShapeFromJson(Tilted.ToJson()).Validated()).AngleRadians, 5);
        Assert.True(Assert.IsType<Ellipse2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"ellipse\",\"center\":{\"x\":0,\"y\":0},\"radii\":{\"x\":2,\"y\":1}}").Validated()).IsAxisAligned);
    }
}
```

`ShapeRaycast2D.TryIntersect(Vector2 origin, Vector2 direction, IShape2D shape, float maxDistance, out RayHit2D hit)` and `ShapeConvexQuery2D.Query(IConvexShape2D first, Similarity2D firstPose, IConvexShape2D second, Similarity2D secondPose)` returning `ConvexQueryResult2D(float SignedDistance, Vector2 Normal, Vector2 PointOnFirst, Vector2 PointOnSecond, float ErrorBound)` are the real signatures.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~Ellipse2DTests"`
Expected: build errors.

- [ ] **Step 3: Rewrite `Ellipse2D` and its builder per Interfaces**

- [ ] **Step 4: Update the consumers**

- `ShapeBounds2D.Calculate`: `Ellipse2D ellipse => ellipse.Frame.EllipseBounds(ellipse.Radii),`. `TryCalculateTransformed`: `TransformedEllipse(Vector2.Zero, ellipse.Radii, ellipse.Frame.Matrix * transform)`.
- `ShapeDistance2D`: `Distance2D.SignedDistanceToEllipse(ellipse.Frame.ToFrame(point), Vector2.Zero, ellipse.Radii)`.
- `ShapeRaycast2D`: project `origin` and `direction` into the frame, call `Raycast2D.TryEllipse(..., Vector2.Zero, ellipse.Radii, maxDistance, out hit)`, then map `hit.Point` with `FromFrame` and `hit.Normal` with `FromFrameDirection`; `hit.Distance` is unchanged (the frame is rigid).
- `ShapeConvexQuery2D.CircleEllipse`: build `Similarity2D.TryFromMatrix(ellipse.Frame.Matrix * ellipsePose.ToMatrix(), out var pose)` and use `pose`, `Vector2.Zero` and `ellipse.Radii` where the method used `ellipsePose`, `ellipse.Center` and `ellipse.Radii`; the `ContainsPoint` call becomes `Containment2D.Ellipse(center, Vector2.Zero, ellipse.Radii)`.
- `ShapeVertices2D.GetOutlineVerts` ellipse case: `WriteEllipse(vertices[..count], Vector2.Zero, ellipse.Radii)` then `ellipse.Frame.MapFromFrame(vertices[..count])`.
- `ShapeArea2D`, `ShapeDiameter2D`, `ShapeTransforms2D.Scaled` (`Ellipse2D.Builder.Create(ellipse.Radii * scale, ellipse.Center * scale, ellipse.Axis)`) and the JSON converter per Interfaces.
- `GeometryValidation2DTests:18`: `new Ellipse2D(new(0, 1), Vector2.Zero, Vector2.UnitX)`. `ShapeConstructionTests:26`: `"new Ellipse2D(new Vector2(2, 1), Vector2.Zero, Vector2.UnitX)"`. `PersonWardrobe.cs:46` compiles unchanged.

- [ ] **Step 5: Run the task tests, then the full gate**

Run: the Step 2 command, then the full gate and the analyzer tests. Expected: pass.

- [ ] **Step 6: Commit**

```bash
git add -A App2d.Core App2d.Tests App2d.Analyzers.Tests
git commit -m "Oriented shapes: Ellipse2D stores center, radii and axis"
```

---

### Task 6: Studio inspector angle fields

**Files:**
- Modify: `App2d.CharacterStudio/Editor/ShapeInspector.cs:15-50,115-130`

**Interfaces:**
- Consumes the Task 2, 4 and 5 builders and `AngleRadians`.
- Produces: `RectangleFields(Vector2 centre, Vector2 size, float angleRadians, Func<Vector2, Vector2, float, IShape2D> fromSize)` replacing the min/max version; an `Angle (deg)` drag for rectangle, rounded rectangle and ellipse that converts degrees to radians on the way out.

- [ ] **Step 1: Rewrite `RectangleFields` and the three cases**

Rectangle: `RectangleFields(rectangle.Center, rectangle.Size, rectangle.AngleRadians, static (size, centre, angle) => Rectangle2D.Builder.FromSize(size, centre, angle))`. `AxisAlignedRectangle2D` keeps a min/max-derived call with no angle. Rounded: centre, size and angle drags rebuild through `RoundedRectangle2D.Builder.FromSize(size, radius, centre, angle)`, the radius drag stays a `with`. Ellipse: add the angle drag rebuilding through `Ellipse2D.Builder.Create(radii, centre, angle)`. Use `Ui.Drag("Angle (deg)", ref degrees, .5f, -180, 180)`, matching the existing `Ui.Drag` signature.

- [ ] **Step 2: Build the studio and run its smoke tests**

Run: `dotnet build App2d.CharacterStudio/App2d.CharacterStudio.csproj` then `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~Editor"`.
Expected: build succeeds; the editor smoke tests pass.

- [ ] **Step 3: Commit**

```bash
git add App2d.CharacterStudio/Editor/ShapeInspector.cs
git commit -m "Oriented shapes: angle fields in the studio shape inspector"
```

---

### Task 7: Posed

**Files:**
- Modify: `App2d.Core/Geometry/Operations/Transforms/ShapeTransforms2D.cs`
- Test: `App2d.Tests/Geometry/ShapePosed2DTests.cs` (new)

**Interfaces:**
- Produces `public static IShape2D Posed(IShape2D shape, Similarity2D pose)` in `ShapeTransforms2D`. Guards: `shape` not null; `pose.IsFinite` and `pose.Scale > 0`, else `ArgumentException`. Each case builds through the kind's `Builder` so the result is validated:
  - `Circle2D`: `ShapeTransforms2D.Circle` tuple. `Capsule2D`: `Capsule` tuple. `HalfSpace2D`: `HalfSpace` tuple.
  - `Triangle2D`, `ConvexPolygon2D`, `SimplePolygon2D`: `pose.TransformPoint` on every vertex.
  - `Rectangle2D`, `RoundedRectangle2D`, `Ellipse2D`: `Create(pose.TransformPoint(Center), extents * pose.Scale, [radius * pose.Scale,] Vector2.Normalize(pose.TransformDirection(Axis)))`.
  - `AxisAlignedRectangle2D`: with `tol = 0.001f * pose.Scale`, allowed when `(|XAxis.Y| <= tol && |YAxis.X| <= tol) || (|XAxis.X| <= tol && |YAxis.Y| <= tol)`; result `AxisAlignedRectangle2D.Builder.Create(Vector2.Min(p, q), Vector2.Max(p, q))` with `p`, `q` the transformed `Min` and `Max`. Otherwise `NotSupportedException("AxisAlignedRectangle2D cannot be rotated; use Rectangle2D.")`.
  - `CompositeShape2D`: each part posed and cast to `IConvexShape2D`, same `IncludeOverlap` and `AreaOutlineSegments`.
  - `HeightFieldShape2D` and unknown types: `NotSupportedException`.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class ShapePosed2DTests
{
    private static readonly Similarity2D Pose = Similarity2D.FromAxis(new(10, -4), OrientedFrame2D.AxisFromAngle(MathF.PI / 3), scale: 2f);

    public static TheoryData<IShape2D> Shapes => [
        Circle2D.Builder.Create(1, new(1, 2)), Capsule2D.Builder.Create(new(0, 0), new(3, 1), .5f), Triangle2D.Builder.Create(new(0, 0), new(2, 0), new(0, 1)),
        ConvexPolygon2D.Builder.Create([new(0, 0), new(2, 0), new(2, 1), new(0, 1)]), Rectangle2D.Builder.FromSize(3, 1, new(1, 1), .4f),
        RoundedRectangle2D.Builder.FromSize(3, 2, .5f, new(1, 1), .4f), Ellipse2D.Builder.Create(new(2, 1), new(1, 1), .4f)];

    [Theory, MemberData(nameof(Shapes))] public void PosedShapeSupportsWhereThePosedOriginalSupports(IShape2D shape)
    {
        var posed = Assert.IsAssignableFrom<IConvexShape2D>(ShapeTransforms2D.Posed(shape, Pose));
        Assert.Equal(shape.GetType(), posed.GetType());
        foreach (var angle in new[] { 0f, .9f, 2.2f, 4f })
        {
            var worldDirection = OrientedFrame2D.AxisFromAngle(angle);
            var expected = Pose.TransformPoint(((IConvexShape2D)shape).GetSupportPoint(Pose.TransposeTransformDirection(worldDirection)));
            Assert.Equal(expected, posed.GetSupportPoint(worldDirection), Loose);
        }
        Assert.Equal(shape.Area * 4f, posed.Area, 3);
    }
    [Fact] public void IdentityReturnsTheSameGeometry()
    {
        var box = Rectangle2D.Builder.FromSize(3, 1, new(1, 1), .4f);
        var same = Assert.IsType<Rectangle2D>(ShapeTransforms2D.Posed(box, Similarity2D.Identity));
        Assert.Equal(box.Center, same.Center, Near); Assert.Equal(box.HalfSize, same.HalfSize, Near); Assert.Equal(box.Axis, same.Axis, Near);
    }
    [Fact] public void MirrorKeepsOrientedShapesValid()
    {
        var mirrored = Similarity2D.FromAxis(Vector2.Zero, Vector2.UnitX, mirror: true);
        var rounded = Assert.IsType<RoundedRectangle2D>(ShapeTransforms2D.Posed(RoundedRectangle2D.Builder.FromSize(3, 2, .5f, new(1, 1), .4f), mirrored));
        Assert.Equal(new Vector2(1, -1), rounded.Center, Near); Assert.Equal(.5f, rounded.Radius);
    }
    [Fact] public void AxisAlignedRectangleAcceptsQuarterTurnsAndRefusesOthers()
    {
        var box = AxisAlignedRectangle2D.Builder.Create(new(0, 0), new(2, 1));
        var turned = Assert.IsType<AxisAlignedRectangle2D>(ShapeTransforms2D.Posed(box, Similarity2D.FromAxis(Vector2.Zero, Vector2.UnitY)));
        Assert.Equal(new Vector2(-1, 0), turned.Min, Near); Assert.Equal(new Vector2(0, 2), turned.Max, Near);
        Assert.Throws<NotSupportedException>(() => ShapeTransforms2D.Posed(box, Similarity2D.FromAxis(Vector2.Zero, OrientedFrame2D.AxisFromAngle(.5f))));
    }
    [Fact] public void CompositeAndHalfSpacePose()
    {
        var composite = CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1), Rectangle2D.Builder.FromSize(2, 2)]);
        var posed = Assert.IsType<CompositeShape2D>(ShapeTransforms2D.Posed(composite, Pose));
        Assert.Equal(2, posed.Parts.Length); Assert.Equal(composite.Area * 4f, posed.Area, 3);
        var floor = Assert.IsType<HalfSpace2D>(ShapeTransforms2D.Posed(HalfSpace2D.Builder.Create(Vector2.UnitY, 0f), Similarity2D.FromTranslation(new(0, 3))));
        Assert.Equal(3f, floor.Offset, 5);
    }
    [Fact] public void PosedRejectsDegeneratePoses()
    {
        Assert.ThrowsAny<ArgumentException>(() => ShapeTransforms2D.Posed(Circle2D.Builder.Create(1), default));
        Assert.Throws<NotSupportedException>(() => ShapeTransforms2D.Posed(HeightFieldShape2D.Builder.Create([new(0, 0), new(1, 0)], -1f), Pose));
    }
}
```

`default(Similarity2D)` has zero axes and zero scale, which is the degenerate pose the guard must reject. `HalfSpace2D.Builder.Create(Vector2 outwardNormal, float offset)` and `HeightFieldShape2D.Builder.Create(IEnumerable<Vector2> surface, float yMin)` are the real signatures.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapePosed2DTests"`
Expected: build error, `Posed` does not exist.

- [ ] **Step 3: Implement `Posed` per Interfaces, next to `Scaled`**

- [ ] **Step 4: Run the tests to verify they pass, then the full gate**

Run: the Step 2 command, then the full gate. Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add App2d.Core/Geometry/Operations/Transforms/ShapeTransforms2D.cs App2d.Tests/Geometry/ShapePosed2DTests.cs
git commit -m "Oriented shapes: Posed maps any built-in shape through a similarity"
```

---

### Task 8: README note and final gate

**Files:**
- Modify: `README.md` (the `## Geometry` section, after the `Rect2D` value-type paragraph)

- [ ] **Step 1: Add one paragraph**

State that `Rectangle2D`, `RoundedRectangle2D` and `Ellipse2D` carry a unit `Axis` and rotate in local space; that JSON writes `center`, `size`/`radii`, `angleRadians` and also reads `min`/`max` for the two rectangle kinds; that `AxisAlignedRectangle2D` is the one shape that does not rotate; and that `ShapeTransforms2D.Posed(shape, Similarity2D)` returns a shape of the same kind. Four sentences.

- [ ] **Step 2: Run the full gate and the analyzer tests one last time**

Run: both commands from Global Constraints. Expected: pass, apart from the known tileset failures when the tileset is absent.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "Oriented shapes: README note"
```
