# Authored objects as the only moving-platform path

Date: 2026-10-22. Status: final draft, builder-checked, handed to Raymond for approval.

## Intent

A game object is a composition of primitives and nothing else: a shape, a render
material drawn on that shape, a physics material, and a path. The moving platform is
the smallest complete composition the game has (a rectangle, a solid color, a kinematic
body, a ping-pong path), which is why it is the first thing to make generic. After this
change there is no "platform" concept anywhere: not in the level database, the editor,
the runtime, the contracts, or presentation. A platform is an object whose primitives
happen to be those four. Every variation is then a swap of data, not new code: a
different shape, a different material, a dynamic body, a curved path. The point is
combinatorics: a few shapes times a few materials times a few paths gives far more
content than hand-authoring each thing, and no new "definition" class per thing.

Raymond's words for the model, which this document follows: shapes are the primitives,
curves are the primitives, render materials are the primitives, physics materials are
the primitives. The human skeleton stays as it is; bones and animation are a later,
harder layer on top of the same idea.

## Current state

Two systems exist side by side.

**The platform vertical** (August 30 spec, `2026-08-30-moving-platform-thing-vertical-design.md`)
built the platform as a code-owned thing type. The level database got a component-shaped
schema (`thing_definitions`, `things`, `rectangle_shapes`, `solid_color_art`, `transforms`,
`ping_pong_motors`), but every piece was required to own typed SQL, a record, editor
presentation, and a spec field, so the C# above the schema is one hardcoded type repeated
at each layer:

- Records and spec: `MovingPlatformDefinitionRecord2D`, `MovingPlatformThingRecord2D`,
  `NewMovingPlatformThing2D` (App2d/Levels/MovingPlatformRecords2D.cs), `MovingPlatformSpec2D`.
- Runtime: `MovingPlatform2D` (App2d/Gameplay/World), built on Core's `PingPongMotion2D`.
- Contracts: `MovingPlatformDefinition2D` and `MovingPlatformState2D`, carried as dedicated
  `MovingPlatforms` fields on `LevelContent2D` and `WorldState2D`.
- Presentation: a `PlatformView` branch in `WorldPresentation2D`.
- Editor: `MovingPlatformDefinitionProperties2D` and `MovingPlatformInstanceProperties2D`
  (App2d/Editor/MovingPlatformProperties2D.cs), the `IsMovingPlatform` special case in
  `ThingTypeRegistry2D`, platform branches in `TileEditor2D` (placement, drag, undo,
  `ThingsChanged`), `ThingEditorInspector2D`, and `TileEditorView2D`.
- Database: about 220 lines of platform CRUD in `LevelDatabase2D.Things.cs`.
- Wiring: `SideScrollerLevel2D` (`_movingPlatforms`, `CreateMovingPlatformsFromSpecs`,
  `ReloadMovingPlatforms`), `SideScrollerSessionDefinition2D.MovingPlatforms`,
  `SideScrollerGame` (`ThingsChanged` to `ReloadMovingPlatforms`), `WorldFile2D` and
  `WorldAsset2D.MovingPlatforms`, the `PlatformStudy2D` smoke in CharacterSeeds.

**The generic object path** (commit ffe25628, October) is complete at the runtime level.
`ObjectAsset2D` (App2d.Core/Objects) is a JSON `app2d-object` with one `geometry`
(any `IShape2D`), an optional inline `material`, optional `physics`
(`PhysicsBodySettings2D`: motion type, mass, inertia, gravity scale, friction,
restitution, transfers contact motion), an optional `surface` (one-way normal and slop),
and an optional `motion` (`CurveMotionDefinition2D`: any `ICurve2D` of kind line,
polyline, arc, cubic-bezier or b-spline, a period, repeat, and a progress-map list such as
ping-pong and smooth). `ObjectPlacement2D(Id, Asset, Position)` places it;
`AuthoredObject2D` binds it to a kinematic entity or a body and the one-way surface
rules; `ObjectDefinition2D` and `ObjectState2D` carry it through `LevelContent2D.Objects`
and `WorldState2D.Objects`; `WorldPresentation2D` draws it through
`ShapeMaterialShader2D`. The proof is in the cavern level: `levels/cavern/objects/lift.json`
is a teal one-way kinematic rectangle on a cubic-bezier path that carries the player,
placed as `arrival-lift` in `levels/cavern/world.json`.

What stalled: the old path was never deleted, and the editor knows nothing about objects,
so the only way to place one today is to hand-edit `world.json`. `KINEMATICS.md` records
the stopping point.

**Attribute-size audit.** The only asset data that describe geometry as width and height
attributes instead of a shape are the platform rows (`rectangle_shapes`) and the legacy
sprite manifest `Assets/Static/characters/player-geometry.json`, which is read only by
`TraversalMetricsLoader2D` (smokes and tests; the game itself derives traversal from the
authored hero's movement shape in `SideScrollerGame`). The many `"width"` keys in the
character JSON are stroke and outline widths and the `PersonBuild.Width` proportion, not
geometry stand-ins. Nothing in the seeds emits attribute geometry.

## Goals

1. One object model. An object asset is geometry, optional collision shape, material,
   physics, surface, and motion. No `MovingPlatform*` type survives in any project.
2. The map editor places, drags, enables, disables, deletes and undoes objects, from a
   palette built from asset files, and draws each placed object's shape and path.
3. Object placements persist in the level database, the same place and the same undo
   stack as every other placed thing. `world.json` stops carrying placements.
4. The existing platform rows and the arrival lift migrate without re-authoring; the
   cavern level plays as it does today (the lift and the migrated platforms move, carry
   the player, and are one-way).
5. The platform vertical and its tests are deleted or rewritten against objects.

## Non-goals

- Editing a path per placement. Decided: the path belongs to the object, so two lifts with
  different travel are two asset files. A placement-level motion override is a later
  addition and must not be designed around now.
- True sensors or triggers. Physics has no sensor flag yet, so a collision-only object is
  an invisible solid. The shape split prepares for sensors; the flag comes later.
- Dynamic objects with brains, joints, grass or animated-texture materials. The model
  enables them; nothing here builds them.
- Position things (player spawn, save point, goal, the enemy entries). They stay as they
  are; the registry keeps its list for them.
- Replacing `player-geometry.json` and `TraversalMetricsLoader2D`. Worth doing later by
  deriving test traversal from the authored hero as the game does; out of scope here.
- A shared cross-world object library. Assets stay in the world's `objects` folder.

## Anti-goals

The change has failed, even if it compiles and plays, when:

- any new class exists whose job is to hold one object's attributes for the database,
  the editor, or the contracts (another `*DefinitionProperties2D`, record, or spec);
- a second persistence path for placements survives (a `world.json` objects list next to
  the database rows);
- the editor loses something platforms have today: click to place, drag, delete, undo,
  and live reload into the paused simulation;
- a new primitive combination (say a capsule with a texture on an arc) needs a code
  change anywhere outside Core's shape, curve, material, or physics libraries.

## Constraints

- Core must not depend on the game project. `ObjectAsset2D` and its validation stay in
  Core; thing-type and database knowledge stay in the game.
- Keep namespaces where they are. Match Raymond's single-line signature style.
- The level database already versions itself with `PRAGMA user_version`
  (`CurrentFormatVersion = 2`, stepwise `Migrate` on open-for-editing; read-only opens
  refuse older files with "open it for editing once"). The conversion is format version 3
  and uses that mechanism.
- The cavern world references four map files (`level.db`, `hub.db`, `sandbox-b.db`, and the
  generated `map-2` file, which the sandbox-a zone points at); a fifth, `sandbox-a.db`, is
  committed but referenced by nothing. All are format version 2 with the three platform
  tables present. Only `level.db` has platform rows: one definition named "Lift" and seven
  placed things, each with its own travel and speed and its own name (Bridge ferry,
  Terrace elevator, Canyon lift, West, Central and East windlift, Summit lift).
  `world.json` carries one object placement. Raymond is not authoring more assets until
  Studio is sorted, so the migration is a one-time step whose output (asset files and the
  converted databases) is committed.
- Test baseline: 2,239 tests with five known gitignored-tileset failures and one unrelated
  Trials failure in a worktree; the same suite is green in the main checkout.

## Design decisions

Each decision is marked (Raymond) when he made it in conversation or (call) when it is
mine; calls are the ones to check first when reading.

### Object asset: render shape and collision shape (Raymond)

- `geometry` is the shape that is drawn and, by default, the shape that collides.
- `collision` is an optional second shape. When present it is the physics collider and
  `geometry` is draw-only.
- A collision-only object has `collision` and no `geometry`: it collides and draws
  nothing. Today that means an invisible solid; a sensor flag is a later addition.
- Validation: at least one of `geometry` or `collision`; `material` requires `geometry`;
  `collision` requires `physics`; the existing rules stay (physics plus motion means
  kinematic; `surface` requires physics). Two shapes plus this heuristic is the whole
  model; a flag is added only if a real case needs one.

Runtime consequences: `AuthoredObject2D` builds the kinematic entity or body on
`collision ?? geometry` and the visual on `geometry`; `ObjectDefinition2D.Geometry`
becomes nullable and `WorldPresentation2D` creates no visual for a null geometry.
`geometry` on `ObjectAsset2D` becomes optional in the JSON contract; the lift and every
existing asset stay valid unchanged.

### Motion lives in the object (Raymond)

`motion` stays on the asset, in local coordinates, offset by the placement position as
`CurveMotionDefinition2D.CreateMotion(origin)` already does. Different travel means a
different asset file. In the cavern data every one of the seven placed lifts has its own
travel and speed, so the migration produces seven small asset files that differ only in
their line and period; Raymond confirmed that with the data in front of him. A
placement-level path override stays a later addition.

### A placement is a position thing whose definition names an asset (call)

The schema is already shaped for this: a `things` row is an instance, `transforms` holds
its position, and `thing_definitions` is the per-map palette. An object needs exactly one
new piece:

```sql
CREATE TABLE object_references(
    definition_id INTEGER PRIMARY KEY REFERENCES thing_definitions(definition_id) ON DELETE CASCADE,
    asset TEXT NOT NULL);
```

`asset` is a path relative to the world root (the folder holding `world.json`), resolved
with `FilePaths.ResolveUnderRoot` so it can never leave that folder; `objects/lift.json`
is therefore the same asset for every map of the cavern world, including the generated
map in a subfolder. `WorldFile2D.Load` already knows the root and passes it to `LoadMap`;
the editor receives it through `ConfigureMaps`. A map opened without a world file (the
legacy single-level fallback) uses its own directory as the root. Rotation in
`transforms` stays unused by objects for now.

The thing type is `object` (type key `object`, display name "Object"), replacing
`moving-platform` in `ThingTypeRegistry2D`. The descriptor's `WorldKind` is null and the
`IsMovingPlatform` flag becomes `IsObject`. `ToRuntime` for a position-thing record whose
definition is an object produces `ObjectPlacement2D(Id: the world thing id as text, Asset,
Position)`; assets are loaded once per map and cached by resolved path, as
`WorldFile2D.LoadObjects` does today. `WorldMapAsset2D` gains `Objects`;
`WorldAsset2D.Objects` becomes the union over maps with the same per-map id offset
`PositionThings` uses. The `rectangle_shapes`, `solid_color_art` and `ping_pong_motors`
tables are dropped in the version 3 migration.

Why the database and not `world.json`: the editor's placement, drag, delete and undo all
operate on database rows through one undo stack, and placements are per map while
`world.json` is per world. Keeping both would be the second persistence path the
anti-goals forbid.

### Definitions are created in the inspector, like every other type (call)

The palette flow stays what it is: "New definition", pick type `object`, pick an asset
from a combo listing `<world>/objects/*.json`, name defaults to the asset's name. Placing
uses the existing position-thing path (`CreatePositionThing`) with no object branch. The
instance inspector is `PositionThingInstanceProperties2D` plus a read-only asset path;
the definition inspector is name plus asset. No new properties class is added:
`PropertyInspector` only builds combos for enums, so the asset picker is an
inspector-level `Ui.Combo`, and the asset path is a nullable field on the existing
definition record.

### The editor view draws the object (call)

`TileEditorView2D` draws, for each placed object, the outline of `geometry` (or
`collision` when there is no geometry) at the placement, and when the asset has motion,
the curve sampled as a polyline from the placement origin, with the endpoints marked when
selected. This replaces the platform segment-and-rectangle drawing.

### Live reload (call)

`ThingsChanged` carries the map's position things. `SideScrollerLevel2D.ReloadObjects
(IReadOnlyList<ObjectPlacement2D>)` replaces `ReloadMovingPlatforms`: dispose and
recreate `_objects`, bump `_definitionRevision` so `LevelContent2D` changes.
`SideScrollerGame` rebuilds the placements from the changed map through the registry.
Position things do not live-reload today (`ReloadThings(notifyRuntime: false)`); objects
do, because platforms did, so this is the one place the object path gains behavior the
other position things lack.

### Migration to format version 3 (call)

Runs inside `LevelDatabase2D.Migrate` when `user_version` is 2:

1. For each `thing_definitions` row with `type_key = 'moving-platform'`, read its
   `rectangle_shapes` and `solid_color_art` rows and every placed thing's
   `ping_pong_motors` row.
2. Group that definition's things by (`travel_x`, `travel_y`, `speed`). Each group becomes
   one asset file in the world root's `objects/` folder. A group holding exactly one named
   thing takes that thing's name for the file slug and the definition name
   (`bridge-ferry.json`, "Bridge ferry"), which is every group in the cavern data; any
   other group takes the definition name with `-2`, `-3` suffixes. A target file with
   identical content is reused; a target file with different content stops the migration
   with a message naming the collision, so nothing hand-authored (such as the existing
   `objects/lift.json`) is ever overwritten. Each file holds:
   - `geometry`: rectangle from (−w/2, −h/2) to (w/2, h/2);
   - `material`: fill from the ARGB color, no outline (the old art had none);
   - `physics`: `Kinematic`, `restitution` 0, `transfersContactMotion` true;
   - `surface`: `oneWayNormal` (0, 1);
   - `motion`: curve `line` from (0, 0) to (`travel_x`, `travel_y`), `periodSeconds`
     = 2 × |travel| / speed (the old smoothstep motion kept the round-trip duration of
     the linear one), `repeat` true, `progress` [`ping-pong`, `smooth`].
3. The first group keeps the definition row: `type_key` becomes `object` and an
   `object_references` row names the file. Further groups get new definition rows, named
   by the same rule; their things are re-pointed. `transforms` rows are untouched.
4. Delete the piece rows, drop the three tables, set `user_version = 3`.

The migration writes files beside the database, which is a side effect a schema migration
would not normally have; it is accepted here because it runs once and its output is
committed. `Migrate` is static and path-less today, so the map path and world root are
threaded in for the file-writing step.

Running it is `dotnet run --project App2d -- --migrate-level`, extended to open every map
file under the world folder for editing (the four the world references and the orphan
`sandbox-a.db`), because read-only opens refuse older files and the game loads every
referenced map at start. All converted databases are committed; a second run changes
nothing in any of them. `sandbox-a.db` is referenced by nothing; whether to delete it is a
separate decision for Raymond, outside this change.

The single `world.json` placement (`arrival-lift` at (80, −240)) is migrated by the
builder, not by code: create an `object` definition for `objects/lift.json` in the cavern
map that contains that position and place it there through the editor or a one-off
script, then remove `objects` from `WorldDocument`, `WorldFile2D.LoadObjects`, and
`LevelBootstrap2D`. The converted `level.db`, the generated asset files, and the edited
`world.json` are committed with the change.

### Deletion list

Types: `MovingPlatform2D`, `MovingPlatformSpec2D`, `MovingPlatformDefinitionRecord2D`,
`MovingPlatformThingRecord2D`, `NewMovingPlatformThing2D`,
`MovingPlatformDefinitionProperties2D`, `MovingPlatformInstanceProperties2D`,
`MovingPlatformDefinition2D`, `MovingPlatformState2D`, `PlatformView`, and Core's
`PingPongMotion2D` (its only production user is `MovingPlatform2D`; the line curve with a
ping-pong map replaces it, and `KinematicEntity2DTests` is ported to that).

Members and branches: `LevelContent2D.MovingPlatforms`, `WorldState2D.MovingPlatforms`,
`SideScrollerSessionDefinition2D.MovingPlatforms` and the matching
`SideScrollerLevel2D` constructor parameter, all platform CRUD in
`LevelDatabase2D.Things.cs`, the platform fields, events and branches in `TileEditor2D`,
`ThingEditorInspector2D`, `TileEditorView2D`, `ThingTypeRegistry2D.MovingPlatform` and
its `ToRuntime` overload, `WorldMapAsset2D.MovingPlatforms` and
`WorldAsset2D.MovingPlatforms`, the `ReloadMovingPlatforms` wiring in `SideScrollerGame`,
and `WorldDocument.Objects`. `PlatformCollisionRules2D` and `PlatformSurface2D` stay:
they are the generic one-way surface data objects already use.

`PlatformStudy2D` (CharacterSeeds) is rewritten to build its platforms as object
placements so the study keeps proving carry and one-way behavior through the real path.

### Tests

Rewrite against objects: `MovingPlatform2DTests` (carry, one-way, reversal, disposal
through an object asset), `MovingPlatformThingDatabase2DTests` (object definitions and
things: create, update, delete, restore, load, plus a migration test that builds a
version 2 file with platform rows, opens it for editing, and asserts the asset files and
converted rows), `AuthoredMovingPlatform2DTests` (level construction and reload from
placements), and `JsonWorldObjectsTests` (placements come from the map database).

Update to drop platform references: `ObservationRoundTripTests`, `WorldStateTests`,
`WorldPresentationTests`, `SideScrollerSimulation2DTests`, `AuthoredEntityEnemyTests`.
Add to `AuthoredObject2DTests`: a collision-only object has a body and no visual; a
collision override collides with the override and draws the geometry.

### Documentation

`App2d/Gameplay/World/KINEMATICS.md` loses its platform paragraph and describes objects
only. README: the platform paragraph and the `--migrate-level` note. `docs/world-maps.md`
describes object definitions and placements. The August spec stays as history.

## Left to the builder

- How `TileEditor2D` learns the world's `objects` folder and caches loaded assets for the
  palette and the view (it already receives the maps through `ConfigureMaps`).
- Sampling density for the editor's path preview, and the slug rule for migrated file names.
- JSON converter details for the now-optional `geometry` and the new `collision`.
- Commit order. A sensible one: asset shape split with tests; object thing type, schema
  version 3 and migration with tests; editor; deletion of the vertical and the
  world.json path; docs.

## Verification

- Solution builds; the suite matches the baseline (five tileset failures and the Trials
  failure only in a worktree, green in the main checkout).
- `dotnet run --project App2d -- --migrate-level` converts every map file under the world
  folder once; the generated asset files and databases are committed and a second run
  changes nothing in any of them.
- Play the cavern level: the arrival lift and every migrated platform move, carry the
  player, and are one-way from below.
- In the map editor: create an object definition from an asset, place it, drag it, undo,
  delete, and see the paused simulation pick each change up.
- `dotnet run --project App2d.CharacterSeeds -- --platform-study <dir>` still produces its
  images through object placements.
