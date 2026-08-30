# Restructure, Composite Shapes, and Rotational Contacts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move all source into its owning project folder (killing link-based projects), dedupe projection math, add `Area`, add non-convex `CompositeShape2D` through dispatch/ray/renderer, give contacts rotational response + friction, and drop a tumbling composite prop into the level.

**Architecture:** Task 1 is a pure `git mv` + csproj simplification (no content edits, namespaces unchanged) verified by the existing 30-test suite. Later tasks follow the established narrow-phase patterns (`Similarity2D` poses, dispatch rows, stackalloc SAT) and gate all physics behavior changes behind opt-in body flags (`FreezeRotation` default true, `Friction` default 0).

**Tech Stack:** .NET 10, System.Numerics, SkiaSharp, xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-08-29-restructure-composite-design.md`

## Global Constraints

- Namespaces stay `App2d.Engine.*` / `App2d.Gameplay` — the restructure moves files only.
- Every task ends with `dotnet build App2d.slnx --nologo -v q` and `dotnet test App2d.Tests --nologo -v q` green at 0 warnings (baseline 30 tests).
- Existing bodies must behave exactly as today: `FreezeRotation` defaults true, `Friction` defaults 0.
- User style: single-line method signatures/parameter lists.
- Work continues on the `collision-cleanup` branch. Commit after each task.
- Visual Studio should be closed (or idle) during Task 1.

---

### Task 1: Physical restructure

**Files:**
- Move (git mv): see table in spec Part 1 — sources into `App2d.Core\`, `App2d.Collision\`, `App2d.Tiles\`, `App2d.Physics\`, `App2d.Rendering\`, `App2d.Audio\`, `App2d.Gameplay\`; `Engine\Game2D.cs`/`GameHost.cs`/`InputState.cs`/`Diagnostics\` flatten into `App2d\`.
- Modify: all seven library csprojs + `App2d\App2d.csproj`
- Modify: `README.md` (folder references)

**Interfaces:** none — no content changes; later tasks use the new paths.

- [ ] **Step 1: Commit any pending user formatting** (`git add -A; git commit -m "style: formatting pass"`) so the move commit is pure.
- [ ] **Step 2: Move with git mv** (PowerShell, from repo root):

```powershell
git mv App2d/ArgGuard.cs App2d.Core/ArgGuard.cs
git mv App2d/StateGuard.cs App2d.Core/StateGuard.cs
git mv App2d/Engine/FrameTime.cs App2d.Core/FrameTime.cs
git mv App2d/Engine/SpatialObject2D.cs App2d.Core/SpatialObject2D.cs
git mv App2d/Engine/Animation App2d.Core/Animation
git mv App2d/Engine/Mathematics App2d.Core/Mathematics
git mv App2d/Engine/Geometry App2d.Core/Geometry
foreach ($item in Get-ChildItem App2d/Engine/Collision) { git mv "App2d/Engine/Collision/$($item.Name)" "App2d.Collision/$($item.Name)" }
foreach ($item in Get-ChildItem App2d/Engine/Tiles) { git mv "App2d/Engine/Tiles/$($item.Name)" "App2d.Tiles/$($item.Name)" }
foreach ($item in Get-ChildItem App2d/Engine/Physics) { git mv "App2d/Engine/Physics/$($item.Name)" "App2d.Physics/$($item.Name)" }
foreach ($item in Get-ChildItem App2d/Engine/Rendering) { git mv "App2d/Engine/Rendering/$($item.Name)" "App2d.Rendering/$($item.Name)" }
git mv App2d/Engine/Camera2D.cs App2d.Rendering/Camera2D.cs
git mv App2d/Engine/Scene2D.cs App2d.Rendering/Scene2D.cs
git mv App2d/Engine/WorldObject2D.cs App2d.Rendering/WorldObject2D.cs
foreach ($item in Get-ChildItem App2d/Engine/Audio) { git mv "App2d/Engine/Audio/$($item.Name)" "App2d.Audio/$($item.Name)" }
foreach ($item in Get-ChildItem App2d/Gameplay | Where-Object { $_.Name -notin 'PlayerInputMapper2D.cs','XboxControllerInput2D.cs' }) { git mv "App2d/Gameplay/$($item.Name)" "App2d.Gameplay/$($item.Name)" }
git mv App2d/Engine/Game2D.cs App2d/Game2D.cs
git mv App2d/Engine/GameHost.cs App2d/GameHost.cs
git mv App2d/Engine/InputState.cs App2d/InputState.cs
git mv App2d/Engine/Diagnostics App2d/Diagnostics
```

Then verify `App2d/Engine` is empty and remove it.
- [ ] **Step 3: Rewrite the csprojs.** Every library csproj: delete `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` and every `<Compile ...>` element; keep TargetFramework, other properties, `ProjectReference`s, and `PackageReference`s (Rendering: SkiaSharp; Audio: NAudio). `App2d\App2d.csproj`: delete the entire `<ItemGroup>` of `Compile Remove`/`Compile Include` lines; keep everything else. Example result (`App2d.Core.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisModePerformance>All</AnalysisModePerformance>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Full clean build + tests.** `dotnet build App2d.slnx --nologo -v q` then `dotnet test App2d.Tests --nologo -v q --no-build`. Expected: 0 warnings, 30/30.
- [ ] **Step 5: Update README** — replace `Engine/Mathematics`→`App2d.Core/Mathematics`, `Engine/Collision/*`→`App2d.Collision/*`, `Engine/Physics/*`→`App2d.Physics/*`, `Engine/Tiles`→`App2d.Tiles`, `Engine/Geometry`→`App2d.Core/Geometry`, `Engine/Animation`→`App2d.Core/Animation`, `Gameplay/CharacterMotor2D`→`App2d.Gameplay/CharacterMotor2D` (read the current README section by section; several sentences describe the link layout — rewrite them to say each project physically owns its sources).
- [ ] **Step 6: Commit** — `build: move sources into their owning projects; drop link-based compilation`

---

### Task 2: Interval1D + lerp touch-up

**Files:**
- Create: `App2d.Core\Geometry\Interval1D.cs`
- Modify: `App2d.Collision\Contacts\ShapeCollision2D.Polygons.cs` (ProjectPolygon/TryGetPolygonMtv/TryGetPolygonCapsuleMtv/TryUpdateMtv), `App2d.Collision\Contacts\ShapeCollision2D.Utilities.cs` (ProjectCapsule), `App2d.Collision\Contacts\ShapeCollision2D.Capsules.cs` (CapsuleVsCapsule loop)
- Modify: `App2d.Gameplay\SideScrollerCamera2D.cs:140,146`
- Test: `App2d.Tests\Geometry\Interval1DTests.cs` (create)

**Interfaces:**
- Produces: `readonly record struct Interval1D(float Min, float Max)` in `App2d.Engine.Geometry` with `static Interval1D ProjectPolygon(ReadOnlySpan<Vector2> vertices, Vector2 axis)` and `static Interval1D ProjectCapsule(Vector2 start, Vector2 end, float radius, Vector2 axis)`.
- `TryUpdateMtv` signature becomes `static bool TryUpdateMtv(Vector2 axis, Interval1D first, Interval1D second, ref Vector2 bestNormal, ref float bestDepth)`.

- [ ] **Step 1: Failing tests**

```csharp
using System.Numerics;
using App2d.Engine.Geometry;

namespace App2d.Tests.Geometry;

public sealed class Interval1DTests
{
    [Fact]
    public void ProjectsPolygonOntoAxis()
    {
        ReadOnlySpan<Vector2> square = [new(0f, 0f), new(2f, 0f), new(2f, 2f), new(0f, 2f)];
        var interval = Interval1D.ProjectPolygon(square, Vector2.UnitX);
        Assert.Equal(0f, interval.Min, 3);
        Assert.Equal(2f, interval.Max, 3);
    }

    [Fact]
    public void ProjectsCapsuleOntoAxisIncludingRadius()
    {
        var interval = Interval1D.ProjectCapsule(new Vector2(-1f, 0f), new Vector2(3f, 0f), 0.5f, Vector2.UnitX);
        Assert.Equal(-1.5f, interval.Min, 3);
        Assert.Equal(3.5f, interval.Max, 3);
    }
}
```

- [ ] **Step 2: Run — expect compile failure.**
- [ ] **Step 3: Implement**

```csharp
using System.Numerics;

namespace App2d.Engine.Geometry;

/// <summary>A [Min, Max] projection of a shape onto an axis.</summary>
public readonly record struct Interval1D(float Min, float Max)
{
    public static Interval1D ProjectPolygon(ReadOnlySpan<Vector2> vertices, Vector2 axis)
    {
        var min = Vector2.Dot(vertices[0], axis);
        var max = min;
        foreach (var vertex in vertices[1..])
        {
            var projection = Vector2.Dot(vertex, axis);
            min = Math.Min(min, projection);
            max = Math.Max(max, projection);
        }

        return new Interval1D(min, max);
    }

    public static Interval1D ProjectCapsule(Vector2 start, Vector2 end, float radius, Vector2 axis)
    {
        var startProjection = Vector2.Dot(start, axis);
        var endProjection = Vector2.Dot(end, axis);
        return new Interval1D(Math.Min(startProjection, endProjection) - radius, Math.Max(startProjection, endProjection) + radius);
    }
}
```

- [ ] **Step 4: Refactor SAT call sites.** Delete `ProjectPolygon` (Polygons.cs) and `ProjectCapsule` (Utilities.cs); `TryUpdateMtv` takes `(Vector2 axis, Interval1D first, Interval1D second, ref Vector2 bestNormal, ref float bestDepth)` computing `pushPositive = second.Max - first.Min` / `pushNegative = first.Max - second.Min`; the three MTV loops build intervals via `Interval1D.ProjectPolygon(...)`/`Interval1D.ProjectCapsule(...)`. In `SideScrollerCamera2D`, line ~140 `normalRate + (maximumRate - normalRate) * urgency` → `float.Lerp(normalRate, maximumRate, urgency)`; line ~146 `current + (target - current) * blend` → `float.Lerp(current, target, blend)`.
- [ ] **Step 5: Build + all tests green (32).**
- [ ] **Step 6: Commit** — `refactor: Interval1D axis projections; BCL lerps in camera`

---

### Task 3: Area on shapes

**Files:**
- Modify: `App2d.Core\Geometry\IShape2D.cs`, `Circle2D.cs`, `Capsule2D.cs`, `Rectangle2D.cs`, `ConvexPolygon2D.cs`, `HalfSpace2D.cs`, `PolygonGeometry2D.cs`
- Test: `App2d.Tests\Geometry\ShapeAreaTests.cs` (create)

**Interfaces:**
- Produces: `float Area { get; }` on `IShape2D`; `static float PolygonGeometry2D.Area(ReadOnlySpan<Vector2> vertices)` = `MathF.Abs(SignedAreaTwice(vertices)) / 2f`.

- [ ] **Step 1: Failing tests**

```csharp
using System.Numerics;
using App2d.Engine.Geometry;

namespace App2d.Tests.Geometry;

public sealed class ShapeAreaTests
{
    [Fact]
    public void CircleAreaIsPiRSquared() => Assert.Equal(MathF.PI * 4f, new Circle2D(2f).Area, 3);

    [Fact]
    public void RectangleAreaIsWidthTimesHeight() => Assert.Equal(12f, Rectangle2D.FromSize(new Vector2(4f, 3f)).Area, 3);

    [Fact]
    public void CapsuleAreaIsRectanglePlusEndCircle() => Assert.Equal(2f * 1f * 4f + MathF.PI * 1f, new Capsule2D(new Vector2(0f, 0f), new Vector2(4f, 0f), 1f).Area, 3);

    [Fact]
    public void PolygonAreaMatchesShoelace() => Assert.Equal(4f, new ConvexPolygon2D([new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 2f), new Vector2(0f, 2f)]).Area, 3);

    [Fact]
    public void HalfSpaceAreaIsInfinite() => Assert.Equal(float.PositiveInfinity, new HalfSpace2D(Vector2.UnitY, 0f).Area);
}
```

- [ ] **Step 2: Run — expect compile failure.**
- [ ] **Step 3: Implement.** `IShape2D` gains `float Area { get; }`. Circle: `MathF.PI * Radius * Radius`. Rectangle: `(Max.X - Min.X) * (Max.Y - Min.Y)` (computed in constructor or as expression body). Capsule: `2f * Radius * Vector2.Distance(Start, End) + MathF.PI * Radius * Radius`. ConvexPolygon: `PolygonGeometry2D.Area(_vertices)` cached in constructor. HalfSpace: `float.PositiveInfinity`. Add `PolygonGeometry2D.Area` as specified.
- [ ] **Step 4: Build + all tests green (37).**
- [ ] **Step 5: Commit** — `feat: Area on all shapes`

---

### Task 4: CompositeShape2D + dispatch + ray

**Files:**
- Create: `App2d.Core\Geometry\CompositeShape2D.cs`
- Modify: `App2d.Collision\Contacts\ShapeCollision2D.cs` (dispatch row), create `App2d.Collision\Contacts\ShapeCollision2D.Composites.cs`
- Modify: `App2d.Collision\Queries\RayIntersection2D.cs` (`TryIntersectLocal`)
- Test: `App2d.Tests\Collision\CompositeShapeTests.cs` (create)

**Interfaces:**
- Produces: `sealed class CompositeShape2D : IShape2D` (namespace `App2d.Engine.Geometry`) with ctor `CompositeShape2D(IEnumerable<IConvexShape2D> parts)` (throws if empty), `ReadOnlySpan<IConvexShape2D> Parts`, `Bounds2D LocalBounds` (union), `bool ContainsPoint` (any part), `float Area` (sum of parts).

- [ ] **Step 1: Failing tests**

```csharp
using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Collision.Queries;
using App2d.Engine.Geometry;

namespace App2d.Tests.Collision;

public sealed class CompositeShapeTests
{
    private static readonly ShapeCollisionContactProvider2D Provider = new();

    // An L: vertical bar plus horizontal foot. The notch is the empty upper-right.
    private static CompositeShape2D LShape() => new([
        new Rectangle2D(new Vector2(-2f, -2f), new Vector2(0f, 2f)),
        new Rectangle2D(new Vector2(0f, -2f), new Vector2(2f, 0f))]);

    private static SpatialObject2D At(IShape2D shape, Vector2 position)
    {
        var worldObject = new SpatialObject2D(shape);
        worldObject.Transform.Position = position;
        return worldObject;
    }

    [Fact]
    public void NotchOfAnLIsEmpty()
    {
        // A small circle sitting fully inside the L's notch must NOT collide —
        // a convex-hull treatment would wrongly report contact here.
        var l = At(LShape(), Vector2.Zero);
        var circle = At(new Circle2D(0.5f), new Vector2(1.2f, 1.2f));

        Assert.False(Provider.TryGetContact(circle, l, out _));
    }

    [Fact]
    public void FootOfTheLCollides()
    {
        var l = At(LShape(), Vector2.Zero);
        var circle = At(new Circle2D(0.5f), new Vector2(1.2f, -0.3f));

        Assert.True(Provider.TryGetContact(circle, l, out var contact));
        Assert.True(contact.PenetrationDepth > 0f);
    }

    [Fact]
    public void CompositeVsCompositeCollides()
    {
        var first = At(LShape(), Vector2.Zero);
        var second = At(LShape(), new Vector2(3.5f, -1f));

        Assert.True(Provider.TryGetContact(first, second, out var contact));
        Assert.True(contact.PenetrationDepth > 0f);
    }

    [Fact]
    public void RaycastHitsTheNearestPart()
    {
        var dumbbell = At(new CompositeShape2D([
            new Circle2D(1f, new Vector2(-3f, 0f)),
            new Circle2D(1f, new Vector2(3f, 0f))]), new Vector2(10f, 0f));

        var found = new[] { dumbbell }.Raycast(new Ray2D(Vector2.Zero, Vector2.UnitX), 20f, out var hit);

        Assert.True(found);
        Assert.Equal(6f, hit.Distance, 3); // nearest sphere surface at x = 10 - 3 - 1
    }

    [Fact]
    public void LocalBoundsIsTheUnionOfParts()
    {
        var bounds = LShape().LocalBounds;
        Assert.Equal(new Vector2(-2f, -2f), bounds.Min);
        Assert.Equal(new Vector2(2f, 2f), bounds.Max);
    }

    [Fact]
    public void AreaSumsParts() => Assert.Equal(8f + 4f, LShape().Area, 3);
}
```

- [ ] **Step 2: Run — expect compile failure.**
- [ ] **Step 3: Implement `CompositeShape2D`**

```csharp
using System.Numerics;

namespace App2d.Engine.Geometry;

/// <summary>
/// A non-convex shape assembled from convex parts positioned in this shape's
/// local space. Collision resolves per part, never against the convex hull.
/// </summary>
public sealed class CompositeShape2D : IShape2D
{
    private readonly IConvexShape2D[] _parts;

    public CompositeShape2D(IEnumerable<IConvexShape2D> parts)
    {
        _parts = [.. ArgGuard.RequireNotNull(parts)];
        ArgGuard.ThrowIfTooShort((ReadOnlySpan<IConvexShape2D>)_parts, 1, nameof(parts));

        var bounds = _parts[0].LocalBounds;
        var area = _parts[0].Area;
        foreach (var part in _parts.AsSpan(1))
        {
            bounds = new Bounds2D(Vector2.Min(bounds.Min, part.LocalBounds.Min), Vector2.Max(bounds.Max, part.LocalBounds.Max));
            area += part.Area;
        }

        LocalBounds = bounds;
        Area = area;
    }

    public ReadOnlySpan<IConvexShape2D> Parts => _parts;
    public Bounds2D LocalBounds { get; }

    /// <summary>Overlapping parts double-count; treat as an upper bound.</summary>
    public float Area { get; }

    public bool ContainsPoint(Vector2 localPoint)
    {
        foreach (var part in _parts)
        {
            if (part.ContainsPoint(localPoint))
                return true;
        }

        return false;
    }
}
```

(If `ArgGuard.ThrowIfTooShort` does not accept that span type directly, use `StateGuard`-style guard or `ArgGuard.ThrowInvalid` when `_parts.Length == 0` — match whichever overload exists.)
- [ ] **Step 4: Dispatch row.** In `ShapeCollision2D.Dispatch`, add as the FIRST arm: `CompositeShape2D composite => CompositeAgainst(composite, firstPose, second, secondPose),`. New partial `ShapeCollision2D.Composites.cs`:

```csharp
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult CompositeAgainst(CompositeShape2D composite, Similarity2D compositePose, IShape2D other, Similarity2D otherPose)
    {
        var best = CollisionResult.None;
        foreach (var part in composite.Parts)
        {
            var result = Dispatch(part, compositePose, other, otherPose);
            if (!result.HasContact)
                result = Dispatch(other, otherPose, part, compositePose).Flipped();
            if (result.HasContact && (!best.HasContact || result.Contact.PenetrationDepth > best.Contact.PenetrationDepth))
                best = result;
        }

        return best;
    }
}
```

(Composite-vs-composite terminates: the reverse `Dispatch` sends the other composite through `CompositeAgainst` against a single convex part.)
- [ ] **Step 5: Ray case.** In `RayIntersection2D.TryIntersectLocal`, add before `default`:

```csharp
case CompositeShape2D composite:
{
    hit = default;
    var found = false;
    foreach (var part in composite.Parts)
    {
        if (TryIntersectLocal(origin, direction, part, maxDistance, out var partHit) && (!found || partHit.Distance < hit.Distance))
        {
            hit = partHit;
            found = true;
        }
    }

    return found;
}
```

- [ ] **Step 6: Build + all tests green (43).**
- [ ] **Step 7: Commit** — `feat: CompositeShape2D resolved per part through dispatch and rays`

---

### Task 5: Renderer composite support

**Files:**
- Modify: `App2d.Rendering\Renderer2D.cs` — the switch in `Draw(WorldObject2D)`, the switch in `DrawShapeOutline`, and the switch in private `DrawShape`.

**Interfaces:** none new — a `WorldObject2D` holding a `CompositeShape2D` renders; the debug collision overlay (`draw_collision_shapes`) draws composites.

- [ ] **Step 1: Extract the `Draw(WorldObject2D)` shape switch into a private helper** `DrawFilledShape(IShape2D shape, SKPaint paint, Matrix3x2 objectToDevice)` (same cases, `NotSupportedException` default kept), called from `Draw`.
- [ ] **Step 2: Add composite cases** to all three switches, each recursing per part, e.g. in `DrawShape`:

```csharp
case CompositeShape2D composite:
    foreach (var part in composite.Parts)
        DrawShape(part, paint, objectToDevice, drawHalfSpaceFill);
    break;
```

and equivalent recursion in `DrawFilledShape` and `DrawShapeOutline`'s switch.
- [ ] **Step 3: Build + tests green** (rendering has no unit tests; compile is the gate).
- [ ] **Step 4: Commit** — `feat: renderer draws composite shapes and their debug overlays`

---

### Task 6: Rotational contacts + friction

**Files:**
- Modify: `App2d.Physics\PhysicsBody2D.cs` (add `FreezeRotation`, `Friction`, `EffectiveInverseInertia`)
- Modify: `App2d.Physics\Solvers\ImpulseVelocitySolver2D.cs` (rewrite)
- Test: `App2d.Tests\Physics\ImpulseVelocitySolver2DTests.cs` (create)

**Interfaces:**
- Produces on `PhysicsBody2D`: `bool FreezeRotation { get; set; } = true;`; `float Friction { get; set; }` (default 0, guard `ArgGuard.ThrowIfNegativeOrNotFinite`); `float EffectiveInverseInertia => MotionType == BodyMotionType2D.Dynamic && !FreezeRotation ? 1f / MomentOfInertia : 0f;`.

- [ ] **Step 1: Failing tests** (bodies are constructed through `PhysicsWorld2D.AddBody`; contacts through `PhysicsContact2D`):

```csharp
using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision.Contacts;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Physics.Solvers;

namespace App2d.Tests.Physics;

public sealed class ImpulseVelocitySolver2DTests
{
    private static PhysicsBody2D DynamicBody(PhysicsWorld2D world, Vector2 position)
    {
        var worldObject = new SpatialObject2D(Rectangle2D.FromSize(new Vector2(2f, 2f)));
        worldObject.Transform.Position = position;
        return world.AddBody(worldObject, BodyMotionType2D.Dynamic);
    }

    private static PhysicsBody2D StaticGround(PhysicsWorld2D world)
    {
        var worldObject = new SpatialObject2D(Rectangle2D.FromSize(new Vector2(100f, 2f), new Vector2(0f, -1f)));
        return world.AddBody(worldObject, BodyMotionType2D.Static);
    }

    [Fact]
    public void OffCenterContactSpinsAnUnfrozenBody()
    {
        var world = new PhysicsWorld2D();
        var body = DynamicBody(world, new Vector2(0f, 1f));
        body.FreezeRotation = false;
        body.MomentOfInertia = 1f;
        body.Restitution = 0f;
        body.LinearVelocity = new Vector2(0f, -5f);
        var ground = StaticGround(world);

        // Contact at the body's right corner, normal up.
        var contact = new PhysicsContact2D(body, ground, new CollisionContact2D(new Vector2(1f, 0f), Vector2.UnitY, 0.1f));
        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.True(body.LinearVelocity.Y > -5f); // normal impulse applied
        Assert.NotEqual(0f, body.AngularVelocity); // and it spun
    }

    [Fact]
    public void FrozenBodyGetsNoSpinAndFullLinearImpulse()
    {
        var world = new PhysicsWorld2D();
        var body = DynamicBody(world, new Vector2(0f, 1f));
        body.Restitution = 0f;
        body.LinearVelocity = new Vector2(0f, -5f);
        var ground = StaticGround(world);

        var contact = new PhysicsContact2D(body, ground, new CollisionContact2D(new Vector2(1f, 0f), Vector2.UnitY, 0.1f));
        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.Equal(0f, body.AngularVelocity);
        Assert.Equal(0f, body.LinearVelocity.Y, 3); // exactly killed, matching today's behavior
    }

    [Fact]
    public void FrictionRemovesTangentialSpeedUpToTheCoulombLimit()
    {
        var world = new PhysicsWorld2D();
        var body = DynamicBody(world, new Vector2(0f, 1f));
        body.Friction = 1f;
        body.Restitution = 0f;
        body.LinearVelocity = new Vector2(3f, -5f);
        var ground = StaticGround(world);
        ground.Friction = 1f;

        var contact = new PhysicsContact2D(body, ground, new CollisionContact2D(new Vector2(0f, 0f), Vector2.UnitY, 0.1f));
        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.True(MathF.Abs(body.LinearVelocity.X) < 3f); // tangential speed reduced
    }

    [Fact]
    public void ZeroFrictionLeavesTangentialSpeedAlone()
    {
        var world = new PhysicsWorld2D();
        var body = DynamicBody(world, new Vector2(0f, 1f));
        body.Restitution = 0f;
        body.LinearVelocity = new Vector2(3f, -5f);
        var ground = StaticGround(world);

        var contact = new PhysicsContact2D(body, ground, new CollisionContact2D(new Vector2(0f, 0f), Vector2.UnitY, 0.1f));
        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.Equal(3f, body.LinearVelocity.X, 3);
    }
}
```

(Check `PhysicsContact2D`'s actual constructor shape — it's a 3-line record; adapt construction if the parameter order differs.)
- [ ] **Step 2: Run — expect failures** (properties missing / no spin).
- [ ] **Step 3: Implement.** `PhysicsBody2D` additions as in Interfaces. Solver rewrite:

```csharp
using System.Numerics;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Physics.Solvers;

public sealed class ImpulseVelocitySolver2D : IPhysicsVelocitySolver2D
{
    public void Solve(PhysicsContact2D contact)
    {
        var first = contact.First;
        var second = contact.Second;
        var firstInverseMass = first.InverseMass;
        var secondInverseMass = second.InverseMass;
        var firstInverseInertia = first.EffectiveInverseInertia;
        var secondInverseInertia = second.EffectiveInverseInertia;
        if (firstInverseMass + secondInverseMass + firstInverseInertia + secondInverseInertia <= 0f)
            return;

        var normal = contact.Geometry.Normal;
        // Centers of mass approximated by the transform positions.
        var firstArm = contact.Geometry.Point - first.WorldObject.Transform.Position;
        var secondArm = contact.Geometry.Point - second.WorldObject.Transform.Position;

        var relativeVelocity = PointVelocity(first, firstArm) - PointVelocity(second, secondArm);
        var normalSpeed = Vector2.Dot(relativeVelocity, normal);
        if (normalSpeed >= 0f)
            return;

        var firstArmCrossNormal = firstArm.Cross(normal);
        var secondArmCrossNormal = secondArm.Cross(normal);
        var normalEffectiveMass = firstInverseMass + secondInverseMass +
            firstInverseInertia * firstArmCrossNormal * firstArmCrossNormal +
            secondInverseInertia * secondArmCrossNormal * secondArmCrossNormal;
        if (normalEffectiveMass <= 0f)
            return;

        var restitution = Math.Min(first.Restitution, second.Restitution);
        var normalImpulse = -(1f + restitution) * normalSpeed / normalEffectiveMass;
        Apply(first, second, firstArm, secondArm, normal * normalImpulse);

        var friction = MathF.Sqrt(first.Friction * second.Friction);
        if (friction <= 0f)
            return;

        var tangent = normal.PerpCcw();
        relativeVelocity = PointVelocity(first, firstArm) - PointVelocity(second, secondArm);
        var tangentSpeed = Vector2.Dot(relativeVelocity, tangent);
        var firstArmCrossTangent = firstArm.Cross(tangent);
        var secondArmCrossTangent = secondArm.Cross(tangent);
        var tangentEffectiveMass = firstInverseMass + secondInverseMass +
            firstInverseInertia * firstArmCrossTangent * firstArmCrossTangent +
            secondInverseInertia * secondArmCrossTangent * secondArmCrossTangent;
        if (tangentEffectiveMass <= 0f)
            return;

        var tangentImpulse = Math.Clamp(-tangentSpeed / tangentEffectiveMass, -friction * normalImpulse, friction * normalImpulse);
        Apply(first, second, firstArm, secondArm, tangent * tangentImpulse);
    }

    private static Vector2 PointVelocity(PhysicsBody2D body, Vector2 arm) =>
        body.LinearVelocity + body.AngularVelocity * arm.PerpCcw();

    private static void Apply(PhysicsBody2D first, PhysicsBody2D second, Vector2 firstArm, Vector2 secondArm, Vector2 impulse)
    {
        first.LinearVelocity += impulse * first.InverseMass;
        first.AngularVelocity += first.EffectiveInverseInertia * firstArm.Cross(impulse);
        second.LinearVelocity -= impulse * second.InverseMass;
        second.AngularVelocity -= second.EffectiveInverseInertia * secondArm.Cross(impulse);
    }
}
```

(`Cross`/`PerpCcw` come from `App2d.Engine.Mathematics.Vector2Extensions`; `ω × r` in 2D = `ω * PerpCcw(r)`.)
- [ ] **Step 4: Run — all tests green (47).** The frozen-body test proves parity with the old solver (`FreezeRotation=true`, `Friction=0` reduces to exactly the old math).
- [ ] **Step 5: Run the game briefly** (player movement must feel unchanged — player/enemies are frozen, friction 0).
- [ ] **Step 6: Commit** — `feat: rotational contact impulses with Coulomb friction behind opt-in body flags`

---

### Task 7: TumbleProp2D in the level

**Files:**
- Create: `App2d.Gameplay\TumbleProp2D.cs`
- Modify: `App2d.Gameplay\SideScrollerEncounterSpawner2D.cs` (spawn one prop near player spawn)
- Test: manual — run the game, whack it.

**Interfaces:**
- Consumes: `CompositeShape2D`, `SolidColorShader` (App2d.Engine.Rendering), `WorldObject2D`, `PhysicsWorld2D.AddBody`, `EnemySystem2D.Register`, `IEnemyActor2D`/`IEnemyCombatant2D`.

- [ ] **Step 1: Implement `TumbleProp2D`**

```csharp
using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using SkiaSharp;

namespace App2d.Gameplay;

/// <summary>
/// An indestructible composite dumbbell that tumbles when hit. The visual
/// object IS the physics collider, so nothing needs syncing.
/// </summary>
internal sealed class TumbleProp2D : IEnemyActor2D, IEnemyCombatant2D
{
    private const float HitAngularKick = 4.5f;

    private readonly Dictionary<object, int> _lastAttackIds = new(ReferenceEqualityComparer.Instance);

    public TumbleProp2D(Scene2D scene, PhysicsWorld2D physics, Vector2 position, uint worldLayer, uint enemyLayer)
    {
        var shape = new CompositeShape2D([
            Rectangle2D.FromSize(new Vector2(72f, 18f)),
            new Circle2D(16f, new Vector2(-42f, 0f)),
            new Circle2D(16f, new Vector2(42f, 0f))]);
        var visual = new WorldObject2D(shape, new SolidColorShader(new SKColor(0xFF, 0x9A, 0x3B))) { ZIndex = 1 };
        visual.Transform.Position = position;
        scene.Add(visual);
        WorldObject = visual;

        Body = physics.AddBody(visual, BodyMotionType2D.Dynamic);
        Body.UserData = this;
        Body.Mass = 2f;
        Body.MomentOfInertia = 1800f;
        Body.FreezeRotation = false;
        Body.Friction = 0.4f;
        Body.Restitution = 0.15f;
        Body.CollisionLayer = enemyLayer;
        Body.CollisionMask = worldLayer;
        Health = new Health2D(1_000_000);
    }

    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; }
    public bool IsAlive => true;
    public IEnemyCombatant2D Combatant => this;

    public void SetSimulationEnabled(bool isEnabled)
    {
        Body.IsCollider = isEnabled;
        Body.MotionType = isEnabled ? BodyMotionType2D.Dynamic : BodyMotionType2D.Static;
        if (!isEnabled)
        {
            Body.LinearVelocity = Vector2.Zero;
            Body.AngularVelocity = 0f;
        }
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
    }

    public void SyncAfterPhysics()
    {
    }

    public bool TryRegisterHit(object attackSource, int attackId)
    {
        ArgGuard.ThrowIfNull(attackSource);
        if (_lastAttackIds.TryGetValue(attackSource, out var lastAttackId) && lastAttackId == attackId)
            return false;

        _lastAttackIds[attackSource] = attackId;
        return true;
    }

    public bool TakeDamage(int damage, Vector2 knockback)
    {
        Body.LinearVelocity += knockback * 0.8f;
        Body.AngularVelocity += -MathF.Sign(knockback.X) * HitAngularKick;
        return true;
    }
}
```

(Adjust to the actual `Scene2D.Add`, `Health2D` ctor, and `WorldObject2D` ctor signatures — all verified to exist; `WorldObject2D(shape, shader)`.)
- [ ] **Step 2: Spawn it.** In `SideScrollerEncounterSpawner2D.Create`, after the enemy loop and before `enemies.UpdateStreaming(...)`:

```csharp
if (TryFindGroundPlacement(30, out var propPlacement))
{
    var prop = new TumbleProp2D(scene, physics, new Vector2(TileCenterX(propPlacement.TileX), tileMap.Origin.Y + (propPlacement.SurfaceTileY + 1) * tileSize + 40f), worldLayer, enemyLayer);
    Register(prop);
}
```

(Tile x≈30 is just right of the player spawn at tile 4, well before the first enemy at 76.)
- [ ] **Step 3: Build + tests green; run the game.** Walk right from spawn, find the orange dumbbell, sword it (`J`/left click). Expected: it launches, spins, lands, tumbles, settles. `draw_collision_shapes = true` in the console shows its composite collider.
- [ ] **Step 4: Commit** — `feat: tumbling composite prop near spawn`

---

## Self-review notes

- Spec coverage: Part 1→Task 1; Part 2→Tasks 2–3; Part 3→Tasks 4–5; Part 4→Task 6; Part 5→Task 7. Half-space convexity is a documented non-change. ✓
- Type consistency: `Interval1D`, `CompositeShape2D.Parts` (`ReadOnlySpan<IConvexShape2D>`), `EffectiveInverseInertia`, `FreezeRotation`, `Friction` used consistently. ✓
- Guard-overload uncertainties are flagged inline where construction signatures must be checked against the moved files.
