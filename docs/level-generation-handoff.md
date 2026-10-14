# Level generation: agent handoff

Verified against the working tree on 2026-10-10. This is an improvement handoff,
not a specification of an already finished procedural-level system.

## User intent and accepted design

The user is a programmer building a Hollow Knight-inspired sidescroller, with
combat, traversal, and exploration. The emphasis is linear progression and combat.
They want quick generation, playtesting, throwing away drafts, and manual editing
of promising results, without spending hours painting tiles.

The agreed hierarchy is **world → zones → assigned maps → generated sections**.
The generator assembles sections into a map; the world layout assigns and places
that map in a zone. Sections are not separate world zones or map databases.
The current generation action supports one map per zone, although the world format
can already describe multiple maps per zone.

Preserve these behaviors when improving it:

- Normal game launch needs no arguments and opens the upstream map (`main`); F2 selects the assigned generated map.
- Generation is an explicit authoring action; normal launch never regenerates.
- Output is ordinary editable terrain and things, not a runtime seed function.
- Rerolling preserves previous databases and edits, then changes the assignment.
- Existing authored maps remain available.
- Playground startup begins at arrival, avoiding confusing stale checkpoints.
- Combat checkpoints still work for deaths within a play session.

The user has several enemy roles already. An area-controller enemy is not needed
to proceed. Flying enemies exist in gameplay but are not used by this generator.

## Current entry points and code ownership

All paths below are relative to the repository root.

| File | Responsibility |
| --- | --- |
| `App2d/Levels/CombatLevelGenerator2D.cs` | Settings, beat selection, terrain recipes, placements, draft export |
| `App2d/Levels/ZoneLevelGenerator2D.cs` | Generate a revision and assign its database to a configured zone |
| `App2d/Levels/WorldFile2D.cs` | World JSON schema, map loading, identities, initial map selection |
| `App2d/Levels/LevelDatabase2D.cs` and `.Things.cs` | SQLite terrain and thing persistence |
| `App2d/LevelBootstrap2D.cs` | Default world path, tileset catalog, root zone-region merging |
| `App2d/Program.cs` | CLI authoring commands and argument-free game launch |
| `App2d/SideScrollerGame.cs` | World/session/editor composition and startup save policy |
| `App2d/Editor/LevelPicker2D.cs` | F2 map travel for playtesting |
| `App2d/Things/ThingTypeRegistry2D.cs` | Persisted thing keys → runtime kinds/editor descriptors |
| `App2d/Gameplay/World/SideScrollerThingSpawner2D.cs` | Actual enemy construction from placements |
| `App2d/Contracts/Player/TraversalMetrics2D.cs` | Movement tuning, measured jump profile, jump arc |
| `App2d.Core/Tiles/WorldTileMap2D.cs` | Combined map view and layout constraints |

The generator belongs in the game's `Levels` authoring layer. Keep simulation,
rendering, and SQLite responsibilities distinct. Runtime consumes the generated
asset through the same path as authored maps.

## Default world and configuration

The authoritative layout is `Assets/Static/levels/cavern/world.json`.
Debug loads this source content directly; the project copies static assets for
other build/publish configurations. Assigned generated maps are therefore durable
content under `Assets/Static`, not disposable runtime assets.

Current zones: `hub`, `sandbox-a` (display name **Combat Grounds**), `sandbox-b`,
and `cavern-world` (original playground). Combat Grounds has map ID `sandbox-a`
and numeric identity `2`. IDs deliberately remain stable despite the display rename.

The layout currently sets:

```json
{
  "startMapId": "main",
  "startAtArrival": true
}
```

Combat Grounds' map entry includes this configuration:

```json
{
  "id": "sandbox-a",
  "name": "Combat Grounds",
  "identity": 2,
  "file": "generated/map-2/seed-12345-<revision-id>/level.db",
  "generation": {
    "originX": 32768,
    "originY": -384,
    "settings": {
      "seed": 12345,
      "beats": 12,
      "difficulty": 0.3,
      "exploration": 0.2,
      "jumpRiseTiles": 3,
      "tilesetIndex": 2,
      "meleeEnemies": ["club-caveman", "shield-defender", "baby-triceratops"],
      "rangedEnemies": ["rock-thrower"]
    }
  }
}
```

The `file` example is schematic: read the actual manifest to find the active
revision. Settings in that manifest govern zone regeneration.
`docs/generator-settings.json` is only the separate standalone-draft example;
changing it does not change zone generation settings.

The generated map is 480 × 64 tiles with current defaults. Each tile is 32 world
units; chunks are 32 tiles. The origin is the map's lower-left corner. Y increases
upward. The floor fills local rows 0–11, so its top is at world Y=0 with origin
Y=-384. Placements are world-space positions, including the configured offset.

World maps must have matching tile/chunk sizes, align to the tile grid, and have
non-overlapping rectangular bounds. Gaps are empty space. Current world placement
is a playground layout, not a generated network of physical connections or portals.
F2 provides travel between maps.

## Run and regenerate

From the repository root, with the game stopped when rebuilding or regenerating:

```powershell
# Normal launch: no game arguments.
dotnet run --project App2d

# Edit generation.settings in world.json, then assign a fresh revision.
dotnet run --project App2d -- --generate-zone sandbox-a

# Launch normally again to load the new assignment.
dotnet run --project App2d
```

F1 toggles the existing editor; F2 opens the map picker. In the editor,
Page Up/Page Down switches maps. Terrain changes apply immediately; position-only
thing edits generally require a restart. Do not promise an in-game reroll button:
generation is currently exposed through the CLI.

Standalone drafts are still supported:

```powershell
dotnet run --project App2d -- --generate-level Assets/Work/levels/new-draft docs/generator-settings.json
dotnet run --project App2d -- --world Assets/Work/levels/new-draft/world.json
```

Standalone output must be a fresh directory. `Assets/Work` and `artifacts` are
ignored. Avoid mistaking a stale executable in `artifacts` for a current build.

## Generation and assignment pipeline

1. `ZoneLevelGenerator2D.Regenerate` reads the layout and selects a zone by ID.
2. It requires exactly one map in that zone, with a `generation` config.
3. It creates `generated/map-<identity>/seed-<seed>-<GUID>/` under the layout directory.
4. `CombatLevelGenerator2D.Create` generates terrain and placements at the requested
   origin, saves `level.db`, and writes its report/helper files.
5. A candidate world document changes only the selected map's `file` assignment;
   zone/map identities, config, and startup settings are retained.
6. The candidate is loaded through `WorldFile2D.Load`, checking layout compatibility
   through `WorldTileMap2D`, plus manifest/start-map validity.
7. `AtomicFile.WriteAllText` replaces the real manifest only after successful validation.

Old revisions are retained. Failed layout validation leaves the assignment unchanged,
but can leave an unassigned generated revision on disk. This is not a transaction
across all files, and there is no revision cleanup or rollback UI.

Every revision contains:

- `level.db`: terrain and position-only things; the actual assigned runtime asset.
- `generation.json`: settings, beat records, and generated placement records.
- `world.json`: helper manifest for playing the revision standalone.
- `zones.json`: legacy standalone helper regions naming each beat.

Important distinction: zone assignment loads the new **database**, not the revision's
helper world/zone JSON. The default bootstrap merges only the root world's
`zones.json`. Nested beat regions are not promoted into top-level world zones.
The helper beat-region output predates the final hierarchy and is a cleanup candidate.

## What the current grammar actually does

This is a small hard-coded constructive generator, not a general grammar engine.
Every beat is 40 tiles wide. Recipe selection prioritizes:

1. First beat: arrival.
2. Last beat: exit; penultimate beat: climax.
3. Release when two main-route fights have accumulated since the previous release.
4. Traversal when beat index modulo four equals two.
5. First fight introduces the first melee roster entry; second introduces a ranged enemy.
6. Remaining fights choose melee/practice/combined recipes using difficulty and randomness.

Release counting tracks main-route fight beats, not elapsed time or optional guards.
Traversal does not reset it. Climax/exit have priority over that release rule.

Terrain/encounters:

- Continuous solid floor throughout, with solid end walls.
- Arrival contains a player spawn and checkpoint; exit contains a goal.
- Traversal builds a raised three-part mound: widths 5/6/5 tiles, heights
  `rise / 2*rise / rise`, where rise is 1 through `jumpRiseTiles`.
- Introduction fights have one enemy. Other base counts are
  `1 + floor(difficulty * 3)`; combined fights have at least two; climax has at
  least two at difficulty >= 0.3.
- Enemies begin 15 tiles into the beat and are spaced six tiles apart.
- Combined fights/climax put the final enemy in a ranged role and elevate its
  five-tile platform by `jumpRiseTiles`. Ranged introduction stays on flat ground.
- Release adds a checkpoint.
- Optional exploration adds three ascending one-way platforms, a top overlook,
  a melee guard, and a checkpoint. It is an elevated detour, with no loot reward.

Difficulty is a coarse enemy-count/recipe control, not an encounter budget.
Only the first melee enemy is deliberately introduced; later picks do not track
whether the player has learned each enemy. There are no explicit combat gates.

Settings limits: 6–60 beats, difficulty/exploration in [0,1], jump rise 1–4 tiles,
an existing tileset index, and nonempty supported enemy rosters. Melee keys:
`club-caveman`, `shield-defender`, `baby-triceratops`, `shieldback`, `boiler-brute`,
`green-dinosaur`. Ranged keys: `rock-thrower`, `rival`.
Other registered gameplay enemies are not automatically allowed by the generator.

Main choices use `Random(seed)`; optional detours use `Random(seed ^ 0x51A7)`.
This keeps exploration changes from consuming the main-route random stream.
Reproducibility applies to generated content for this implementation/runtime;
GUID paths and database generation timestamps are not reproducible bytes.

## Startup, saves, and identities

`WorldFile2D.Load` moves `startMapId` to the front of the loaded map list. The first
enabled spawn then supplies the normal initial session position. The authored
manifest's zone ordering itself is not rewritten for startup selection.

`SideScrollerGame` skips saved-progress loading if either `startAtArrival` is true
or an explicit `--world` was supplied. This happens on every such launch, not only
after regeneration. Checkpoints still update in-session respawn and write saves.

- Default world with `startAtArrival: true`: regular save path plus `.playground`.
- Default world with it false: regular save store and normal resume behavior.
- Explicit `--world`: `player-save.json` beside that manifest, but ignored on startup.

Regeneration does not delete saves or store a content-revision fingerprint.
Disabling arrival startup can therefore expose stale checkpoint associations.
World thing IDs namespace each local ID with the stable map identity:
`((long)identity << 32) | localId`. Local IDs are generated in placement order and
can change meaning after a reroll; retaining map identity alone does not make
checkpoint identity safe across different generated revisions.

## Validation and evidence

Relevant tests:

- `App2d.Tests/Levels/CombatLevelGeneratorTests.cs`: reproducible content, mixed
  encounter roles, exploration independence, placement centers outside solid
  terrain across seeds, persistence/export, input validation, and a headless run
  with actual authored enemy assets.
- `App2d.Tests/Levels/ZoneLevelGeneratorTests.cs`: world-space offsets, assignment,
  start-map policy, stable map identity, preserving edited revisions, and rejecting
  overlap without changing the assignment.
- `App2d.Tests/Gameplay/World/WorldFileTests.cs`: layout loading, identities, and
  the shipped Combat Grounds default.
- Existing `WorldZoneTests` and `LevelDatabase2DTests` cover related formats.

The previous implementation session passed 33 relevant tests and launched the
game without arguments. This handoff session inspected current source; it did not
rerun those tests or evaluate gameplay quality.

```powershell
dotnet test App2d.Tests/App2d.Tests.csproj --artifacts-path artifacts/generator --filter 'FullyQualifiedName~ZoneLevelGeneratorTests|FullyQualifiedName~CombatLevelGeneratorTests|FullyQualifiedName~WorldFileTests|FullyQualifiedName~WorldZoneTests|FullyQualifiedName~LevelDatabase2DTests' -v minimal
```

The project targets .NET 10 on Windows and needs the installed Windows SDK.
The separate artifacts path avoids locked binaries when another game/Visual Studio
instance holds the normal output. Sandbox SDK-discovery access previously needed
an escalation. SQLite pooling can hold test databases open on Windows; existing
generator tests call `SqliteConnection.ClearAllPools()` for cleanup.
An existing nullable warning in `PersonAnimationDirector.cs` was unrelated.

Passing these checks does **not** establish route reachability, combat fairness,
enemy-body clearance, or fun. The placement test checks centers, not full bodies.
The smoke test starts actors and simulates; it does not complete the level.

## Recommended next improvements

These are suggestions for the next session, not additional user-authorized tasks.

1. **Playtest and improve a few recipes.** Compare seeds and difficulty settings.
   Current fixed-width, flat-floor fights will feel repetitive. Add varied approach,
   retreat space, platform arrangement, and meaningful enemy-role combinations.
2. **Separate planning from construction.** Introduce a beat/section plan and small
   recipe implementations with explicit inputs, output geometry, entry/exit surfaces,
   intent, and constraints. Keep that plan inspectable in `generation.json`.
3. **Validate actual traversal.** Reuse `TraversalMetrics2D.MeasureJump` and
   `BuildJumpArc`, together with the same player geometry/tuning used by the game.
   `jumpRiseTiles` currently caps geometry but is not linked to those measurements.
   Check horizontal reach, full-body clearance, landing width, optional detours,
   and approaches from rest. Add safety margins rather than requiring perfect inputs.
4. **Add role-aware budgets and teaching state.** Enemy counts miss combined threat,
   sightlines, retreat room, ranged coverage, and narrow terrain. Track introduced
   roles/types and control escalation. Use existing roles before adding new enemies.
5. **Improve reroll ergonomics.** Zone selection/config UI, preview, explicit reroll,
   revision selection, then section locking/partial regeneration. Preserve manual
   edits and no-argument launch. Current section indices are not durable identities.
6. **Make exploration useful.** Meaningful optional rewards and loops, then explicit
   zone connection/portal authoring if needed. Current overlooks are only detours.
7. **Tighten persistence.** Generator version in reports, clearer section metadata,
   removal of legacy beat-zone helper ambiguity, revision-aware saves, and safe
   unassigned-revision management. Publishing currently includes retained static
   revisions, so asset growth deserves attention.

For an initial improvement pass, focus on a few better encounter recipes and real
traversal validation. Keep world assignment/persistence working while changing the
generator; avoid replacing the whole system before comparing playable drafts.

## Working-tree caution

At handoff creation, the generator code/tests/config and several playground assets
were still untracked, with other modified gameplay/editor files also present.
Do not assume this feature is committed or reset unrelated changes. Run `git status`
and inspect the current tree before editing. In particular, preserve existing
session travel, level-picker, and weapon-test changes from other work.

Related docs: [user workflow](level-generation.md), [world maps](world-maps.md).
