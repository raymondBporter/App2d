# Character migration baseline (P0a, P0b, P0c)

Date: 2026-10-23. Passes P0a, P0b and P0c of the [Character system and Studio migration plan](character-system-migration-plan.md), section 5, "P0. Establish a trustworthy baseline and conversion cases".

This file is the evidence record for P0a and P0b: the exact source snapshot, a schema-aware inventory of the authored character library with its reference graph, a fresh run of the existing character, editor, rendering and combat checks with pre-existing failures listed separately, (section 6) the reference set the conversion passes compare against, and (section 7) the conversion disposition of every general-affine and point-specific case in that set. Nothing was converted. Numbers here come from the run on this date; rerun the commands in "How to reproduce" before comparing anything against them.

## 1. Source snapshot

| Item | Value |
| --- | --- |
| Branch / commit | `main` at `769d60b58b9181de91caac69410f2395fa3c626d` ("Merge origin/main and port rounded limb paths onto the reorganized geometry", 2026-10-23 12:04 -0500) |
| Working tree | Clean: `git status --porcelain` empty, no untracked files, no unrelated edits to preserve |
| Toolchain | .NET SDK 10.0.303, Windows 11 Pro 10.0.26200; projects target `net10.0-windows10.0.19041.0` |
| Authored root | `Assets/Characters/authored` (Studio and Seeds resolve it by walking up from the working directory with `preferSource`, so the checked-in source tree was exercised, not `Assets/Runtime`) |

The inventory below was produced by loading the folder through the real readers (`AuthoredCatalog.Load` and `ModelTemplateCatalog.Load` in App2d.Core) from a throwaway console program outside the repo, then printing what the typed objects contain. Every omitted JSON field therefore carries the default its C# type declares; nothing was inferred from the text. The defaults that matter are quoted at the head of each section.

## 2. Test baseline

### 2.1 Full App2d.Tests run

~~~powershell
dotnet test App2d.Tests/App2d.Tests.csproj --logger "trx;LogFileName=full.trx" --results-directory <scratch>/p0a/tests
~~~

Result: Failed 7, Passed 2337, Skipped 0, Total 2344, 1 m 36 s. The seven failures are all pre-existing; none involves the character, editor, rendering or combat suites that the plan names.

| Pre-existing failure | Cause | Relevance to this migration |
| --- | --- | --- |
| `Presentation.World.WorldPresentationTests.CameraTerrainSurvivesSimulationStreamingChangesAndUnloadsWhenOutOfView` | `FileNotFoundException: Tileset manifest was not found` (the `dark-cave` tileset is absent from `Assets/Static`) | None (world terrain presentation) |
| `Presentation.World.WorldPresentationTests.WorldViewsStayIndependentUntilNewStateArrivesAndRemoveReplacedPlatforms` | Same missing tileset | None |
| `Presentation.World.ObjectLevelTests.LevelReloadsObjectsAndPresentationDropsTheirVisuals` | Same missing tileset | None |
| `Presentation.World.LadderTerrainTests.LadderArtUpdatesAcrossChunkBoundaryAndNeverCreatesSolids(tilesetId: "dark-cave")` | Same missing tileset | None |
| `Gameplay.World.AuthoredObject2DTests.SnapshotPresentationPreservesGenericGeometryAndOnlyMovesWhenStateArrives` | Same missing tileset | None |
| `Trials.TrialsCourse2DTests.GameUsesConfiguredWidthForTheActualCollidableTerrain` | Same missing tileset | None |
| `Geometry.TriangleMeshBuilder3DTests.FailedVertexFactoryPreservesThePreviousMeshAndVersion` | Expects mesh count 1, gets 2 | None (3D mesh builder versioning; unrelated to character geometry) |

These failures must stay listed as pre-existing in later passes; a pass that touches them should say so explicitly rather than absorbing them into its own evidence.

### 2.2 Suites named in plan section 7

All classes in the section 7 table exist and pass. Counts are from the full run above.

| Area | Class | Passed / total |
| --- | --- | --- |
| Rig/IK | BoneAuthoringTests | 3 / 3 |
| Rig/IK | RigConstraintTests | 29 / 29 |
| Rig/IK | PoseEvaluatorTests | 11 / 11 |
| Rig/IK | ResolvedModelTests | 9 / 9 |
| Rig/IK | NativeRigTests | 18 / 18 |
| Proportions/motion | AppearanceAndBuildTests | 12 / 12 |
| Proportions/motion | HumanProportionsTests | 8 / 8 |
| Proportions/motion | PersonVariantMotionTests | 26 / 26 |
| Proportions/motion | PersonStudyConversionTests | 4 / 4 |
| Geometry/appearance | CurveAttachmentTests | 24 / 24 |
| Geometry/appearance | CutoutAttachmentTests | 14 / 14 |
| Geometry/appearance | PartPaintTests | 6 / 6 |
| Geometry/appearance | ModelDrawingTests | 1 / 1 |
| Geometry/appearance | SlotTimelineTests | 11 / 11 |
| Parts/variants | ModelTemplateTests | 21 / 21 |
| Parts/variants | ModelEditingTests | 6 / 6 |
| Parts/variants | RepeatedAuthoringTests | 10 / 10 |
| Parts/variants | AppearanceAuthoringTests | 7 / 7 |
| 2D/3D/rendering | WeaponOrientationTests | 9 / 9 |
| 2D/3D/rendering | Rendering.PointCharacterProjectionTests | 7 / 7 |
| 2D/3D/rendering | Rendering.CharacterBufferRenderingTests | 6 / 6 |
| 2D/3D/rendering | Rendering.CharacterActorRenderingTests | 3 / 3 |
| Collision/combat | EntityRegionShapeTests | 8 / 8 |
| Collision/combat | EntityRuntimeTests | 17 / 17 |
| Editor | EditorSessionTests | 15 / 15 |
| Editor | AnimationEditingTests | 14 / 14 |

Namespace totals from the same run, for the "existing collision/physics and game combat tests" and "gameplay simulation/state round-trip tests" rows that name no single class:

| Namespace | Passed / total |
| --- | --- |
| App2d.Tests.Authored | 434 / 434 |
| App2d.Tests.Rendering | 163 / 163 |
| App2d.Tests.Collision | 53 / 53 |
| App2d.Tests.Physics | 52 / 52 |
| App2d.Tests.Constraints | 6 / 6 |
| App2d.Tests.Animation | 17 / 17 |
| App2d.Tests.Gameplay.Combat | 11 / 11 |
| App2d.Tests.Gameplay.Entities | 11 / 11 |
| App2d.Tests.Gameplay.Enemies | 81 / 81 |
| App2d.Tests.Gameplay.Persons | 111 / 111 |
| App2d.Tests.Gameplay.Player.Weapons | 4 / 4 |
| App2d.Tests.Gameplay.Simulation | 18 / 18 |
| App2d.Tests.Gameplay.World | 107 / 108 (pre-existing failure above) |
| App2d.Tests.Presentation.Persons | 50 / 50 |
| App2d.Tests.Presentation.Enemies | 26 / 26 |
| App2d.Tests.Editor | 8 / 8 |
| App2d.Tests.EditorUi | 8 / 8 |

### 2.3 The plan's own Authored command

~~~powershell
dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"
~~~

Result: Passed 434, Failed 0, Skipped 0, Total 434, 9 s.

## 3. Studio smoke paths and CharacterSeeds contact study

Each command was run once from the repository root with a fresh empty output directory under a disposable scratch folder (`<scratch>/p0a/smoke/<command>`), after the test runs above, on the same checkout. Every process exited 0. The frames were produced and spot-checked for being real renders (the editor smoke's first frame shows the Animate shell with the Person walk compared across three builds), but no frame-by-frame visual review against a reference was done in this pass; that review is P0b's job, with the reference set and tolerances it defines. A successful exit here records that the current code runs these paths, not that their output is approved.

| Command | Exit | Output | What the report says |
| --- | --- | --- | --- |
| `--smoke-editor` | 0 | 41 frames, `editor-smoke.txt`, a `workspace` copy of the authored tree plus `scribble-walk.puppet.json` | 50 `PASS` lines, 0 failures, over 40 recorded frames: shared walk compare, new tall variant, build change while walking, drag as one undo step, save and reopen, bone-constraint and contact clips reopening, rounded-path inspector on the Person arm |
| `--smoke-editor-surface` | 0 | 34 frames (`animation-control`, `animation-slot`, model views, curve facial studies at 1200x800 and 1600x940) | Rendered inspectors and views at two sizes and scales; no textual checks |
| `--smoke-motion` | 0 | 72 frames, `motion-proof.txt` | person-walk, person-run and person-heavy-walk on person, tall-thin and short-broad: strides 0.400 / 0.480 / 0.328 (walk) and 1.280 / 1.536 / 1.050 (run); max contact residual <= 2.8e-8, max reach residual <= 4.5e-8 |
| `--smoke-weapons` | 0 | `weapon-turntable.png`, `sword-swing.png`, `weapons.txt` | Turntable rows sword, pistol, hammer, spear, sheath at twist 0..180 and twist 30 with tilt 45; sword swing at six times, both facings |
| `--smoke-wardrobe` | 0 | 310 frames, `wardrobe.txt` | Hero, maul brute, cinder gunner: both facings, game scale, idle/run/attack and climbing back view or death recoil, packaged assets and the shared depth-tested renderer |
| `--smoke-quadrupeds` | 0 | `triceratops-poses.png`, `triceratops-silhouette.png`, `quadrupeds.txt` | Pose rows idle, scrape, head-down, walk, rush, brake, run, recover at four times; silhouette in both facings |
| `--smoke-pterosaur` | 0 | `pterosaur-poses.png`, `pterosaur-silhouette.png`, `pterosaur.txt` | Rows hover, fold, dive, brake, low-recovery, reset at four times; artwork and poses only |
| CharacterSeeds `--contact-study` | 0 | 900 frames, `contact-report.txt`, `index.html` viewer | Scenarios miss, hit, kill, run, reverse: hit/kill/run/reverse each register one contact at tick 19 on entity 2 (kill reports killed = true); miss registers none; final player X -368.00 (miss, hit, kill), 231.73 (run), -921.00 facing -1 (reverse) |

The motion residuals and contact ticks above are the first numeric reference values for P0b to compare against; they were observed, not asserted, and no tolerance has been attached to them yet.

## 4. Inventory summary and observations for P0b/P0c

Counts agree with plan section 2: 7 models, 7 variants, 91 animations, 30 props, 11 entities (all 11 compile), 5 templates, 1 physics prop. The catalog and template catalog load with zero errors. Every file is at the reader's current schema version (model 5, variant 4, clip 3, prop 3, entity 2, template 1, physics prop 1), so there is no mixed-version content to convert before the migration's own conversions.

Facts the later P0 passes and the conversion decisions depend on:

- **All IK is point IK.** 23 chains across person, stalker, quadruped, triceratops and baby-triceratops-rig; every one uses `solver: point` (the `bone` solver has no shipped user) and lives in `chains` (the typed `constraints` list is empty in every model). Pterosaur and curve-face have no chains.
- **No control declares a length.** `length` is 0 on all 149 controls; `restAngle` is 0 everywhere. Only curve-face has `transform` set (`left-eye`, `right-eye`, the two affine controls the plan mentions); every other control is a model-space point.
- **Scale tracks occur only in curve-face clips**: blink, crossed-eyes, expressions and wink animate `scale` on `left-eye`/`right-eye` (intentional eye squash). No clip uses `shear`. No track sets an explicit `scale` measure; all inherit the target control's.
- **Orient tracks (socket orientation) occur in 15 person clips**, all weapon or throw actions (sword set, gun set, hammer/club slams, rock throw, death). These are the planar-to-3D weapon cases for P1c.
- **Face tracks occur in 28 person clips**, using the fixed expression vocabulary (`relaxed`, `focused`, `determined`, `angry`, `hurt`, `knocked-out`, `strained`, `worried`, `panic`, `happy`, `delighted`, `none`). The procedural face is the `head` part of person (`face: relaxed`, `faceX .065`); variants override `face` per part. The curve-face model is the only authored curve face and the only wave user (`hair` part, amplitude .045, 5 cycles, phase control `hair-wave`).
- **Contacts are widespread**: 56 clips carry foot/IK contacts, including every quadruped/triceratops/baby gait and every person locomotion clip. 10 keys carry an explicit IK bend override.
- **Depth values are absolute character layers**: part `depth` ranges roughly -0.31..0.12 and is authored per part, not relative to a parent; sockets `back`/`back-view` carry explicit Z offsets (±0.2). This is the double-counting risk named in plan section 3.3.
- **Hit anchoring takes three routes**: prop point (`hammer.tip`, `wooden-club.tip`, `spear.tip`, `scavenged-shell.tip`), socket (`head-art`, `stinger`) and, for the hero's three sword actions, neither (an actor-frame offset from the feet, the "actor box" of the swing work). Every route goes through `EntityCollision.Anchor`.
- **Equipment is bound by prop asset ID per wearer** (for example `pistol@gun-hand` on both cinder-gunner and hero, `spear@right-grip` on player and spear-guard); no entity equips the same prop twice, so the plan's left/right instancing case (R05) has no shipped example yet. Footwear uses separate `boot-left`/`boot-right` and `sandal-left`/`sandal-right` assets.
- **Socket IDs are shared across models**: `head-art` and `body-art` exist on person, pterosaur, quadruped, triceratops and baby-triceratops-rig, so hair/clothing props that name them would attach to any of those; only person has the hand/foot art sockets.
- **Code-only bindings** (data shows them as unreferenced, code uses them): 14 player clips (`player-balance-*`, `player-celebrate`, `player-climb*`, `player-dash`, `player-gun-aim`, `player-gun-charge`, `player-heal-gather`, `player-land`, `player-sword-down-attack`, `player-sword-sheathe`, `player-wall-grip`) are selected by `App2d/Presentation/Persons/PersonAnimationDirector.cs` and written by CharacterSeeds `PlayerMoves`; props `hair-short-back` and `sheath` by `PersonLoadout`; variant `short-broad` by `PersonBuild` presets, `PersonTemplate` and the Studio smokes. These are the "code-specific bindings" that P9c/P11 must move into profiles; a conversion report that only follows JSON references would miss them.
- **Duplicated structure**: quadruped and triceratops share the same 18 controls, 4 chains and 30 of 34 parts; baby-triceratops-rig is the same rig at smaller part scales. These are the obvious first candidates for shared anatomy templates (P10) and for measuring whether a conversion preserves them identically.
- **Unused shipped content**: models curve-face, quadruped and triceratops have no entity (curve-face and the quadrupeds are template/preview content; the enemies use baby-triceratops-rig); baby-triceratops-rig and pterosaur have no template.
- **Representative bundles for P0b** map to these asset IDs: Person tall-thin / short-broad (variants, entity spear-guard uses tall-thin), Hero (`hero` on `person`), Cinder (`cinder-gunner` on variant `cinder`), Maul brute (`maul-brute` on variant `brute`, motion set `heavy`), quadruped/triceratops gait (`quadruped-walk`/`run`, `triceratops-walk`/`run`, `baby-triceratops-charge` with 7 contacts), pterosaur wing bindings (`far-wing`/`near-wing` polygons with 6 point bindings each on `body`), curve-face (`curve-face` model and its 9 clips). The small nonhumanoid mechanical linkage fixture has no authored asset and is to be written as a shared test fixture in P0b.
- **Tolerances** have not been set; P0b records them per observable quantity before any comparison, as section 7 requires.

## 5. How to reproduce

~~~powershell
git rev-parse HEAD   # expect 769d60b58b9181de91caac69410f2395fa3c626d for this baseline
dotnet test App2d.Tests/App2d.Tests.csproj --logger "trx;LogFileName=full.trx" --results-directory <scratch>/p0a/tests
dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"
dotnet run --project App2d.CharacterStudio -- --smoke-editor <scratch>/p0a/smoke/smoke-editor
dotnet run --project App2d.CharacterStudio -- --smoke-editor-surface <scratch>/p0a/smoke/smoke-editor-surface
dotnet run --project App2d.CharacterStudio -- --smoke-motion <scratch>/p0a/smoke/smoke-motion
dotnet run --project App2d.CharacterStudio -- --smoke-weapons <scratch>/p0a/smoke/smoke-weapons
dotnet run --project App2d.CharacterStudio -- --smoke-wardrobe <scratch>/p0a/smoke/smoke-wardrobe
dotnet run --project App2d.CharacterStudio -- --smoke-quadrupeds <scratch>/p0a/smoke/smoke-quadrupeds
dotnet run --project App2d.CharacterStudio -- --smoke-pterosaur <scratch>/p0a/smoke/smoke-pterosaur
dotnet run --project App2d.CharacterSeeds -- --contact-study <scratch>/p0a/smoke/contact-study
~~~

Use a new empty directory for `<scratch>`; the editor smoke deletes and recreates its `workspace` subfolder under its output directory. The inventory program is not checked in; it is a ~250-line console project that references `App2d.Core`, calls `AuthoredCatalog.Load(root)` and `ModelTemplateCatalog.Load(root)`, and prints the properties listed in section 8. Regenerate it the same way when the schemas change rather than editing section 8 by hand.

## 6. P0b reference set

The reference set lives in shared tests, as plan section 5 asks: [MigrationReferenceTests](../App2d.Tests/Authored/MigrationReferenceTests.cs) samples each case through the current `PoseEvaluator`, `ActorPose`, `EntityCollision`, `PersonLoadout` and `PartContours2D` paths, and `AuthoredArena` for combat, and compares the result with [character-migration-references.json](../App2d.Tests/Authored/References/character-migration-references.json). The file was generated once from this checkout (commit 12315aaa plus the P0b test files) and is now fixed; the test fails on any drift outside the tolerances below. The mechanical linkage fixture is `TestModels.Linkage()` with its two clips, so it does not depend on an experimental app.

~~~powershell
dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~MigrationReferenceTests"      # compare
$env:APP2D_WRITE_REFERENCES = "1"; dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~MigrationReferenceTests"   # regenerate (approved differences only)
~~~

### 6.1 Tolerances

Set per observable quantity before any comparison was made, as section 7 of the plan requires. They are stored in the reference file and applied by JSON path, so a representation change that keeps these quantities passes and one that moves them fails.

| Quantity | Tolerance | Basis |
| --- | --- | --- |
| Control, socket, prop-point, contact-target and region positions | 1e-4 authored units | Float evaluation reproduces itself to ~1e-7; room for a different but equivalent operation order |
| Accumulated frame angles, socket orientations, clip seconds, part depth | 1e-4 | Radians / seconds / layer units |
| Socket frame unit axes | 1e-4 | Dimensionless components |
| IK and contact residuals | 1e-5 | Today's residuals are ~3e-8 for every authored chain and contact; anything larger is a real change |
| Silhouette bounds, centroid and first point | 1e-3 | 48-segment outlines from `PartContours2D`, line widths excluded |
| Silhouette shoelace area | 1e-3 square units | Part areas range roughly 0.01..1 |
| Combat positions | 1e-3 | End-of-scenario actor positions |
| Ticks, damage, health, counts, ids, reached flags, facings, mirrored flags | exact | Integers and discrete facts |

### 6.2 Cases

81 pose cases (75 from P0b, 6 added in P0c: the native wheel scene and a mid-transition climb-on), each one bundle at one instant and facing, in actor-local units placed through `ActorPose` so facing -1 is a real mirrored capture. Every case records control positions and angles, chain residual/reached, active contact targets and residuals, every socket's 3D frame, expressions, and per visible part its depth, outline point count, bounds, centroid, area and first point (the silhouette record; order is carried by the depth values). Entity cases add prop points (grip/tip/second-grip/muzzle) for each equipped or dressed prop, the muzzle point and axis, the movement box, every hurt region and every hit window of the clip's action with its interval, active flag, bounds, translation, axis and mirroring.

| Bundle | Cases | Named P0 inputs covered |
| --- | --- | --- |
| Person, tall-thin, short-broad | rest; person-walk at 0, 0.3, 0.9 and 0.3 facing left; person-run 0.2; person-jump at the apex marker | Shared clips on varied lengths, foot contacts, both facings |
| Hero | sword side-cut at strike (both facings) and swoosh; forehand and backhand at strike; sheathe at 0, 0.31 and the end; gun shot at fire (both facings); climb 0.4; idle with the hurt expression | Tilted/twisted weapon (orient tracks), sheathing (sword moves from `sword-hand` to `back` by 0.31 s), actor-anchored blade windows, muzzle, back-view hair |
| Cinder gunner, Maul brute, spear-guard | pistol shot at fire; idle 0.5; hammer slam at strike (both facings); heavy walk 0.5; thrust at strike | Variant builds with wardrobe, prop-anchored hit windows |
| Quadruped, triceratops, baby triceratops | quadruped walk 0, 0.4, 0.8; run 0.3 (both facings); triceratops walk 0.4, rush 0.2; baby charge at rush and brake | Gaits with four point chains and contacts, socket-anchored horn window |
| Pterosaur | hover 0.5; fold at end; dive 0.3 (both facings); swoop at rush | Point-bound wing polygons, no chains, beak window |
| Curve face | rest; blink 1.0; crossed-eyes 1.0; expressions 2.0 and 6.5; hair-wave 0.5; smile 1.5 facing left | Affine eye controls, scale tracks, wave, bound curves |
| Stalker | lunge at strike (entity); walk 0.5 | Three-leg point rig, socket off the body |
| Linkage | rest; crank at 0, 1/8 .. 7/8 turn and 1/4 turn facing left; reach at 0, 0.5, 1 | Full-turn rotation, reached and unreachable IK, zero-length target |
| Wheel (native explicit bones, P0c) | rest; turn at 0.25, 0.5, 1 and 0.5 facing left | Inherited reflection, nonuniform scale and shear, animated scale/shear, slot and draw-order timelines; records full bone matrices |

Observed facts worth carrying into P0c and P4b:

- Every authored chain and contact in the set is reached with residual <= 2.8e-8. The only residuals above the tolerance are in the linkage: the pin beyond full reach at 1/4, 3/8, 1/2, 5/8 and 3/4 turn (residual 0.31 .. 0.66, reported `reached: false`), and at 1/8 and 7/8 turn, where the target is about 6e-3 past full reach but the solver still reports `reached: true` with residual 5.6e-3. `ChainResult.Reached` therefore comes from the two-bone solver's own reach decision, not from the residual; that is the "clamping must not keep reporting reached" case of plan section 3.5, now pinned by a fixture.
- A zero-length target (rod target on its own root) leaves the end 1.0e-3 from the root with `reached: true`: the point solver keeps a minimum root-to-end distance for a degenerate target.
- Sheathing is a placement change, not a pose change: at 0.31 s of `player-sword-sheathe` the dressed sword's grip/tip move from the `sword-hand` frame to the `back` frame while the control positions continue smoothly.
- Mirroring a case to facing -1 mirrors every point, socket origin and region translation in X and flips socket X axes; hurt and attack regions report `mirrored: true` on the left-facing hero and brute cases.

### 6.3 Combat scenarios

Eight graphics-free `AuthoredArena` fights at 120 Hz, actor 0 driven by scripted input and the others by the arena AI. Recorded exactly: every hit (tick, attacker, target, window, damage), every marker/event dispatch with its sound and action sequence, each actor's final health, position, facing, role, action and sequence, and the bolt count.

| Scenario | Set-up | Outcome recorded |
| --- | --- | --- |
| guard-thrust/right | player at 0, spear-guard at +2.05, 360 steps, no input | guard hits spear-tip at ticks 48 and 289 for 1 each; player 3 hp; two swing sounds |
| guard-thrust/left | same with the guard at -2.05 | identical ticks and damage; the guard faces +1 |
| hero-combo | hero at 0, stalker-pest at 1.2, attack at steps 1/25/50/75 | blade hits at tick 8 for 2, stalker killed (0 hp); four swings dispatched |
| brute-slam | maul-brute at 0 attacking at step 1, player at 1.7 | the player's AI thrusts hit the brute at ticks 55 and 188 (brute 22 -> 20); the slam itself does not land |
| club-slam | club-caveman at 0 attacking at step 1, player at 1.5 | player thrusts land at ticks 48 and 181 (caveman 9 -> 7); the club does not land; club-windup and swing sounds |
| gunner-bolt | cinder-gunner at 0 attacking at step 1, player at 4 | bolt hits at tick 93 for 2 (player 5 -> 3); one shot sound; player closes to 1.6 |
| baby-charge | baby-triceratops at 0 attacking at step 1, player at 3.5 | no hits; charge-rush, charge-scrape and charge-stop events fire once each |
| player-walk-thrust | player at -3 walking right, attacking at step 150, guard at +2.05 | guard hits spear-tip at tick 179 (player 5 -> 4); player ends at 0.54, guard retreats to 1.07 |

These outcomes are what the current combat path does, including the attacks that miss; they are references, not approvals. The slam and club misses in particular are inputs for P8/P9, where hit windows become attached colliders.

### 6.4 Test results after P0b and P0c

The Authored, Gameplay, Collision and Rendering suites with the new tests: 995 passed, 1 failed (the pre-existing `AuthoredObject2DTests` tileset failure). Full App2d.Tests after P0b: 2339 passed, 7 failed of 2346, the same seven pre-existing failures as section 2.1. The two new tests are `MigrationReferenceTests.ReferencesMatchTheCurrentEvaluatorCollisionAndCombat` and `TheLinkageExercisesReachedUnreachableAndZeroLengthTargets`.

Not captured in P0b and left for P0c or the owning phase: pixel-level rendered frames (the Studio smokes in section 3 remain the visual record and were not reviewed against a reference), slot/skin image attachments (no authored model uses them), physics props and projectiles beyond the one bolt hit.
## 7. P0c conversion decisions

Each general-affine or point-specific behavior in the reference set gets one disposition, the evidence a later pass must show to accept its conversion, the tolerance that applies (section 6.1 unless stated) and the pass that owns it. Dispositions use four words:

- **Preserve exactly**: the converted representation must reproduce the reference within tolerance; any difference is a defect.
- **Scoped deformation / bake**: the behavior survives, but moves into an explicitly scoped feature (artwork fit, typed modifier, discrete channel) whose output must match the reference.
- **Intentional difference**: the behavior is allowed to change at a named pass, only with the evidence listed; until that pass it is preserved exactly.
- **Unsupported, report**: the canonical representation will not express it; conversion must detect and report it, and the legacy path that evaluates it keeps a named adapter with a retirement condition.

Before the P0c additions, the extended capture was compared against the committed P0b file: 13 differences, all `new` properties or cases, no value moved. The reference set is therefore reproducible across two builds of the test project on this checkout.

### 7.1 Summary

| Case | Disposition | Owner | Accepts when |
| --- | --- | --- | --- |
| Point IK chains (all 23 authored) | Preserve exactly via the named legacy point-constraint adapter; per-bundle conversion to orientation-propagating bones is an intentional difference | P3b, retire in P11 | All chain-bearing cases: points 1e-4, residuals 1e-5, `reached` exact, socket frames 1e-4 |
| `reached` with a nonzero residual (linkage 1/8 and 7/8 turn) | Intentional difference: `reached` becomes residual-based | P4b | Those two cases flip to `reached: false` with the same residual; every authored chain unchanged |
| Zero-length IK target (1e-3 fold) | Preserve exactly until P4b; intentional difference there only with a stated residual | P4b | linkage-reach@0 residual 1.0e-3 preserved, or the new value recorded here first |
| Leaf affine scale on curve-face eyes | Scoped deformation: artwork fit on the pupil parts, no rig scale | P2a/P7b | Pupil silhouettes 1e-3 and eye control points 1e-4 across blink, crossed-eyes, wink, expressions |
| Inherited reflection/shear (wheel) | Unsupported in the canonical similarity rig, report; keep the explicit-bone evaluator as the legacy affine adapter | P3b classify, P11 retire | Wheel bone matrices 1e-4 and determinant sign while the adapter exists; NativeRigTests pass |
| Shear channel | Unsupported, report (no authored user) | P11 | Inventory shows zero shear tracks; wheel is the only fixture |
| Absolute control Z | Preserve exactly by converting to local layer offsets `restZ - parentRestZ` | P1a/P1b | Every point Z 1e-4, part Z range 1e-3, socket origin Z 1e-4, ordering by Z identical |
| Animated translate Z (25 tracks) | Preserve exactly as local layer deltas | P1a | climb and sword cases: chest/shoulder/head Z 1e-4 |
| Part depth, curve Z interpolation, bound-point depth | Preserve exactly as leaf offsets | P1b | Silhouette min/max Z and depth 1e-3 / 1e-4 |
| Weapon tilt/twist/turn, grip/tip/muzzle, XY projection | Preserve exactly through one planar-to-3D bridge | P1c, P8 | Socket frames, prop points, muzzle, attack regions 1e-4; mirrored exact; both facings |
| Foot contacts and measure-scaled targets | Preserve exactly | P3/P4a | Contacts: chain exact, target 1e-4, residual 1e-5 on person, variants, quadrupeds, stalker |
| Sheathing and back-view dressing | Scoped: becomes a discrete attachment-selection channel / profile binding with identical placement | P5b/P7/P9c | Prop keys exact and points 1e-4 at sheathe 0 / 0.31 / 0.42 and climb cases |
| Hit windows (actor, socket, prop anchored) and hurt boxes | Preserve exactly through attached colliders; rotating hurt shapes are an intentional difference needing their own row | P8a-c | Region bounds/translation/axis 1e-4, mirrored and active exact; combat hits and events exact |
| Arena outcomes incl. misses | Preserve exactly; an attack that starts landing is an intentional difference | P8c/P9b | Hits, events, end states exact |
| Person build proportions | Preserve exactly through data bindings/presets | P4a/P6b | tall-thin, short-broad, brute, cinder cases 1e-4; resolved measures (section 8) exact to 1e-4 |
| Fixed facial vocabulary | Scoped: expressions become profile bindings; procedural style retained until P7c | P2b/P7c | `expressions` exact; head silhouette 1e-3 |
| Wave and point bindings | Scoped: typed curve modifier and unchanged bindings | P2a | hair / wing / limb silhouettes 1e-3 |
| Facing mirror (X only, Z kept) | Preserve exactly | P1a | All `/left` cases |

### 7.2 Point IK chains versus rigid bones

What the code does now: every authored chain uses the point solver (section 4). The solve moves the joint and end positions; it does not write orientation into those controls' frames, so `angles` for solved controls stay at their rest-plus-rotate-track values. Orientation reaches artwork and sockets by other routes: a part with `a` and `b` takes its up axis from the a→b direction (`PartAttachment2D.FrameOf`), a socket with `toward` likewise (person hand and foot art sockets, quadruped head/body art), and a socket with `frame` takes that control's accumulated angle (person grips use `chest`, `sword-hand` and `gun-hand` use `right-shoulder`). Nothing but parts hangs below a solved joint in any authored model.

Disposition. Position-only constraint behavior is preserved exactly through a named legacy point-constraint adapter (plan rule 7, P3 removal obligation). Converting a bundle to orientation-propagating bones is a deliberate per-bundle act in P3b and later; it must keep every derived orientation route producing the same frames: a→b parts and `toward` sockets keep deriving from positions, and a `frame` socket keeps reading the same control's angle, unless the bundle's conversion report re-declares the socket with its own evidence. Evidence: every chain-bearing case (person family, quadrupeds, baby, stalker, spear-guard, linkage) at position 1e-4, residual 1e-5, `reached` exact and socket frames 1e-4; `RigConstraintTests`, `PoseEvaluatorTests`, `PersonVariantMotionTests` and `EntityRuntimeTests.SpearGripAndTipAgreeWithCollisionInBothFacings` unchanged.

Reach reporting. `ChainResult.Reached` comes from the two-bone solver's own decision, not from the residual: linkage-crank at 1/8 and 7/8 turn report `reached: true` with residual 5.6e-3, where the target is about 6e-3 past full reach. This is the plan's section 3.5 case. Intentional difference, owned by P4b: `reached` becomes residual-based (or the solver stops clamping silently), accepted when exactly those two cases flip to `false` with the same residual and no authored case changes. The zero-length target (linkage-reach@0) folds to a 1e-3 root-to-end gap along the retained FK axis; preserve that until P4b, where a limit-aware solve may replace it only after recording the new residual here.

### 7.3 Inherited affine scale and shear versus leaf fitting

Two different things carry the word affine today, and they get different dispositions.

Leaf affine controls (curve-face `left-eye`, `right-eye`). They have no children; their scale tracks squash the pupil parts (blink@1.0 collapses both pupils to a line: area 0, min Y = max Y; crossed-eyes and wink scale them too). Disposition: scoped deformation. The squash becomes an artwork fit on the pupil parts (plan section 3.4), evaluated per part, never an inherited rig scale. Accepted when the pupil silhouettes (bounds, centroid, area) match within 1e-3 and the eye control points within 1e-4 across the blink, crossed-eyes, wink and expressions cases. Owner: P2a for the feature, P7b when curve-face becomes a face part.

Inherited affine (wheel). `wheel-root` is set up with reflection, nonuniform scale and shear (determinant -2.90 at rest, -2.81 at 0.5 s, -2.17 at 1 s), and `spoke-tip` inherits the whole matrix (its bone determinant equals its parent's in every case). No authored asset does this; the wheel is the only fixture, reached through the explicit-bone branch of `PoseEvaluator` (the `SetupTransforms` path) and protected by `NativeRigTests` (18 tests). Disposition: unsupported in the canonical similarity rig, report. Conversion must classify an inherited nonuniform or sheared setup as "not representable" and say so with the control's reference path, never decompose it into an approximate similarity pose and never drop the shear. The explicit-bone evaluator is retained as the named legacy affine adapter for imported/native rigs; retirement in P11 requires either a scoped deformation feature that reproduces the wheel's child frames or an explicit decision to drop native inherited affine, recorded in the plan. Accepted (for the adapter) when the wheel cases' bone matrices match to 1e-4 with the same determinant sign, slot assignments and part silhouettes unchanged; NativeRigTests green.

Scale and shear channels. The inventory shows scale tracks only on the two curve-face eyes and no shear track anywhere. Scale maps to artwork fit as above; the shear channel has no authored user and is slated for retirement with the adapter.

### 7.4 Absolute control Z versus relative layer offsets

What the code does now. For a point control, the evaluator computes `Z = parentZ + (restZ - parentRestZ) + translate.Z`, so the authored absolute rest Z already behaves as a sum of local offsets, and an animated chest Z carries the head and shoulders with it (climb-on@0.15: chest, head at -0.35, left-shoulder at -0.375; sword clips key shoulder Z up to 0.15 across 25 tracks). For an explicit-bone control the evaluator uses `restZ + translate.Z` with no parent term; every shipped explicit-bone control (curve-face, wheel) has Z = 0, so no authored case distinguishes the two. Parts draw at `controlZ + depth`; curves interpolate Z between `a` and `b` and add depth; bound points keep their rest depth relative to the bound control; sockets add their offset Z after XY rotation (person `back` +0.2, `back-view` -0.2); props add real mesh Z on top of the socket frame (hammer tip at 0.244, sword tip at 0.454 in the strike cases). Facing never mirrors Z.

Disposition: preserve exactly. P1a converts authored absolute rest Z to local layer offsets `restZ - parentRestZ` (root offsets equal their rest Z), translate-Z keys stay as local deltas, and part depth, socket offset Z and bound-point depth stay leaf offsets; composed layer = local + parent reproduces today's values by construction, with no double counting. Explicit-bone controls adopt the same local meaning; with every shipped value at 0 this is a representation decision with no behavioral difference, and the wheel/curve-face cases pin it. Accepted when every point Z matches to 1e-4, every part's silhouette Z range to 1e-3 and depth to 1e-4, socket origins to 1e-4 and the order of parts sorted by Z is identical in every case. No intentional difference is allowed in P1; a renderer that changes the depth convention must do it at its one mapping boundary with these values unchanged.

### 7.5 Weapon tilt, twist and turn

What the code does now. `ActorPose.Socket` builds a `Frame3D` from the orient channel (`X`, `Y`) plus the planar angle (`base + socket.Angle + orient.Z`) at the socket origin, then composes with the actor frame whose X axis is `(facing, 0, 0)`. Prop points, hit anchors and the muzzle all transform through that frame. The hero side-cut `sword-hand` frame has a genuinely 3D Z axis (0.13, -0.96, 0.24); the hammer slam window's XY axis is the projection (0.62, -0.78) of its tilted frame; the gun muzzle at fire mirrors exactly in X between facings; the muzzle axis is the normalized XY projection, or the facing when the barrel is head-on.

Disposition: preserve exactly. P1c defines one bridge from the planar similarity (XY pose + layer) to this `Frame3D` using the orient channel, and one XY projection for gameplay; both must reproduce the references, including foreshortening. Accepted when socket origins and axes, prop grip/tip/second-grip/muzzle points, the muzzle point and axis, and attack-region bounds, translation and X axis match to 1e-4 with `mirrored` exact, in both facings, for the hero, brute, cinder, spear-guard and club cases. Collider placement in P8 inherits the same acceptance.

### 7.6 Foot contacts

What the code does now. A clip contact pins a chain end over the half-open interval `[start, finish)` (closed at the clip's end) to `cycleOrigin + restEnd + authoredTarget * measureRatio`, so a variant's longer leg plants farther (tall-thin: 0.048 versus 0.06 on person for the same key); `ContactHold` keeps the world anchor across steps per chain and clears it on a facing change; a masked overlay releases a contact gradually. Residuals are 2.4e-8 in every authored case.

Disposition: preserve exactly. P3 keeps the pin operation on canonical nodes; P4a's dimension bindings must keep the reference-measure ratio semantics so the same walk keys plant at the same scaled targets. Accepted when the `contacts` list matches (chain exact, target 1e-4, residual 1e-5) on every walk, run, idle and gait case, and `EntityRuntimeTests.PlantedFeetHoldTheirWorldPositionWhileTheControllerMoves` and `ContactsResetOnStartsStopsFacingAndInterruptions` stay green. Normalized or limit-aware channels (P4b) may not change these cases.

### 7.7 Sheathing and dressing

What the code does now. `PersonLoadout` decides from clip markers which socket holds the sword (`sword-draw` / `sword-sheathe`) and whether the back-view hair and `back-view` socket are used (`view-back` / `view-profile`); the references show the sword on `sword-hand` at 0 s and on `back` by 0.31 s of `player-sword-sheathe`, and `hair-short-back` plus `sword@back-view` during the climb. This is code, keyed by marker names.

Disposition: scoped change of representation with identical placement. In P5b/P7 the selection becomes a discrete attachment channel (held values, switching at the marker instants) or a profile binding (P9c); the sword's grip and tip must land on the same socket at the same instants. Accepted when each case's `props` keys (prop@socket) are exactly the reference's and the points match to 1e-4 at sheathe 0 / 0.31 / 0.42 and in the climb cases, and `PersonMoveSetTests` stays green. The switch instant itself is pinned only to the sampled times; a conversion that moves it by less than the gap between samples must add a case at the marker time.

### 7.8 Collision and combat

What the code does now. Hit windows anchor three ways: to the actor frame by an offset from the feet (hero blade windows, region translation (1.143, 0.874) mirrored exactly on the left), to a socket (horns, beak, sting) or to an equipped prop's named point pushed `along` its axis (hammer, club, spear, shell). Hurt regions are the control bounding box of each layout region inflated by its pad, or a hairline capsule when degenerate; they do not rotate with bones. The arena deduplicates per action sequence, window and target and records the outcomes in section 6.3, including the brute slam and club slam that do not land at their test distances.

Disposition: preserve exactly through P8's attached colliders: the same shapes, the same similarity placement, the same activation intervals (half-open, crossing-aware). Hurt regions becoming bone-attached shapes, or a slam starting to land, are intentional differences that need their own evidence rows (the new region bounds and the new hit ticks recorded here before the reference is regenerated). Accepted when hurt and attack bounds, translation and X axis match to 1e-4 with `mirrored` and `active` exact, and every scenario's hits, events and end states match exactly. Owner: P8a-c for placement, P9b for contact resolution.

### 7.9 Decided elsewhere or not decided

- Person build proportions: preserve exactly through P4a bindings and P6b migration; the variant cases and the resolved measures in section 8 are the acceptance set (1e-4).
- Fixed facial vocabulary: scoped to profile bindings in P7c, procedural style retained through P2b; `expressions` exact and the head silhouette 1e-3.
- Wave and point bindings: scoped to the typed curve modifier (P2a) and unchanged bindings; hair, wing and rounded-limb silhouettes 1e-3.
- Not decided: whether hurt regions should rotate with bones once they are colliders (left to P8c with evidence); whether explicit-bone controls' absolute Z semantics matter for any future import (no shipped case, so local was chosen and the fixtures pin Z = 0); whether the wheel's inherited affine is retired or replaced by a scoped deformation (P11, with the adapter named until then); and a tolerance for rendered pixels, since no pixel reference exists yet (the Studio smokes remain unreviewed renders).

P0 gate: the reference set is reproducible (section 6 and the P0c comparison above), existing failures are identified (section 2.1), and every general-affine and point-specific case has a stated disposition. No new abstraction was added; the only new code is test fixtures and the capture.
## 8. Full inventory

### Catalog load

- Root: `Assets/Characters/authored` (C:\Users\rober\source\repos\App2d\Assets\Characters\authored)
- Loaded: 7 models, 7 variants, 91 animations, 30 props, 11 entity files (11 compiled), 5 templates, 1 physics props.
- Catalog errors: 0; template errors: 0.

Schema versions in files versus the code's current version (every file is parsed by the current reader; a mismatch would be a load error above):

| Kind | Format id | Current version (code) | Versions found in files |
| --- | --- | --- | --- |
| Model | `app2d-model` | 5 | 5 |
| Variant | `app2d-variant` | 4 | 4 |
| Clip | `app2d-clip` | 3 | 3 |
| Prop | `app2d-prop` | 3 | 3 |
| Entity | `app2d-entity` | 2 | 2 |
| Template | `app2d-model-template` | 1 (no constant; property default) | 1 |
| Physics prop | `app2d-physics-prop` | 1 | 1 |

### Models

Defaults applied by `CharacterModel`/`ModelControl`/`PuppetPart` when JSON omits them: control `length` 0, `restAngle` 0, `scale` `unit`, `transform` null (point semantics); chain `solver` `point`, `bend` 1, `frame` `locomotion`; part `scaleX/Y` 1, `waveAmplitude` 0, `waveCycles` 1, `curveSections` 20, `face` `none`, `hidden` false; model `ink` `#222b32`, `lineWidth` .045, `structureRevision` 1.

| Model | File | Ver | Rev | Build | Controls (affine / explicit length>0) | Chains point/bone | Measures | Parts (geometry kinds) | Wave / face parts | Materials | Slots / skins | Sockets | Motion sets (roles) | Hurt layouts (regions) | Groups | Looks | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baby-triceratops-rig | `models/baby-triceratops-rig.json` | 5 | 1 | - | 18 (0 / 0) | 4/0 | 0 | 34 (Ellipse2D 22, SimplePolygon2D 12) | 0 / 0 | 0 | 0 / 0 | 2 | standard (10) | body (1) | 2 | 0 | - |
| curve-face | `models/curve-face.json` | 5 | 2 | - | 28 (2 / 0) | 0/0 | 0 | 12 (CubicBezier2D 1, Ellipse2D 3, QuadraticBezier2D 8) | 1 / 0 | 0 | 0 / 0 | 0 | expressions (9) |  | 2 | 0 | - |
| person | `models/person.json` | 5 | 1 | person | 15 (0 / 0) | 4/0 | 3 | 11 (Ellipse2D 5, LineSegmentCurve2D 1, RoundedPolylineCurve2D 4, SimplePolygon2D 1) | 0 / 1 | 2 | 0 / 0 | 12 | standard (6); deliberate (4); heavy (5) | standard (3) | 2 | 4 | - |
| pterosaur | `models/pterosaur.json` | 5 | 1 | - | 22 (0 / 0) | 0/0 | 0 | 18 (Ellipse2D 3, LineSegmentCurve2D 8, SimplePolygon2D 7) | 0 / 0 | 0 | 0 / 0 | 2 | standard (7) | body (1) | 1 | 0 | - |
| quadruped | `models/quadruped.json` | 5 | 1 | - | 18 (0 / 0) | 4/0 | 0 | 30 (Ellipse2D 23, SimplePolygon2D 7) | 0 / 0 | 0 | 0 / 0 | 2 | standard (8) | body (1) | 2 | 0 | - |
| stalker | `models/stalker.json` | 5 | 1 | - | 10 (0 / 0) | 3/0 | 1 | 7 (Ellipse2D 1, LineSegmentCurve2D 6) | 0 / 0 | 0 | 0 / 0 | 1 | standard (4) | standard (2) | 0 | 0 | - |
| triceratops | `models/triceratops.json` | 5 | 1 | - | 18 (0 / 0) | 4/0 | 0 | 34 (Ellipse2D 22, SimplePolygon2D 12) | 0 / 0 | 0 | 0 / 0 | 2 | standard (8) | body (1) | 2 | 0 | - |

#### Model details

##### baby-triceratops-rig

- Controls: body, body-up<body, head<body, head-up<head, tail<body, tail-up<tail, far-hind-root<body, far-hind-joint<far-hind-root, far-hind-foot<far-hind-joint, far-front-root<body, far-front-joint<far-front-root, far-front-foot<far-front-joint, near-hind-root<body, near-hind-joint<near-hind-root, near-hind-foot<near-hind-joint, near-front-root<body, near-front-joint<near-front-root, near-front-foot<near-front-joint
- Chains (4 in `chains`, 0 in `constraints`): far-hind: far-hind-root>far-hind-joint>far-hind-foot solver=point bend=-1 frame=locomotion scale=unit, far-front: far-front-root>far-front-joint>far-front-foot solver=point bend=1 frame=locomotion scale=unit, near-hind: near-hind-root>near-hind-joint>near-hind-foot solver=point bend=-1 frame=locomotion scale=unit, near-front: near-front-root>near-front-joint>near-front-foot solver=point bend=1 frame=locomotion scale=unit
- Parts: tail(SimplePolygon2D; a=tail,b=tail-up,scale=0.595,0.364,depth=0.014), far-hind-upper(Ellipse2D; a=far-hind-root,b=far-hind-joint,scale=0.175,0.422), far-hind-lower(Ellipse2D; a=far-hind-joint,b=far-hind-foot,scale=0.161,0.375,depth=-0.004), far-hind-paw(SimplePolygon2D; a=far-hind-foot,scale=0.252,0.154,depth=-0.011), far-hind-toe-0(Ellipse2D; a=far-hind-foot,scale=0.032,0.046,depth=-0.018), far-hind-toe-1(Ellipse2D; a=far-hind-foot,scale=0.032,0.046,depth=-0.018), far-hind-toe-2(Ellipse2D; a=far-hind-foot,scale=0.032,0.046,depth=-0.018), far-front-upper(Ellipse2D; a=far-front-root,b=far-front-joint,scale=0.175,0.412), far-front-lower(Ellipse2D; a=far-front-joint,b=far-front-foot,scale=0.161,0.375,depth=-0.004), far-front-paw(SimplePolygon2D; a=far-front-foot,scale=0.252,0.154,depth=-0.011), far-front-toe-0(Ellipse2D; a=far-front-foot,scale=0.032,0.046,depth=-0.018), far-front-toe-1(Ellipse2D; a=far-front-foot,scale=0.032,0.046,depth=-0.018), far-front-toe-2(Ellipse2D; a=far-front-foot,scale=0.032,0.046,depth=-0.018), near-hind-upper(Ellipse2D; a=near-hind-root,b=near-hind-joint,scale=0.175,0.422), near-hind-lower(Ellipse2D; a=near-hind-joint,b=near-hind-foot,scale=0.161,0.375,depth=-0.004), near-hind-paw(SimplePolygon2D; a=near-hind-foot,scale=0.252,0.154,depth=-0.011), near-hind-toe-0(Ellipse2D; a=near-hind-foot,scale=0.032,0.046,depth=-0.018), near-hind-toe-1(Ellipse2D; a=near-hind-foot,scale=0.032,0.046,depth=-0.018), near-hind-toe-2(Ellipse2D; a=near-hind-foot,scale=0.032,0.046,depth=-0.018), near-front-upper(Ellipse2D; a=near-front-root,b=near-front-joint,scale=0.175,0.412), near-front-lower(Ellipse2D; a=near-front-joint,b=near-front-foot,scale=0.161,0.375,depth=-0.004), near-front-paw(SimplePolygon2D; a=near-front-foot,scale=0.252,0.154,depth=-0.011), near-front-toe-0(Ellipse2D; a=near-front-foot,scale=0.032,0.046,depth=-0.018), near-front-toe-1(Ellipse2D; a=near-front-foot,scale=0.032,0.046,depth=-0.018), near-front-toe-2(Ellipse2D; a=near-front-foot,scale=0.032,0.046,depth=-0.018), body(Ellipse2D; a=body,b=body-up,scale=1.344,0.63), head(SimplePolygon2D; a=head,b=head-up,scale=0.679,0.476,depth=-0.168), frill(SimplePolygon2D; a=head,b=head-up,scale=0.616,0.966,depth=-0.182), far-brow-horn(SimplePolygon2D; a=head,b=head-up,scale=0.174,0.202,depth=-0.179), near-brow-horn(SimplePolygon2D; a=head,b=head-up,scale=0.202,0.207,depth=-0.203), nose-horn(SimplePolygon2D; a=head,b=head-up,scale=0.123,0.14,depth=-0.203), eye(Ellipse2D; a=head,b=head-up,scale=0.076,0.101,depth=-0.217), brow(SimplePolygon2D; a=head,b=head-up,scale=0.112,0.063,depth=-0.217), mouth(SimplePolygon2D; a=head,b=head-up,scale=0.161,0.056,depth=-0.217)
- Sockets: head-art@head,toward=head-up, body-art@body,toward=body-up
- Motion set `standard`: brake=baby-triceratops-brake, death=baby-triceratops-death, head-down=baby-triceratops-head-down, hit=baby-triceratops-hit, idle=baby-triceratops-idle, recover=baby-triceratops-recover, run=baby-triceratops-run, rush=baby-triceratops-rush, scrape=baby-triceratops-scrape, walk=baby-triceratops-walk
- Hurt layout `body`: body[body,head] pad=0.245
- Groups: head(2), legs(4)

##### curve-face

- Controls: head, mouth-left<head, mouth-bend<head, mouth-right<head, mouth-upper-bend<mouth-bend, left-eye<head [affine], left-eye-upper-left<head, left-eye-upper-bend<head, left-eye-upper-right<head, left-eye-lower-left<head, left-eye-lower-bend<head, left-eye-lower-right<head, left-brow-left<head, left-brow-bend<head, left-brow-right<head, right-eye<head [affine], right-eye-upper-left<head, right-eye-upper-bend<head, right-eye-upper-right<head, right-eye-lower-left<head, right-eye-lower-bend<head, right-eye-lower-right<head, right-brow-left<head, right-brow-bend<head, right-brow-right<head, hair-left<head, hair-right<head, hair-wave<head
- Parts: head(Ellipse2D; a=head,frame=head,scale=1,1.1), mouth(QuadraticBezier2D; a=mouth-left,b=mouth-right,bindings=3,depth=-0.012), mouth-upper(QuadraticBezier2D; a=mouth-left,b=mouth-right,bindings=3,depth=-0.012), left-pupil(Ellipse2D; a=left-eye,frame=left-eye,scale=0.027,0.06,depth=-0.016), left-eye-upper(QuadraticBezier2D; a=left-eye-upper-left,b=left-eye-upper-right,bindings=3,depth=-0.012), left-eye-lower(QuadraticBezier2D; a=left-eye-lower-left,b=left-eye-lower-right,bindings=3,depth=-0.012), left-brow(QuadraticBezier2D; a=left-brow-left,b=left-brow-right,bindings=3,depth=-0.012), right-pupil(Ellipse2D; a=right-eye,frame=right-eye,scale=0.027,0.06,depth=-0.016), right-eye-upper(QuadraticBezier2D; a=right-eye-upper-left,b=right-eye-upper-right,bindings=3,depth=-0.012), right-eye-lower(QuadraticBezier2D; a=right-eye-lower-left,b=right-eye-lower-right,bindings=3,depth=-0.012), right-brow(QuadraticBezier2D; a=right-brow-left,b=right-brow-right,bindings=3,depth=-0.012), hair(CubicBezier2D; a=hair-left,b=hair-right,wave=0.045x5/hair-wave,depth=-0.008)
- Motion set `expressions`: angry=curve-face-angry, blink=curve-face-blink, crossed-eyes=curve-face-crossed-eyes, expressions=curve-face-expressions, hair-wave=curve-face-hair-wave, smile=curve-face-smile, surprised=curve-face-surprised, wink=curve-face-wink, worried=curve-face-worried
- Groups: face(24), hair(1)

##### person

- Controls: hips [scale leg], chest<hips [scale torso], head<chest [scale torso], left-shoulder<chest [scale torso], left-elbow<left-shoulder, left-hand<left-elbow, left-hip<hips [scale leg], left-knee<left-hip, left-foot<left-knee, right-shoulder<chest [scale torso], right-elbow<right-shoulder, right-hand<right-elbow, right-hip<hips [scale leg], right-knee<right-hip, right-foot<right-knee
- Chains (4 in `chains`, 0 in `constraints`): left-arm: left-shoulder>left-elbow>left-hand solver=point bend=-1 frame=left-shoulder scale=arm, left-leg: left-hip>left-knee>left-foot solver=point bend=1 frame=locomotion scale=leg, right-arm: right-shoulder>right-elbow>right-hand solver=point bend=-1 frame=right-shoulder scale=arm, right-leg: right-hip>right-knee>right-foot solver=point bend=1 frame=locomotion scale=leg
- Measures: leg=[right-hip>right-knee>right-foot], arm=[right-shoulder>right-elbow>right-hand], torso=[hips>chest]
- Parts: chest-head(LineSegmentCurve2D; a=chest,b=head), left-arm(RoundedPolylineCurve2D; a=left-shoulder,b=left-hand,bindings=3), left-leg(RoundedPolylineCurve2D; a=left-hip,b=left-foot,bindings=3), right-arm(RoundedPolylineCurve2D; a=right-shoulder,b=right-hand,bindings=3), right-leg(RoundedPolylineCurve2D; a=right-hip,b=right-foot,bindings=3), body(SimplePolygon2D; a=hips,b=chest,scale=0.36,0.68), head(Ellipse2D; a=head,face=relaxed@0.065,scale=0.56,0.58,depth=-0.12), left-hand(Ellipse2D; a=left-hand,b=left-elbow,depth=-0.004), right-hand(Ellipse2D; a=right-hand,b=right-elbow,depth=-0.004), left-foot(Ellipse2D; a=left-foot,b=left-knee,depth=-0.004), right-foot(Ellipse2D; a=right-foot,b=right-knee,depth=-0.004)
- Materials: skin, spotted-hide
- Sockets: right-grip@right-hand,frame=chest,angle=0.12, left-grip@left-hand,frame=chest,angle=0.12, back@chest,frame=chest,off=(-0.1,-0.08,0.2),angle=-1.691, back-view@chest,frame=chest,off=(-0.13,0.12,-0.2),angle=-1.021, sword-hand@right-hand,frame=right-shoulder, gun-hand@right-hand,frame=right-shoulder, head-art@head,frame=locomotion, body-art@hips,frame=hips,toward=chest, left-hand-art@left-hand,toward=left-elbow, right-hand-art@right-hand,toward=right-elbow, left-foot-art@left-foot,toward=left-knee, right-foot-art@right-foot,toward=right-knee
- Motion set `standard`: death=person-death, hit=person-hit, idle=person-idle, jump=person-jump, run=person-run, walk=person-walk
- Motion set `deliberate`: death=person-death, hit=person-hit, idle=person-idle, walk=person-walk
- Motion set `heavy`: death=person-death, hit=person-hit, idle=person-idle, run=person-run, walk=person-heavy-walk
- Hurt layout `standard`: body[hips,chest,left-shoulder,right-shoulder] pad=0.14, head[head] pad=0.3, legs[left-hip,right-hip,left-knee,right-knee,left-foot,right-foot] pad=0.07
- Groups: upper(6), lower(5)
- Looks: sage(1 part overrides), slate(2 part overrides), ember(2 part overrides), night(2 part overrides)

##### pterosaur

- Controls: body, body-up<body, head<body, head-up<head, jaw<head, jaw-up<jaw, near-shoulder<body, near-bend<near-shoulder, near-tip<near-bend, near-leg<body, near-foot<near-leg, near-toe-0<near-foot, near-toe-1<near-foot, near-toe-2<near-foot, far-shoulder<body, far-bend<far-shoulder, far-tip<far-bend, far-leg<body, far-foot<far-leg, far-toe-0<far-foot, far-toe-1<far-foot, far-toe-2<far-foot
- Parts: far-wing(SimplePolygon2D; a=body,b=body-up,bindings=6,depth=0.12), far-leg(LineSegmentCurve2D; a=far-leg,b=far-foot,depth=0.12), far-toe-0(LineSegmentCurve2D; a=far-foot,b=far-toe-0,depth=0.12), far-toe-1(LineSegmentCurve2D; a=far-foot,b=far-toe-1,depth=0.12), far-toe-2(LineSegmentCurve2D; a=far-foot,b=far-toe-2,depth=0.12), near-wing(SimplePolygon2D; a=body,b=body-up,bindings=6,depth=-0.02), near-leg(LineSegmentCurve2D; a=near-leg,b=near-foot,depth=-0.02), near-toe-0(LineSegmentCurve2D; a=near-foot,b=near-toe-0,depth=-0.02), near-toe-1(LineSegmentCurve2D; a=near-foot,b=near-toe-1,depth=-0.02), near-toe-2(LineSegmentCurve2D; a=near-foot,b=near-toe-2,depth=-0.02), tail(SimplePolygon2D; a=body,b=body-up,scale=0.3,0.15,depth=-0.04), body(Ellipse2D; a=body,b=body-up,scale=0.45,0.38,depth=-0.07), crest(SimplePolygon2D; a=head,b=head-up,scale=0.18,0.3,depth=-0.15), head(Ellipse2D; a=head,b=head-up,scale=0.33,0.29,depth=-0.16), beak(SimplePolygon2D; a=head,b=head-up,scale=0.35,0.23,depth=-0.17), lower-beak(SimplePolygon2D; a=jaw,b=jaw-up,scale=0.26,0.075,depth=-0.18), eye(Ellipse2D; a=head,b=head-up,scale=0.048,0.069,depth=-0.19), brow(SimplePolygon2D; a=head,b=head-up,scale=0.1,0.035,depth=-0.19)
- Sockets: head-art@head,toward=head-up, body-art@body,toward=body-up
- Motion set `standard`: brake=pterosaur-brake, dive=pterosaur-dive, fold=pterosaur-fold, hover=pterosaur-hover, idle=pterosaur-hover, low-recovery=pterosaur-low-recovery, reset=pterosaur-reset
- Hurt layout `body`: body[body,head] pad=0.16
- Groups: wings(6)

##### quadruped

- Controls: body, body-up<body, head<body, head-up<head, tail<body, tail-up<tail, far-hind-root<body, far-hind-joint<far-hind-root, far-hind-foot<far-hind-joint, far-front-root<body, far-front-joint<far-front-root, far-front-foot<far-front-joint, near-hind-root<body, near-hind-joint<near-hind-root, near-hind-foot<near-hind-joint, near-front-root<body, near-front-joint<near-front-root, near-front-foot<near-front-joint
- Chains (4 in `chains`, 0 in `constraints`): far-hind: far-hind-root>far-hind-joint>far-hind-foot solver=point bend=-1 frame=locomotion scale=unit, far-front: far-front-root>far-front-joint>far-front-foot solver=point bend=1 frame=locomotion scale=unit, near-hind: near-hind-root>near-hind-joint>near-hind-foot solver=point bend=-1 frame=locomotion scale=unit, near-front: near-front-root>near-front-joint>near-front-foot solver=point bend=1 frame=locomotion scale=unit
- Parts: tail(SimplePolygon2D; a=tail,b=tail-up,scale=0.85,0.52,depth=0.02), far-hind-upper(Ellipse2D; a=far-hind-root,b=far-hind-joint,scale=0.25,0.602), far-hind-lower(Ellipse2D; a=far-hind-joint,b=far-hind-foot,scale=0.23,0.536,depth=-0.005), far-hind-paw(SimplePolygon2D; a=far-hind-foot,scale=0.36,0.22,depth=-0.015), far-hind-toe-0(Ellipse2D; a=far-hind-foot,scale=0.045,0.065,depth=-0.025), far-hind-toe-1(Ellipse2D; a=far-hind-foot,scale=0.045,0.065,depth=-0.025), far-hind-toe-2(Ellipse2D; a=far-hind-foot,scale=0.045,0.065,depth=-0.025), far-front-upper(Ellipse2D; a=far-front-root,b=far-front-joint,scale=0.25,0.588), far-front-lower(Ellipse2D; a=far-front-joint,b=far-front-foot,scale=0.23,0.536,depth=-0.005), far-front-paw(SimplePolygon2D; a=far-front-foot,scale=0.36,0.22,depth=-0.015), far-front-toe-0(Ellipse2D; a=far-front-foot,scale=0.045,0.065,depth=-0.025), far-front-toe-1(Ellipse2D; a=far-front-foot,scale=0.045,0.065,depth=-0.025), far-front-toe-2(Ellipse2D; a=far-front-foot,scale=0.045,0.065,depth=-0.025), near-hind-upper(Ellipse2D; a=near-hind-root,b=near-hind-joint,scale=0.25,0.602), near-hind-lower(Ellipse2D; a=near-hind-joint,b=near-hind-foot,scale=0.23,0.536,depth=-0.005), near-hind-paw(SimplePolygon2D; a=near-hind-foot,scale=0.36,0.22,depth=-0.015), near-hind-toe-0(Ellipse2D; a=near-hind-foot,scale=0.045,0.065,depth=-0.025), near-hind-toe-1(Ellipse2D; a=near-hind-foot,scale=0.045,0.065,depth=-0.025), near-hind-toe-2(Ellipse2D; a=near-hind-foot,scale=0.045,0.065,depth=-0.025), near-front-upper(Ellipse2D; a=near-front-root,b=near-front-joint,scale=0.25,0.588), near-front-lower(Ellipse2D; a=near-front-joint,b=near-front-foot,scale=0.23,0.536,depth=-0.005), near-front-paw(SimplePolygon2D; a=near-front-foot,scale=0.36,0.22,depth=-0.015), near-front-toe-0(Ellipse2D; a=near-front-foot,scale=0.045,0.065,depth=-0.025), near-front-toe-1(Ellipse2D; a=near-front-foot,scale=0.045,0.065,depth=-0.025), near-front-toe-2(Ellipse2D; a=near-front-foot,scale=0.045,0.065,depth=-0.025), body(Ellipse2D; a=body,b=body-up,scale=1.92,0.9), head(Ellipse2D; a=head,b=head-up,scale=0.74,0.58,depth=-0.24), eye(Ellipse2D; a=head,b=head-up,scale=0.09,0.12,depth=-0.31), brow(SimplePolygon2D; a=head,b=head-up,scale=0.16,0.09,depth=-0.31), mouth(SimplePolygon2D; a=head,b=head-up,scale=0.23,0.08,depth=-0.31)
- Sockets: head-art@head,toward=head-up, body-art@body,toward=body-up
- Motion set `standard`: brake=quadruped-brake, head-down=quadruped-head-down, idle=quadruped-idle, recover=quadruped-recover, run=quadruped-run, rush=quadruped-rush, scrape=quadruped-scrape, walk=quadruped-walk
- Hurt layout `body`: body[body,head] pad=0.35
- Groups: head(2), legs(4)

##### stalker

- Controls: body [scale leg], hip-0<body [scale leg], knee-0<hip-0, foot-0<knee-0, hip-1<body [scale leg], knee-1<hip-1, foot-1<knee-1, hip-2<body [scale leg], knee-2<hip-2, foot-2<knee-2
- Chains (3 in `chains`, 0 in `constraints`): foot-0-chain: hip-0>knee-0>foot-0 solver=point bend=1 frame=locomotion scale=leg, foot-1-chain: hip-1>knee-1>foot-1 solver=point bend=1 frame=locomotion scale=leg, foot-2-chain: hip-2>knee-2>foot-2 solver=point bend=1 frame=locomotion scale=leg
- Measures: leg=[hip-1>knee-1>foot-1]
- Parts: stroke(LineSegmentCurve2D; a=hip-0,b=knee-0), stroke-2(LineSegmentCurve2D; a=knee-0,b=foot-0), stroke-3(LineSegmentCurve2D; a=hip-1,b=knee-1), stroke-4(LineSegmentCurve2D; a=knee-1,b=foot-1), stroke-5(LineSegmentCurve2D; a=hip-2,b=knee-2), stroke-6(LineSegmentCurve2D; a=knee-2,b=foot-2), ellipse(Ellipse2D; a=body,scale=0.95,0.38,depth=-0.05)
- Sockets: stinger@body,off=(0.5,-0.02,0)
- Motion set `standard`: death=stalker-death, hit=stalker-hit, idle=stalker-idle, walk=stalker-walk
- Hurt layout `standard`: shell[body,hip-0,hip-2] pad=0.2, legs[knee-0,knee-1,knee-2,foot-0,foot-1,foot-2] pad=0.05

##### triceratops

- Controls: body, body-up<body, head<body, head-up<head, tail<body, tail-up<tail, far-hind-root<body, far-hind-joint<far-hind-root, far-hind-foot<far-hind-joint, far-front-root<body, far-front-joint<far-front-root, far-front-foot<far-front-joint, near-hind-root<body, near-hind-joint<near-hind-root, near-hind-foot<near-hind-joint, near-front-root<body, near-front-joint<near-front-root, near-front-foot<near-front-joint
- Chains (4 in `chains`, 0 in `constraints`): far-hind: far-hind-root>far-hind-joint>far-hind-foot solver=point bend=-1 frame=locomotion scale=unit, far-front: far-front-root>far-front-joint>far-front-foot solver=point bend=1 frame=locomotion scale=unit, near-hind: near-hind-root>near-hind-joint>near-hind-foot solver=point bend=-1 frame=locomotion scale=unit, near-front: near-front-root>near-front-joint>near-front-foot solver=point bend=1 frame=locomotion scale=unit
- Parts: tail(SimplePolygon2D; a=tail,b=tail-up,scale=0.85,0.52,depth=0.02), far-hind-upper(Ellipse2D; a=far-hind-root,b=far-hind-joint,scale=0.25,0.602), far-hind-lower(Ellipse2D; a=far-hind-joint,b=far-hind-foot,scale=0.23,0.536,depth=-0.005), far-hind-paw(SimplePolygon2D; a=far-hind-foot,scale=0.36,0.22,depth=-0.015), far-hind-toe-0(Ellipse2D; a=far-hind-foot,scale=0.045,0.065,depth=-0.025), far-hind-toe-1(Ellipse2D; a=far-hind-foot,scale=0.045,0.065,depth=-0.025), far-hind-toe-2(Ellipse2D; a=far-hind-foot,scale=0.045,0.065,depth=-0.025), far-front-upper(Ellipse2D; a=far-front-root,b=far-front-joint,scale=0.25,0.588), far-front-lower(Ellipse2D; a=far-front-joint,b=far-front-foot,scale=0.23,0.536,depth=-0.005), far-front-paw(SimplePolygon2D; a=far-front-foot,scale=0.36,0.22,depth=-0.015), far-front-toe-0(Ellipse2D; a=far-front-foot,scale=0.045,0.065,depth=-0.025), far-front-toe-1(Ellipse2D; a=far-front-foot,scale=0.045,0.065,depth=-0.025), far-front-toe-2(Ellipse2D; a=far-front-foot,scale=0.045,0.065,depth=-0.025), near-hind-upper(Ellipse2D; a=near-hind-root,b=near-hind-joint,scale=0.25,0.602), near-hind-lower(Ellipse2D; a=near-hind-joint,b=near-hind-foot,scale=0.23,0.536,depth=-0.005), near-hind-paw(SimplePolygon2D; a=near-hind-foot,scale=0.36,0.22,depth=-0.015), near-hind-toe-0(Ellipse2D; a=near-hind-foot,scale=0.045,0.065,depth=-0.025), near-hind-toe-1(Ellipse2D; a=near-hind-foot,scale=0.045,0.065,depth=-0.025), near-hind-toe-2(Ellipse2D; a=near-hind-foot,scale=0.045,0.065,depth=-0.025), near-front-upper(Ellipse2D; a=near-front-root,b=near-front-joint,scale=0.25,0.588), near-front-lower(Ellipse2D; a=near-front-joint,b=near-front-foot,scale=0.23,0.536,depth=-0.005), near-front-paw(SimplePolygon2D; a=near-front-foot,scale=0.36,0.22,depth=-0.015), near-front-toe-0(Ellipse2D; a=near-front-foot,scale=0.045,0.065,depth=-0.025), near-front-toe-1(Ellipse2D; a=near-front-foot,scale=0.045,0.065,depth=-0.025), near-front-toe-2(Ellipse2D; a=near-front-foot,scale=0.045,0.065,depth=-0.025), body(Ellipse2D; a=body,b=body-up,scale=1.92,0.9), head(SimplePolygon2D; a=head,b=head-up,scale=0.97,0.68,depth=-0.24), frill(SimplePolygon2D; a=head,b=head-up,scale=0.88,1.38,depth=-0.26), far-brow-horn(SimplePolygon2D; a=head,b=head-up,scale=0.31,0.36,depth=-0.255), near-brow-horn(SimplePolygon2D; a=head,b=head-up,scale=0.36,0.37,depth=-0.29), nose-horn(SimplePolygon2D; a=head,b=head-up,scale=0.22,0.25,depth=-0.29), eye(Ellipse2D; a=head,b=head-up,scale=0.09,0.12,depth=-0.31), brow(SimplePolygon2D; a=head,b=head-up,scale=0.16,0.09,depth=-0.31), mouth(SimplePolygon2D; a=head,b=head-up,scale=0.23,0.08,depth=-0.31)
- Sockets: head-art@head,toward=head-up, body-art@body,toward=body-up
- Motion set `standard`: brake=triceratops-brake, head-down=triceratops-head-down, idle=triceratops-idle, recover=triceratops-recover, run=triceratops-run, rush=triceratops-rush, scrape=triceratops-scrape, walk=triceratops-walk
- Hurt layout `body`: body[body,head] pad=0.35
- Groups: head(2), legs(4)

### Variants

Defaults: `build`, `rest` and `parts` are empty dictionaries when omitted; a `PartOverride` field that is absent is null (inherit).

| Variant | File | Ver | Base | Build values | Rest overrides | Part overrides (fields used) |
| --- | --- | --- | --- | --- | --- | --- |
| brute | `variants/brute.json` | 4 | person | arms=1.1, head=0.95, legs=0.95, torso=1.05, width=1.45 | 0 | 6 (body, head, left-hand, right-hand, left-foot, right-foot) [face, material] |
| cinder | `variants/cinder.json` | 4 | person | arms=0.95, head=1.05, legs=0.9, torso=0.95, width=1.05 | 0 | 6 (body, head, left-hand, right-hand, left-foot, right-foot) [face, material] |
| club-caveman-build | `variants/club-caveman-build.json` | 4 | person | arms=0.95, head=1.05, legs=0.9, torso=0.95, width=1.05 | 0 | 6 (body, head, left-hand, right-hand, left-foot, right-foot) [face, material] |
| rock-thrower-build | `variants/rock-thrower-build.json` | 4 | person | arms=1.05, head=1.05, legs=0.9, torso=0.95, width=1.05 | 0 | 6 (body, head, left-hand, right-hand, left-foot, right-foot) [face, material] |
| shield-defender-build | `variants/shield-defender-build.json` | 4 | person | arms=0.95, head=1.05, legs=0.9, torso=0.95, width=1.05 | 0 | 6 (body, head, left-hand, right-hand, left-foot, right-foot) [face, material] |
| short-broad | `variants/short-broad.json` | 4 | person | arms=0.88, head=1.1, legs=0.82, torso=0.9, width=1.3 | 0 | 1 (body) [material] |
| tall-thin | `variants/tall-thin.json` | 4 | person | arms=1.15, head=0.92, legs=1.2, torso=1.12, width=0.8 | 0 | 1 (body) [material] |

### Animations

Defaults: track `kind` `translate`, `scale` null (inherits the target control's scale), `setupBeforeFirst` false; clip `duration` 1, `loop` false, `structureRevision` 1; contact `finish` .5. Track kinds: translate, rotate, target (IK), orient (socket orientation), scale, shear.

| Clip | File | Ver | Model | Rev | Duration | Loop | Tracks t/r/tg/o/s/sh | Setup-before-first | Contacts | Markers | Face tracks | Slot attach / color / draw order | Events | Reference keys | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baby-triceratops-brake | `animations/baby-triceratops-brake.json` | 3 | baby-triceratops-rig | 1 | 0.65 | no | 1/2/4/0/0/0 | 0 | 0 | stop | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-charge | `animations/baby-triceratops-charge.json` | 3 | baby-triceratops-rig | 1 | 2.7 | no | 2/2/4/0/0/0 | 0 | 7 | scrape rush brake recover | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-death | `animations/baby-triceratops-death.json` | 3 | baby-triceratops-rig | 1 | 0.8 | no | 1/1/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-head-down | `animations/baby-triceratops-head-down.json` | 3 | baby-triceratops-rig | 1 | 0.55 | no | 1/1/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-hit | `animations/baby-triceratops-hit.json` | 3 | baby-triceratops-rig | 1 | 0.3 | no | 1/1/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-idle | `animations/baby-triceratops-idle.json` | 3 | baby-triceratops-rig | 1 | 2 | yes | 1/2/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-recover | `animations/baby-triceratops-recover.json` | 3 | baby-triceratops-rig | 1 | 1.2 | no | 1/2/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-run | `animations/baby-triceratops-run.json` | 3 | baby-triceratops-rig | 1 | 0.65 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-rush | `animations/baby-triceratops-rush.json` | 3 | baby-triceratops-rig | 1 | 0.48 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-scrape | `animations/baby-triceratops-scrape.json` | 3 | baby-triceratops-rig | 1 | 1.1 | yes | 0/1/1/0/0/0 | 0 | 3 | scrape | 0 | 0 / 0 / 0 | 0 |  | - |
| baby-triceratops-walk | `animations/baby-triceratops-walk.json` | 3 | baby-triceratops-rig | 1 | 1.2 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| club-caveman-slam | `animations/club-caveman-slam.json` | 3 | person | 1 | 1.8 | no | 3/3/2/1/0/0 | 0 | 2 | windup strike recover | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| curve-face-angry | `animations/curve-face-angry.json` | 3 | curve-face | 2 | 2 | yes | 7/0/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| curve-face-blink | `animations/curve-face-blink.json` | 3 | curve-face | 2 | 2 | yes | 4/0/0/0/2/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| curve-face-crossed-eyes | `animations/curve-face-crossed-eyes.json` | 3 | curve-face | 2 | 2 | yes | 12/0/0/0/2/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| curve-face-expressions | `animations/curve-face-expressions.json` | 3 | curve-face | 2 | 8 | yes | 23/0/0/0/2/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| curve-face-hair-wave | `animations/curve-face-hair-wave.json` | 3 | curve-face | 2 | 1.5 | yes | 1/0/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| curve-face-smile | `animations/curve-face-smile.json` | 3 | curve-face | 2 | 3 | yes | 3/0/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| curve-face-surprised | `animations/curve-face-surprised.json` | 3 | curve-face | 2 | 2 | yes | 14/0/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| curve-face-wink | `animations/curve-face-wink.json` | 3 | curve-face | 2 | 2 | yes | 4/0/0/0/1/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| curve-face-worried | `animations/curve-face-worried.json` | 3 | curve-face | 2 | 2 | yes | 9/0/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| person-death | `animations/person-death.json` | 3 | person | 1 | 1.1 | no | 1/2/2/1/0/0 | 0 | 2 | impact | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-hammer-slam | `animations/person-hammer-slam.json` | 3 | person | 1 | 1.5 | no | 3/3/2/1/0/0 | 0 | 2 | windup strike recover | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-heavy-walk | `animations/person-heavy-walk.json` | 3 | person | 1 | 1.56 | yes | 2/0/4/0/0/0 | 0 | 2 |  | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-hit | `animations/person-hit.json` | 3 | person | 1 | 0.3 | no | 1/2/2/0/0/0 | 0 | 2 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-idle | `animations/person-idle.json` | 3 | person | 1 | 2 | yes | 1/1/2/0/0/0 | 0 | 2 |  | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-jump | `animations/person-jump.json` | 3 | person | 1 | 0.9 | no | 1/0/4/0/0/0 | 0 | 2 | launch apex | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-pistol-shot | `animations/person-pistol-shot.json` | 3 | person | 1 | 1.1 | no | 3/3/2/1/0/0 | 0 | 2 | fire | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-run | `animations/person-run.json` | 3 | person | 1 | 0.72 | yes | 7/0/4/0/0/0 | 0 | 2 |  | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-thrust | `animations/person-thrust.json` | 3 | person | 1 | 0.9 | no | 1/1/2/0/0/0 | 0 | 2 | windup strike recover | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| person-walk | `animations/person-walk.json` | 3 | person | 1 | 1.2 | yes | 1/0/4/0/0/0 | 0 | 2 |  | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-balance-backward | `animations/player-balance-backward.json` | 3 | person | 1 | 1 | yes | 3/2/2/0/0/0 | 0 | 2 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-balance-forward | `animations/player-balance-forward.json` | 3 | person | 1 | 1 | yes | 3/2/2/0/0/0 | 0 | 2 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-celebrate | `animations/player-celebrate.json` | 3 | person | 1 | 1.4 | no | 3/2/4/0/0/0 | 0 | 4 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-climb | `animations/player-climb.json` | 3 | person | 1 | 0.8 | yes | 7/2/4/0/0/0 | 0 | 0 | view-back rung-right rung-left | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-climb-off | `animations/player-climb-off.json` | 3 | person | 1 | 0.3 | no | 7/2/4/0/0/0 | 0 | 0 | view-back view-profile | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-climb-on | `animations/player-climb-on.json` | 3 | person | 1 | 0.3 | no | 7/2/4/0/0/0 | 0 | 0 | view-profile view-back | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-dash | `animations/player-dash.json` | 3 | person | 1 | 0.28 | no | 3/2/4/0/0/0 | 0 | 0 | dash-end | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-death | `animations/player-death.json` | 3 | person | 1 | 1.6 | no | 3/2/4/0/0/0 | 0 | 0 | impact | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-fall | `animations/player-fall.json` | 3 | person | 1 | 0.6 | yes | 3/2/4/0/0/0 | 0 | 0 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-gun-aim | `animations/player-gun-aim.json` | 3 | person | 1 | 2 | yes | 3/3/2/1/0/0 | 0 | 2 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-gun-charge | `animations/player-gun-charge.json` | 3 | person | 1 | 1 | no | 2/2/2/0/0/0 | 0 | 0 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-gun-shot | `animations/player-gun-shot.json` | 3 | person | 1 | 0.2 | no | 2/2/2/1/0/0 | 0 | 0 | fire | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-gun-wall-shot | `animations/player-gun-wall-shot.json` | 3 | person | 1 | 0.3 | no | 3/3/4/1/0/0 | 0 | 0 | fire | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-heal-gather | `animations/player-heal-gather.json` | 3 | person | 1 | 1 | no | 3/2/2/0/0/0 | 0 | 2 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-hit | `animations/player-hit.json` | 3 | person | 1 | 0.35 | no | 3/2/2/0/0/0 | 0 | 2 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-idle | `animations/player-idle.json` | 3 | person | 1 | 2.4 | yes | 3/2/2/0/0/0 | 0 | 2 |  | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-jump | `animations/player-jump.json` | 3 | person | 1 | 0.5 | no | 3/2/4/0/0/0 | 0 | 0 | launch apex | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-land | `animations/player-land.json` | 3 | person | 1 | 0.36 | no | 3/2/2/0/0/0 | 0 | 2 | impact | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-sword-backhand | `animations/player-sword-backhand.json` | 3 | person | 1 | 0.25 | no | 5/3/2/1/0/0 | 0 | 2 | sword-draw swoosh strike swoosh-end recover | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-sword-down-attack | `animations/player-sword-down-attack.json` | 3 | person | 1 | 0.25 | no | 3/3/4/1/0/0 | 0 | 0 | sword-draw strike recover | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-sword-forehand | `animations/player-sword-forehand.json` | 3 | person | 1 | 0.25 | no | 5/3/2/1/0/0 | 0 | 2 | sword-draw swoosh strike swoosh-end recover | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-sword-put-away | `animations/player-sword-put-away.json` | 3 | person | 1 | 0.483 | no | 5/3/2/1/0/0 | 0 | 2 | sword-sheathe | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-sword-put-away-backhand | `animations/player-sword-put-away-backhand.json` | 3 | person | 1 | 0.483 | no | 5/3/2/1/0/0 | 0 | 2 | sword-sheathe | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-sword-sheathe | `animations/player-sword-sheathe.json` | 3 | person | 1 | 0.42 | no | 3/3/2/1/0/0 | 0 | 2 | sword-sheathe | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-sword-side-cut | `animations/player-sword-side-cut.json` | 3 | person | 1 | 0.167 | no | 5/3/2/1/0/0 | 0 | 2 | sword-draw swoosh strike swoosh-end recover | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| player-wall-grip | `animations/player-wall-grip.json` | 3 | person | 1 | 1.2 | yes | 3/2/4/0/0/0 | 0 | 0 |  | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| pterosaur-brake | `animations/pterosaur-brake.json` | 3 | pterosaur | 1 | 0.32 | no | 0/7/0/0/0/0 | 0 | 0 | open | 0 | 0 / 0 / 0 | 0 |  | - |
| pterosaur-death | `animations/pterosaur-death.json` | 3 | pterosaur | 1 | 0.8 | no | 0/5/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| pterosaur-dive | `animations/pterosaur-dive.json` | 3 | pterosaur | 1 | 0.65 | no | 0/7/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| pterosaur-fold | `animations/pterosaur-fold.json` | 3 | pterosaur | 1 | 0.48 | no | 0/6/0/0/0/0 | 0 | 0 | screech commit | 0 | 0 / 0 / 0 | 0 |  | - |
| pterosaur-hit | `animations/pterosaur-hit.json` | 3 | pterosaur | 1 | 0.45 | no | 0/5/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| pterosaur-hover | `animations/pterosaur-hover.json` | 3 | pterosaur | 1 | 1 | yes | 1/5/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| pterosaur-low-recovery | `animations/pterosaur-low-recovery.json` | 3 | pterosaur | 1 | 1 | yes | 0/7/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| pterosaur-reset | `animations/pterosaur-reset.json` | 3 | pterosaur | 1 | 1.2 | no | 0/5/0/0/0/0 | 0 | 0 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| pterosaur-swoop | `animations/pterosaur-swoop.json` | 3 | pterosaur | 1 | 3.65 | no | 0/8/0/0/0/0 | 0 | 0 | screech commit rush brake low reset | 0 | 0 / 0 / 0 | 0 |  | - |
| quadruped-brake | `animations/quadruped-brake.json` | 3 | quadruped | 1 | 0.65 | no | 1/2/4/0/0/0 | 0 | 0 | stop | 0 | 0 / 0 / 0 | 0 |  | - |
| quadruped-head-down | `animations/quadruped-head-down.json` | 3 | quadruped | 1 | 0.55 | no | 1/1/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| quadruped-idle | `animations/quadruped-idle.json` | 3 | quadruped | 1 | 2 | yes | 1/2/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| quadruped-recover | `animations/quadruped-recover.json` | 3 | quadruped | 1 | 1.2 | no | 1/2/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| quadruped-run | `animations/quadruped-run.json` | 3 | quadruped | 1 | 0.65 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| quadruped-rush | `animations/quadruped-rush.json` | 3 | quadruped | 1 | 0.48 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| quadruped-scrape | `animations/quadruped-scrape.json` | 3 | quadruped | 1 | 1.1 | yes | 0/1/1/0/0/0 | 0 | 3 | scrape | 0 | 0 / 0 / 0 | 0 |  | - |
| quadruped-walk | `animations/quadruped-walk.json` | 3 | quadruped | 1 | 1.2 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| rock-thrower-throw | `animations/rock-thrower-throw.json` | 3 | person | 1 | 1.85 | no | 3/3/2/1/0/0 | 0 | 2 | search lift aim release recover | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| shield-defender-bash | `animations/shield-defender-bash.json` | 3 | person | 1 | 1.4 | no | 1/1/2/0/0/0 | 0 | 2 | pullback strike recover | 1 | 0 / 0 / 0 | 0 | leg arm torso | - |
| shield-defender-idle | `animations/shield-defender-idle.json` | 3 | person | 1 | 2 | yes | 1/1/2/0/0/0 | 0 | 2 |  | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| shield-defender-walk | `animations/shield-defender-walk.json` | 3 | person | 1 | 1.2 | yes | 1/0/4/0/0/0 | 0 | 2 |  | 0 | 0 / 0 / 0 | 0 | leg arm torso | - |
| stalker-death | `animations/stalker-death.json` | 3 | stalker | 1 | 0.9 | no | 1/0/0/0/0/0 | 0 | 3 | impact | 0 | 0 / 0 / 0 | 0 | leg | - |
| stalker-hit | `animations/stalker-hit.json` | 3 | stalker | 1 | 0.3 | no | 1/0/0/0/0/0 | 0 | 3 |  | 0 | 0 / 0 / 0 | 0 | leg | - |
| stalker-idle | `animations/stalker-idle.json` | 3 | stalker | 1 | 1.5 | yes | 1/0/0/0/0/0 | 0 | 3 |  | 0 | 0 / 0 / 0 | 0 | leg | - |
| stalker-lunge | `animations/stalker-lunge.json` | 3 | stalker | 1 | 0.8 | no | 1/0/0/0/0/0 | 0 | 3 | windup strike recover | 0 | 0 / 0 / 0 | 0 | leg | - |
| stalker-walk | `animations/stalker-walk.json` | 3 | stalker | 1 | 1 | yes | 1/0/3/0/0/0 | 0 | 3 | step step-middle | 0 | 0 / 0 / 0 | 0 | leg | - |
| triceratops-brake | `animations/triceratops-brake.json` | 3 | triceratops | 1 | 0.65 | no | 1/2/4/0/0/0 | 0 | 0 | stop | 0 | 0 / 0 / 0 | 0 |  | - |
| triceratops-head-down | `animations/triceratops-head-down.json` | 3 | triceratops | 1 | 0.55 | no | 1/1/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| triceratops-idle | `animations/triceratops-idle.json` | 3 | triceratops | 1 | 2 | yes | 1/2/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| triceratops-recover | `animations/triceratops-recover.json` | 3 | triceratops | 1 | 1.2 | no | 1/2/0/0/0/0 | 0 | 4 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| triceratops-run | `animations/triceratops-run.json` | 3 | triceratops | 1 | 0.65 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| triceratops-rush | `animations/triceratops-rush.json` | 3 | triceratops | 1 | 0.48 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |
| triceratops-scrape | `animations/triceratops-scrape.json` | 3 | triceratops | 1 | 1.1 | yes | 0/1/1/0/0/0 | 0 | 3 | scrape | 0 | 0 / 0 / 0 | 0 |  | - |
| triceratops-walk | `animations/triceratops-walk.json` | 3 | triceratops | 1 | 1.2 | yes | 1/2/4/0/0/0 | 0 | 6 |  | 0 | 0 / 0 / 0 | 0 |  | - |

Clips with scale/shear tracks (4): curve-face-blink [scale:left-eye, scale:right-eye], curve-face-crossed-eyes [scale:left-eye, scale:right-eye], curve-face-expressions [scale:left-eye, scale:right-eye], curve-face-wink [scale:left-eye]

Clips with orient tracks (15): club-caveman-slam, person-death, person-hammer-slam, person-pistol-shot, player-gun-aim, player-gun-shot, player-gun-wall-shot, player-sword-backhand, player-sword-down-attack, player-sword-forehand, player-sword-put-away, player-sword-put-away-backhand, player-sword-sheathe, player-sword-side-cut, rock-thrower-throw

Clips with face tracks (28): club-caveman-slam [head:focused/determined/angry], person-death [head:hurt/knocked-out], person-hammer-slam [head:focused/determined/angry], person-hit [head:hurt], person-pistol-shot [head:focused/determined], person-thrust [head:focused/determined], player-balance-backward [head:worried], player-balance-forward [head:panic], player-celebrate [head:happy/delighted], player-climb [head:none], player-climb-off [head:none/relaxed], player-climb-on [head:relaxed/none], player-dash [head:determined], player-death [head:hurt/knocked-out], player-fall [head:focused], player-gun-aim [head:focused], player-gun-charge [head:focused/determined], player-gun-shot [head:determined/focused], player-gun-wall-shot [head:determined], player-heal-gather [head:focused], player-hit [head:hurt/strained], player-jump [head:determined/focused], player-land [head:strained/relaxed], player-sword-down-attack [head:determined], player-sword-sheathe [head:focused/relaxed], player-wall-grip [head:strained], rock-thrower-throw [head:focused/determined/angry], shield-defender-bash [head:focused/determined/angry]

Clips with foot/IK contacts (56): baby-triceratops-charge (7), baby-triceratops-head-down (4), baby-triceratops-idle (4), baby-triceratops-recover (4), baby-triceratops-run (6), baby-triceratops-rush (6), baby-triceratops-scrape (3), baby-triceratops-walk (6), club-caveman-slam (2), person-death (2), person-hammer-slam (2), person-heavy-walk (2), person-hit (2), person-idle (2), person-jump (2), person-pistol-shot (2), person-run (2), person-thrust (2), person-walk (2), player-balance-backward (2), player-balance-forward (2), player-celebrate (4), player-gun-aim (2), player-heal-gather (2), player-hit (2), player-idle (2), player-land (2), player-sword-backhand (2), player-sword-forehand (2), player-sword-put-away (2), player-sword-put-away-backhand (2), player-sword-sheathe (2), player-sword-side-cut (2), quadruped-head-down (4), quadruped-idle (4), quadruped-recover (4), quadruped-run (6), quadruped-rush (6), quadruped-scrape (3), quadruped-walk (6), rock-thrower-throw (2), shield-defender-bash (2), shield-defender-idle (2), shield-defender-walk (2), stalker-death (3), stalker-hit (3), stalker-idle (3), stalker-lunge (3), stalker-walk (3), triceratops-head-down (4), triceratops-idle (4), triceratops-recover (4), triceratops-run (6), triceratops-rush (6), triceratops-scrape (3), triceratops-walk (6)

Explicit track `scale` values used: 

Key eases: linear 2792, smooth 1647; keys with custom curves: 0; keys with explicit IK bend: 10.

### Props

Defaults: `usage` `prop`, `attachment` null, `backView` null, `ink` `#222b32`, `lineWidth` .035, `scale` 1, `grip` (0,0,0), `tip` (1,0,0), `secondGrip`/`muzzle` null; solid `thickness` 0, `outline` null.

| Prop | File | Ver | Usage | Attachment | Back view | Points (grip/tip/2nd/muzzle) | Shapes (filled) | Solids (with outline / thickness>0) | Scale | Line width |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| boot-left | `props/boot-left.json` | 3 | clothing | left-foot-art | - | (0,0) / (0,0) / - / - | 0 (0) | 3 (3 / 3) | 1 | 0.045 |
| boot-right | `props/boot-right.json` | 3 | clothing | right-foot-art | - | (0,0) / (0,0) / - / - | 0 (0) | 3 (3 / 3) | 1 | 0.045 |
| brute-beard | `props/brute-beard.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 1 (1 / 1) | 1 | 0.045 |
| brute-hair | `props/brute-hair.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 2 (2 / 2) | 1 | 0.045 |
| brute-hide-wrap | `props/brute-hide-wrap.json` | 3 | clothing | body-art | - | (0,0) / (0,0) / - / - | 0 (0) | 4 (4 / 4) | 1 | 0.045 |
| cinder-beard | `props/cinder-beard.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 1 (1 / 1) | 1 | 0.045 |
| cinder-hair | `props/cinder-hair.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 2 (2 / 2) | 1 | 0.045 |
| cinder-hide-wrap | `props/cinder-hide-wrap.json` | 3 | clothing | body-art | - | (0,0) / (0,0) / - / - | 0 (0) | 4 (4 / 4) | 1 | 0.045 |
| club-caveman-beard | `props/club-caveman-beard.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 1 (1 / 1) | 1 | 0.045 |
| club-caveman-hair | `props/club-caveman-hair.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 2 (2 / 2) | 1 | 0.045 |
| club-caveman-hide-wrap | `props/club-caveman-hide-wrap.json` | 3 | clothing | body-art | - | (0,0) / (0,0) / - / - | 0 (0) | 4 (4 / 4) | 1 | 0.045 |
| hair-short | `props/hair-short.json` | 3 | hair | head-art | hair-short-back | (0,0) / (0,0) / - / - | 0 (0) | 2 (2 / 2) | 1 | 0.045 |
| hair-short-back | `props/hair-short-back.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 1 (1 / 1) | 1 | 0.045 |
| hammer | `props/hammer.json` | 3 | prop | - | - | (0,0) / (0.8,0) / - / - | 0 (0) | 7 (7 / 7) | 1 | 0.016 |
| pistol | `props/pistol.json` | 3 | prop | - | - | (0,0) / (0.26,0.06) / - / yes | 0 (0) | 5 (5 / 5) | 1 | 0.011 |
| rock-thrower-bag | `props/rock-thrower-bag.json` | 3 | clothing | body-art | - | (0,0) / (0,0) / - / - | 0 (0) | 2 (2 / 2) | 1 | 0.025 |
| rock-thrower-beard | `props/rock-thrower-beard.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 1 (1 / 1) | 1 | 0.045 |
| rock-thrower-hair | `props/rock-thrower-hair.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 2 (2 / 2) | 1 | 0.045 |
| rock-thrower-hide-wrap | `props/rock-thrower-hide-wrap.json` | 3 | clothing | body-art | - | (0,0) / (0,0) / - / - | 0 (0) | 4 (4 / 4) | 1 | 0.045 |
| sandal-left | `props/sandal-left.json` | 3 | clothing | left-foot-art | - | (0,0) / (0,0) / - / - | 0 (0) | 3 (3 / 3) | 1 | 0.045 |
| sandal-right | `props/sandal-right.json` | 3 | clothing | right-foot-art | - | (0,0) / (0,0) / - / - | 0 (0) | 3 (3 / 3) | 1 | 0.045 |
| scavenged-shell | `props/scavenged-shell.json` | 3 | prop | - | - | (0,0) / (0,0) / - / - | 0 (0) | 4 (4 / 4) | 1 | 0.03 |
| sheath | `props/sheath.json` | 3 | prop | - | - | (0,0) / (0.85,0) / - / - | 0 (0) | 3 (3 / 3) | 1 | 0.012 |
| shield-defender-beard | `props/shield-defender-beard.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 1 (1 / 1) | 1 | 0.045 |
| shield-defender-hair | `props/shield-defender-hair.json` | 3 | hair | head-art | - | (0,0) / (0,0) / - / - | 0 (0) | 2 (2 / 2) | 1 | 0.045 |
| shield-defender-hide-wrap | `props/shield-defender-hide-wrap.json` | 3 | clothing | body-art | - | (0,0) / (0,0) / - / - | 0 (0) | 4 (4 / 4) | 1 | 0.045 |
| spear | `props/spear.json` | 3 | prop | - | - | (0,0) / (1.38,0) / yes / - | 0 (0) | 4 (0 / 0) | 1 | 0.013 |
| sword | `props/sword.json` | 3 | prop | - | - | (0,0) / (1.02,0.03) / - / - | 1 (0) | 2 (2 / 2) | 1 | 0.02 |
| throwing-rock | `props/throwing-rock.json` | 3 | prop | - | - | (0,-0.17) / (0,0) / - / yes | 0 (0) | 1 (1 / 1) | 1 | 0.025 |
| wooden-club | `props/wooden-club.json` | 3 | prop | - | - | (0,0) / (0.98,0) / - / - | 0 (0) | 1 (1 / 1) | 1 | 0.025 |

### Physics props

| Physics prop | File | Ver | Shape | Body (motion, mass, inertia, freezeRotation, friction, restitution) | Hit velocity scale | Hit angular kick |
| --- | --- | --- | --- | --- | --- | --- |
| dumbbell | `physics-props/dumbbell.json` | 1 | CompositeShape2D | Dynamic, 2, 1800, False, 0.4, 0.15 | 0.8 | 4.5 |

### Templates

| Template | Ver | Model | Animations (role=clip) | Preview |
| --- | --- | --- | --- | --- |
| curve-face | 1 | curve-face | angry=curve-face-angry, blink=curve-face-blink, crossed-eyes=curve-face-crossed-eyes, expressions=curve-face-expressions, hair-wave=curve-face-hair-wave, smile=curve-face-smile, surprised=curve-face-surprised, wink=curve-face-wink, worried=curve-face-worried | expressions |
| person | 1 | person | death=person-death, heavy-walk=person-heavy-walk, hit=person-hit, idle=person-idle, jump=person-jump, run=person-run, walk=person-walk | idle |
| quadruped | 1 | quadruped | brake=quadruped-brake, head-down=quadruped-head-down, idle=quadruped-idle, recover=quadruped-recover, run=quadruped-run, rush=quadruped-rush, scrape=quadruped-scrape, walk=quadruped-walk | idle |
| stalker | 1 | stalker | death=stalker-death, hit=stalker-hit, idle=stalker-idle, lunge=stalker-lunge, walk=stalker-walk | idle |
| triceratops | 1 | triceratops | brake=triceratops-brake, head-down=triceratops-head-down, idle=triceratops-idle, recover=triceratops-recover, run=triceratops-run, rush=triceratops-rush, scrape=triceratops-scrape, walk=triceratops-walk | idle |

### Entities

Defaults: controller `kind` `walker`, `walkSpeed` 1.5, `range` 1.5, `cooldown` 1, `verticalRange` 2, `brakeSeconds` .3; `health` 3; `mass` 1; movement shape box .55x1.9; hit `id` `hit`, `point` `tip`, shape .3x.3 box, `damage` 1, `start` at 0, `finish` at 1; projectile speed 8, lifetime 3, damage 1.

| Entity | File | Ver | Compiled | Model | Motion set | Controller | Role overrides | Health / mass | Movement shape | Hurt layout (region overrides) | Guard | Equipment (prop@socket) | Actions |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baby-triceratops | `entities/baby-triceratops.json` | 2 | yes | baby-triceratops-rig | standard | walker | - | 8 / 1.5 | Rectangle2D | body | - | - | attack→baby-triceratops-charge hits=1[horns@head-art dmg 3 snd hit] events=3 |
| cinder-gunner | `entities/cinder-gunner.json` | 2 | yes | cinder | standard | walker | - | 7 / 1 | Rectangle2D | standard | - | pistol@gun-hand, cinder-hide-wrap@body-art, cinder-hair@head-art, cinder-beard@head-art, sandal-left@left-foot-art, sandal-right@right-foot-art | attack→person-pistol-shot projectile hits=0 events=1 |
| club-caveman | `entities/club-caveman.json` | 2 | yes | club-caveman-build | standard | walker | walk=person-run, hit=person-hit, death=person-death | 9 / 1 | Rectangle2D | standard | - | wooden-club@sword-hand, club-caveman-hide-wrap@body-art, club-caveman-hair@head-art, club-caveman-beard@head-art | attack→club-caveman-slam hits=1[club-head@wooden-club.tip dmg 3 snd heavy] events=2 |
| flying-pterosaur | `entities/flying-pterosaur.json` | 2 | yes | pterosaur | standard | flying (flight) | hit=pterosaur-hit, death=pterosaur-death | 4 / 0.8 | Rectangle2D | body | - | - | attack→pterosaur-swoop hits=1[beak@head-art dmg 2 snd hit] events=6 |
| hero | `entities/hero.json` | 2 | yes | person | standard | traversal | idle=player-idle, jump=player-jump, fall=player-fall, hit=player-hit, death=player-death | 30 / 1 | Rectangle2D | standard | - | sword@sword-hand, pistol@gun-hand, hair-short@head-art | attack→player-sword-side-cut hits=1[blade@? dmg 2] events=1 next=follow-up recovery=player-sword-put-away; follow-up→player-sword-backhand hits=1[blade@? dmg 2] events=1 next=forehand recovery=player-sword-put-away-backhand; forehand→player-sword-forehand hits=1[blade@? dmg 2] events=1 next=follow-up recovery=player-sword-put-away; shoot→player-gun-shot projectile hits=0 events=1; wall-shot→player-gun-wall-shot projectile hits=0 events=1 |
| maul-brute | `entities/maul-brute.json` | 2 | yes | brute | heavy | walker | - | 22 / 3 | Rectangle2D | standard | - | hammer@sword-hand, brute-hide-wrap@body-art, brute-hair@head-art, brute-beard@head-art, boot-left@left-foot-art, boot-right@right-foot-art | attack→person-hammer-slam hits=1[hammer-head@hammer.tip dmg 5 snd heavy] events=1 |
| player | `entities/player.json` | 2 | yes | person | standard | platformer | - | 5 / 1 | Rectangle2D | standard (legs:pad 0.05) | - | spear@right-grip | attack→person-thrust hits=1[spear-tip@spear.tip dmg 1] events=1; jump/jump→(role clip) hits=0 events=1 |
| rock-thrower | `entities/rock-thrower.json` | 2 | yes | rock-thrower-build | standard | walker | walk=person-run, hit=person-hit, death=person-death | 6 / 1 | Rectangle2D | standard | - | throwing-rock@sword-hand, rock-thrower-hide-wrap@body-art, rock-thrower-bag@body-art, rock-thrower-hair@head-art, rock-thrower-beard@head-art | attack→rock-thrower-throw projectile hits=0 events=2 |
| shield-defender | `entities/shield-defender.json` | 2 | yes | shield-defender-build | standard | walker | walk=shield-defender-walk, hit=person-hit, death=person-death, idle=shield-defender-idle | 8 / 1.5 | Rectangle2D | standard | scavenged-shell | scavenged-shell@left-grip, shield-defender-hide-wrap@body-art, shield-defender-hair@head-art, shield-defender-beard@head-art | attack→shield-defender-bash hits=1[shell-bash@scavenged-shell.tip dmg 2 snd hit] events=2 |
| spear-guard | `entities/spear-guard.json` | 2 | yes | tall-thin | deliberate | walker | - | 3 / 1 | Rectangle2D | standard | - | spear@right-grip | attack→person-thrust hits=1[spear-tip@spear.tip dmg 1] events=1 |
| stalker-pest | `entities/stalker-pest.json` | 2 | yes | stalker | standard | walker | - | 2 / 1 | Rectangle2D | standard | - | - | attack→stalker-lunge hits=1[sting@stinger dmg 1] events=1 |

### Reference graph

#### Model → dependents

- **baby-triceratops-rig**: variants []; clips (11) [baby-triceratops-brake, baby-triceratops-charge, baby-triceratops-death, baby-triceratops-head-down, baby-triceratops-hit, baby-triceratops-idle, baby-triceratops-recover, baby-triceratops-run, baby-triceratops-rush, baby-triceratops-scrape, baby-triceratops-walk]; entities [baby-triceratops]; templates []
- **curve-face**: variants []; clips (9) [curve-face-angry, curve-face-blink, curve-face-crossed-eyes, curve-face-expressions, curve-face-hair-wave, curve-face-smile, curve-face-surprised, curve-face-wink, curve-face-worried]; entities []; templates [curve-face]
- **person**: variants [brute, cinder, club-caveman-build, rock-thrower-build, shield-defender-build, short-broad, tall-thin]; clips (41) [club-caveman-slam, person-death, person-hammer-slam, person-heavy-walk, person-hit, person-idle, person-jump, person-pistol-shot, person-run, person-thrust, person-walk, player-balance-backward, player-balance-forward, player-celebrate, player-climb, player-climb-off, player-climb-on, player-dash, player-death, player-fall, player-gun-aim, player-gun-charge, player-gun-shot, player-gun-wall-shot, player-heal-gather, player-hit, player-idle, player-jump, player-land, player-sword-backhand, player-sword-down-attack, player-sword-forehand, player-sword-put-away, player-sword-put-away-backhand, player-sword-sheathe, player-sword-side-cut, player-wall-grip, rock-thrower-throw, shield-defender-bash, shield-defender-idle, shield-defender-walk]; entities [cinder-gunner (via cinder), club-caveman (via club-caveman-build), hero, maul-brute (via brute), player, rock-thrower (via rock-thrower-build), shield-defender (via shield-defender-build), spear-guard (via tall-thin)]; templates [person]
- **pterosaur**: variants []; clips (9) [pterosaur-brake, pterosaur-death, pterosaur-dive, pterosaur-fold, pterosaur-hit, pterosaur-hover, pterosaur-low-recovery, pterosaur-reset, pterosaur-swoop]; entities [flying-pterosaur]; templates []
- **quadruped**: variants []; clips (8) [quadruped-brake, quadruped-head-down, quadruped-idle, quadruped-recover, quadruped-run, quadruped-rush, quadruped-scrape, quadruped-walk]; entities []; templates [quadruped]
- **stalker**: variants []; clips (5) [stalker-death, stalker-hit, stalker-idle, stalker-lunge, stalker-walk]; entities [stalker-pest]; templates [stalker]
- **triceratops**: variants []; clips (8) [triceratops-brake, triceratops-head-down, triceratops-idle, triceratops-recover, triceratops-run, triceratops-rush, triceratops-scrape, triceratops-walk]; entities []; templates [triceratops]

#### Entity → model/variant, clips, props

- **baby-triceratops** → baby-triceratops-rig; motion set `standard`; role clips [brake=baby-triceratops-brake, death=baby-triceratops-death, head-down=baby-triceratops-head-down, hit=baby-triceratops-hit, idle=baby-triceratops-idle, recover=baby-triceratops-recover, run=baby-triceratops-run, rush=baby-triceratops-rush, scrape=baby-triceratops-scrape, walk=baby-triceratops-walk]; action clips [baby-triceratops-charge]; props []
- **cinder-gunner** → cinder (base person); motion set `standard`; role clips [death=person-death, hit=person-hit, idle=person-idle, jump=person-jump, run=person-run, walk=person-walk]; action clips [person-pistol-shot]; props [cinder-beard, cinder-hair, cinder-hide-wrap, pistol, sandal-left, sandal-right]
- **club-caveman** → club-caveman-build (base person); motion set `standard`; role clips [death=person-death, hit=person-hit, idle=person-idle, jump=person-jump, run=person-run, walk=person-run]; action clips [club-caveman-slam]; props [club-caveman-beard, club-caveman-hair, club-caveman-hide-wrap, wooden-club]
- **flying-pterosaur** → pterosaur; motion set `standard`; role clips [brake=pterosaur-brake, death=pterosaur-death, dive=pterosaur-dive, fold=pterosaur-fold, hit=pterosaur-hit, hover=pterosaur-hover, idle=pterosaur-hover, low-recovery=pterosaur-low-recovery, reset=pterosaur-reset]; action clips [pterosaur-swoop]; props []
- **hero** → person; motion set `standard`; role clips [death=player-death, fall=player-fall, hit=player-hit, idle=player-idle, jump=player-jump, run=person-run, walk=person-walk]; action clips [player-gun-shot, player-gun-wall-shot, player-sword-backhand, player-sword-forehand, player-sword-put-away, player-sword-put-away-backhand, player-sword-side-cut]; props [hair-short, pistol, sword]
- **maul-brute** → brute (base person); motion set `heavy`; role clips [death=person-death, hit=person-hit, idle=person-idle, run=person-run, walk=person-heavy-walk]; action clips [person-hammer-slam]; props [boot-left, boot-right, brute-beard, brute-hair, brute-hide-wrap, hammer]
- **player** → person; motion set `standard`; role clips [death=person-death, hit=person-hit, idle=person-idle, jump=person-jump, run=person-run, walk=person-walk]; action clips [person-thrust]; props [spear]
- **rock-thrower** → rock-thrower-build (base person); motion set `standard`; role clips [death=person-death, hit=person-hit, idle=person-idle, jump=person-jump, run=person-run, walk=person-run]; action clips [rock-thrower-throw]; props [rock-thrower-bag, rock-thrower-beard, rock-thrower-hair, rock-thrower-hide-wrap, throwing-rock]
- **shield-defender** → shield-defender-build (base person); motion set `standard`; role clips [death=person-death, hit=person-hit, idle=shield-defender-idle, jump=person-jump, run=person-run, walk=shield-defender-walk]; action clips [shield-defender-bash]; props [scavenged-shell, shield-defender-beard, shield-defender-hair, shield-defender-hide-wrap]
- **spear-guard** → tall-thin (base person); motion set `deliberate`; role clips [death=person-death, hit=person-hit, idle=person-idle, walk=person-walk]; action clips [person-thrust]; props [spear]
- **stalker-pest** → stalker; motion set `standard`; role clips [death=stalker-death, hit=stalker-hit, idle=stalker-idle, walk=stalker-walk]; action clips [stalker-lunge]; props []

#### Props → attachment sockets and wearers

- **boot-left** (clothing): attachment left-foot-art (on person); used by entities [maul-brute]
- **boot-right** (clothing): attachment right-foot-art (on person); used by entities [maul-brute]
- **brute-beard** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [maul-brute]
- **brute-hair** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [maul-brute]
- **brute-hide-wrap** (clothing): attachment body-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [maul-brute]
- **cinder-beard** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [cinder-gunner]
- **cinder-hair** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [cinder-gunner]
- **cinder-hide-wrap** (clothing): attachment body-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [cinder-gunner]
- **club-caveman-beard** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [club-caveman]
- **club-caveman-hair** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [club-caveman]
- **club-caveman-hide-wrap** (clothing): attachment body-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [club-caveman]
- **hair-short** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [hero]
- **hair-short-back** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities []
- **hammer** (prop): attachment -; used by entities [maul-brute]
- **pistol** (prop): attachment -; used by entities [cinder-gunner, hero]
- **rock-thrower-bag** (clothing): attachment body-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [rock-thrower]
- **rock-thrower-beard** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [rock-thrower]
- **rock-thrower-hair** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [rock-thrower]
- **rock-thrower-hide-wrap** (clothing): attachment body-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [rock-thrower]
- **sandal-left** (clothing): attachment left-foot-art (on person); used by entities [cinder-gunner]
- **sandal-right** (clothing): attachment right-foot-art (on person); used by entities [cinder-gunner]
- **scavenged-shell** (prop): attachment -; used by entities [shield-defender]
- **sheath** (prop): attachment -; used by entities []
- **shield-defender-beard** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [shield-defender]
- **shield-defender-hair** (hair): attachment head-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [shield-defender]
- **shield-defender-hide-wrap** (clothing): attachment body-art (on baby-triceratops-rig, person, pterosaur, quadruped, triceratops); used by entities [shield-defender]
- **spear** (prop): attachment -; used by entities [player, spear-guard]
- **sword** (prop): attachment -; used by entities [hero]
- **throwing-rock** (prop): attachment -; used by entities [rock-thrower]
- **wooden-club** (prop): attachment -; used by entities [club-caveman]

#### Unreferenced assets

- Clips not referenced by any motion set, entity, action or template (14): player-balance-backward, player-balance-forward, player-celebrate, player-climb, player-climb-off, player-climb-on, player-dash, player-gun-aim, player-gun-charge, player-heal-gather, player-land, player-sword-down-attack, player-sword-sheathe, player-wall-grip
- Props not referenced by any entity (2): hair-short-back, sheath
- Models with no entity (directly or via a variant): curve-face, quadruped, triceratops
- Variants with no entity: short-broad
- Models with no template: baby-triceratops-rig, pterosaur

### Resolution results

| Id | Kind | Resolved controls | Measures (resolved values) | Drawn height |
| --- | --- | --- | --- | --- |
| baby-triceratops-rig | model | 18 |  | 1.33 |
| brute | variant of person | 15 | arm=0.72, leg=0.754, torso=0.682 | 2.123 |
| cinder | variant of person | 15 | arm=0.622, leg=0.714, torso=0.617 | 2.01 |
| club-caveman-build | variant of person | 15 | arm=0.622, leg=0.714, torso=0.617 | 2.01 |
| curve-face | model | 28 |  | 1.575 |
| person | model | 15 | arm=0.654, leg=0.793, torso=0.65 | 2.125 |
| pterosaur | model | 22 |  | 1.55 |
| quadruped | model | 18 |  | 1.41 |
| rock-thrower-build | variant of person | 15 | arm=0.687, leg=0.714, torso=0.617 | 2.01 |
| shield-defender-build | variant of person | 15 | arm=0.622, leg=0.714, torso=0.617 | 2.01 |
| short-broad | variant of person | 15 | arm=0.576, leg=0.65, torso=0.585 | 1.911 |
| stalker | model | 10 | leg=0.854 | 1.09 |
| tall-thin | variant of person | 15 | arm=0.753, leg=0.952, torso=0.728 | 2.381 |
| triceratops | model | 18 |  | 1.9 |
