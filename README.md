# App2d

A deliberately small SkiaSharp 2D engine skeleton with compile-time module boundaries.

The solution is split into `App2d.Core`, `App2d.Collision`, `App2d.Physics`,
`App2d.Rendering`, `App2d.Audio`, and `App2d.Gameplay`, plus the Windows executable
composition root. Core, collision, physics, and rendering target plain `net10.0`;
platform hosting, gameplay input, and audio remain in the Windows-targeted projects.

The engine is grouped by responsibility:

- `Engine/Mathematics` contains transforms and reusable numeric helpers. The namespace
  uses `Mathematics` rather than `Math` so it never shadows `System.Math`.
- `Engine/Rendering` contains the renderer and shader abstractions.
- `App2d.Collision` presents geometry, tile collision maps, and render-agnostic
  collidables under `Shapes`; broad/narrow phase work under the top-level `BroadPhase`,
  `Contacts`, `Filtering`, and `Intersections` folders; and ray primitives and shape
  queries under `Raycasts`.
- `Engine/Collision/BroadPhase` contains generic AABB candidate-pair searches, while
  `Engine/Collision/Filtering` contains their generic filtering contract.
- `Engine/Collision/Contacts` contains overlap/contact generation. Shape-pair code is
  split into circle, capsule, polygon/SAT, and utility partials behind one dispatcher.
- `Engine/Collision/Queries` contains rays, exact shape intersections, and generic
  spatial-object raycasts. Physics-world extensions live in `Engine/Physics/Queries`,
  preventing the collision module from depending back on physics. Query hits retain
  direct references to the object or body that was hit.
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
- `ConvexPolygon2D` accepts and validates any ordered convex vertex loop.
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

Every shape implements local-space point containment. `SpatialObject2D` owns only a
shape and transform and applies the inverse transform for point queries. Physics bodies
reference this render-agnostic type. `WorldObject2D` adds shader, visibility, and draw
order only when the spatial object is renderable.

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
using App2d.Engine.Physics.Queries;

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

## First run

The repository keeps source models, render plans, licenses, and small hand-selected
assets in Git. Character animation frames, equipment layers, previews, and sparse atlas
packages are generated locally and ignored so ordinary commits stay small.

From a clean clone, create a local virtual environment, install the two Python
dependencies, and build the runtime art:

```powershell
python -m venv .codex-art-venv
.\.codex-art-venv\Scripts\python -m pip install -r tools/ArtPipeline/requirements.txt
.\.codex-art-venv\Scripts\python tools/ArtPipeline/build_runtime_assets.py
```

The virtual environment is ignored by Git. Reinstall its packages at any time with
the same `python -m pip install -r tools/ArtPipeline/requirements.txt` command, using
the virtual environment's Python executable as shown above.

The full build validates every right-hand weapon, plans the configured motion profile,
and writes the shared sparse library to
`Assets/Content/sparse/player-one-handed-v1`. That output contains one character atlas
set plus equipment-only atlas sets; it does not generate per-weapon copies of the
character. During pipeline development, `--skip-sparse` produces the complete playable
full-canvas fallback without motion planning or the final atlas-packing pass.

Generated files remain below `Assets/Content` because the game ships that tree, but the
generated subdirectories are excluded by `.gitignore`. See
`tools/ArtPipeline/README.md` for individual render, validation, and proof commands.
Tests that inspect the sparse production library are included automatically when their
ignored proof and runtime fixtures are present; the remaining engine tests run on a
clean clone without generated art. To repack only the sparse library from existing
master renders, follow the staged planning commands in
`tools/ArtPipeline/README.md`.

## Run

```powershell
dotnet run --project App2d
```

Startup currently runs `SideScrollerGame`. It is the composition root and explicit
fixed-step scheduler; concrete gameplay behavior is grouped under `Gameplay` instead of
being implemented by the game class. `SideScrollerLevel2D` owns the seeded world
definition and composes dedicated chunk streaming, terrain visual, and encounter
spawning services. `PlayerCharacter2D`, `PlayerArsenal2D`, and `PlayerPresentation2D`
own the player's simulation, registered weapons, and visuals respectively.
`CombatSystem2D` resolves attacks through generic source/sequence hit registration, so
new weapons do not add weapon-specific bookkeeping to enemies. Camera/parallax, input
mapping, and traversal diagnostics have similarly narrow owners.

The 640x96-tile level provides merged tilemap collision, explicit one-way strips, fall
respawning, a goal flag, smooth bounded camera follow, and procedural parallax depths.
Its terrain, bounded pits, vertical-region skylines, overlapping climb spines, and side
ledges all derive from one coordinate seed. A small separately seeded population near
spawn exists only as a mechanics playground; it is not the eventual authored encounter
format.

Procedural cells are `Empty`, `Solid`, or `OneWay`; collision generation never infers
behavior from a rectangle's dimensions. Solid cells merge in both axes and use the full
terrain treatment. One-way cells merge only into horizontal strips, collide only from
above, and render with dedicated standalone, left-end, middle, and right-end art without
solid fill, making their behavior visible before the player commits to a jump.

Solid tilemaps expose a four-bit `TileSurface2D` topology (`Top`, `Right`, `Bottom`,
and `Left`) plus four outer and four diagonal-aware inner `TileCorner2D` cases. A
surface bit is present only where a solid cell borders an empty cell. The side-scroller
temporarily splits its width into equal contiguous preview regions for an arbitrary
list of tilesets. Its first half uses the Rust Cyberpunk textures and its second half uses the collision-test
tileset: dark fill, bright cyan walkable tops, blue walls, violet undersides, white
outer joins, pink inner joins, and an amber one-way collision line. Merged visual fills
are split at tileset boundaries while their collision remains merged. Chunk boundaries
do not create false edges, and additional tilesets can use the same topology without
changing authored or procedural map data. Tileset resolution is injected by tile
coordinate and can later read stable, authored tileset IDs from the saved map; the
equal-width rule is not part of the renderer. The active-window streamer consumes an
`IChunkedTileMap2D` data view; its current implementation generates data, while a later
implementation can load saved chunks through filesystem and cache layers. Seeded solid slabs, notched blocks, hollow frames, and stair-step
formations exercise these combinations above the guaranteed ground route; climb spines,
side ledges, and balcony formations are the map generator's one-way strips.
Player traversal has
acceleration, coyote time, jump buffering, variable jump height, a sword, and a fixed
pool of 16 fireballs.

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
held-jump arc used at runtime. The F3 overlay draws the full-speed and standstill arcs
with their tile-relative measurements. This keeps authored
distances tied to the movement implementation instead of duplicated design notes.

`JumpableWorldGenerator2D` consumes that same contract. Tower tiers use the reliable
four-tile rise, which leaves the three-tile standing passage between full-height ledges;
their first tier is anchored to nearby terrain instead of an absolute row. Pit width is
derived from measured running-jump distance with two tiles reserved for takeoff and
landing error. Floating formations likewise use standing clearance, while grounded
stairs and staggered balcony sequences add denser local silhouettes without claiming to
be authored levels.

## Controls

Xbox controller:

- Left stick or D-pad: run
- Left stick down or D-pad Down: crouch; press A while held to drop through a one-way platform
- A: jump; release early to shorten the jump
- Right stick: aim
- X: the same chop action as H
- Y, left bumper, or right trigger: the same equipped-weapon attack as J
- B: the same stab action as L
- Right bumper: switch the equipped weapon

Keyboard and mouse fallback:

- A / D or Left / Right: run
- W, Up, or Space: jump; press again in the air to double jump
- S or Down: duck; movement is slower while ducking
- S or Down + jump: drop through the supporting one-way strip
- Release jump early: shorten the jump
- Q: cycle the equipped weapon
- J or left click: use the equipped weapon
- H: preview the equipped weapon's one-handed chop animation (visual only)
- L: preview the equipped weapon's one-handed stab animation (visual only)
- F3: toggle traversal arcs and movement metrics
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

## Assets and textures

Repository assets are separated by lifecycle under the top-level `Assets` directory.
Only `Assets/Content` ships; the project copies that tree beside the executable as
`Assets`. `Assets/Library` holds useful alternatives, `Assets/Sources` retains original
inputs and licenses, and ignored `Assets/Work` holds regenerable pipeline output.

Runtime paths use lowercase semantic IDs. Every `Game2D` owns a `TextureCache2D`
rooted at the deployed `Assets` directory, so textures load only when requested:

```csharp
var fireball = Textures.Load("effects/fireball/ember-energy.png");
var fireballShader = new TextureShader2D(fireball, new Vector2(96f, 96f));
```

Repeated loads of the same path return the cached `Texture2D`. `Textures.Unload(path)`
releases one decoded bitmap, `Textures.Clear()` releases all currently loaded bitmaps,
and `GameHost.Dispose()` disposes the game's cache automatically. A texture shader
borrows its texture, so stop using shaders that reference an asset before unloading it.

`TextureShader2D` uses Skia's image shader with configurable X/Y tile modes, filtering,
and world-unit tile size. Ordinary textures require no custom shader compilation;
`SKRuntimeEffect` is only needed for future custom SkSL effects. The side-scroller's
terrain maps topology roles to the conventional files in its selected tileset, while
pooled fireballs use `effects/fireball/ember-energy.png`.

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

Clips may use a uniform frame rate or an explicit duration for every sample, and may
loop or stop on their last frame. The sparse pipeline treats the 30 FPS renders as
high-fidelity source material and selects nonuniform points by accumulated screen-space
motion, while stored source times and sample durations preserve the authored pose timing
and playback length. Motion measurement, quality/memory profile planning, and atlas
materialization are separate stages, so profile and byte-budget changes reuse immutable
master renders and cached numerical analysis. Players support
pause, resume, stop, restart, and playback-speed changes. `SpriteShader2D` maps one complete texture onto
finite object bounds, corrects image orientation for the engine's Y-up world, and can
flip sprites horizontally or vertically.

Equipped rendering prefers the independent layer library at
`Assets/Content/sparse/player-one-handed-v1/library.json`. The character atlas is stored
once; each equipment package contains weapon pixels only. Character and weapon layers
select their own nonuniform samples against the same clip clock, then the existing GPU
depth shader combines them without creating a full-canvas intermediate texture. Atlas
pages load lazily, and changing equipment releases the previous equipment package while
keeping the shared character pages resident. Combinations that add another equipment
layer, such as a shield, continue through the compatible full-canvas fallback.

Each character owns a `character.json` and semantic folders such as
`characters/player/animations/walk`. The animation ID and folder name are identical;
source names such as `Walking_B` remain provenance rather than runtime vocabulary.
Frames use contiguous four-digit names beginning at `frame-0001.png`. Manifests record
frame rate or total duration plus looping behavior without repeating asset paths.

Sword and magic-shot one-shots take priority over locomotion and remain synchronized to
their gameplay timing. The character artwork is rendered by a separate visual object
that follows the smaller physics collider, keeping transparent frame padding out of
collision calculations.
