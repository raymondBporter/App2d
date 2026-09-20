# Entity authoring and playtest

Character Studio now opens on the Player entity type. Use **Entities** to switch
between Player, Needle, Maul, Cinder and Scrap Hound. These are small working
examples for developing the authoring workflow, not finished combat designs.

## Editing

The left panel selects semantic actions. The stage previews authoritative action
time mapped onto a source clip. The inspector has four tabs:

- **Type**: stable ID, display name, health, movement, simple controller settings,
  ground alignment and notes. **Save as new type** creates an independent named
  copy; there is no implicit inheritance or parent lookup.
- **Look**: existing anatomy, head workshop, faces, color and weapons.
- **Action**: source clip, duration, looping, trim, contact mapping, weapon override,
  damage window, attack geometry, projectile size/speed and sound bindings.
- **Collision**: stable movement body plus body/head/leg/arm hurt regions. Follow
  anatomy, adjust padding/scale/offset, use a custom box, or disable a region.

The action timeline highlights the damage window in orange, the contact marker in
white, and the current time in green. The clip's contact position maps to the
action's contact time, with separate linear segments before and after it. Clip
start/end can coincide to make an explicit held-pose placeholder.

Enable collision overlays to see movement (cyan), hurt regions (green), and active
attack geometry (orange; muted when inactive). In **Collision**, drag the selected
hurt region in the viewport to offset it; Shift-drag resizes it. In **Action**, the
same gestures edit attack geometry. Sliders provide precise alternatives. Fit
motion includes movement and attack extents. Projectile previews show a spawn
shape and aim line; projectile size is also used in the playtest.

## Files and undo

Types use versioned `app2d-entity-type` JSON. The five examples live in
`Assets/Characters/entities`. Running from the repository opens these source files
for editing; a published standalone folder opens its packaged examples. **Save**
writes back to the opened file. **Open** also accepts the earlier appearance-only
look files. **Entities → Browse source motions** returns to the original library
browser; **New from current look** creates an entity using a compatible starter's
actions. The first entity pass supports Person and Quadruped.

Undo/redo includes appearance, head, type, action and collision changes. A slider
or viewport drag is grouped into one edit. Closing the application offers to save
unsaved documents. Unsaved documents survive entity switching. Head-only exports
remain compatible with the original browser workshop.

Refreshing motion assets does not regenerate or overwrite entity types. The
explicit `node tools/CharacterPipeline/create-starter-types.cjs` command resets the
five examples and should only be used when deliberately replacing them.

## Playtest

**Playtest** opens a small flat arena using snapshots of the open type documents,
including unsaved edits. **Restart / apply edits** rebuilds the arena. **Play as**
lets any type become the controlled character. Toggle AI, sound, collision or pause.

- A/D or arrows: movement.
- Space: jump.
- J: primary attack.
- K: shoot when a separate shoot action exists; otherwise primary attack.
- R: restart.

Player is the neutral sword/pistol example. Needle approaches and thrusts. Maul
uses a slower hammer swing and recovery. Cinder keeps its distance and shoots.
Scrap Hound chases and bites. Controllers are intentionally small policies, not a
behavior-tree editor. Sound IDs currently resolve to generated placeholder tones.
Timeline sounds are emitted once per action cycle; impact sounds require a hit.

The arena exercises ground movement, jumping and combat inside the tooling.
The main side-scroller also consumes these definitions; see the integration below.

## Main game integration

`dotnet run --project App2d` uses the authored Player, Needle, Maul, Cinder and
Scrap Hound. Debug runs read `Assets/Characters` directly, so save a type in Studio
and restart the game to apply it. Published games carry their own definitions,
Person/Quadruped libraries and face textures under `Assets/PointCharacters`.
Only referenced motion libraries load, once per game. Each renderer shares one
geometry workspace per library, with a small observation per actor and isolated
depth for each draw in normal scene order.

Existing level keys remain compatible: shieldback becomes Needle, boiler-brute
becomes Maul, rival becomes Cinder, and green-dinosaur becomes Scrap Hound. The
level editor displays the new names. Tumble props remain physics props. Additional
named types can be authored in Studio, but arbitrary type selection on level
placements still needs an editor/schema extension.

`AuthoredEnemy2D` uses the real terrain physics, streaming and session checkpoints.
Enemy movement dimensions, health, speed, range, cooldown, attack timing, damage,
weapons, appearance and collision regions come from the type. Enemy attack clocks,
projectiles, cues and hit history rewind with the session. Cinder bullets stop at
terrain, including terrain between the body and muzzle. Melee attacks hit once per
swing. Damage geometry uses posed convex regions; terrain uses the stable movement
box. Current player attacks are axis-aligned, so the combat adapter tests their
bounds against those regions.

One authoring unit maps to 40 game units. The player's movement height is rounded
to the level's four-unit clearance grid. The Player type supplies appearance,
health, movement dimensions, semantic animation bindings and the normal sword
attack's duration/window/damage/attached region. The gun muzzle comes from the
sampled pose. Jump/fall/land, wall grip/attack/shot, climb and balance select their
named bindings. Existing player traversal speeds, dash, gun charge/projectile
rules, downward bounce and unarmed combat remain controller rules; the latter
actions retime the selected animation to their existing gameplay duration. The
player still receives damage through its stable body box. Migrating those remaining
rules and player hurt regions is a separate tuning step, not implicit behavior of
editing the corresponding Studio fields.

The four enemy controllers are deliberately simple pursuit/attack placeholders.
They do not navigate gaps or separate crowds. Sound cue names map to the game's
existing sound effects. A session snapshots data at startup; there is no live
mutation of an active game from the editor.

## Runtime boundaries

`App2d.Core/Characters` owns the portable definitions, shared Person/Hound pose
evaluation, action-to-clip time mapping, attachment points and convex collision
regions. Rendering and collision derive from those same posed controls. Collision
uses simplified head/torso hulls and a filled leg envelope, not the tessellated ink
mesh. Arms are disabled initially. Quadruped legs are disabled in the hound example.
The stable movement box is independent of animated hurt regions and is fitted only
on explicit request. Region offsets mirror with facing.

`App2d.Gameplay/Entities/EntityPlaytest` runs a 120 Hz fixed-step simulation with no
graphics, editor or audio reference. It owns instance health, movement, action
clocks, attack sequences, hit deduplication, projectile sweeps and cue events.
Definitions and motion libraries are shared; rendering reuses one geometry
workspace per anatomy/library. The studio owns controls, audio playback and GPU
submission, including per-character depth isolation.

Horizontal travel removal currently anchors the root landmark to the start of
the action. This is a visible authoring option, not a finalized root-motion system.
Precise foot locking, action cancellation/combo rules, actor body separation,
bespoke sound assets and broader arena environments
remain future work. Collision and anatomy evaluation still allocate temporary
CPU arrays; this pass validates the workflow, not crowd performance.

## Checks

`--check` covers all imported motions, the existing JS parity fixtures, head
editing, every starter action at multiple times/facings, and entity save/load and
undo/redo. `--smoke <folder>` captures the source libraries, heads, weapons, five
entity types, action/collision tabs and the running arena using the real GPU path.
`App2d.Tests/EntityAuthoringTests.cs` checks time mapping, collision mirroring,
stable movement geometry, one-hit-per-action behavior and repeatable fixed-step
combat/cue output. `App2d.Gameplay.Tests/Enemies/AuthoredEntityGameTests.cs` checks
real-game spawn replacement, authored melee/hurt regions, projectile terrain
blocking and checkpoint replay. The game's `--render-smoke <folder>` also captures
the five authored characters in both directions across idle/walk/attack/death.
These checks are not large-crowd benchmarks.
