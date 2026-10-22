# Oriented Boundary Area Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ShapeArea2D.Union` exact for rotated ellipses by giving the internal boundary part a frame, and expose `ShapeArea2D.Intersection(first, second)` from the same coverage walk.

**Architecture:** `Area2D.Union.cs` already integrates exposed conic arcs and polygon edges with a tan-half-angle quartic for crossings and a Green's theorem arc integral. The part gains a unit axis in canonical form; every projection goes through it; the quartic coefficients become six dot products. The per-piece walk gains a second selection (covered by the other group, exposed within its own) that yields the intersection of two groups of parts.

**Tech Stack:** .NET 10, `App2d.Core` geometry (`Vector2d`, `Interval<double>`, `PolynomialRoots`), xUnit in `App2d.Tests`.

**Spec:** `docs/superpowers/specs/2026-10-22-oriented-union-area-design.md`

## Global Constraints

- Work in the worktree `.claude/worktrees/rotatable-rect-design-5e94ff`, branch `claude/rotatable-rect-design-5e94ff`, at or above main's `3e4125ee`.
- Single-line method signatures and parameter lists, however long.
- The Write tool and sed emit LF; normalize every written or created file to CRLF (`sed -i 's/\r$//; s/$/\r/' FILE`), and verify `grep -c $'\r$'` equals `wc -l`.
- No Bash heredocs containing code.
- Commit per task on the branch. Merge into `main` only when Raymond says. Never push.
- Visual Studio stays open; retry a locked write once.
- Whole-suite gate: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName!~GameUsesConfiguredWidthForTheActualCollidableTerrain"` and `dotnet test App2d.Analyzers.Tests/App2d.Analyzers.Tests.csproj`. Redirect output to the SDD workspace and read the tail.
- Axis-aligned results stay unchanged up to rounding; every existing test in `ShapeArea2DTests` stays green after every task.
- No polyline approximation of an ellipse in production code. No `ParameterAt`. No public boundary-arc interface.

## Review Focus

1. A canonical axis exactly on the half-plane boundary (`(0, 1)` and `(0, -1)` spellings of the same ellipse) must coincide; pinned in Task 2 `RotatedEllipsesCoincideThroughCanonicalForm`.
2. A polygon corner lying exactly on a rotated ellipse vertex where a covered arc ends (the corner-contact cut with rotation) must keep that cut; pinned in Task 2 `RotatedEllipseAndPolygonUnionsAreExact` (the `1.5 pi + 4` straddling rectangle, upright and turned by a quarter).
3. Nearly coincident rotated ellipses, such as an ellipse and its JSON round trip whose axis differs by a float ulp, must give one area within `1e-5`; pinned in Task 2 `RotatedEllipsesCoincideThroughCanonicalForm`.
4. A zero-area part in an intersection group must give zero, not a throw; pinned in Task 4 `IntersectionValidatesItsArgumentsAndHandlesDegenerateParts`.
5. Rotated pairs scaled uniformly by `1e-8` and `1e8` must keep their intersection area up to the scale squared; pinned in Task 3 `IntersectionIsInvariantUnderTranslationRotationAndUniformScaling`.

---

### Task 1: Name the boundary part and its arc integral

**Files:**
- Rename: `App2d.Core/Geometry/Operations/Area/Area2D.Union.cs` to `App2d.Core/Geometry/Operations/Area/Area2D.Boundary.cs`
- Modify: `App2d.Core/Geometry/Operations/Area/Area2D.Boundary.cs` (whole file: `UnionPart` to `BoundaryPart`, `ArcArea` to `SignedArcArea`)
- Modify: `App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs:38,92` (`Area2D.UnionPart` to `Area2D.BoundaryPart`)

**Interfaces:**
- Produces: `internal sealed class Area2D.BoundaryPart` with today's members; `private static double SignedArcArea(BoundaryPart curve, double from, double to)`. Behavior unchanged.

- [ ] **Step 1: Rename the file**

```bash
git mv App2d.Core/Geometry/Operations/Area/Area2D.Union.cs App2d.Core/Geometry/Operations/Area/Area2D.Boundary.cs
```

- [ ] **Step 2: Rename the identifiers**

`sed -i 's/UnionPart/BoundaryPart/g; s/\bArcArea\b/SignedArcArea/g'` over `Area2D.Boundary.cs` and `ShapeArea2D.cs`. Keep the `Union` method name. Verify the CR count of each file is unchanged.

- [ ] **Step 3: Build and run the area tests**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapeArea2DTests"`
Expected: PASS, 52 tests, 0 failed.

- [ ] **Step 4: Commit**

```bash
git add -A App2d.Core/Geometry/Operations/Area
git commit -m "Area: name the boundary part and its signed arc integral for what they are"
```

---

### Task 2: The frame in the part; rotated ellipses exact in the union

**Files:**
- Modify: `App2d.Core/Geometry/Operations/Area/Area2D.Boundary.cs:132-172` (`CoverCurveWithCurve`), `:186-187` (magnitude guard), `:249` (`CurveRoots` direction), `:366-475` (`BoundaryPart`)
- Modify: `App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs:46-51` (drop the `IsAxisAligned` guard)
- Test: `App2d.Tests/Geometry/ShapeArea2DTests.cs` (new tests; oracle at `:503-520`; helpers `:442`, `:477`, `:596` become `internal static` / `internal`)

**Interfaces:**
- Consumes: `Area2D.BoundaryPart`, `SignedArcArea` from Task 1; `OrientedFrame2D.EllipseBounds` formula (reproduced in doubles, not called).
- Produces:
  - `internal BoundaryPart(Vector2 center, Vector2 radii, Vector2 axis)` replacing `(Vector2 center, Vector2 radii)`; `internal BoundaryPart(Vector2 center, float radius)` unchanged, passing `Vector2.UnitX`.
  - `internal Vector2d Axis { get; }`, `internal Vector2d Perp => Axis.PerpCcw`.
  - `internal Vector2d ToFrame(Vector2d point)`, `internal Vector2d FromFrame(Vector2d point)`, both center-relative.
  - `internal Vector2d NormalizeRelative(Vector2d point)` now `ToFrame(point) / Radii` componentwise; still linear.
  - `internal Vector2d PointAt(double angle)` now `FromFrame(new(Radii.X * cos, Radii.Y * sin))`.
  - Test helpers `internal static double CircleLens(double a, double b, double distance)`, `internal static double CrossSectionArea(IConvexShape2D[] parts)` (rotation-aware), `internal sealed class PointShape`.

- [ ] **Step 1: Make the oracle rotation-aware**

In `ShapeArea2DTests.CrossSectionArea`, replace the `Ellipse2D` branch (`:503-514`): with `f = ellipse.Frame`, `dx = x - C.X`, `e = (f.Axis.X * dx / Rx, f.Perp.X * dx / Ry)` and `g = (f.Axis.Y / Rx, f.Perp.Y / Ry)`, solve `|e + g s|^2 = 1` for `s = y - C.Y` (quadratic `g.g s^2 + 2 e.g s + e.e - 1 = 0`; a discriminant `<= 0` means no crossing) and add `(C.Y + sLow, C.Y + sHigh)`. Replace the `Rectangle2D` branch (`:515-518`) with one that handles `Rectangle2D` (its four `WriteCorners` corners) and `ConvexPolygon2D` (its `Vertices`) alike: collect the `y` of every edge the vertical line `x` crosses strictly between its endpoints' `x`, and add `(min y, max y)` when two were found. Make `CircleLens`, `CrossSectionArea` and `PointShape` `internal`.

- [ ] **Step 2: Run the existing area tests to prove the oracle still agrees**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapeArea2DTests"`
Expected: PASS, 52 tests (the mixed-union oracle tests still pass with the new branches).

- [ ] **Step 3: Write the failing union tests**

Append to `ShapeArea2DTests`:

```csharp
[Fact]
public void RotatedEllipseAndPolygonUnionsAreExact()
{
    var ellipse = Ellipse2D.Builder.Create(new(2, 1), new(.5f, -.25f), .6f);
    var rectangle = Rectangle2D.Builder.FromSize(2.5f, 1.2f, new(1, .5f), -.4f);
    var polygon = ConvexPolygon2D.Builder.Create([new(-1, -2), new(2.5f, -1), new(1, 1.5f), new(-2, .5f)]);
    IConvexShape2D[] parts = [ellipse, rectangle, polygon];
    Assert.Equal(ShapeArea2D.Union(parts, 3), ShapeArea2D.Union(parts, 128));
    Assert.InRange(Math.Abs(ShapeArea2D.Union(parts, 3) - CrossSectionArea(parts)), 0d, .0002d);
    // A rectangle corner exactly on the ellipse vertex, where the covered quarter arc ends; then the same turned by a quarter.
    Assert.Equal(1.5f * MathF.PI + 4, ShapeArea2D.Union([Ellipse2D.Builder.Create(new(2, 1)), Rectangle2D.Builder.Create(new(0, 0), new(2, 2))]), 5);
    var upright = Ellipse2D.Builder.Create(new(2, 1), default, Vector2.UnitY);
    var straddling = Rectangle2D.Builder.Create(new(-1, 1), new(1, 1), Vector2.UnitY);
    Assert.Equal(1.5f * MathF.PI + 4, ShapeArea2D.Union([upright, straddling]), 5);
}

[Fact]
public void RotatedEllipsePairsAgreeWithTheRotatedCircleLens()
{
    var scale = new Vector2(3, .5f);
    const float angle = .7f;
    foreach (var (first, second, distance) in new[] { (1f, 1f, 1f), (2f, 1f, .5f), (2f, 1f, 2f) })
    {
        var expected = scale.X * scale.Y * (Math.PI * (first * first + second * second) - CircleLens(first, second, distance));
        var a = Ellipse2D.Builder.Create(first * scale, default, angle);
        var b = Ellipse2D.Builder.Create(second * scale, Vector2.Transform(new(distance * scale.X, 0), Matrix3x2.CreateRotation(angle)), angle);
        Assert.Equal((float)expected, ShapeArea2D.Union([a, b], 3), 5);
        Assert.Equal(ShapeArea2D.Union([a, b]), ShapeArea2D.Union([b, a]), 5);
    }
}

[Theory]
[InlineData(2f, 1f)]
[InlineData(50f, .01f)]
public void RotatedEllipsesWithFourCrossingsKeepEveryExposedArc(float a, float b)
{
    var expected = (float)(4d * a * b * Math.Atan2(a, b));
    Assert.Equal(expected, ShapeArea2D.Union([Ellipse2D.Builder.Create(new(a, b)), Ellipse2D.Builder.Create(new(a, b), default, Vector2.UnitY)]), 5);
    Assert.Equal(expected, ShapeArea2D.Union([Ellipse2D.Builder.Create(new(a, b), default, MathF.PI / 4), Ellipse2D.Builder.Create(new(a, b), default, 3 * MathF.PI / 4)]), 5);
}

[Fact]
public void RotatedEllipsesCoincideThroughCanonicalForm()
{
    var axis = Polar2D.Direction(.3f);
    var ellipse = Ellipse2D.Builder.Create(new(2, 1), new(1, 1), axis);
    Assert.Equal(ellipse.Area, ShapeArea2D.Union([ellipse, Ellipse2D.Builder.Create(new(2, 1), new(1, 1), -axis)]), 5);
    Assert.Equal(ellipse.Area, ShapeArea2D.Union([ellipse, Ellipse2D.Builder.Create(new(1, 2), new(1, 1), axis.PerpCcw)]), 5);
    Assert.Equal(ellipse.Area, ShapeArea2D.Union([Ellipse2D.Builder.Create(new(2, 1), new(1, 1), Vector2.UnitY), Ellipse2D.Builder.Create(new(2, 1), new(1, 1), -Vector2.UnitY)]), 5);
    Assert.Equal(MathF.PI, ShapeArea2D.Union([Circle2D.Builder.Create(1), Ellipse2D.Builder.Create(Vector2.One, default, axis)]), 5);
    var roundTrip = (Ellipse2D)GeometryJson2D.ShapeFromJson(ellipse.ToJson());
    Assert.InRange(MathF.Abs(ShapeArea2D.Union([ellipse, roundTrip]) - ellipse.Area), 0f, 1e-5f);
}

[Fact]
public void RotatedMixedUnionsAgreeWithTheOracleAndAreRotationInvariant()
{
    var random = new Random(31907);
    Check([Ellipse2D.Builder.Create(new(10, .02f), default, .35f), Circle2D.Builder.Create(.25f, 3 * Polar2D.Direction(.35f))]);
    for (var sample = 0; sample < 32; sample++)
    {
        var angle = (float)random.NextDouble() * MathF.Tau;
        var parts = new IConvexShape2D[6];
        for (var i = 0; i < parts.Length; i++)
        {
            var center = new Vector2((float)random.NextDouble() * 4 - 2, (float)random.NextDouble() * 4 - 2);
            var turn = (float)random.NextDouble() * MathF.Tau;
            parts[i] = (i % 3) switch
            {
                0 => Ellipse2D.Builder.Create(new(.1f + (float)random.NextDouble() * 2, .1f + (float)random.NextDouble() * 2), center, turn),
                1 => Circle2D.Builder.Create(.3f + (float)random.NextDouble(), center),
                _ => Rectangle2D.Builder.FromSize(.5f + (float)random.NextDouble(), .5f + (float)random.NextDouble(), center, turn)
            };
        }
        Check(parts);
        var rotation = Matrix3x2.CreateRotation(angle);
        var rotated = parts.Select(part => part switch
        {
            Ellipse2D e => (IConvexShape2D)Ellipse2D.Builder.Create(e.Radii, Vector2.Transform(e.Center, rotation), e.AngleRadians + angle),
            Circle2D c => Circle2D.Builder.Create(c.Radius, Vector2.Transform(c.Center, rotation)),
            Rectangle2D r => Rectangle2D.Builder.FromSize(r.Size, Vector2.Transform(r.Center, rotation), r.AngleRadians + angle),
            _ => throw new InvalidOperationException()
        }).ToArray();
        Assert.Equal(ShapeArea2D.Union(parts), ShapeArea2D.Union(rotated), 4);
    }

    static void Check(IConvexShape2D[] parts)
    {
        var expected = CrossSectionArea(parts);
        Assert.InRange(Math.Abs(ShapeArea2D.Union(parts, 3) - expected), 0d, .0002d);
        Assert.Equal(ShapeArea2D.Union(parts, 3), ShapeArea2D.Union(parts.Reverse().ToArray(), 128), 5);
    }
}

[Fact]
public void ASlightlyLongAxisStillIntegratesTheEllipseArea()
{
#pragma warning disable APP2D001 // Raw construction is the point: a validated axis may be 0.001 off unit length.
    var ellipse = new Ellipse2D { Radii = new(2, 1), Axis = new(1.0005f, 0) };
#pragma warning restore APP2D001
    Assert.Equal(Area2D.Ellipse(new(2, 1)) + MathF.PI, ShapeArea2D.Union([ellipse, Circle2D.Builder.Create(1, new(10, 0))]), 5);
}
```

- [ ] **Step 4: Run the new tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapeArea2DTests.Rotated|FullyQualifiedName~ASlightlyLongAxis"`
Expected: FAIL. `RotatedEllipseAndPolygonUnionsAreExact` fails on the 3-versus-128 equality (sampled outline); the lens, four-crossing, coincidence and slightly-long-axis tests miss by sampling error; the oracle test exceeds `.0002`.

- [ ] **Step 5: Give `BoundaryPart` its frame**

In `Area2D.Boundary.cs` `BoundaryPart`: add `Axis` (get-only) and `Perp`; change the radii constructor to `(Vector2 center, Vector2 radii, Vector2 axis)` and apply the spec's canonical form in order (normalize the axis in double; circle to `(1, 0)`; `radii.X < radii.Y` swaps the radii and takes `axis.PerpCcw`; `axis.X < 0 || axis.X == 0 && axis.Y < 0` negates). The circle constructor passes `Vector2.UnitX`. Add `ToFrame` and `FromFrame`; route `NormalizeRelative` and `PointAt` through them. Bounds: `Axis == (1, 0)` keeps `_min = -Radii`, `_max = Radii`; otherwise half extents `(sqrt((Rx*Ax)^2 + (Ry*Px)^2), sqrt((Rx*Ay)^2 + (Ry*Py)^2))`.

- [ ] **Step 6: Generalize the ellipse-against-ellipse coefficients**

In `CoverCurveWithCurve` replace `sx`/`sy` and the five coefficient lines with:

```csharp
var o = other.NormalizeRelative(curve.Center - other.Center);
var u = other.NormalizeRelative(curve.Axis * curve.Radii.X);
var v = other.NormalizeRelative(curve.Perp * curve.Radii.Y);
var k = o.Dot(o) - 1d;
// per chart, s = +1 then -1:
coefficients[0] = k + u.Dot(u) + 2d * s * o.Dot(u);
coefficients[1] = 4d * s * o.Dot(v) + 4d * u.Dot(v);
coefficients[2] = 2d * k - 2d * u.Dot(u) + 4d * v.Dot(v);
coefficients[3] = 4d * s * o.Dot(v) - 4d * u.Dot(v);
coefficients[4] = k + u.Dot(u) - 2d * s * o.Dot(u);
```

The existing `offset` is `o`; keep the coincidence branch and add `curve.Axis == other.Axis` to its condition. The root-to-angle mapping and `CoverArcsInside` are unchanged.

- [ ] **Step 7: Route the segment direction and the magnitude guard through the frame**

`CurveRoots` line `:249` becomes `direction = curve.NormalizeRelative(direction);`. In `CoverCurveWithPolygon` the magnitude becomes `1d + (Math.Abs(offset.X) + Math.Abs(offset.Y) + Math.Abs(vertex.X) + Math.Abs(vertex.Y)) / curve.Radii.Y`.

- [ ] **Step 8: Drop the guard in `ShapeArea2D.Union`**

`ShapeArea2D.cs:46-51` becomes `if (shape is Ellipse2D ellipse) { boundaries.Add(new(ellipse.Center, ellipse.Radii, ellipse.Axis)); continue; }` without the comment.

- [ ] **Step 9: Run the area tests**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapeArea2DTests"`
Expected: PASS, 59 tests, 0 failed.

- [ ] **Step 10: Run the whole-suite gate**

Run both gate commands from Global Constraints.
Expected: PASS, 0 failed in each.

- [ ] **Step 11: Commit**

```bash
git add App2d.Core/Geometry/Operations/Area/Area2D.Boundary.cs App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs App2d.Tests/Geometry/ShapeArea2DTests.cs
git commit -m "Area: the boundary part carries a frame, so rotated ellipses are exact in the union"
```

---

### Task 3: The intersection selection and `ShapeArea2D.Intersection`

**Files:**
- Modify: `App2d.Core/Geometry/Operations/Area/Area2D.Boundary.cs:13-96` (the walk), `:323-346` (interval integration helpers)
- Modify: `App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs:23-90` (extract `AddParts`; add `Intersection`)
- Create: `App2d.Tests/Geometry/ShapeIntersectionArea2DTests.cs`

**Interfaces:**
- Consumes: `BoundaryPart` with its frame (Task 2); `ShapeArea2DTests.CircleLens`, `CrossSectionArea` (Task 2).
- Produces:
  - `internal static float Area2D.Union(ReadOnlySpan<BoundaryPart> parts)` unchanged in signature, now `Integrate(parts, parts.Length, false)`.
  - `internal static float Area2D.Intersection(ReadOnlySpan<BoundaryPart> first, ReadOnlySpan<BoundaryPart> second)`.
  - `private static float Area2D.Integrate(ReadOnlySpan<BoundaryPart> parts, int firstCount, bool intersect)`.
  - `private static double Area2D.IntegrateCoveredGaps(List<Interval<double>> own, List<Interval<double>> other, double end, Func<double, double, double> integrate)`.
  - `public static float ShapeArea2D.Intersection(IConvexShape2D first, IConvexShape2D second, int outlineSegments = DefaultOutlineSegments)`.
  - `private static void ShapeArea2D.AddParts(List<Area2D.BoundaryPart> parts, IConvexShape2D shape, int outlineSegments)`.

- [ ] **Step 1: Write the failing intersection tests**

Create `ShapeIntersectionArea2DTests.cs` (same usings as `ShapeArea2DTests` plus the namespaces of `Similarity2D` and `ShapeTransforms2D`):

```csharp
public sealed class ShapeIntersectionArea2DTests
{
    [Fact]
    public void RandomPairsSatisfyInclusionExclusionSymmetryAndBounds()
    {
        var random = new Random(4471);
        for (var sample = 0; sample < 64; sample++)
        {
            var a = RandomShape(random, sample % 4);
            var b = RandomShape(random, (sample / 4) % 4);
            var intersection = ShapeArea2D.Intersection(a, b);
            Assert.InRange(Math.Abs(intersection + ShapeArea2D.Union([a, b]) - (a.Area + b.Area)), 0f, 1e-4f);
            Assert.Equal(intersection, ShapeArea2D.Intersection(b, a), 5);
            Assert.InRange(intersection, 0f, MathF.Min(a.Area, b.Area) + 1e-5f);
        }
    }

    [Fact]
    public void IdenticalContainedDisjointAndTangentEllipses()
    {
        const float angle = .4f;
        var ellipse = Ellipse2D.Builder.Create(new(2, 1), new(.5f, -.25f), angle);
        Assert.Equal(ellipse.Area, ShapeArea2D.Intersection(ellipse, Ellipse2D.Builder.Create(new(2, 1), new(.5f, -.25f), angle)), 5);
        var inner = Ellipse2D.Builder.Create(new(1, .5f), new(.5f, -.25f), angle);
        Assert.Equal(inner.Area, ShapeArea2D.Intersection(ellipse, inner), 5);
        Assert.Equal(0f, ShapeArea2D.Intersection(ellipse, Ellipse2D.Builder.Create(new(2, 1), new(10, 0), angle)));
        var upright = Ellipse2D.Builder.Create(new(2, 1));
        Assert.Equal(0f, ShapeArea2D.Intersection(upright, Ellipse2D.Builder.Create(new(2, 1), new(4, 0))), 5);
        Assert.Equal(0f, ShapeArea2D.Intersection(Circle2D.Builder.Create(1), Circle2D.Builder.Create(1, new(2, 0))), 5);
        var touchingInside = Ellipse2D.Builder.Create(new(1, .5f), new(1, 0));
        Assert.Equal(touchingInside.Area, ShapeArea2D.Intersection(upright, touchingInside), 5);
        Assert.Equal(touchingInside.Area, ShapeArea2D.Intersection(Rotated(upright, angle), Rotated(touchingInside, angle)), 5);
    }

    [Fact]
    public void CircleLensesAreExactEvenForThinSlivers()
    {
        Assert.InRange(Math.Abs(ShapeArea2D.Intersection(Circle2D.Builder.Create(1), Circle2D.Builder.Create(1, new(1, 0))) - ShapeArea2DTests.CircleLens(1, 1, 1)), 0d, 1e-6d);
        const float distance = 1.999f;
        var sliver = ShapeArea2D.Intersection(Circle2D.Builder.Create(1), Circle2D.Builder.Create(1, new(distance, 0)));
        Assert.InRange(Math.Abs(sliver - ShapeArea2DTests.CircleLens(1, 1, distance)), 0d, 1e-9d);
    }

    [Fact]
    public void RotatedEllipsePairsMatchTheOracleTheFourCrossingFormulaAndTheSeamLens()
    {
        var a = Ellipse2D.Builder.Create(new(2, 1), default, .3f);
        var b = Ellipse2D.Builder.Create(new(2, 1), new(1.5f, .4f), -.5f);
        Assert.InRange(Math.Abs(ShapeArea2D.Intersection(a, b) - (a.Area + b.Area - ShapeArea2DTests.CrossSectionArea([a, b]))), 0d, .0002d);
        var c = Ellipse2D.Builder.Create(new(2, .5f), default, .3f);
        var d = Ellipse2D.Builder.Create(new(2, .5f), default, .3f + MathF.PI / 2);
        Assert.Equal((float)(2 * Math.PI * 2 * .5 - 4 * 2 * .5 * Math.Atan2(2, .5)), ShapeArea2D.Intersection(c, d), 5);
        // B centered on A's +X vertex: the crossings straddle A's parameter seam.
        var seam = (float)(2 * 1 * ShapeArea2DTests.CircleLens(1, 1, 1));
        Assert.Equal(seam, ShapeArea2D.Intersection(Ellipse2D.Builder.Create(new(2, 1)), Ellipse2D.Builder.Create(new(2, 1), new(2, 0))), 5);
        Assert.Equal(seam, ShapeArea2D.Intersection(Ellipse2D.Builder.Create(new(2, 1), default, .3f), Ellipse2D.Builder.Create(new(2, 1), 2 * Polar2D.Direction(.3f), .3f)), 5);
    }

    [Fact]
    public void CapsuleAgainstRotatedEllipseUsesTheGroupSemantics()
    {
        var capsule = Capsule2D.Builder.Create(new(-1, 0), new(1, .5f), .4f);
        var ellipse = Ellipse2D.Builder.Create(new(1.5f, .7f), new(.3f, .2f), .6f);
        var expected = capsule.Area + ellipse.Area - ShapeArea2D.Union([capsule, ellipse]);
        Assert.Equal(expected, ShapeArea2D.Intersection(capsule, ellipse), 5);
        Assert.Equal(expected, ShapeArea2D.Intersection(ellipse, capsule), 5);
    }

    [Fact]
    public void IntersectionIsInvariantUnderTranslationRotationAndUniformScaling()
    {
        var a = Ellipse2D.Builder.Create(new(2, 1), default, .3f);
        var b = Ellipse2D.Builder.Create(new(1.5f, .75f), new(1.5f, .5f), -.8f);
        var expected = ShapeArea2D.Intersection(a, b);
        var offset = new Vector2(1 << 20, -(1 << 20));
        Assert.Equal(expected, ShapeArea2D.Intersection(Ellipse2D.Builder.Create(a.Radii, offset, .3f), Ellipse2D.Builder.Create(b.Radii, b.Center + offset, -.8f)), 5);
        Assert.Equal(expected, ShapeArea2D.Intersection(Rotated(a, 1.1f), Rotated(b, 1.1f)), 5);
        foreach (var scale in new[] { 1e-8f, 1e8f })
        {
            var scaled = ShapeArea2D.Intersection(Ellipse2D.Builder.Create(a.Radii * scale, default, .3f), Ellipse2D.Builder.Create(b.Radii * scale, b.Center * scale, -.8f));
            Assert.InRange(MathF.Abs(scaled / scale / scale - expected), 0f, 2e-6f);
        }
    }

    [Fact]
    public void ASlightlyLongAxisIntersectsItselfInTheEllipseArea()
    {
#pragma warning disable APP2D001
        var ellipse = new Ellipse2D { Radii = new(2, 1), Axis = new(1.0005f, 0) };
#pragma warning restore APP2D001
        Assert.Equal(Area2D.Ellipse(new(2, 1)), ShapeArea2D.Intersection(ellipse, ellipse), 5);
    }

    private static IConvexShape2D Rotated(IConvexShape2D shape, float angle)
    {
        Assert.True(Similarity2D.TryFromMatrix(Matrix3x2.CreateRotation(angle), out var pose));
        return (IConvexShape2D)ShapeTransforms2D.Posed(shape, pose);
    }

    private static IConvexShape2D RandomShape(Random random, int kind)
    {
        var center = new Vector2((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1);
        var turn = (float)random.NextDouble() * MathF.Tau;
        return kind switch
        {
            0 => Ellipse2D.Builder.Create(new(.2f + (float)random.NextDouble() * 2, .2f + (float)random.NextDouble() * 2), center, turn),
            1 => Circle2D.Builder.Create(.3f + (float)random.NextDouble(), center),
            2 => Rectangle2D.Builder.FromSize(.5f + (float)random.NextDouble(), .5f + (float)random.NextDouble(), center, turn),
            _ => Capsule2D.Builder.Create(center, center + Polar2D.Direction(turn) * (.5f + (float)random.NextDouble()), .2f + (float)random.NextDouble() * .5f)
        };
    }
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapeIntersectionArea2DTests"`
Expected: FAIL to compile: `ShapeArea2D` does not contain a definition for `Intersection`.

- [ ] **Step 3: Turn the walk into `Integrate` with two coverage lists**

In `Area2D.Boundary.cs`: rename the body of `Union` to `private static float Integrate(ReadOnlySpan<BoundaryPart> parts, int firstCount, bool intersect)`. Per boundary piece keep two lists, `own` and `other`; for every other part `j` the target list is `(j < firstCount) == (i < firstCount) ? own : other`, passed to the existing `Cover*` functions with today's `preferOther = j < i`. The conic integrand becomes `intersect ? IntegrateCoveredGaps(own, other, Math.Tau, f) : IntegrateExposed(own, Math.Tau, f)`; the edge length likewise with `end = 1d` and the `(low, high) => high - low` integrand. Add `internal static float Union(ReadOnlySpan<BoundaryPart> parts) => Integrate(parts, parts.Length, false);` and `internal static float Intersection(ReadOnlySpan<BoundaryPart> first, ReadOnlySpan<BoundaryPart> second)` that concatenates into one array and calls `Integrate(all, first.Length, true)`.

`IntegrateCoveredGaps(own, other, end, integrate)`: sort both lists; merge `other` into disjoint intervals; walk the gaps of `own` exactly as `IntegrateExposed` does (including the final `[position, end]` gap) and for each gap add `integrate(max(gapLow, o.Min), min(gapHigh, o.Max))` for every merged `o` where that range is positive.

- [ ] **Step 4: Extract `AddParts` and add `ShapeArea2D.Intersection`**

Move the per-shape body of the `foreach` in `ShapeArea2D.Union` (`:39-88`) into `private static void AddParts(List<Area2D.BoundaryPart> parts, IConvexShape2D shape, int outlineSegments)` and call it from `Union`. Add `public static float Intersection(IConvexShape2D first, IConvexShape2D second, int outlineSegments = DefaultOutlineSegments)`: `ArgumentNullException.ThrowIfNull` on both shapes, the same `outlineSegments` guard as `Union`, two lists filled by `AddParts`, then `Area2D.Intersection(CollectionsMarshal.AsSpan(a), CollectionsMarshal.AsSpan(b))`. XML doc: counts the overlap of two convex shapes exactly for every kind `Union` is exact for; the pair fast path arrives in Task 4.

- [ ] **Step 5: Run the intersection and area tests**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapeIntersectionArea2DTests|FullyQualifiedName~ShapeArea2DTests"`
Expected: PASS, 66 tests, 0 failed.

- [ ] **Step 6: Run the whole-suite gate**

Run both gate commands from Global Constraints.
Expected: PASS, 0 failed in each.

- [ ] **Step 7: Commit**

```bash
git add App2d.Core/Geometry/Operations/Area/Area2D.Boundary.cs App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs App2d.Tests/Geometry/ShapeIntersectionArea2DTests.cs
git commit -m "Area: intersection of two convex shapes from the same boundary walk, selecting covered arcs"
```

---

### Task 4: Rectangle fast path, degenerate inputs, composite pin and README

**Files:**
- Modify: `App2d.Core/Geometry/Operations/Area/Area2D.cs:33-39` (`RectangleUnion`; add `RectangleIntersection`)
- Modify: `App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs` (`Intersection` fast path)
- Modify: `App2d.Core/Geometry/README.md:195-212`
- Test: `App2d.Tests/Geometry/ShapeIntersectionArea2DTests.cs`

**Interfaces:**
- Consumes: `ShapeArea2D.Intersection` (Task 3); `ShapeArea2DTests.PointShape` (Task 2).
- Produces: `public static float Area2D.RectangleIntersection(Vector2 firstMin, Vector2 firstMax, Vector2 secondMin, Vector2 secondMax)`; `RectangleUnion` calls it.

- [ ] **Step 1: Write the failing tests**

Append to `ShapeIntersectionArea2DTests`:

```csharp
[Fact]
public void AxisAlignedRectanglePairsTakeTheDirectFormula()
{
    Assert.Equal(2f, Area2D.RectangleIntersection(new(0, 0), new(2, 2), new(1, 0), new(3, 2)));
    Assert.Equal(0f, Area2D.RectangleIntersection(new(0, 0), new(2, 2), new(3, 0), new(5, 2)));
    var first = AxisAlignedRectangle2D.Builder.Create(new(0, 0), new(2, 2));
    var second = AxisAlignedRectangle2D.Builder.Create(new(1, .5f), new(3, 2.5f));
    Assert.Equal(1.5f, ShapeArea2D.Intersection(first, second));
    Assert.Equal(ShapeArea2D.Intersection(first, second), ShapeArea2D.Intersection(Rectangle2D.Builder.Create(new(0, 0), new(2, 2)), Rectangle2D.Builder.Create(new(1, .5f), new(3, 2.5f))), 5);
}

[Fact]
public void IntersectionValidatesItsArgumentsAndHandlesDegenerateParts()
{
    var circle = Circle2D.Builder.Create(1);
    Assert.ThrowsAny<ArgumentException>(() => ShapeArea2D.Intersection(null!, circle));
    Assert.ThrowsAny<ArgumentException>(() => ShapeArea2D.Intersection(circle, null!));
    Assert.ThrowsAny<ArgumentException>(() => ShapeArea2D.Intersection(circle, circle, 2));
    Assert.Equal(0f, ShapeArea2D.Intersection(new ShapeArea2DTests.PointShape(), circle));
}

[Fact]
public void CompositeUnionOfRotatedEllipsesNoLongerDependsOnSampling()
{
    IConvexShape2D[] parts = [Ellipse2D.Builder.Create(new(2, 1), default, .3f), Ellipse2D.Builder.Create(new(2, 1), new(1, .5f), -.9f)];
    var coarse = CompositeShape2D.Builder.Create(parts, includeOverlap: false, areaOutlineSegments: 3);
    var fine = CompositeShape2D.Builder.Create(parts, includeOverlap: false, areaOutlineSegments: 128);
    Assert.Equal(ShapeArea2D.Union(parts), coarse.Area);
    Assert.Equal(coarse.Area, fine.Area);
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapeIntersectionArea2DTests"`
Expected: FAIL to compile: `Area2D` does not contain a definition for `RectangleIntersection`. (After adding only the raw op, `IntersectionValidatesItsArgumentsAndHandlesDegenerateParts` and the composite pin pass through Task 3's code; that is expected, since they pin behavior the review focus asked for rather than new code.)

- [ ] **Step 3: Add `Area2D.RectangleIntersection` and the fast path**

`RectangleIntersection` returns the clamped overlap width times height in double, cast to float; `RectangleUnion` becomes the two areas minus `RectangleIntersection`. In `ShapeArea2D.Intersection`, after the guards: `if (first is IRect2D a && second is IRect2D b) return Area2D.RectangleIntersection(a.Min, a.Max, b.Min, b.Max);`.

- [ ] **Step 4: Run the intersection and area tests**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ShapeIntersectionArea2DTests|FullyQualifiedName~ShapeArea2DTests"`
Expected: PASS, 69 tests, 0 failed.

- [ ] **Step 5: Update the README paragraph**

`App2d.Core/Geometry/README.md:197`: ellipses stay analytic in any orientation (the part carries a frame; crossings are still quartic). Add one sentence after `:199`: `ShapeArea2D.Intersection(first, second)` returns the overlap of two convex shapes from the same walk by selecting covered arcs, exact for every kind the union is exact for, with a direct formula for two axis-aligned rectangles. Leave `:211-212` (rounded rectangles and custom shapes are still sampled).

- [ ] **Step 6: Run the whole-suite gate**

Run both gate commands from Global Constraints.
Expected: PASS, 0 failed in each.

- [ ] **Step 7: Commit**

```bash
git add App2d.Core/Geometry/Operations/Area/Area2D.cs App2d.Core/Geometry/Operations/Area/ShapeArea2D.cs App2d.Core/Geometry/README.md App2d.Tests/Geometry/ShapeIntersectionArea2DTests.cs
git commit -m "Area: rectangle fast path for intersection, degenerate and composite pins, README"
```
