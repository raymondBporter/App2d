# App2d

A deliberately small MonoGame/XNA 2D engine skeleton with compile-time module boundaries.

The solution has separate projects for core types, collision, tiles, authored content,
physics, rendering, audio, and game code, plus the game and NoodleBRO rig-lab Windows executables.
Each project physically owns its source files — there are no linked-file views.
Core, collision, tiles, levels, physics, `App2d.Game.Contracts`, `App2d.Gameplay`, and
the gameplay tests target plain `net10.0`. Rendering uses MonoGame WindowsDX;
rendering, audio, `App2d.Game.Presentation`, the host, and their tests target
`net10.0-windows10.0.19041.0`.

`App2d.Game.Contracts` owns commands, immutable observations/events, and shared
configuration. `App2d.Gameplay` owns simulation and rollback; it cannot reference
presentation, rendering, audio, or persistence. `App2d.Game.Presentation` owns views,
camera, HUD, sound selection, and the local client endpoint; it cannot reference
simulation. `Directory.Build.targets` rejects forbidden direct or transitive internal
assembly references. Existing `App2d.Gameplay.*` namespaces are retained across these
assemblies. See [the project boundaries](docs/session-architecture.md#project-boundaries).

The engine is grouped by responsibility:

- `App2d.Core/Mathematics` contains transforms and reusable numeric helpers, including
  `Similarity2D` — the validated rotation + uniform scale + mirror + translation pose
  that collision consumes. The namespace uses `Mathematics` rather than `Math` so it
  never shadows `System.Math`.
- `App2d.Rendering` contains the renderer and shader abstractions.
- `App2d.Core` presents guards, mathematics, animation, geometry, and the
  render-agnostic `SpatialObject2D`. `App2d.Tiles` presents the tile maps and mesher.
  `App2d.Collision` contains only collision work: `BroadPhase`, `Contacts`,
  `Filtering`, `Intersections`, and `Queries`.
- `App2d.Levels` stores authored levels as SQLite files. Tiles are run-length encoded
  per chunk so a single edit rewrites a single row; a missing chunk row means an
  entirely empty chunk. It is the only project that references `Microsoft.Data.Sqlite`,
  and it never references simulation. It also loads authored player geometry into
  shared traversal configuration through `TraversalMetricsLoader2D`; its game
  dependency is limited to `App2d.Game.Contracts`.
- `CollisionSystem2D` owns runtime collider registration, collision layers and masks,
  cached static/dynamic spatial indexes, candidate discovery, and exact contacts. It has
  no dependency on physics; physics and gameplay are consumers of collision data.
- `App2d.Collision/BroadPhase` contains generic AABB candidate-pair searches, while
  `App2d.Collision/Filtering` contains their generic filtering contract.
- `App2d.Collision/Contacts` contains overlap/contact generation. Shape-pair code is
  split into circle, capsule, polygon/SAT, and utility partials behind one dispatcher.
- `App2d.Collision/Queries` contains rays, exact shape intersections, and generic
  spatial-object raycasts. Physics-world extensions live in `App2d.Physics/Queries`,
  preventing the collision module from depending back on physics. Query hits retain
  direct references to the object or body that was hit.
- `App2d.Physics/Integration`, `App2d.Physics/Filtering`, and `App2d.Physics/Solvers`
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
that builds a session and everything it owns; the host, a future server, and a
predicting client all construct through it. The local game advances through
`SideScrollerSession2D`: ID-addressed held-state input commands enter a fixed 120 Hz
session (a player with no input this tick repeats its last command), and immutable
per-player, enemy, level-content, and world observations plus gameplay events return
to `SideScrollerClient2D`. Presses and releases are derived inside the simulation from
consecutive commands, so a lost or repeated command cannot drop or double an action.
The production level, weapons, enemy actors, moving platforms, checkpoints, and
terrain colliders run without constructing graphics or playing audio. Client presentation owns
sprites, terrain, effects, HUD, and sound; the host handles persistence and editing.
Terrain observations use cached immutable chunk revisions, including neighboring
tiles for correct edge rendering. Simulation and presentation live in separate
assemblies. A complete `SessionSnapshot2D` supports attaching at any tick; input
production and display-time advancement run independently of received frames.
Persistent character poses and attack phases reconstruct from state, and terrain
observations can be rebuilt from values without a live map. Local simulation
capture/restore and bounded input replay are available.
Networking and client prediction/reconciliation are not yet wired up. See
[the session architecture](docs/session-architecture.md) for the boundaries and
next capture/restore work.

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
- `Bounds2D` supplies local bounds to rendering and shaders.

`App2d.Tiles/TileMap2D` stores compact authored maps as a bool-only grid; it carries
no `TileKind2D`. `App2d.Tiles/EditableTileMap2D` is the only `IChunkedTileMap2D`
implementation: a dense, mutable map loaded from a level file rather
than evaluated from a seed function. It still greedily merges each 32x32 chunk into
AABB colliders on demand, and raises a `ChunkChanged` event that the in-game tile editor
uses to drive streamer reloads after an edit. The side-scroller keeps at most 15
nearby simulation chunks live, bounding weapon queries and the collision spatial
index. Terrain visuals stream independently from `Camera2D.VisibleWorldBounds`,
with one tile of padding for overhanging artwork. Zoom, resize, and editor panning
therefore load all visible terrain without expanding the physics neighborhood.

Each editable cell is one byte: four bits select one of up to 16 tilesets and four bits
hold the composable tile type. Code exposes those as separate values; the packing is an
in-memory and level-blob detail. The level metadata stores the ordered, stable tileset IDs.

Editor mode is part of the game rather than a separate tool. `F1` freezes the simulation
and detaches the camera. A right sidebar provides tileset buttons and a visual tile-type
grid for filled, grippable, one-way, spikes, and ladders. The previews use the selected tileset's
actual terrain art. The right mouse button erases and `Ctrl+Z` undoes a brush stroke.
Painting mutates the loaded `EditableTileMap2D`,
whose `ChunkChanged` event feeds a `DirtyChunkTracker2D`; the editor flushes that tracker once
per frame so a drag rebuilds each affected chunk at most once instead of once per event. Each
stroke commits its changed chunks to the level file in a single transaction. `TileEditSession2D`
holds the strokes and undo history and lives in `App2d.Tiles`, so the editable core is testable
without input or storage.

Paint a vertical column of **Ladder** tiles to make a climbable ladder of any height.
Ladders use Kenney's top and repeating rung art (also as a fallback for other tilesets).
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
currently offers the player spawn, goal, three enemy types, tumble props, and moving platforms.
The position-only types deliberately expose just reusable definition names plus instance name,
enablement, and position; their health, AI, art, physics, and other tuning remain in gameplay
code. Moving platforms keep their richer rectangle, art, path, and speed properties. A native
WinForms `PropertyGrid` writes only when **Apply** is pressed, while MonoGame draws map markers,
placement ghosts, selection outlines, paths, and draggable endpoints. This avoids turning the
renderer into a general UI library. All definitions, instances, transforms, and typed pieces
remain relational rows in the level database. Moving-platform changes reload live; the other
map objects are picked up on the next run.

Non-convex geometry can become another `IShape2D` implementation later, with its own
triangulation/rendering path, without weakening the convex polygon guarantees.

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
    -> iterative contact + constraint position projections
    -> contact response + constraint velocity projections
```

`GameHost` feeds gameplay exact 1/120-second updates through an accumulator. The
physics world's matching maximum substep therefore becomes one physics step per
gameplay update in normal operation, while rendering remains independent. Transient
input is consumed after the first fixed update so a catch-up frame cannot repeat a
button press or mouse-wheel action.

The defaults are semi-implicit Euler integration, mass-weighted MTV correction, and a
linear impulse solver. Physics policies remain replaceable through `Integrator`,
`PairFilter`, `PositionSolver`, and `VelocitySolver`. Collision shape/contact policy is
configured independently through `CollisionSystem2D.ContactProvider`. `Constraints` run their
position projection during each `PositionIterations` pass, followed by their velocity
projection for `VelocityIterations` passes.

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

## Ray queries

`Ray2D` stores a normalized world-space direction, so every hit distance is measured in
world units. `RayIntersection2D` intersects transformed circles, rectangles, convex
polygons, capsules, and half-spaces exactly through the object's `CollisionPose`. Rays
that start inside a finite shape return its exit surface, and rotation, uniform scale,
and mirroring preserve the correct world normal.

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

For pixel collision, implement `ICollisionContactProvider2D` and either replace the shape
provider or combine providers with `CompositeCollisionContactProvider2D`. Physics only
consumes the resulting collision contacts.

## First run

The game reads generated art from ignored `Assets/Runtime`, so a fresh clone must build
it once. The build needs Python 3.12+ and two Maaot cave packs that their license does
not allow us to redistribute:

1. Download [2D DarkCave Assets](https://maaot.itch.io/2d-browncave-assets) and
   [Mossy Cavern](https://maaot.itch.io/mossy-cavern) from itch.io.
2. Save them as `Assets/Sources/third-party/maaot/dark-cave.zip` and
   `Assets/Sources/third-party/maaot/mossy-cavern.zip`.
3. From the repository root, run:

```powershell
.\tools\setup.ps1
```

The script creates an ignored `.venv`, installs the pipeline's only dependency
(Pillow), checks for the Maaot archives, and runs the asset build. Re-run it whenever
source art or an importer changes; it is safe to repeat. `tools/ArtPipeline/README.md`
documents what the build produces and how to author new art.

## Run

```powershell
dotnet run --project App2d
```

Run the C# / Dear ImGui character motion studio with:

```powershell
dotnet run --project App2d.CharacterStudio
```

The studio includes 759 point-animation clips (662 Person clips, including the Kevin Iglesias free packs) across Person, Quadruped, Blob,
Flying and 11 creature studies. Search and scrub motions, edit appearances,
preview sequences, and save JSON looks with semantic animation bindings.
Its depth-tested character renderer lives in `App2d.Rendering`; sampling and
playback live in `App2d.Core`, with no ImGui dependency. Imported assets are
included, so this tool does not require the game's raster asset build or Blender.
See [Character Studio](docs/character-studio.md) for architecture, import and checks.
Quadruped includes nine [mapped Tomek wolf motions](docs/wolf-animation-mapping.md),
available under the **Tomek / Wolf** source filter and in entity action pickers.
The studio also edits named entity types, action timing and collision regions, and
includes a playable arena with five starter characters. See [entity authoring](docs/entity-authoring.md).
The main game now uses those same authored characters: Player, Needle, Maul,
Cinder and Scrap Hound. Save edits in Studio and restart the game to apply them.
Existing enemy placements map to the new types; their names also appear in the
level editor. Enemy combat and the player's normal sword swing use authored
timing/geometry, while the existing traversal controller remains in charge of
movement. See [game integration and current limits](docs/entity-authoring.md#main-game-integration).

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
depths. Authored moving platforms use kinematic one-way slabs that follow horizontal or
vertical ping-pong paths and carry dynamic bodies supported on top without replacing the
rider's own movement velocity.
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
a fixed pool of 16 fireballs, and an unarmed punch/kick loadout. A hostile unarmed rival
uses the same person locomotion, punch, kick, damage, and presentation-state paths under a small AI command producer;
the patrol enemies and Boiler Brute remain bespoke actors. While airborne and falling, holding toward a
nearby static solid wall suspends the player; jumping launches away. One-way platforms
do not allow wall grip.

Short sound effects are decoded once at startup and played through one polyphonic mixer.
Gameplay emits semantic cues through `ISoundEffectSink2D`, so movement, combat, and weapon
code never knows asset paths or audio-device details. `SoundEffectBank2D` owns the cue-to-file
mapping, per-cue levels, non-repeating variants, and subtle per-play volume and pitch variation.
`SpatialSoundEffectSink2D` wraps the bank with the player's live position as its listener.
World effects use `PlayAt(effect, position)`; controllable effects use `BeginAt` and can
move their source with `SetPosition`. The game updates active voices each frame, multiplying
their requested volume by smooth distance falloff. `SoundFalloff2D` sets the tuning:
ordinary effects stay at full volume within 96 world units (3 tiles) and fade to silence
at 800 (25 tiles); hammer impacts use 128 and 1200 units. Out-of-range one-shots are
skipped. Player movement/feedback and global cues use `Play`/`Begin` at their normal levels.
Set `sfx_volume` in the developer
console to a value from 0 through 1 to adjust the master sound-effect level.

Person movement is owned by `App2d.Gameplay/Persons/PersonLocomotion2D`. `PersonMovementIntent2D` describes
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

Xbox controller:

- Left stick or D-pad: run
- Hold toward a solid wall while falling: wall grip; press A to jump away
- Hold Down on the left stick or D-pad and press A: drop through a one-way platform
- A: jump; release early to shorten the jump
- Right trigger: dash
- X: use the selected primary action (sword, gun, or punch)
- Sword: hold Down and press X while airborne for a downward cut
- Right bumper: kick while unarmed
- Y: reserved for future interaction (currently unbound)

Keyboard and mouse fallback:

- A / D or Left / Right: run
- Hold toward a solid wall while falling: wall grip; press jump to jump away
- W / S or Up / Down: climb ladders
- Space: jump; press again in the air to double jump
- Hold S or Down and press jump: drop through the supporting one-way strip
- Release jump early: shorten the jump
- Left or Right Shift: dash
- F or left click: use the selected primary action (punch while unarmed)
- Sword: hold S or Down and press F or left click while airborne for a downward cut.
  The 0.25-second stab hits immediately during its first two frames. Connecting
  with an enemy or spikes bounces you upward once per attack, preserving sideways
  movement. A successful bounce takes priority over body-contact damage that frame.
- Gun: hold F, left click, or controller X for 0.6 seconds to automatically fire
  one blue bolt. Release and press again for another shot. You can run, jump,
  dash, wall-grip, or climb while charging. Movement and gravity work normally;
  the shot follows your facing direction (away from a wall while gripping it).
  The HUD ring fills and the muzzle glow shimmers during charging.
  Releasing early, taking damage, switching gear, or entering the editor cancels it.
  Input suppression cancels instead of firing.
- Q or right click: kick while unarmed
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
  units and movement are unchanged. In the console, set `ballistics_speed`,
  `ballistics_angle`, or `ballistics_gravity`, then press F4 to repeat the experiment.
- F1: toggle the tile editor; freezes gameplay, detaches the camera, and switches the
  mouse to painting (left button paints or selects from the right sidebar, right erases,
  middle-drag pans, wheel zooms, and `Ctrl+Z` undoes an edit)
- Backtick (`): open or close the developer console
- Escape: close

Input ownership stays small: `InputState` reads keyboard/mouse events, and
`App2d/Input/XboxControllerInput2D` reads controller state. Default gameplay bindings
live together in `PlayerInputMapper2D`, which produces `PersonCommand2D` without
camera or pointer coordinates. Attacks use facing direction. `PlayerDebugInput2D`
owns temporary player inspection keys; the editor and console own their shortcuts.
Opening the editor/console or losing window focus cancels held gameplay input.
Held buttons must be released before they can trigger again after cancellation.

Sword/gun loadout changes, interaction behavior, and a pause menu are future gameplay
work. E/Y remain available for interaction; Escape currently retains its host close
behavior. There is no remapping UI or configurable binding file yet.

## Developer console

Press backtick (`) to open the in-game developer console. Gameplay keeps running, but
gameplay input is suppressed while the console has focus. Variables can be read by name
and changed with either whitespace or an equals sign:

```text
draw_fps
draw_fps true
draw_collision_shapes = true
draw_graphics = false
camera_zoom = 2
```

`camera_zoom` controls gameplay framing (default `1.35`, range `0.05`–`20`);
larger values zoom in. Visible terrain follows the camera automatically.
Gameplay scales uniformly from a 1080-pixel-high reference view: resizing the
window preserves the visible world height at the current zoom, while wider
aspect ratios reveal more world horizontally. Editor picking and panning use
the same camera transform.

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
MonoGame device space (origin top-left, Y points down)
```

Mouse events arrive in WinForms client coordinates. `InputState` first accounts for the
client-to-backbuffer device scale, then `Camera2D.DeviceToWorld()` applies the inverse camera
matrix. This is why the click marker remains correct after resize and zoom.

`IShader2D` describes a solid, gradient, tiled, or sprite material. `Renderer2D`
transforms geometry into device-space triangles and submits ordered batches through
MonoGame's `GraphicsDevice` and `BasicEffect`. Color values use
`Microsoft.Xna.Framework.Color`; floating-point HUD bounds use `ScreenRectangle2D`.
Gradients interpolate vertex colors, and textures use XNA sampler states. Circles and
capsules are tessellated according to their on-screen size. The host requests 4x MSAA.

`GraphicsSurface2D` owns a WindowsDX graphics device bound to a WinForms child control.
It resizes the backbuffer with the control, while the existing 120 Hz simulation,
keyboard/mouse input, native property inspector, and developer console keep their
existing host. The former Skia raster/OpenGL backend and `APP2D_RENDER_BACKEND`
switch have been removed.

Scene objects also expose a general-purpose `ZIndex`. Lower values render first, and
objects with equal values keep their scene insertion order. Changing a z-index at
runtime invalidates the scene's cached draw order. The side-scroller places the player
at z-index 1 so streamed terrain and one-way platforms at the default 0 stay behind the
character.

## Assets and textures

Repository assets are separated by lifecycle under the top-level `Assets` directory.
Only generated `Assets/Runtime` ships; Debug reads it in place and Release packages it
beside the executable as `Assets`. `Assets/Static` holds curated runtime-ready inputs,
`Assets/Sources` retains originals and licenses, and ignored `Assets/Work` holds
intermediate pipeline output and previews. `Assets/README.md` describes the lifecycle.

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

## Graphics verification

Build and test on Windows with a Direct3D-capable adapter:

```powershell
dotnet build App2d.slnx
dotnet test App2d.slnx
dotnet run --project App2d -- --render-smoke artifacts/monogame-smoke
```

Rendering tests draw into real GPU render targets and read pixels back to check sprite
flips, tiling, gradients, alpha blending, ordering, resize, and disposal. The smoke
command renders the actual game and tile palette at 1280x720 and 960x640, exercises
backbuffer reset/presentation, saves PNGs, and exits without entering an editing
session or advancing gameplay. It creates only a hidden window.
