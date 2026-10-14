# Combat level generator playground

## Normal world workflow

Launch normally, with no arguments. The shipped world starts at **Combat Grounds**,
the generated map assigned to zone `sandbox-a`. F2 still travels to the hub,
Sandbox B, and the original playground. F1 edits the selected map.

`Assets/Static/levels/cavern/world.json` owns the layout and configuration:
world → zones → assigned maps → generated sections. The map's `generation`
property holds `originX`, `originY`, and `settings` (seed, beats, difficulty,
exploration, jump rise, tileset, and enemy rosters). Its `file` is the currently
assigned, editable database. Maps without `generation` remain authored content.
Sections are recorded in the map revision's `generation.json`; they are not
additional world zones.

After changing the zone settings, regenerate with the game stopped:

```powershell
dotnet run --project App2d -- --generate-zone sandbox-a
```

This creates a fresh map revision, validates it against the world layout, then
updates the assignment. Existing revisions and manual edits remain on disk.
Origins must align to the 32-unit grid and map rectangles cannot overlap.
The first version supports one generated map per zone.

`startMapId` chooses the arrival map. `startAtArrival: true` bypasses checkpoint
resume on startup, while checkpoints still work for deaths during play. These
playground launches use a separate save file and preserve the regular game save.
Set `startAtArrival` to false to restore normal saved-progress startup.
Normal launches load the assigned map without regenerating it.

## Standalone drafts

Generate a draft from the repository root, then play it:

```powershell
dotnet run --project App2d -- --generate-level Assets/Work/levels/draft-01 docs/generator-settings.json
dotnet run --project App2d -- --world Assets/Work/levels/draft-01/world.json
```

Change the seed in `docs/generator-settings.json`, then generate `draft-02`.
Output must be a fresh directory: rerolling never overwrites an edited draft.
F1 edits the generated terrain and things using the existing editor.
Each draft has its own player save. Explicit `--world` launches start at arrival;
checkpoints still work for respawning during the session.
The regular game still opens its usual world and resumes its saved checkpoint.

## Settings

* `seed`: reproducible terrain and placement choices for this generator version.
* `beats`: 6–60 sections, each 40 tiles wide.
* `difficulty`: 0–1. Controls enemy count (1–4), some recipe choices, and climax strength.
* `exploration`: 0–1 chance of an elevated optional detour in eligible sections.
* `jumpRiseTiles`: 1–4 tiles per step; choose a comfortable rise for your player.
* `tilesetIndex`: index in the game's existing terrain tileset list; 2 is prehistoric ground.
* `meleeEnemies`, `rangedEnemies`: existing thing type keys. The first melee type is introduced first.

## Grammar and recipes

Arrival → introduce melee → traversal → introduce ranged → release →
vary combat/traversal with a release after two fights → climax → exit.

Combat sections keep a flat approach and retreat space. Mixed encounters elevate
the ranged enemy. Traversal sections build connected stepped terrain with no pits.
Exploration adds ascending one-way platforms, a guarded overlook, and an optional
checkpoint. It is a short detour above the main route, not a branching room graph.
There is no loot reward system in this prototype.

`generation.json` records settings, recipes, and enemy placements. `zones.json`
gives every beat a named region. `level.db` and `world.json` are ordinary authored
formats: the runtime does not generate terrain.

This is a first playable grammar, not a guarantee of fun or simulated reachability.
Rise limits and continuous ground keep traversal conservative; combat combinations,
enemy patrol behavior, sightlines, and optional-platform jumps need playtesting.
The prototype does not use flying enemies yet. Tune one parameter at a time and
compare seeds before adding more recipes.
