# Characters rewrite: status and open items

Updated 2026-11-21, after the switch. The game runs on the new code; the old rig, evaluator, parts, variants, parameter
system and their JSON are deleted.

## What a character is now

| Piece | Where |
|---|---|
| Skeleton, joints, limits, limbs, mounts, two-bone IK, grafting one skeleton onto another | `App2d.Core/Kinematics` |
| Keyed tracks and skeleton clips (overlay, contacts, travel, mount turns, layers, expressions) | `App2d.Core/Animation` |
| Drawn pieces (`Attachment` kinds), their painter, the face | `App2d.Core/Characters/Attachments` |
| A character: skeleton, body, worn things, face, clips by role | `App2d.Core/Characters/Character.cs` |
| Pose, planted feet, the marker names clips use | `CharacterPose.cs`, `PlantedLimbs.cs`, `ClipMarkers.cs` |
| Person, quadruped, pterosaur, stalker, centaur: skeletons, clips, bodies, wardrobe, face | `App2d.Core/Characters/Skeletons` |
| A character in play (`Combatant`): its actions, hits, cues, projectiles, movement shape and colliders | `App2d/Gameplay/Characters/Combatant.cs` |
| Animator and hit reaction | `App2d/Gameplay/Characters/CharacterAnimator.cs` |
| Colliders in the world and who hits whom | `App2d/Gameplay/Characters/CharacterColliders.cs` |
| The game's cast: the hero and nine enemies, with their AI numbers | `App2d/Gameplay/Characters/GameCharacters.cs` |
| Enemy, hero timing, hero drawing, wardrobe | `CharacterEnemy2D`, `Hero2D`, `HeroPresentation2D`, `PlayerWardrobe2D` |
| Level objects (lifts, crates): a small format of their own | `App2d.Core/Objects/ObjectAsset2D.cs` |

Everything under `Skeletons` is plain C# now. Nothing regenerates it; edit it by hand and rename with the IDE.

Core holds what a character looks like and how it is posed. Everything about play is in the game project: `Combatant` is a
`Character` with actions, a movement shape and colliders added.

## How it was checked

Before the old code was deleted, tests held the new code to it: every creature body, every worn and held thing (and its
mirror), the face, the centaur, and for all ten characters what is drawn, where each collider is, when each hit opens, where
the gun fires from, and the animator step for step over 9,000 steps. Those tests went with the old code. What remains is
`App2d.Tests/Characters/CharacterTests.cs`, which tests the new code on its own.

## Differences from before, on purpose

- **The centaur's rider** is nine tenths of a person in everything. Before, its joints were nine tenths apart but its head,
  hands and torso were drawn full size.
- **The centaur stands** where the quadruped stands (body a quarter of a unit behind the origin), not over the origin.
- **Level objects** are `app2d-object` files with shapes, a fill and an optional outline. The eight lifts and the two shared
  objects (`Assets/Static/levels/objects`) were converted. A level still names a shared one as `parts/<name>.json`.
- **A wardrobe item that replaces body art** is gone; nothing used it.
- **Numbers are rounded to four decimals** in every file under `Skeletons` (5,496 of 14,436 changed). Drawings moved by at
  most 0.0006 units and poses by less than 0.001, with one exception: key times moved by up to 0.00005 s, so in the fastest
  frames of the sword swings the blade's angle is up to 0.3 degrees off for one frame.
- **588 keys were removed** from the clips: each lay on the straight line between its neighbours, or repeated them. Poses
  moved by at most 0.0003 units. A key between eased segments was kept unless all three values were the same.
- **The arena "player" entity** (the platformer one with a spear) was not carried over; nothing spawned it.

## Decisions waiting on you

- **Arm rest pose.** The arm's rest has the elbow on the side IK never uses. `PersonSkeleton` keeps those numbers.
- **Shoulder and upper arm** are two joints at the same spot. Merging them is optional.
- **Foot layer.** A foot keeps its resting layer when a clip keys the hip's layer.
- **Face blending.** Expressions still snap from one to the next.
- **Unloaded assets.** `Assets/Characters` (181 MB of import source) is no longer read by anything. Not deleted.
- **Sprite enemies.** `Shieldback` and `Rival` still have sprite versions that only run when a session is made without the
  cast (tests). The game never uses them.
- **Names that still say "authored".** `AuthoredObject2D`, `CreateAuthoredWorldThings`, `GameWorldUnits2D.AuthoredToWorld`.
- **`tools/ClothLab`** does not build; it used puppet types that were removed before this work.
- **Unused points in `PersonWardrobe`.** Only `SwordTip` is read. `PistolMuzzle`, `HammerTip` and the other `...Tip` fields
  are read by nothing; the muzzle the game uses is on the worn pistol in `GameCharacters`.

## Things to know

- A smaller layer is nearer the viewer. Left limbs are in front of the body.
- Every person clip was made at a leg length of 0.99 against a rig leg of 0.79; the clips have that 0.8 worked in.
- Two tests fail only in a fresh worktree, because a tileset is not checked in: both in `ViewportTerrainSource2DTests`.
- The switch was checked by tests, not by eye. Play it once: walk, swing all three sword cuts, shoot, climb, take a hit,
  and meet each enemy.
