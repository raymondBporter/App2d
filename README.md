# App2d

A deliberately small SkiaSharp 2D engine skeleton.

The engine is grouped by responsibility:

- `Engine/Mathematics` contains transforms and reusable numeric helpers. The namespace
  uses `Mathematics` rather than `Math` so it never shadows `System.Math`.
- `Engine/Rendering` contains the renderer and shader abstractions.
- `Engine/Collision/BroadPhase` contains generic AABB candidate-pair searches, while
  `Engine/Collision/Filtering` contains their generic filtering contract.
- `Engine/Collision/Contacts` contains overlap/contact generation. Shape-pair code is
  split into circle, capsule, polygon/SAT, and utility partials behind one dispatcher.
- `Engine/Physics/Integration`, `Engine/Physics/Filtering`, and `Engine/Physics/Solvers`
  contain physics-specific policies; bodies, contacts, constraints, and the world remain
  at the physics root.

When ray and segment queries arrive, `Engine/Collision/Intersections` can be a sibling
of `Contacts`. That keeps hit queries (point, distance, normal) separate from solver
contacts (normal and penetration) while still allowing both to reuse the math and
closest-point helpers.

Geometry lives under `Engine/Geometry`:

- `IShape2D` is the common local-space shape contract.
- `ConvexPolygon2D` accepts any ordered convex vertex loop, validates it, and also
  provides regular-polygon and triangle factories.
- `Circle2D` is a real circle primitive. Non-uniform object scale can render it as a
  rotated ellipse without polygonizing it.
- `Capsule2D` is a local line segment swept by a radius.
- `Rectangle2D` is a local rectangle that may be oriented by its world transform.
- `AxisAlignedRectangle2D` preserves explicit AABB intent for tile, slab, and broad-phase
  code. It shares rectangle geometry today and remains world-axis-aligned while its owner
  keeps transform rotation at zero.
- `HalfSpace2D` represents an infinite solid side of a line. Its normal points out of
  the solid region and into permitted space.
- `Bounds2D` supplies local bounds to rendering and shaders.

`Engine/Tiles/TileMap2D` stores a compact solid-cell map and greedily merges neighboring
tiles into larger AABB colliders. Gameplay can therefore remain tile-authored while the
physics broad phase sees only a small set of rectangles. The current side-scroller draws
those merged rectangles with a repeating procedural stripe shader, so it gets visible
tile seams without one render object per tile.

Non-convex geometry can become another `IShape2D` implementation later, with its own
triangulation/rendering path, without weakening the convex polygon guarantees.

Every shape implements local-space point containment. `WorldObject2D.ContainsWorldPoint`
applies the inverse object transform before running that query.

Finite convex shapes also implement `IConvexShape2D.GetSupportPoint`. The generic
`Collision/Contacts/HalfSpaceCollision2D` query uses that support mapping to return a world-space contact
normal, penetration depth, and minimum translation vector for any convex shape against
a transformed half-space. `ConstrainOutside` applies the MTV to the object's position.
Ray and segment casts are intentionally left for the separate intersection API.

`ShapeCollision2D` uses nested type switches: one selects the first-shape row and the
second selects its implemented pair. Unknown pairs return no contact, and reverse-order
fallback makes each implemented pair callable in either argument order. Collision-query
temporaries for current shapes use stack-backed spans, so pair dispatch, rectangle
vertices, capsule feature axes, and the ellipse approximation do not allocate per query.
The circle row covers circles, convex polygons, rectangles, capsules, and half-spaces.
Contacts consistently describe how to push the first object out of the second, so the
same normal drives positional correction and reflected velocity.

The capsule row covers circles, capsules, and rectangles. Segment-to-segment closest
points handle parallel and zero-length spines without unstable division. Separated spines
use the exact closest-point normal; crossing or coincident spines use capsule feature axes
to select a stable MTV. The rectangle row covers circles, capsules, and rectangles using
transformed feature axes, so rotated object transforms also work. Non-uniform capsule
scale remains intentionally unimplemented because it produces a swept ellipse rather
than a true capsule.

The preserved collision stress demo (`DemoGame`) uses four half-spaces as an enclosure and simulates 12 deterministic circles,
12 moving/rotating capsules, and 6 moving axis-aligned rectangles. They resolve against
the walls, implemented demo obstacles, and each other. True circle-circle contact uses
exact math; a non-uniformly transformed circle (an ellipse) uses a 40-edge convex
collision boundary for now.

## Physics step

`PhysicsWorld2D.Step(dt)` owns the simulation sequence:

```text
substep integration
    -> broad-phase candidates + pair filtering
    -> narrow-phase contact generation
    -> iterative contact + constraint position projections
    -> contact response + constraint velocity projections
```

The defaults are semi-implicit Euler integration, shape-based contact generation,
mass-weighted MTV correction, and a linear impulse solver. None of those policies are
hardwired: replace `Integrator`, `PairFilter`, `BroadPhase`, `ContactProvider`,
`PositionSolver`, or `VelocitySolver` to use controller physics, position-derived
velocity, springs, pixel masks, or a wholly non-Newtonian model. `Constraints` run their
position projection during each `PositionIterations` pass, followed by their velocity
projection for `VelocityIterations` passes.

`DistanceConstraint2D` holds direct references to two bodies and locally projects them
back to a configurable `RestLength`, weighted by inverse mass. Its velocity projection
removes relative speed along the rope axis. `PositionStrength` and `VelocityStrength`
can soften either projection; the default value of one performs the full projection.
The world alternates forward and reverse constraint sweeps so tension can travel through
a chain in a few Gauss-Seidel passes rather than dozens.

The default `SweepAndPruneBroadPhase2D<T>` computes each body's transformed bounds,
sorts their X intervals by `Left`, stops scanning when the next `Left` exceeds the
current `Right`, and prunes candidates whose `Bottom`/`Top` intervals do not overlap.
It can sweep Y instead through its `Axis` property. Half-spaces retain unbounded bounds,
so they remain candidates for finite bodies. The generic broad-phase API only needs an
item-to-bounds function and an `IPairFilter2D<T>`; it has no dependency on physics.
Candidate pairs hold direct object references, while sweep ordinals remain a private
sorting detail rather than coupling consumers to positions in the source list.
`BruteForceAabbBroadPhase2D<T>` remains available as a simple reference/fallback.

`DefaultPhysicsPairFilter2D` applies collider enablement, collision layers/masks, and
motion-type policy. Its defaults reject static-static, static-kinematic, and
kinematic-kinematic pairs, so at least one body must currently be dynamic. The six
pairing switches can opt specific combinations back in without changing the broad phase.

Bodies support static, kinematic, and dynamic motion; linear/angular velocity; force,
torque, and impulses; mass and inertia; gravity scaling; restitution; sensors; and
collision layers/masks. Static bodies can opt into `IsOneWayPlatform`; only top-facing
contacts from bodies that were previously above and are now moving downward are kept.
Accumulated forces clear after a public step. The latest contacts remain available
through `LastContacts`, `IsTouching(body)`, and directional `IsTouching(body, direction)`.

A simple platform controller can therefore remain gameplay code:

```csharp
player.LinearVelocity = new Vector2(inputX * runSpeed, player.LinearVelocity.Y);

if (jumpPressed && physics.IsTouching(player, Vector2.UnitY))
    player.AddImpulse(Vector2.UnitY * jumpImpulse);

physics.Step(dt);
```

For pixel collision, implement `IPhysicsContactProvider2D` and either replace the shape
provider or combine providers with `CompositeContactProvider2D`. The physics world only
consumes contacts, so it does not care how they were produced.

## Run

```powershell
dotnet run --project App2d
```

Startup currently runs `SideScrollerGame`. It provides merged tilemap collision, a
rectangle player controller with acceleration, coyote time, jump buffering, variable
jump height, fall respawning, smooth bounded camera follow, a goal flag, visible-world
render culling, and two procedural parallax depths. Its gameplay layer adds reusable
health, patrol enemies with hit-stun, a timed capsule sword hitbox, a seven-segment
distance-constrained bionic arm, player damage and knockback, plus a fixed pool of 16
circle fireballs. The rope begins at the real player body. When its descending tip
contacts a platform's upward-facing top, only the tip is frozen; the existing link
constraints shorten and transmit tension back to the player for pulling and swinging.
Thin elevated tile rectangles are one-way, so the player can travel upward through them
and land while descending. `BionicArm2D.LatchedPullSpeed` and `ReleaseUpwardImpulse` are
the two main traversal tuning values. Releasing disconnects the physical constraints
immediately so they do not consume the launch impulse; the visible rope then retracts
kinematically. Player/enemy interaction is handled as gameplay overlap while both still
collide with the static world through physics layers. `DemoGame` remains in the project
as the shape/physics stress scene.

## Controls

- A / D or Left / Right: run
- W, Up, or Space: jump
- Release jump early: shorten the jump
- Ctrl + mouse wheel: switch between Sword and Bionic Arm
- J or left click: use the active weapon; the arm aims toward the mouse
- While latched: the rope pulls inward; J or left click releases with an upward impulse
- K or right click: shoot a fireball
- Escape: close

## Frame and coordinate flow

Each UI-timer tick calls `Update(FrameTime, InputState)` and then synchronously repaints,
which calls `Render(Renderer2D)`.

Rendering follows this transform chain:

```text
geometry vertices (object space)
    -> Transform2D.LocalToWorldMatrix
world space (Y points up)
    -> Camera2D.WorldToDeviceMatrix
Skia device space (origin top-left, Y points down)
```

Mouse events arrive in WinForms client coordinates. `InputState` first accounts for the
client-to-Skia device scale, then `Camera2D.DeviceToWorld()` applies the inverse camera
matrix. This is why the click marker remains correct after resize and zoom.

`IShader2D` currently means a Skia paint shader. The gradient implementation creates an
`SKShader`; it can later be joined by image, noise, or `SKRuntimeEffect`/SkSL shaders
without changing scene objects or the renderer's transform path.

## Textures

PNG assets under `Assets/Textures` are copied beside the executable. Every `Game2D`
owns a `TextureCache2D` rooted at that directory, so textures load only when requested:

```csharp
var stone = Textures.Load("mossy-stone.png");
var stoneShader = new TextureShader2D(stone, new Vector2(512f, 512f));
```

Repeated loads of the same path return the cached `Texture2D`. `Textures.Unload(path)`
releases one decoded bitmap, `Textures.Clear()` releases all currently loaded bitmaps,
and `GameHost.Dispose()` disposes the game's cache automatically. A texture shader
borrows its texture, so stop using shaders that reference an asset before unloading it.

`TextureShader2D` uses Skia's image shader with configurable X/Y tile modes, filtering,
and world-unit tile size. Ordinary textures require no custom shader compilation;
`SKRuntimeEffect` is only needed for future custom SkSL effects. The side-scroller uses
`mossy-stone.png` on platforms and `ember-energy.png` on pooled fireballs while retaining
the existing solid-color and gradient shader examples elsewhere.
