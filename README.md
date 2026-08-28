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
- `Engine/Collision/Queries` contains rays, exact shape intersections, and scene/physics
  raycasts. Query hits retain direct references to the object or body that was hit.
- `Engine/Physics/Integration`, `Engine/Physics/Filtering`, and `Engine/Physics/Solvers`
  contain physics-specific policies; bodies, contacts, constraints, and the world remain
  at the physics root.

`ArgGuard` centralizes null, null-or-whitespace, and range validation for integers,
finite scalars, and `Vector2` values. It uses caller expressions for parameter names and
preserves rejected range values in `ArgumentOutOfRangeException.ActualValue`; successful
checks do not allocate. Specialized guards cover positive, non-negative, bounded, and
non-zero vector inputs, plus minimum lengths for span-backed collections. Unbounded ray
distances deliberately have a separate guard that permits positive infinity while
rejecting negative values and NaN.

`StateGuard` performs the corresponding checks for invalid engine state while preserving
`InvalidOperationException` semantics. Physics iteration settings, collapsed transforms,
and renderer lifecycle checks therefore do not masquerade as caller argument failures.

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

`Engine/Tiles/TileMap2D` stores compact authored maps. `ProceduralTileMap2D` addresses
larger seeded worlds without allocating their full tile area: it evaluates stateless
X/Y cells on demand, greedily merges one 32x32 chunk into AABB colliders, and permits
old chunks to be discarded and regenerated exactly. The side-scroller keeps at most 15
nearby chunks live, so scene rendering, weapon queries, and the physics broad phase are
bounded by the local neighborhood rather than total world size.

Non-convex geometry can become another `IShape2D` implementation later, with its own
triangulation/rendering path, without weakening the convex polygon guarantees.

Every shape implements local-space point containment. `WorldObject2D.ContainsWorldPoint`
applies the inverse object transform before running that query.

Finite convex shapes also implement `IConvexShape2D.GetSupportPoint`. The generic
`Collision/Contacts/HalfSpaceCollision2D` query uses that support mapping to return a world-space contact
normal, penetration depth, and minimum translation vector for any convex shape against
a transformed half-space. `ConstrainOutside` applies the MTV to the object's position.
Raycasts remain separate because query hits describe a point, surface normal, and travel
distance, while solver contacts describe penetration between two shapes.

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

`GameHost` feeds gameplay exact 1/120-second updates through an accumulator. The
physics world's matching maximum substep therefore becomes one physics step per
gameplay update in normal operation, while rendering remains independent. Transient
input is consumed after the first fixed update so a catch-up frame cannot repeat a
button press or mouse-wheel action.

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

## Ray queries

`Ray2D` stores a normalized world-space direction, so every hit distance is measured in
world units. `RayIntersection2D` intersects transformed circles/ellipses, rectangles,
convex polygons, capsules, and half-spaces exactly. Rays that start inside a finite shape
return its exit surface, and non-uniform scale and rotation preserve the correct world
normal.

The scene and physics extensions return the nearest hit without allocating:

```csharp
var ray = Ray2D.FromPoints(origin, mouseWorld);

if (physics.Raycast(ray, 800f, out var hit, layerMask: WorldLayer))
{
    PhysicsBody2D body = hit.Item;
    Vector2 point = hit.Point;
    Vector2 normal = hit.Normal;
    float distance = hit.Distance;
}
```

`RaycastAll` writes nearest-first hits into a caller-provided span. If the span is too
small, it retains the nearest hits. Because each generic hit contains a managed object
reference, back that span with an array or pooled array rather than `stackalloc`.
Physics queries skip disabled colliders and accept a layer mask, sensor toggle, and an
optional predicate. Scene queries return `WorldObject2D` references; physics queries
return `PhysicsBody2D` references.

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

Startup currently runs `SideScrollerGame`. It is the composition root and explicit
fixed-step scheduler; concrete gameplay behavior is grouped under `Gameplay` instead of
being implemented by the game class. `SideScrollerLevel2D` owns the seeded procedural
world, streamed platforms, and goal. `PlayerCharacter2D`, `PlayerArsenal2D`, and
`PlayerPresentation2D` own the player's simulation, attacks, and visuals respectively.
`CombatSystem2D` resolves attacks through generic source/sequence hit registration, so
new weapons do not add weapon-specific bookkeeping to enemies. Camera/parallax, input
mapping, and traversal diagnostics have similarly narrow owners.

The 10,000x96-tile level provides merged tilemap collision, one-way platforms, fall
respawning, a goal flag, smooth bounded camera follow, and procedural parallax depths.
Its terrain, bounded pits, vertical-region skylines, overlapping climb spines, and side
ledges all derive from one coordinate seed. A small separately seeded population near
spawn exists only as a mechanics playground; it is not the eventual authored encounter
format.

Solid tilemaps expose a four-bit `TileSurface2D` topology (`Top`, `Right`, `Bottom`,
and `Left`) plus four outer and four diagonal-aware inner `TileCorner2D` cases. A
surface bit is present only where a solid cell borders an empty cell. The side-scroller
currently renders this as a generated collision-line tileset: dark fill, bright cyan
walkable tops, blue walls, violet undersides, white outer joins, and pink inner joins.
No texture atlas is required, chunk boundaries do not create false edges, and later
visual tiles can select art from the same topology without changing authored or
procedural map data. Seeded solid slabs, notched blocks, hollow frames, and stair-step
formations exercise these combinations above the guaranteed ground route.
Player traversal has
acceleration, coyote time, jump buffering, variable jump height, a sword, a ceiling
grapple, a ball and chain, and a fixed pool of 16 fireballs. `DemoGame` remains the
shape/physics stress scene.

Short sound effects are decoded once at startup and played through one polyphonic mixer.
Gameplay emits semantic cues through `ISoundEffectSink2D`, so movement, combat, and weapon
code never knows asset paths or audio-device details. `SoundEffectBank2D` owns the cue-to-file
mapping, per-cue levels, and non-repeating variants. Set `sfx_volume` in the developer
console to a value from 0 through 1 to adjust the master sound-effect level.

Player movement is owned by `Gameplay/CharacterMotor2D`. `PlayerIntent2D` describes
what the player requested; the motor turns that into desired velocity and grace-window
state; `PhysicsWorld2D` decides what the level and active constraints permit. The motor
then consumes new landing contacts, allowing a buffered jump to fire on the fixed step
that establishes ground support. It also provides a two-world-unit ground skin,
four-unit landing snap, eight-unit upward corner correction, held-jump apex gravity,
and a terminal fall speed without weakening collision tolerances globally.

Side-scroller dimensions use an eight-world-unit design increment. A terrain tile is
32 units (four increments), while the player collider is 56 by 88 units (seven by
eleven increments). The character is 2.75 tiles tall: two-tile openings are blocked,
three-tile passages leave one eight-unit increment of clearance, four-tile rises are
reliable, and five-tile rises exceed the normal held jump. The padded sprite canvas
renders at 184 by 138 world units so the artwork follows the larger body scale. Spawn
height, presentation size, and collision size all come from `TraversalMetrics2D`
rather than repeating unrelated literals.

`TraversalMetrics2D` is the single source for locomotion tuning and simulates the same
held-jump arc used at runtime. The F3 overlay draws the full-speed and standstill arcs,
the truthful grapple radius, and their tile-relative measurements. This keeps authored
distances tied to the movement implementation instead of duplicated design notes.

`JumpableWorldGenerator2D` consumes that same contract. Tower tiers use the reliable
four-tile rise, which leaves the three-tile standing passage between full-height ledges;
their first tier is anchored to nearby terrain instead of an absolute row. Pit width is
derived from measured running-jump distance with two tiles reserved for takeoff and
landing error. Floating formations likewise use standing clearance, while grounded
stairs and staggered balcony sequences add denser local silhouettes without claiming to
be authored levels.

The grapple counts its initial head offset as part of its advertised reach. Extension
uses a swept-circle-versus-AABB query, including a small aim-assist radius, so a slow
render frame cannot skip a platform. A grace-range latch retains its real rope length
rather than silently snapping the player inward. Once latched, the arm reels toward a
safe minimum length while preserving lateral swing momentum; pressing the grapple again
releases that momentum.

## Controls

Xbox controller:

- Left stick or D-pad: run
- A: jump; release early to shorten the jump
- Right stick: aim
- Left / right trigger: use the weapon in that side's HUD slot
- Left / right bumper: change the weapon in that side's HUD slot

Keyboard and mouse fallback:

- A / D or Left / Right: run
- W, Up, or Space: jump
- Release jump early: shorten the jump
- Ctrl + left click: change the weapon assigned to the left mouse button
- Ctrl + right click: change the weapon assigned to the right mouse button
- J or left click: use the left-slot weapon; aimed weapons point toward the mouse
- K or right click: use the right-slot weapon; aimed weapons point toward the mouse
- While latched: click the button assigned to the Bionic Arm to release with swing momentum
- F3: toggle traversal arcs, grapple reach, and movement metrics
- Backtick (`): open or close the developer console
- Escape: close

## Developer console

Press backtick (`) to open the in-game developer console. Gameplay keeps running, but
gameplay input is suppressed while the console has focus. Variables can be read by name
and changed with either whitespace or an equals sign:

```text
draw_fps
draw_fps true
draw_collision_shapes = true
draw_graphics = false
```

`list` shows registered variables, `help` shows syntax, `toggle <name>` flips a boolean,
and `clear` clears the output. Tab completes names and Up/Down navigate command history.
Games can expose additional runtime values with
`DeveloperConsole.RegisterVariable(name, getter, setter, description)` and can register
their `PhysicsWorld2D` for collider visualization with `RegisterDebugPhysicsWorld`.
Physics collision shapes are drawn as translucent green overlays above the game
graphics, while active combat hitboxes use a red-orange overlay. Set
`draw_graphics = false` (or `toggle draw_graphics`) to keep the simulation running and
play using only collision geometry; the collision overlay and FPS display remain available.

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

Scene objects also expose a general-purpose `ZIndex`. Lower values render first, and
objects with equal values keep their scene insertion order. Changing a z-index at
runtime invalidates the scene's cached draw order. The side-scroller places the player
at z-index 1 so streamed terrain and one-way platforms at the default 0 stay behind the
character.

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
`SKRuntimeEffect` is only needed for future custom SkSL effects. The side-scroller's
terrain currently uses generated topology visuals rather than a texture; pooled
fireballs use `ember-energy.png` while other scenes retain solid-color and gradient
shader examples.

## Frame animation

`Engine/Animation` contains a generic, update-driven frame animation layer. A clip can
hold textures, shaders, numeric values, or any other frame state, so the timing code is
not tied to the player or even to rendering:

```csharp
var clip = new AnimationClip2D<Texture2D>(frames, framesPerSecond: 10f);
var animation = new AnimationPlayer2D<Texture2D>();
animation.Play(clip);

// Update loop:
animation.Update(time.DeltaSeconds);
spriteShader.Texture = animation.CurrentFrame;
```

Clips may loop or stop on their last frame. Players support pause, resume, stop,
restart, and playback-speed changes. `SpriteShader2D` maps one complete texture onto
finite object bounds, corrects image orientation for the engine's Y-up world, and can
flip sprites horizontally or vertically.

The side-scroller loops `Player/A1/idle-01.png` through `idle-06.png` while standing and
`walk-01.png` through `walk-06.png` while moving. Leaving the ground plays the eight-frame
`jump` clip once and holds its final frame until landing. Sword and shotgun one-shots take
priority over locomotion: the sword clip remains synchronized to its hitbox, while the
0.40-second shotgun clip spawns its projectile at 0.20 seconds before returning to the
appropriate idle, walk, or jump state. Gameplay timing is expressed in seconds rather
than frame indices, so changing the clip's frame count does not change firing behavior.
The knight artwork is rendered by a separate visual object that follows the smaller
physics collider, keeping transparent frame padding out of collision calculations.
