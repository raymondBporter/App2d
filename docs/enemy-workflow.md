# Enemy production workflow

The first worked examples are [Club caveman](club-caveman.md) and
[Rock thrower](rock-thrower.md), followed by [Baby triceratops](baby-triceratops.md) and [Shell-shield defender](shield-defender.md)
as the first charger using a quadruped rig. This process aims to deliver playable, editable
enemies quickly, then improve them through play. A generated pose is a starting
point; passing tests does not establish that an enemy feels good.

## 1. Define the player decision

Write the role, visible tell, commitment point, escape option and punish window.
Use guaranteed player abilities. Decide normal-hit interruption before authoring.
For these two, normal damage cancels the preparation and recovery action. Released
rocks remain in flight after interruption or death.

| Enemy | Pressure | Tell | Commitment | Counter |
| --- | --- | --- | --- | --- |
| Club caveman | Pursues into melee | Slow club lift and overhead hold | Facing locks when the action starts | Walk away during lift; punish the low recovery |
| Rock thrower | Blocks an approach with an arc | Bag search, overhead stone, brief hold | Facing and target position lock when the action starts | Move after commitment; close during search/recovery |

## 2. Make the smallest editable asset set

Use stable semantic IDs under `Assets/Characters/authored`. Keep the curated
reference under `Assets/Sources/characters/prehistoric/<id>/reference.png`.
Prefer existing human rig, locomotion, hit and death roles when they fit. Give
each enemy its own entity, build, equipment and defining attack clip. New rigs
are appropriate when the silhouette or anatomy requires them.

The entity owns health, movement box, controller settings, hit/projectile
definitions, marker-bound cues and equipment. The clip owns poses, expressions,
contacts and markers. Props own art and named grip/tip/release points. Avoid
copying gameplay timings into the renderer; draw the simulation's final pose.

Explicit generator scripts are optional seed tools, never automatic build steps.
Record their overwrite scope. Once an asset has been edited manually, regenerate
only intentionally. Existing scripts:

```powershell
python tools/ArtPipeline/build_club_caveman.py
python tools/ArtPipeline/build_rock_thrower.py
python tools/ArtPipeline/build_baby_triceratops.py
python tools/ArtPipeline/build_shield_defender.py
```

## 3. Author a readable action mathematically

Start with a few solved rig poses, not many unrelated pictures. Use slow
anticipation, a held commitment tell, a short decisive strike/release, and longer
recovery. Keep feet planted during stationary actions; gait follows actual ground
distance during movement. Respect arm reach and equipment depth, and inspect
both facings at the game's 40 world units per authored unit.

Bind damage/release/sound to named clip markers. A melee hit region should follow
the equipment point shown in the pose. A throwable disappears from the hand at
release and becomes a simulated projectile. Avoid hit regions that extend far
beyond the drawn attack. Give expressions intent without hiding the tell.

## 4. Connect behavior and feedback

Use authored controller settings for ordinary differences. Both examples share
`AuthoredEntityEnemy2D`: terrain-safe pursuit, attack commitment, interruptible
reactions, final-pose collision and rollback. Rock thrower adds a bounded retreat
with a mandatory pause, plus a ballistic projectile configured by gravity and
flight time. Straight gun shots keep zero gravity and zero flight time.

New mutable controller/projectile fields must be captured and restored, including
sampled targets and retreat timers. Streamed-out actors clear projectiles. Rocks
sweep through terrain and player collision, then disappear on impact or expiry.
There is no automatic body contact damage for these two enemies.

Add cue mappings and reuse existing placeholder sound assets first. Record the
mapping in the enemy notes. If downloading replacements, retain the source and
license before promoting them to durable content. Do not add a network dependency
to the runtime.

Register a dedicated Things palette entry and spawner mapping. Missing authored
content should fail clearly. Add a solo placement before a mixed encounter.

## 5. Verify behavior and review visuals

Test decisions with consequences, not copies of the implementation: one hit per
attack, escape before impact, commitment after crossing the player, interruption,
death, walls/ledges, projectile obstruction/expiry, and exact rollback replay.
Use terrain-backed simulation for pursuit and paired encounters. Keep existing
straight-shot and melee regression tests passing.

```powershell
dotnet build App2d.slnx
dotnet test App2d.Gameplay.Tests
dotnet run --project App2d.CharacterStudio -- --smoke-wardrobe artifacts/prehistoric-enemies
python tools/ArtPipeline/pack_enemy_reviews.py artifacts/prehistoric-enemies
dotnet run --project App2d -- --render-smoke artifacts/prehistoric-game
```

The wardrobe review emits pose sheets and action sequences for the enemy roster and attack frame sequences
at 24 fps. The optional packer makes GIFs from those native rendered frames.
The rock animation review illustrates a fixed target 3.8 authored units ahead;
actual gameplay aims at the sampled player position. The review is a visual aid,
not a substitute for gameplay collision tests.

Inspect for hidden equipment, outlines cutting across faces, sliding feet,
unreadable game-size poses, visual/damage mismatches, and abrupt transitions.
Fix functional/readability problems immediately; leave aesthetic tuning editable.

## 6. Hand off for play and record the result

Deliver entity/clip/prop files, tuning notes, solo and paired locations, visual
reviews, verification results and known limits. Use [the enemy note template](enemy-template.md)
for subsequent enemies. Do not claim playtested feel from automated tests.

During the first playtest, answer: can the player read the tell, escape with basic
movement, reach the punish window, and understand why damage happened? Tune one
variable at a time: timing, distance/speed, then damage/health. Test the solo enemy
before tuning the pair. Prefer encounter placement and timing over extra health.

For guards, verify attack direction and posed shield coverage independently. Test shield-only projectile contact, rear and uncovered body hits, commitment, and the exposed recovery window. A block must stop the attack without emitting damage feedback.
