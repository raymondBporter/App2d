# App2d

A small MonoGame/XNA 2D engine and the side-scroller built on it.

The solution contains the engine, games, editors, supporting tools, and tests.
`App2d.Core` is the engine: mathematics, geometry,
collision, physics, tiles, rendering, audio, authored characters, and the WinForms game
host. `App2d` is the game executable: contracts, gameplay simulation, presentation,
level storage, the editor, and the in-game debug tools. `App2d.Tests` holds every test.
`App2d.CharacterStudio` is the character editor and `App2d.Noodle` holds the rig
experiments; both build on `App2d.Core`. `App2d.EditorUi` supplies shared editor UI.
`App2d.CharacterSeeds` is the home for everything that is not the game or Studio: the
retained character generators and the rendering smokes and studies, run with
`dotnet run --project App2d.CharacterSeeds -- <flag>`. It references the game; the game
does not reference it. `tools/ClothLab` is a small cape study.
Everything targets `net10.0-windows10.0.19041.0`.

`App2d.ImageLab` is a small raster workbench: load an image, preview parameterized
transforms, apply them to an undo/redo stack, and save the current step as PNG.
Run `dotnet run --project App2d.ImageLab`. See [Image Lab](App2d.ImageLab/README.md)
for the transforms and a few starting pipelines.

`App2d.Beamwright` is the native Windows optics puzzle game port. Run it with
`dotnet run --project App2d.Beamwright`. It reuses Core's host, input, renderer,
affine poses, filled shapes, and raycasts, and includes all 36 original campaign
levels and 14 optical parts. See [the Beamwright notes](App2d.Beamwright/README.md)
for controls, reference verification, attribution, and the remaining port scope.

`App2d.Trials` is a separate, minimal trials-game prototype. Run it with
`dotnet run --project App2d.Trials`. It uses the shared `GameHost`, `Game2D` camera,
and physics world to simulate a double-triangle bike on a cubic B-spline terrain course.
Use **A/D** or **Left/Right** to drive, **W/S** to lean, **Space** to brake,
**R** to reset, and **Esc** to close. The camera follows the terrain height; surface marks show movement.
Three repeating depth layers provide clouds, distant mountains, and nearby hills;
both games use the shared [parallax layer API](docs/rendering-passes.md#parallax-layers).
No external assets are needed. `TrialsBike2D` holds the editable tube geometry, fork,
wheel radius, motor and suspension tuning. The front triangle and rear swingarm are
separate rigid bodies. A front wheel joint allows 28 units of travel along the fork;
a rear pivot and wheel bearing keep the rear triangle connected while its shock
compresses up to 14 units. Wheels rotate freely and rear-wheel torque drives through
ground friction. The fork telescopes and the rear shock graphic follows its mounts.

`TrialsCourse2D` holds the course `Width`, `FrequencyAmplitudes` and additive `Ramps`.
Change `Width` in that file to grow the default map, or pass a different configuration to
`TrialsGame2D(course)` / `TrialsTerrain2D(world, course)`. Width is measured from `XMin`
to `XMax = XMin + Width`, including the area left of the spawn. Frequencies are cycles
per world unit (`1 / wavelength`), amplitudes are heights in world units, and ramps use
world-space X/Y points. Increasing width adds more waves without stretching their scale
or moving authored ramps. The final wave fade follows the new right endpoint.

```csharp
var course = TrialsCourse2D.Default with
{
    Width = 40_000f,
    FrequencyAmplitudes = [(1f / 2400f, 48f), (1f / 700f, 20f)],
    Ramps = [PolylineCurve2D.Builder.Create([new(2000, 0), new(2200, 120), new(2400, 0)])]
};
using var game = new TrialsGame2D(course);
```

`ControlPointSpacing`, `YMin`, `WaveStartX` and wave fade lengths are also configurable.
The control-point grid remains anchored when width changes, with an exact final endpoint
even when the width is not a multiple of the spacing. Empty wave/ramp collections are valid.
The default map keeps the three waves and two hand-shaped ramp profiles.
The clamped cubic `BSpline2D` has continuous position, first and second
derivatives at its simple interior knots; its control points influence the curve rather
than being interpolation targets. `Curve2D.ToPolyline` samples each knot span adaptively,
with a 12-unit chord limit and 0.05-unit sampled-deviation target. Visible and collidable
terrain use the same sampled boundary. `SimplePolygon2D.Builder.BelowPolyline` closes
the region down to a bottom plane and builds narrow strip triangles directly. This is a
finite default course of width 12800 from x = -1600 to 11200, with a flat starting area.

`IHeightField2D` in Core describes the solid `XMin <= x <= XMax` and
`YMin <= y <= HeightAt(x)`: a function on top, vertical end caps, and a supplied flat bottom.
`PolylineHeightField2D`, `BSplineHeightField2D`, and `FunctionHeightField2D` share this API.
The function source needs no vertices; height, slope, outward top normal and containment
queries evaluate the source directly. B-splines invert their X coordinate before evaluating
Y, and require strictly increasing control-point X coordinates. Polyline slopes use the
segment to the right at corners. Supplied bottoms must lie below the top; evaluating an
invalid height throws instead of moving the bottom or changing the function.

```csharp
using App2d.Core.Geometry.HeightFields;
using App2d.Core.Geometry.Operations.HeightFields;

IHeightField2D hills = new FunctionHeightField2D(
    xMin: -1000f, xMax: 1000f, yMin: -200f,
    height: x => 30f * MathF.Sin(x / 100f),
    slope: x => 0.3f * MathF.Cos(x / 100f),
    yMax: 30f);
var normal = hills.NormalAt(50f); // normalize((-slope, 1))
var collider = hills.ToShape(); // HeightFieldShape2D implements IShape2D.
var terrain = new TrialsTerrain2D(world, hills);
```

No minimum/maximum search is performed. `YMax` is a conservative upper bound: polylines
use their highest vertex, B-splines use their highest control point, and functions may supply
a bound or leave it at positive infinity. `Bounds()` permits infinite rectangles.
`YMin = float.NegativeInfinity` supports unbounded-downward source queries; polygon baking
then requires `ToClippedShape(finiteBottom)` (or `ToClippedPolygon`) explicitly. Sampling is an approximation;
choose a segment length that resolves a function's shortest features. Trials samples once
and shares that boundary between drawing, camera heights and existing shape collision.
The side-scroller's tile terrain remains unchanged.

`HeightFieldShape2D` attaches directly to a `SpatialObject2D`/`WorldObject2D`, with bounds,
area, mass/inertia, point distances, raycasts, drawing and JSON support. Its serialized data
is the frozen polyline top and finite `YMin`; analytic delegates remain in the source API.
Polyline sources retain their authored edges exactly; B-splines and functions are sampled
once, and every shape operation uses the same approximation.

Collision splits heightfields with a linear slope scan: nonincreasing top-edge slopes keep
one convex run; an increasing slope starts the next run at the shared vertex. Each run
closes vertically down to `YMin`. Cross products compare slopes without division, and
exactly collinear samples collapse inside the collision pieces. Their interiors are disjoint
and their union is the heightfield. This needs no general polygon decomposition or root
search. All built-in finite shapes and half-spaces use the existing convex collision path
against these pieces; circles use the complete outer perimeter to avoid internal seams.
Concave pair contacts retain the solver's per-piece behavior rather than claiming a single
global minimum escape translation. Other shapes can still encounter shared piece edges
at concave joins; this is a smaller set of edges than a triangle decomposition.

Circle-versus-simple-polygon contacts query the actual outer perimeter, including its
vertices, rather than internal triangle seams. Containment determines the outward normal;
other shapes use the existing convex-part contacts, with bounds rejection before narrow phase.
Large composites cache a local-space bounds tree so only nearby parts reach the narrow phase;
moving or rotating the body reuses the tree, and equal-depth contacts retain source-part ordering.
The spline is the authoring curve; collision uses its polyline approximation, not an analytic
spline contact. Adaptive deviation checks sample quarter/midpoints and are not certified
error bounds for arbitrary user-defined curves.

A support function on an entire nonconvex spline would make GJK see its convex hull,
bridging valleys. Minkowski support addition is valid for convex shapes:
`support(A + B, d) = support(A,d) + support(B,d)`. A disk adds `r * normalize(d)`.
`NormalOffsetCurve2D` instead evaluates `C(t) + offset(t)*normal(t)`; it can describe a
local wheel-center path for a constant radius, but endpoints and overlapping offsets
still require geometric handling. It is not a general nonconvex GJK collision shape.

Within the game project, `Contracts` owns commands, immutable observations/events, and
shared configuration; `Gameplay` owns the simulation; `Presentation` owns views, camera,
HUD, and sound selection; `Levels` owns SQLite level storage. Those are folders and
namespaces, not assemblies. Networking was considered and dropped on 2026-10-01; the
remaining frame/observation layer is described in
[the session architecture](docs/session-architecture.md).

The engine is grouped by responsibility:

- `App2d.Core/Grids` contains reusable cell coordinates, row-major indexing, world-space
  cell geometry and ranges, and dense `Grid2D<T>` storage. Tile maps, viewport terrain,
  and the collision spatial index share it. See [the grid API](App2d.Core/Grids/README.md)
  for wind-field and sparse-bucket examples and boundary rules.
- `App2d.Core/Imaging` contains plain `Image2D<T>` pixel arrays, pixel mapping and
  arithmetic, and binary skeletonization. See [the image pipeline](App2d.Core/Imaging/README.md)
  for converting decoded textures into float channels, masks, and skeletons.
- `App2d.Core/Mathematics` contains transforms and reusable numeric helpers, including
  `Similarity2D` — the validated rotation + uniform scale + mirror + translation pose
  that collision consumes. The namespace uses `Mathematics` rather than `Math` so it
  never shadows `System.Math`.
- `Frame2D` and `Frame3D` describe affine coordinate systems with explicit axes, an
  origin, composition, and point/direction transforms. Sockets return `Frame3D`;
  callers project an axis or point with `.XY` when entering 2D gameplay.
- Geometry supplies `Rect2D.FromPoints(points).InflatedBy(padding)` and shape outlines
  placed by `Similarity2D`, with span overloads for reusable buffers. Collision can
  query `(shapeA, poseA, shapeB, poseB)` directly through `ShapeCollision2D.Overlaps`
  or `TryGetContact`; no registered spatial objects are required.
- `Color4b` is the shared global alias for `Microsoft.Xna.Framework.Color`, with four
  byte channels. `ColorExtensions` parses and formats RGB/RGBA hex and converts to
  normalized `System.Numerics.Vector4` for float color math, using straight alpha.
- Appearance palettes are simple source files: [UiTheme](App2d.EditorUi/UiTheme.cs)
  for shared controls, [StudioTheme](App2d.CharacterStudio/Editor/StudioTheme.cs) for
  editor viewports and timelines, [GameUiTheme](App2d/Presentation/GameUiTheme.cs) for
  the HUD, menus and level-editor overlays, and [DebugTheme](App2d.Core/Rendering/DebugTheme.cs)
  for grids, colliders and diagnostics. Edit their named colors, rebuild and restart.
  ImGui colors use normalized float RGBA; renderer palettes use `Color4b` byte RGBA.
- `App2d.Core/Rendering` contains the renderer, shader abstractions, and the `GraphicsSurface2D` WinForms host surface.
- `App2d.Core` presents guards, mathematics, animation, geometry, collision, physics, and the
  render-agnostic `SpatialObject2D`. `App2d.Core/Tiles` presents the tile maps and mesher.
  `App2d.Core/Collision` contains collision work: `BroadPhase`, `Contacts`,
  `Filtering`, `Intersections`, and `Queries`.
- `App2d/Levels` stores authored levels as SQLite files. Tiles are run-length encoded
  per chunk so a single edit rewrites a single row; a missing chunk row means an
  entirely empty chunk. It is the only code that uses `Microsoft.Data.Sqlite`,
  and it never references simulation. It also loads authored player geometry into
  shared traversal configuration through `TraversalMetricsLoader2D`; its game
  dependency is limited to the `App2d.Contracts` namespace.
- `CollisionSystem2D` owns runtime collider registration, collision layers and masks,
  cached static/dynamic spatial indexes, candidate discovery, and exact contacts. It has
  no dependency on physics; physics and gameplay are consumers of collision data.
- `App2d.Core/Collision/BroadPhase` contains generic AABB candidate-pair searches, while
  `App2d.Core/Collision/Filtering` contains their generic filtering contract.
- `App2d.Core/Collision/Contacts` contains overlap/contact generation. Shape-pair code is
  split into circle, capsule, polygon/SAT, and utility partials behind one dispatcher.
- `App2d.Core/Collision/Queries` contains rays, exact shape intersections, and generic
  spatial-object raycasts. Physics-world extensions live in `App2d.Core/Physics/Queries`,
  keeping physics-specific queries alongside physics. Query hits retain
  direct references to the object or body that was hit.
- `App2d.Core/Physics/Integration`, `App2d.Core/Physics/Filtering`, and `App2d.Core/Physics/Solvers`
  contain physics-specific policies; bodies, contacts, constraints, and the world remain
  at the physics root.

Collision and physics namespaces follow their Core folders: `App2d.Core.Collision`
and `App2d.Core.Physics`, including their subnamespaces. Both compile into
`App2d.Core.dll`; consumers need only the Core project reference for these types.

`App2d.Core/Validation` separates reusable `NumericValidation` predicates, `ArgGuard`
checks, and shared exception helpers. Numeric guards support integer and floating-point
types plus `Vector2`, `Vector3`, and `Vector4`. Plain comparisons check their named
condition; explicit `ThrowIfNotFiniteOr...` variants also reject NaN and infinity.
Caller names and rejected values are preserved, including whole vectors, and successful
checks do not allocate. Unbounded rays retain a separate guard that permits positive
infinity. See [the validation API](App2d.Core/Validation/README.md) for naming, vector
semantics, range checks, and examples.

`StateGuard` performs the corresponding checks for invalid engine state while preserving
`InvalidOperationException` semantics. Physics iteration settings, collapsed transforms,
and renderer lifecycle checks therefore do not masquerade as caller argument failures.

Gameplay combatants now expose an `EntityId2D`, independent of their object reference.
Their bodies and colliders carry that owner ID; combat and contact damage resolve it
through the session-owned `CombatantRegistry2D`. Register actors when adding them to
the session, and unregister them when removing them permanently. Death, respawn, and
streaming enablement preserve identity. Melee action sources also have IDs so hit
deduplication uses source ID plus attack sequence rather than object identity.
Zero means no entity. Every simulation object receives its ID from the session's
`EntityIdAllocator2D`, so two sessions built from the same definition in the same order
allocate identical IDs; `EntityId2D.Create()` is a process-local convenience for tests
and diagnostics only. IDs are distinct from authored level IDs. Object storage, physics
body references, and rendering ownership are unchanged; there is no packed storage.

`SideScrollerSimulation2D.Create(SideScrollerSessionDefinition2D)` is the one recipe
that builds a session and everything it owns; the host constructs through it. The local game advances through
`SideScrollerSession2D`: ID-addressed held-state input commands enter a fixed 120 Hz
session (a player with no input this tick repeats its last command), and immutable
per-player, enemy, level-content, and world observations plus gameplay events return
to `SideScrollerClient2D`. Presses and releases are derived inside the simulation from
consecutive commands, so a lost or repeated command cannot drop or double an action.
The production level, weapons, enemy actors, authored objects, checkpoints, and
terrain colliders run without constructing graphics or playing audio. Client presentation owns
sprites, terrain, effects, HUD, and sound; the host handles persistence and editing.
Terrain observations use cached immutable chunk revisions, including neighboring
tiles for correct edge rendering. Simulation and presentation are separate folders of the game project. `CaptureSnapshot()` returns a complete frame with no events to
initialize presentation; display-time advancement runs independently of simulation ticks.
Persistent character poses and attack phases reconstruct from state, and terrain
observations can be rebuilt from values without a live map. See
[the session architecture](docs/session-architecture.md) for the frame and observation flow.

Geometry lives under `App2d.Core/Geometry`:

- `IShape2D` is the common local-space shape contract.
- `ConvexPolygon2D` accepts and validates any ordered convex vertex loop.
- `Circle2D` is a real circle primitive. Non-uniform object scale can render it as a
  rotated ellipse without polygonizing it, but collision requires uniform scale.
- `Capsule2D` is a local line segment swept by a radius.
- `Rectangle2D` is a local rectangle that may be oriented by its world transform.
- `AxisAlignedRectangle2D` preserves explicit AABB intent for tile, slab, and broad-phase
  code. It shares rectangle geometry today and remains world-axis-aligned while its owner
  keeps transform rotation at zero.
- `HalfSpace2D` represents an infinite solid side of a line. Its normal points out of
  the solid region and into permitted space.
- `Rect2D` supplies local bounds to rendering and shaders.

`App2d.Core/Tiles/TileMap2D` stores compact authored maps as a bool-only grid; it carries
no `TileKind2D`. `App2d.Core/Tiles/EditableTileMap2D` is the only `IChunkedTileMap2D`
implementation: a dense, mutable map loaded from a level file rather
than evaluated from a seed function. It still greedily merges each 32x32 chunk into
AABB colliders on demand, and raises a `ChunkChanged` event that the in-game tile editor
uses to drive streamer reloads after an edit. The side-scroller keeps at most 15
nearby simulation chunks live, bounding weapon queries and the collision spatial
index. Terrain visuals stream independently from `Camera2D.VisibleWorldBounds`,
with padding for overhanging artwork and procedural tree canopies. Zoom, resize, and editor panning
therefore load all visible terrain without expanding the physics neighborhood.

Each editable cell is one byte: four bits select one of up to 16 tilesets and four bits
hold the composable tile type. Code exposes those as separate values; the packing is an
in-memory and level-blob detail. The level metadata stores the ordered, stable tileset IDs.

Exposed plain solid tiles grow seeded grass, using an
anchored wind equation and batched geometry. Active sword strikes (including downward
attacks) cut a tile's tuft around `VegetationPlacement2D.CutHeightInTiles` (0.35 tiles),
with seeded per-blade height variation for a ragged edge and matching severed tops.
The actual severed tops, including flowers, launch upward, tumble and flutter with
the wind, and fall under gravity. They disappear on terrain contact or fade out
within one second. Cut state is limited to the nearby active simulation chunks and
forgotten when those chunks unload, so returning grass is full height. Snapshot
attachment preserves this bounded state; cuts are not written to the
level or player save. Seeded branching trees occupy
open stretches behind the terrain and actors; they are decorative and cannot be chopped.
The shared vegetation code lives in `App2d.Core/Vegetation`.
Wind deformation currently runs on the CPU and uses the existing GPU triangle batch.
The camera's terrain coverage includes extra foliage margin for overhanging canopies.

Editor mode is part of the game rather than a separate tool. `F1` freezes the simulation
and detaches the camera. A right sidebar provides tileset buttons and a visual tile-type
grid for filled, grippable, one-way, spikes, and ladders. The previews use the selected tileset's
actual terrain art. The right mouse button erases and `Ctrl+Z` undoes a brush stroke.
Painting mutates the loaded `EditableTileMap2D`,
whose `ChunkChanged` event feeds a `DirtyChunkTracker2D`; the editor flushes that tracker once
per frame so a drag rebuilds each affected chunk at most once instead of once per event. Each
stroke commits its changed chunks to the level file in a single transaction. `TileEditSession2D`
holds the strokes and undo history and lives in `App2d.Core/Tiles`, so the editable core is testable
without input or storage.

Paint a vertical column of **Ladder** tiles to make a climbable ladder of any height.
Ladders use each tileset's top and repeating rung art; tilesets without one borrow the medieval ink ladder.
Hold `W/S` or `Up/Down`, or use the controller's vertical left stick / D-pad, to
climb. Release to hang, move sideways to let go, or press `Space` / controller `A`
to jump off. Holding Up stops at the last rung. Dash also releases the ladder.
`W` and `Up` are climb controls; jumping always uses `Space` / controller `A`.
The top stops the player's body center at the last rung until they step off or descend; solids
still block climbing. Sword, pistol, and unarmed characters use their source pack's
four-frame climbing loop, paused while hanging still.
Ladders are non-solid grippable cells (`Grippable` without
`Solid`), so the existing four-bit tile kind and level format remain compatible.

The sidebar's **Things** button switches to authored map objects. A small code-owned type list
currently offers objects, the player spawn, goal, three enemy types, and tumble props. An object
definition picks one of the world's `objects/*.json` files, which carries the shape, material,
physics and path; the moving lifts are such objects. The position-only types deliberately expose
just reusable definition names plus instance name, enablement, and position; their health, AI,
art, physics, and other tuning remain in gameplay code. A native WinForms `PropertyGrid` writes
only when **Apply** is pressed, while MonoGame draws map markers, placement ghosts, selection
outlines, object outlines and their paths. Any placed thing drags to a new grid corner, and
**Undo** reverts the last thing edit. This avoids turning the renderer into a general UI
library. All definitions, instances, and transforms remain relational rows in the level
database. Object placements reload live as they are placed, dragged, edited or deleted; the
other map objects are picked up on the next run.

Non-convex geometry can become another `IShape2D` implementation later, with its own
triangulation/rendering path, without weakening the convex polygon guarantees.

`Geometry/Meshes/MeshTopology` provides allocation-free `foreach` views:
`Triangles(indices)` reads index triples, `TriangleFan(vertexCount)` supplies
first-vertex fan indices, and `Edges(vertexCount, closed)` supplies directed perimeter
or polyline edges. `TriangleMesh2D.Triangles` and `MeshTopology.IndexedTriangles<T>`
resolve index triples to vertex values. Span views borrow their buffers without
copying; do not change the buffers or resize their owning lists during enumeration.
Fewer than three fan vertices produce no faces. A fan requires the perimeter to be
visible from its first vertex; concave outlines still use `PolygonTriangulation2D`.

Every shape implements local-space point containment. `SpatialObject2D` owns only a
shape and transform and applies the inverse transform for point queries. Physics bodies
reference this render-agnostic type. `WorldObject2D` adds shader, visibility, and draw
order only when the spatial object is renderable.

Finite convex shapes also implement `IConvexShape2D.GetSupportPoint`. The half-space
row of `ShapeCollision2D` uses that support mapping for any convex shape against a
transformed half-space, reporting the deepest support point projected onto the boundary.
`Collision/Contacts/HalfSpaceCollision2D` exposes the same math as a standalone query
returning a normal, penetration depth, and minimum translation vector; `ConstrainOutside`
applies the MTV to the object's position. Raycasts remain separate because query hits
describe a point, surface normal, and travel distance, while solver contacts describe
penetration between two shapes.

`ShapeCollision2D` uses nested type switches: one selects the first-shape row and the
second selects its implemented pair. Unknown pairs return no contact, and reverse-order
fallback makes each implemented pair callable in either argument order. Collision-query
temporaries for current shapes use stack-backed spans, so pair dispatch, rectangle
vertices, and capsule feature axes do not allocate per query. The circle row covers
circles, convex polygons, rectangles, capsules, and half-spaces; the half-space row
covers every finite convex shape through support mapping. Contacts consistently
describe how to push the first object out of the second, so the same normal drives
positional correction and reflected velocity.

Narrow-phase and ray code consume `SpatialObject2D.CollisionPose`, a cached
`Similarity2D` extracted from the transform. Rotation, uniform scale, and mirroring
(the player's facing flip) are exact; a non-uniformly scaled collidable throws instead
of silently missing or approximating, while render-only objects keep the fully
flexible `Transform2D`.

The capsule row covers circles, capsules, and rectangles. Segment-to-segment closest
points handle parallel and zero-length spines without unstable division. Separated spines
use the exact closest-point normal; crossing or coincident spines use capsule feature axes
to select a stable MTV. The rectangle row covers circles, capsules, and rectangles using
transformed feature axes, so rotated object transforms also work.

## Physics step

`PhysicsWorld2D.Step(dt)` owns integration and physical response while consuming contacts
from its injected `CollisionSystem2D`:

```text
substep integration
    -> collision-system candidate + contact query
    -> OnBeforeCollision: accept or reject the detected contact
    -> iterative contact + constraint position projections
    -> contact response + constraint velocity projections
after all substeps: OnAfterCollision once per accepted body pair
```

`GameHost` feeds gameplay exact 1/120-second updates through an accumulator. The
physics world's matching maximum substep therefore becomes one physics step per
gameplay update in normal operation, while rendering remains independent. Transient
input is consumed after the first fixed update so a catch-up frame cannot repeat a
button press or mouse-wheel action.

The defaults are semi-implicit Euler integration, mass-weighted MTV correction, and a
linear impulse solver. Physics policies remain replaceable through `Integrator`,
`PairFilter`, `CollisionCallbacks`, `PositionSolver`, and `VelocitySolver`. Collision shape/contact policy is
configured independently through `CollisionSystem2D.ContactProvider`. `Constraints` run their
position projection during each `PositionIterations` pass, followed by their velocity
projection for `VelocityIterations` passes. Contact response is revisited between
velocity passes so joints and supporting contacts can settle together.

`IPhysicsConstraint2D.PrepareVelocity(dt)` runs once per substep after contact response,
before velocity iterations. Existing constraints inherit a no-op. Stateful impulse
constraints use it to prepare anchor masses and reset their per-substep impulse.

`SoftPointJoint2D` brings two body-local anchors together with a damped spring in both
axes, without restricting relative rotation or projecting positions. Offset anchors use
the bodies' centers of mass, angular velocity and inertia. Its implicit impulse solver
uses [Solver2D's soft constraint coefficients](https://box2d.org/posts/2024/02/solver2d/#soft-constraints)
with accumulated impulses so extra solver iterations do not multiply spring strength.
Tune `FrequencyHertz` for stiffness and `DampingRatio` for settling: below 1 overshoots,
1 is critical damping, and above 1 is overdamped. The underlying equation is
`x'' + 2*zeta*omega*x' + omega²*x = 0`, with `omega = 2*pi*frequency`.
Keep frequency below half the physics substep rate; integration adds some numerical damping.

Suspension uses three additional constraints:

- `RevoluteJoint2D`: firm coincident anchors, free relative rotation (swingarm pivot or wheel bearing).
- `WheelJoint2D`: firm motion perpendicular to a body-local guide axis, a spring/damper
  along that axis, optional travel limits, and free wheel rotation. The axis rotates with
  the first body; differentiating that moving direction is included in the angular lever arm.
- `SpringDamperJoint2D`: a shock between two offset mounts. It applies spring/damping
  only along their connecting line, including the mounts' angular velocities and reaction
  torques. Optional length limits prevent bottoming or extending beyond the stops.

Trials uses a 6 Hz fork, 7 Hz rear shock and damping ratios of 0.7, with twelve
position/velocity passes at 120 Hz. The shock has no separate rigid body: its two
mount points determine its length and direction. The swingarm determines the axle's arc.

For the math, let `n` be the unit suspension direction and `v` relative attachment velocity.
`n^T v = dot(n,v)` is a scalar speed. Multiplying back by `n` gives the projected vector,
so damping is `Fd = -c * n * dot(n,v) = -c * (n n^T) * v`. No division is needed for a unit vector;
for a non-unit direction `a`, the projection matrix is `(a a^T)/(a^T a)`.
Complete `n` with a perpendicular unit vector `t` to form the orthogonal basis `R = [n t]`.
Then `R * diag(c,0) * R^T = c * n n^T`: ordinary change of basis for damping along one axis.
In a single linearized suspension coordinate `q`, the oscillator is
`m_eff*q'' + c*q' + k*(q-q0) = external_force`. A whole bike also has rotating axes,
changing lever arms, gravity and contacts; we solve their coupled impulses rather than
treating world X and Y as independent oscillators. Spring frequency defines
`k = m_eff*omega²`, damping defines `c = 2*zeta*m_eff*omega`.
See [Catto's soft constraint derivation](https://box2d.org/files/ErinCatto_SoftConstraints_GDC2011.pdf)
and the [wheel joint constraint equations](https://github.com/erincatto/box2d/blob/main/src/wheel_joint.c).

`DistanceConstraint2D` holds direct references to two bodies and locally projects them
back to a configurable `RestLength`, weighted by inverse mass. Its velocity projection
removes relative speed along the rope axis. `PositionStrength` and `VelocityStrength`
can soften either projection; the default value of one performs the full projection.
The world alternates forward and reverse constraint sweeps so tension can travel through
a chain in a few Gauss-Seidel passes rather than dozens.

`CollisionSystem2D` keeps separate static and dynamic spatial hashes. Chunk terrain only
rebuilds the static index when colliders load, unload, or move; dynamic transforms dirty
only the dynamic index. Queries visit intersecting cells and use per-collider stamps to
deduplicate shapes spanning multiple cells without allocating. Half-spaces and unusually
large colliders use an overflow list. The generic sweep-and-prune and brute-force broad
phases remain available as reusable references for other workloads.

`DefaultColliderPairFilter2D` applies collider enablement and collision layers/masks.
Physics adds `DefaultPhysicsPairFilter2D` for motion-type policy. Its defaults reject static-static, static-kinematic, and
kinematic-kinematic pairs, so at least one body must currently be dynamic. The six
pairing switches can opt specific combinations back in without changing the broad phase.

`IPairFilter2D` decides eligibility before geometry exists, with the same answer in
either argument order. `RigidBody2D.CanCollideWith` checks enabled/layer-mask flags;
`PhysicsWorld2D.CanCollide` also consults both the collision-system and physics-world
pair filters. Gameplay support queries use the latter so a filtered pair cannot
still count as ground.

Set `PhysicsWorld2D.CollisionCallbacks` to an `IPhysicsCollisionCallbacks2D` policy:
`OnBeforeCollision(contact)` runs after detection and before recording or response;
return false to discard the contact. It can run repeatedly as position iterations
refresh geometry. `OnAfterCollision(contact)` runs once per accepted pair after
the complete step's response and constraints. Sensors receive notifications without
physical response. Removing bodies in the after callback is supported; recursive
stepping is rejected. Raw collision/raycast queries remain geometric queries and
do not invoke physics callbacks.

## Ray queries

`Ray2D` lives in `App2d.Core.Geometry.Shapes` alongside `Line2D`. Both implement
`IGeometry2D` and support point containment, closest points, and distances without scene
objects. `Intersection2D` provides line/line, line/ray, and ray/ray tests. For world queries,
`Ray2D` stores a normalized world-space direction, so every hit distance is measured in
world units. `RayIntersection2D` intersects transformed circles, rectangles, convex
polygons, capsules, and half-spaces exactly through the object's `CollisionPose`. Rays
that start inside a finite shape return its exit surface, and rotation, uniform scale,
and mirroring preserve the correct world normal.

The scene and physics extensions return the nearest hit without allocating:

```csharp
using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics.Queries;

var ray = Ray2D.FromPoints(origin, mouseWorld);

if (physics.Raycast(ray, 800f, out var hit, layerMask: WorldLayer))
{
    RigidBody2D body = hit.Item;
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
return `RigidBody2D` references.

Bodies support static, kinematic, and dynamic motion; linear/angular velocity; force,
torque, and impulses; mass and inertia; gravity scaling; restitution; sensors; and
collision layers/masks. Side-scroller platform behavior lives in App2d's
`PlatformCollisionRules2D`, rather than on `RigidBody2D`:

```csharp
var platforms = PlatformCollisionRules2D.Attach(physics);
platforms.Surface(platformBody).OneWaySurfaceNormal = Vector2.UnitY;
platforms.IgnorePlatform(playerBody, platformBody); // only this pair drops through
```

Attaching preserves existing pair filters and callbacks. Drop-through exclusions
run in the pair filter before narrow phase; the normal, previous-side and approach
checks run in `OnBeforeCollision`. Surface slop and wall-grip metadata also live in
the game policy. Body removal cleans its state. Trials attaches no platform policy.
Accumulated forces clear after a public step. The latest contacts remain available
through `LastContacts`, `IsTouching(body)`, and directional `IsTouching(body, direction)`.

Uniform solid mass properties are available through
`ShapeMassProperties2D.Calculate(shape, density)` in
`App2d.Core.Geometry.Operations.MassProperties`. It returns area, mass, center of
mass, and moment of inertia about that center. See the
[formulas and supported shapes](App2d.Core/Geometry/README.md#solid-mass-properties).
On a live rigid body, choose either:

```csharp
body.SetMassFromDensity(0.01f); // mass per square world unit
// Or choose total mass and derive inertia from the uniform solid shape:
body.SetMassFromShape(2f);
```

Both include the body's current uniform scale and set `LocalCenterOfMass`;
`WorldCenterOfMass` follows its transform. Recalculate after resizing. Contacts
and center-to-center distance constraints use this center, and integration rotates
around it. Linear velocity describes the center's motion. Existing explicit
`Mass`, `MomentOfInertia`, and `PhysicsBodySettings2D` values remain supported;
shape-derived values are opt-in. Trials derives the frame's mass distribution from its
solid tubes and each wheel's inertia from `I = 0.5 * mass * radius²` (mass 1.2, radius 26).

A simple platform controller can therefore remain gameplay code:

```csharp
player.LinearVelocity = new Vector2(inputX * runSpeed, player.LinearVelocity.Y);

if (jumpPressed && physics.IsTouching(player, Vector2.UnitY))
    player.AddImpulse(Vector2.UnitY * jumpImpulse);

physics.Step(dt);
```

For pixel collision, implement `ICollisionContactProvider2D` and either replace the shape
provider or combine providers with `CompositeCollisionContactProvider2D`. Physics only
consumes the resulting collision contacts.

## First run

Open `App2d.slnx` in Visual Studio, select `App2d` as the startup project, and
press Run. Runtime-ready art, sound effects, music, and gameplay settings are
committed under `Assets/Static`. Normal builds automatically copy them into the
Debug resource tree; Release builds and publishes package those committed files.
After pulling changes, build and run again. No Python, cave-pack downloads, or
asset generation is required to play or edit levels.

Asset authors can optionally run `tools/setup.ps1` to regenerate imported art and
procedural effects. It requires Python and installs Pillow. The pipeline updates
both `Assets/Runtime` and the baked outputs under `Assets/Static`; commit those
outputs together with source/script changes so everyone receives the update.
See [the authoring pipeline](tools/ArtPipeline/README.md).

## Run

```powershell
dotnet run --project App2d
```

Level files written before the current format are upgraded once with
`dotnet run --project App2d -- --migrate-level` (old moving-platform rows become `objects/*.json`
assets and object things); see [world maps](docs/world-maps.md).

Run the C# / Dear ImGui character editor with:

```powershell
dotnet run --project App2d.CharacterStudio
```

It works on the authored assets in `Assets/Characters/authored`, which the game plays: base models, variants with
build values, looks and reset-to-base overrides, shared animations that play on every build, motion sets, and
entities (controller, actions, hit windows, equipment, collision) in the **Entity** workspace. **Test** plays entities
in an arena from the current drafts. The game's player (`hero`) and every enemy placement come from these assets.

The 759 imported point-animation clips (Person, Quadruped, Blob, Flying and creature studies) are read-only sources
under **Sources**: converting one onto a model, or importing a `.puppet.json`, is an explicit step that records its
source. See [Character Studio](docs/character-studio.md) for the workflow and headless renders, and
[the replacement design](docs/character-editor-replacement.md) for the design and what remains.

Run the rigid-puppet side-scroller experiment with:

```powershell
dotnet run --project App2d.Noodle
```

It uses one fixed humanoid skeleton and rigid per-bone stand-ins for future PNG parts.
Use `A`/`D` to move, `Space` to jump, `J` to attack, `H` to preview a hit reaction,
and `V` to swap the entire part set without changing any animation. `Tab` opens the
pose authoring mode: drag hand/foot IK handles, rotate the torso/head with `Q`/`E`
and `Z`/`X`, capture keyframes with `K`, preview with `P`, and save the pose list with
`Ctrl+S`. `B` overlays the standard skeleton.

Run the general entity/bones editor with:

```powershell
dotnet run --project App2d.Noodle -- --bones
```

The right sidebar authors a parent/child bone hierarchy and attached rectangle, circle,
capsule, or convex-polygon shapes. The property grid edits transforms, dimensions, color,
and visual/collision purpose. Middle-drag pans, the mouse wheel zooms, and `Escape` closes.

The earlier weighted noodle-person and joint-gallery experiment remains available with:

```powershell
dotnet run --project App2d.Noodle -- --prototype
```

Its joint gallery visualizes distance, axis/prismatic, and polar-link projections backed
by the same pure constraint-coordinate math used by physics. Editor projectors remain
separate from body-bound physics constraints.

Startup currently runs `SideScrollerGame`. It is the composition root and explicit
fixed-step scheduler; concrete gameplay behavior is grouped under `Gameplay` instead of
being implemented by the game class. `LevelBootstrap2D` loads the cavern level file
into an `EditableTileMap2D`, and `SideScrollerLevel2D` consumes that loaded tile map,
composing dedicated chunk streaming, terrain visual, and encounter spawning services.
`Person2D`, `PersonArsenal2D`, and `PersonPresentation2D` own reusable humanoid
simulation, registered actions, and visuals respectively. Human input and rival AI both
produce `PersonCommand2D`; neither is built into the person. `CombatSystem2D` resolves
attacks against faction-aware `ICombatant2D` targets, so the same sword and gun work in
both directions without making every enemy a person. Camera/parallax, input
mapping, and traversal diagnostics have similarly narrow owners.

The 640x96-tile level provides merged tilemap collision, explicit one-way strips, fall
respawning, an authorable goal flag, smooth bounded camera follow, and procedural parallax
depths. Authored objects provide the moving lifts: kinematic one-way rectangles defined in
`objects/*.json` that ping-pong along a line with smooth turnarounds. Physics carries supported
dynamic bodies along those surfaces. The same kinematic motion driver accepts any `ICurve2D` path;
see [Kinematics](App2d/Gameplay/World/KINEMATICS.md).
Its terrain, bounded pits, vertical-region skylines, overlapping climb spines, and side
ledges are authored in `Assets/Static/levels/cavern/level.db`, the committed level file
`LevelBootstrap2D` loads at startup. That load opens the file read-only, so ordinary
play never dirties the committed level; only an editor session opens it read-write to persist
changes. The committed thing layer may be empty: a temporary code fallback keeps the player
spawned so the level can still open in the editor, and no implicit goal or encounter population
is persisted.

Tile cells use composable `Solid`, `OneWay`, and `Grippable` flags; collision
generation never infers behavior from a rectangle's dimensions. Solid cells merge in
both axes, while one-way cells merge only into horizontal strips and collide only from
above. Grippable solids remain distinct collision runs, carry their flag into physics,
and currently render as bright green temporary tiles so their behavior is obvious.

Solid tilemaps expose a four-bit `TileSurface2D` topology (`Top`, `Right`, `Bottom`,
and `Left`) plus four outer and four diagonal-aware inner `TileCorner2D` cases. A
surface bit is present only where a solid cell borders an empty cell. Each cell's authored
tileset selects the art for its fill and exposed topology. Merged visual fills split at
tileset boundaries while their collision remains merged; chunk boundaries do not create
false edges. The active-window streamer consumes an `IChunkedTileMap2D` data
view; `EditableTileMap2D` is its current implementation, loaded from a level file rather
than generated live. The baked level's solid slabs, notched blocks, hollow frames, and
stair-step formations exercise these combinations above the guaranteed ground route; its
climb spines, side ledges, and balcony formations are the world generator's one-way
strips.
Player traversal has
acceleration, coyote time, jump buffering, variable jump height, wall grip and wall jump,
a high-speed enemy-phasing dash with one airborne charge restored on landing, a sword,
a short charged shot, and held healing powered by a shared energy reserve. Weapon switching has been removed. A hostile unarmed rival
uses the same person locomotion, punch, kick, damage, and presentation-state paths under a small AI command producer;
the patrol enemies and Boiler Brute remain bespoke actors. While airborne and falling, holding toward a
nearby static solid wall suspends the player; jumping launches away. One-way platforms
do not allow wall grip.

Short sound effects are decoded once at startup and played through one polyphonic mixer.
The sound-effect machinery lives in `App2d.Core/Audio` and is generic over the game's cue
enum: gameplay emits semantic cues through `ISoundEffectSink2D<SoundEffect2D>`, so movement,
combat, and weapon code never knows asset paths or audio-device details. `SoundEffectBank2D`
loads a cue table of `SoundCue2D` values (stems, level, pitch jitter, falloff) and plays
non-repeating variants with subtle per-play volume and pitch variation. The game's table is
`SoundEffectCues2D` next to the `SoundEffect2D` enum. `SpatialSoundEffectSink2D` wraps the
bank with the player's live position as its listener and a per-cue falloff lookup.
World effects use `PlayAt(effect, position)`; controllable effects use `BeginAt` and can
move their source with `SetPosition`. The game updates active voices each frame, multiplying
their requested volume by smooth distance falloff. `SoundFalloff2D` sets the tuning:
ordinary effects stay at full volume within 96 world units (3 tiles) and fade to silence
at 800 (25 tiles); the hammer impact cue carries 128 to 1200 units, and the gun charge cue
holds its pitch. Out-of-range one-shots are skipped. Player movement/feedback and global
cues use `Play`/`Begin` at their normal levels.
The debug panel (F5) has an SFX volume slider from 0 through 1 for the master
sound-effect level.

Music plays automatically from four synchronized Ogg stems on a separate streaming
bus. Rectangular world zones choose the piece and mood, with a default outside zones
and smooth transitions. Music volume, piece and mood overrides, a now-playing readout
and a zone outline toggle are in the debug panel (F5). Geometry is authored in
`Assets/Static/levels/cavern/zones.json`; music assignments live in
`Assets/Static/audio/music/soundtrack.json`. See [music and zones](docs/music-and-zones.md).

Person movement is owned by `App2d/Gameplay/Persons/PersonLocomotion2D`. `PersonMovementIntent2D` describes
what a human or AI controller requested; locomotion turns that into desired velocity and grace-window
state; `PhysicsWorld2D` decides what the level and active constraints permit. The motor
then consumes new landing contacts, allowing a buffered jump to fire on the fixed step
that establishes ground support. It also provides a two-world-unit ground skin,
four-unit landing snap, eight-unit upward corner correction, held-jump apex gravity,
and a terminal fall speed without weakening collision tolerances globally.

Side-scroller dimensions use an eight-world-unit design increment. A terrain tile is
32 units (four increments). The RGS player keeps its square 512-pixel canvas aspect
ratio and renders at 138 by 138 world units. The art pipeline measures the idle feet,
head inset and authored foot anchor, then writes the visual and collider
geometry to `Assets/Runtime/characters/player-geometry.json`. Runtime loads that manifest
rather than duplicating sprite measurements in gameplay code. The collider offset
mirrors with the player's facing.

`TraversalMetrics2D` is the single source for locomotion tuning and simulates the same
held-jump arc used at runtime. The F3 overlay draws the full-speed and standstill arcs
with their tile-relative measurements. This keeps authored
distances tied to the movement implementation instead of duplicated design notes.

The cavern is now entirely durable authored content in its level database; startup never
regenerates missing terrain. Traversal measurements remain useful while editing jumps and
clearances, but there is no parallel procedural source of truth to drift away from the map.

## Controls

Player spell behavior, tunable values and the visual preview command are documented in
[Player spells](docs/player-spells.md).

Xbox controller:

- Left stick or D-pad: run
- Hold toward a solid wall while falling: wall grip; press A to jump away
- Hold Down on the left stick or D-pad and press A: drop through a one-way platform
- A: jump; release early to shorten the jump
- Right trigger: dash
- X: sword attack
- Sword: hold Down and press X while airborne for a downward cut
- Y: hold briefly to charge and fire the spell shot
- B: hold while stationary on the ground to heal

Keyboard and mouse fallback:

- A / D or Left / Right: run
- Hold toward a solid wall while falling: wall grip; press jump to jump away
- W / S or Up / Down: climb ladders
- Space: jump; press again in the air to double jump
- Hold S or Down and press jump: drop through the supporting one-way strip
- Release jump early: shorten the jump
- Left or Right Shift: dash
- F or left click: sword attack
- Sword: hold S or Down and press F or left click while airborne for a downward cut.
  The 0.25-second stab hits immediately during its first two frames. Connecting
  with an enemy or spikes bounces you upward once per attack, preserving sideways
  movement. A successful bounce takes priority over body-contact damage that frame.
- Q or right click: hold for 0.25 seconds to fire one blue spell bolt. Release and
  press again for another shot. Running, jumping and wall gripping are supported;
  dashing, climbing, damage, early release or input suppression cancels charging.
- R: hold while stationary on the ground to heal 20% of maximum health per second.
  Continued holding repeats until full or out of energy. Moving, jumping, dashing,
  attacking, damage and input suppression interrupt. Damage requires a fresh hold.
- Both spells cost 30 energy; the reserve starts at 90 and restores 10 per second
  during active play, up to 90. Cancelled casts cost nothing. Hits do not restore energy.
  The HUD shows energy, controls and cast progress.
- E: reserved for future interaction (currently unbound)

Developer / editor controls:

- B: preview the shield pose (no gameplay block)
- F3: toggle traversal arcs and movement metrics
- F4: fire two ballistic reference markers; F6 clears their trails. Both launch from
  the player's current center in the facing direction at 10 tiles/s, 45 degrees.
  Yellow uses gravity 10 tiles/s²; pink uses current world gravity (59.375 tiles/s²).
  These are exact constant-gravity free-flight paths, independent of the character
  motor, with no drag, speed cap, inherited velocity, or terrain collisions. Each
  marker stops at launch height and leaves its trail for comparison. Editor mode
  freezes them. Labels interpret 1 tile as 1 meter for this experiment only; gameplay
  units and movement are unchanged. Adjust speed, angle, or reference gravity in the
  debug panel's Ballistics section (F5), then press F4 to repeat the experiment.
- F1: toggle the tile editor; freezes gameplay, detaches the camera, and switches the
  mouse to painting (left button paints or selects from the right sidebar, right erases,
  middle-drag pans, wheel zooms, and `Ctrl+Z` undoes an edit)
- F5: open or close the debug panel
- Escape: close

Input ownership stays small: `InputState` reads keyboard/mouse events, and
`App2d/Input/XboxControllerInput2D` reads controller state. Default gameplay bindings
live together in `PlayerInputMapper2D`, which produces `PersonCommand2D` without
camera or pointer coordinates. Attacks use facing direction. `PlayerDebugInput2D`
owns temporary player inspection keys; the editor and debug panel own their shortcuts.
Opening the editor or losing window focus cancels held gameplay input.
Held buttons must be released before they can trigger again after cancellation.

Interaction behavior and a pause menu are future gameplay work. E remains available for interaction; Escape currently retains its host close
behavior. There is no remapping UI or configurable binding file yet.

## Debug panel

Press F5 to open or close the in-game debug panel: a floating ImGui window of
checkboxes, sliders, dropdowns and buttons. It is non-modal, so gameplay keeps running
and keeps its keys except while a slider or value field is being edited. Hover a control
for its description. The engine registers the Draw toggles; the side scroller adds the
rest:

- Draw: graphics, grid, FPS, collision shapes, zones, traversal metrics
- Audio: SFX and music volume, music mood and piece overrides, now playing
- Camera: zoom
- World: reload giant trees
- Ballistics: speed, angle, reference gravity
- Resources: registered resource keys, consumers and sources

Camera zoom controls gameplay framing (default `1.35`, range `0.05`–`20`);
larger values zoom in. Visible terrain follows the camera automatically.
Gameplay scales uniformly from a 1080-pixel-high reference view: resizing the
window preserves the visible world height at the current zoom, while wider
aspect ratios reveal more world horizontally. Editor picking and panning use
the same camera transform.

Games expose runtime values with `DebugPanel.Section(title)` followed by `Toggle`,
`Slider`, `Choice`, `Button` or `Readout`; a section is created on first use, so a game
can extend the engine's Draw section. `Game2D` also exposes `DrawGraphics`, `DrawGrid`,
`DrawFps` and `DrawCollisionShapes` directly for tests and captures, and games register
their `PhysicsWorld2D` for collider visualization with `AttachPhysicsWorld`.
Physics collision shapes are drawn as translucent green overlays above the game
graphics, while active combat hitboxes use a red-orange overlay. Turn off Graphics to
keep the simulation running and inspect collision geometry; all world debug and UI
remain available. Grid enables the world-space debug grid (off by default).
The grid and other world diagnostics draw over game graphics, while HUD, menus,
and diagnostic labels draw last.

## Frame and coordinate flow

Each UI-timer tick advances simulation in fixed steps and then synchronously repaints.
The host calls `RenderFrame(Renderer2D, FrameTime)`, which clears once and draws
`Render` (game graphics), `RenderWorldDebug`, and `RenderUI` in that order.
See [rendering passes](docs/rendering-passes.md) for pass responsibilities and toggles.

Rendering follows this transform chain:

```text
geometry vertices (object space)
    -> Transform2D.LocalToWorldMatrix
world space (Y points up)
    -> Camera2D.WorldToDeviceMatrix
MonoGame device space (origin top-left, Y points down)
```

Mouse events arrive in WinForms client coordinates. `InputState` first accounts for the
client-to-backbuffer device scale, then `Camera2D.DeviceToWorld()` applies the inverse camera
matrix. This is why the click marker remains correct after resize and zoom.

`IShader2D` describes a solid, gradient, tiled, or sprite material. `Renderer2D`
transforms geometry into device-space triangles and submits ordered batches through
MonoGame's `GraphicsDevice` and `BasicEffect`. Color values use
`Color4b` (the global alias for `Microsoft.Xna.Framework.Color`); floating-point HUD
bounds use `ScreenRectangle2D`.
`Renderer2D.WrapScreenText(text, width, scale, maxLines)` lays out text using the
font atlas's glyph advances. `DrawScreenTextWrapped` draws from a first-line baseline
with the font's scaled line spacing. Explicit newlines are preserved, long words
are split without dropping characters, and a line limit adds a width-fitting ellipsis.
Gradients interpolate vertex colors, and textures use XNA sampler states. Circles and
capsules are tessellated according to their on-screen size. The host requests 4x MSAA.

`GraphicsSurface2D` owns a WindowsDX graphics device bound to a WinForms child control.
It resizes the backbuffer with the control, while the existing 120 Hz simulation,
keyboard/mouse input and native property inspector keep their existing host. The former Skia raster/OpenGL backend and `APP2D_RENDER_BACKEND`
switch have been removed.

Scene objects also expose a general-purpose `ZIndex`. Lower values render first, and
objects with equal values keep their scene insertion order. Changing a z-index at
runtime invalidates the scene's cached draw order. The side-scroller places the player
at z-index 1 so streamed terrain and one-way platforms at the default 0 stay behind the
character.

## Assets and textures

Repository assets are separated by lifecycle under the top-level `Assets` directory.
Debug reads generated art from `Assets/Runtime` and durable levels/music directly
from `Assets/Static`. Release packages content beside the executable as `Assets`,
using all runtime-ready content from `Static` and authored character documents from
`Assets/Characters/authored`. `Assets/Sources` retains originals and licenses, and
ignored `Assets/Work` holds intermediate output and previews.
[The asset layout](Assets/README.md) defines where each kind of new content belongs.

`App2d.Core.Assets.AssetLocations` names the main roots and categories; the game
resolves them once as `AssetPaths.Current`. `UserDataLocations` names player saves
and editor settings outside the asset tree. Shared relative-path checks and safe
single-file saves live in `App2d.Core.IO`; asset formats retain their own validation.

Runtime paths use lowercase semantic IDs. Every `Game2D` owns a `TextureCache2D`
rooted at the deployed `Assets` directory, so textures load only when requested:

```csharp
var fireball = Textures.Load("effects/fireball/ember-energy.png");
var fireballShader = new TextureShader2D(fireball, new Vector2(96f, 96f));
```

Repeated loads of the same path return the cached `Texture2D`. Windows image decoding
produces straight-alpha RGBA pixels without requiring a graphics device. On first draw,
the renderer uploads a premultiplied GPU copy and uses `BlendState.AlphaBlend`.
GPU copies are cached with a 384 MiB LRU budget and trimmed after frame submission.
`Textures.Unload(path)` and `Textures.Clear()` release decoded pixels and notify
renderers to release their GPU copies. `GameHost.Dispose()` releases both caches.

`TextureShader2D` supports independent X/Y `TextureAddressMode` values, a
`TextureFilter`, and world-unit tile size. Tiling is anchored at the local origin.
`SpriteShader2D` maps the complete texture to object bounds and corrects orientation
for the Y-up world. No content-pipeline conversion or custom effect compilation is
required for existing PNG assets. HUD text uses a font atlas rasterized once by
Windows and drawn as GPU triangles; the atlas covers Latin text and the HUD symbols.

The side-scroller's terrain maps topology roles to conventional files in the selected
tileset, while pooled fireballs use `effects/fireball/ember-energy.png`.

## Frame animation

`App2d.Core/Animation` contains a generic, update-driven frame animation layer. A clip can
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
loop or stop on their last frame. Players support pause, resume, stop, restart, and
playback-speed changes. `SpriteShader2D` maps one complete texture onto finite object
bounds, corrects image orientation for the engine's Y-up world, and flips the baked
right-facing sprites when the player faces left.

The sword, gun, and unarmed fighter are complete 2D character sprite sets. Switching gear swaps the
active character animation set.

Each generated player character owns a `character.json` and semantic folders such as
`Assets/Runtime/characters/player-sword/animations/walk`. The asset build generates the
manifest from the importer configuration; the animation ID and folder name are
identical, while source names remain provenance rather than runtime vocabulary.
Frames use contiguous four-digit names beginning at `frame-0001.png`. Manifests record
frame rate or total duration plus looping behavior without repeating asset paths.

Sword and gun-shot one-shots take priority over locomotion and remain synchronized to
their gameplay timing. The character artwork is rendered by a separate visual object
that follows the smaller physics collider, keeping transparent frame padding out of
collision calculations.

## Code formatting

The existing `.editorconfig` keeps statements on separate lines and requires braces
around control-flow bodies. It does not impose a line-width limit. Apply these rules
with the formatter included in the .NET SDK:

```powershell
dotnet format style App2d.slnx --diagnostics IDE0011 --severity warn
dotnet format whitespace App2d.slnx
```

Add `--verify-no-changes` to both commands to check formatting without editing files.
For the standalone cape study, use `tools/ClothLab/ClothLab.csproj` in place of
`App2d.slnx`.

## Graphics verification

Build and test on Windows with a Direct3D-capable adapter:

```powershell
dotnet build App2d.slnx
dotnet test App2d.slnx
dotnet run --project App2d.CharacterSeeds -- --render-smoke artifacts/monogame-smoke
```

Rendering tests draw into real GPU render targets and read pixels back to check sprite
flips, tiling, gradients, alpha blending, ordering, resize, and disposal. The smoke
command renders the actual game and tile palette at 1280x720 and 960x640, exercises
backbuffer reset/presentation, saves PNGs, and exits without entering an editing
session or advancing gameplay. It creates only a hidden window.
