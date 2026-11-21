# Characters rewrite: status and open items

Written 2026-11-21. Everything below is uncommitted in the main checkout unless it says otherwise.

## What exists now

New code, all verified against the old evaluator and drawing by tests:

| Piece | Where |
|---|---|
| Skeleton, joints, limits, limbs, mounts, two-bone IK | `App2d.Core/Kinematics` |
| Keyed tracks and skeleton clips (overlay, contacts, travel, mount turns, layers) | `App2d.Core/Animation/Track.cs`, `SkeletonClip2D.cs` |
| Attachments and their painter | `App2d.Core/Characters/Attachments` |
| Person skeleton (hand-written), body, clips, wardrobe | `App2d.Core/Characters/Skeletons/Person*.cs` |
| Quadruped, baby triceratops, pterosaur, stalker skeletons and clips | `App2d.Core/Characters/Skeletons` |
| Person face: 14 shapes and 17 expressions, no rig (generated) | `App2d.Core/Characters/Attachments/Face.cs`, `Skeletons/PersonFace.cs` |
| Person demo | `App2d.Demos/PersonDemo.cs` |

The game still runs on the old path. Nothing old has been deleted since the first cleanup commit (`77976a86`).

## Generated files

`PersonClips.cs`, `PersonWardrobe.cs`, `PersonFace.cs`, and the Quadruped, BabyTriceratops, Pterosaur and Stalker files are generated from the JSON
while the JSON exists. If you edit a JSON rig, clip or part, a test fails until you regenerate:

```powershell
$env:APP2D_WRITE_CLIPS = "1"; dotnet test App2d.Tests --filter "FullyQualifiedName~Tests.Characters"; $env:APP2D_WRITE_CLIPS = $null
```

When the JSON is retired, delete the `Legacy*` files in `App2d.Tests/Characters` and the "up to date" tests; the C# files then
become hand-edited.

## Left to do before the switch

1. **Mirroring.** Right-side shoes and gloves are mirrored today; a mount cannot mirror, so it needs a flipped copy of the outline.
2. **The centaur.** A quadruped lower half and a person upper half; its clips only pick what each half plays.
3. **Game logic that picks mounts and reads points.** Sword in hand or on back by clip markers, the blade tip for the sword
   trail, the gun muzzle. `PosedSkeleton2D.MountFrame` and `PersonWardrobe.SwordTip` give the points; nothing calls them yet.
4. **The switch itself**, then deleting the old rig, evaluator, parts, variants and parameter system.

## Decisions waiting on you

- **Shared mesh names.** In `PersonWardrobe.cs` a shared mesh is named after the first part that used it, for example
  `BruteBeardMesh1` is used by three characters. Better names are yours to choose.
- **Duplicate wardrobe functions.** `Boot`, `BootLeft` and `BootRight` (and the sandals, beards, hair, hide wraps) are separate
  functions over the same meshes, as in the JSON. They can collapse once the JSON is gone.
- **Arm rest pose.** The authored arm rest has the elbow on the opposite side from the IK bend, so the rig's rest is never
  what is shown. `PersonSkeleton` keeps the authored numbers to stay comparable; change them to the solved pose after the switch.
- **Shoulder and upper arm** are two joints at the same spot, as authored. Merging them is optional.
- **Foot layer.** A foot keeps its resting layer when a clip keys the hip's layer, because that is what the old rig does.
  Decide after the switch whether a foot should follow its leg.
- **Material type.** Attachments reuse the old `RenderMaterialDefinition2D`. It wants a simpler replacement.
- **Gameplay in Characters.** The 11 "character" parts hold colliders, health, attacks and AI settings (about 2,660 lines in
  Core). Suggested: move to the game.
- **The clip sync tests.** If they get in the way while you edit JSON, they can be removed.
- **Face blending.** An expression is only numbers, so two can be mixed; nothing does that yet, and expressions still snap.
- **Who picks the expression.** Clips key it (28 tracks, not converted) and the game overrides it. That choice is game logic.
- **Key trimming.** About 680 keys in the person clips are exact straight-line in-betweens and could be dropped.
- **Unloaded assets.** 183 MB under `Assets/Characters` outside `authored` is import source no C# loads; six template files too.

## Things to know

- A smaller layer is nearer the viewer. Left limbs are in front of the body.
- Every person clip was authored at a leg length of 0.99 against a rig leg of 0.79; the generated clips have that 0.8 baked in.
- Two tests outside Characters failed once under a full parallel run and passed on rerun: a level-generator test and an
  allocation test.
- The Person demo builds but was first run by you, not by the assistant.
- This session works in a worktree and copies files into the main checkout, because the app blocks direct writes there.
