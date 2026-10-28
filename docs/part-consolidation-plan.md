# Part consolidation plan

Date: 2026-10-26.

Status: complete (2026-10-27). S0-S6 are implemented and verified. Parts use one authored definition, geometry collection and document workflow; the temporary migration paths are retired.

This follows the [completed character migration](character-system-migration-plan.md). Keep its working rig, proportions, constraints, module composition, collision registration, animation, document/undo infrastructure and game artwork. The next objective is fewer independent definitions, loaders, placement paths, inspectors and spawners.

Scope: Core, Studio, the game, authored content and relevant tests. Noodle is excluded from the migration; its existing bridge only follows the public geometry type rename. Do not restart the previous migration or build a new component framework.

## 1. Decisions from the discussion

1. A reusable part and its asset are the same authored definition. The word “asset” describes how it is stored and referenced, not another kind of part.
2. A part can contain multiple geometry entries: shapes, curves, textured rectangles and meshes. Each entry can have local placement/fitting, character layer and optional rendering/collision settings.
3. Named attachment locations are ordinary data. Grip, tip, muzzle, eye locations and connections are names, not dedicated fields required on every object.
4. A shared part definition and a placed instance still have different lifetimes. Keep a small instance reference with its asset ID, instance ID, placement and overrides. Two gloves share geometry, not pose, physics or playback state.
5. Put game configuration under one optional Behavior class. Reuse existing typed settings and collections; a future sectioned key/value store is deferred until another consumer needs it.
6. A passive physical object is the ordinary part definition with collision geometry and existing physics settings. It needs no PhysicsPropAsset format or class written for that particular object.
7. Remove the dumbbell-specific HitVelocityScale and HitAngularKick experiment rather than moving it into another mandatory schema.
8. Animate a small useful set of targets through shared timing/bindings: poses, exposed parameters, material values, texture frames and enabled/selection state. A kinematic path is a motion source for a transform or body.
9. Keep the faces used by the game. Demo-only curve faces, wave examples and generated studies may be removed or moved to test fixtures after checking references.
10. Ship in stages. Studio support, conversions and deletion of superseded paths belong to the stage that changes the capability.

The strange combinations in Studio's All list are not evidence of corrupted game art. The current library includes extracted person/quadruped body modules and face/composition studies. Some modules are dependencies of the centaur study. Do not delete every unfamiliar file or reauthor the working game models to “repair” that list.

## 2. Target vocabulary

Conceptual names below describe responsibilities. Prefer evolving current types over adding replacement types beside them.

~~~text
Part                         shared definition loaded from a file
  Id, Name
  Geometry[]                 local shape / curve / image / mesh entries
  Locations[]                optional named attachment frames
  Attachments[]              optional references to other parts
  Rig / Animation            optional, using the existing machinery
  Physics                    optional existing body settings
  Behavior                   optional game configuration

PartInstance                 this occurrence of a shared Part
  InstanceId, PartId
  Attachment / LocalPose
  Fit / local overrides
~~~

This is a consolidation of authored data. Runtime poses, GPU resources, physics bodies and animation clocks remain instance state and are not serialized into the shared definition.

### Part and geometry

S1 evolves the reusable container into [Part](../App2d.Core/Characters/Authored/Part.cs), with one `Geometry` collection of [PartGeometry](../App2d.Core/Characters/PartGeometry.cs) entries. The old `PartAsset` and `PuppetPart` types are gone. Shapes, curves, textured rectangles and meshes share entry placement, material resolution, visibility, animation, picking and inspection. `CharacterModel.Parts` retains its serialized name while using those same entries; it remains the working rig envelope.

Do not merge every field of both classes into a larger ambiguous class. A geometry entry is not another asset category or a second part system.

Use one geometry-entry path for procedural shapes, curves, textured rectangles and meshes. Reuse the existing shape/curve values and material. Preserve per-entry local physical Z for meshes separately from planar character layer.

An invisible collider can be a geometry entry with collision enabled and rendering absent. A decorative curve need not have collision. Visual and collision shapes can be separate entries sharing a placement; the visible outline is not automatically the collision boundary.

Keep the existing separation between inherited similarity poses and leaf affine geometry fitting. Dimension-driven attachments still follow bone length. This plan does not reintroduce nonuniform scale into the rig hierarchy.

### Named locations

Move PropAsset's dedicated Grip/Tip/Muzzle/SecondGrip vocabulary onto the shared named-location mechanism. Built-in game code can use constants for those names, but the schema accepts other names.

Preserve genuine local 3D positions on weapon points, including a grip's physical Z. The current PartLocation is planar with a layer offset; extend its shared representation only as needed rather than storing physical Z in Layer or introducing another weapon-only attachment system.

Geometry and attachments can bind to a rig node, a named location or the part root. Stored placement remains local; editor character/world-coordinate views convert edits back into it. One resolution path handles composition and inverse placement.

### Rendering, collision, physics and behavior

| Configuration | Contents and scope | Reuse |
| --- | --- | --- |
| Render material | Fill, texture, tint, outline, stroke and appearance properties. Keeping this fairly broad is acceptable. | RenderMaterialDefinition2D and current renderer/texture cache |
| Collision settings | Enabled/sensor state, category, mask and owner identity at runtime. | GeometryCollision, Collider2D and shared shape collision |
| Physics settings | Motion type, finite positive mass/inertia, rotation lock, friction, restitution and gravity settings for a body. | PhysicsBodySettings2D and PhysicsWorld2D |
| Behavior | Controller/action/role bindings and game-specific damage, receiving damage, guarding, sound/event configuration. | Behavior and existing typed action/controller records |

Use existing PhysicsBodySettings2D as the pragmatic physics block initially. There is no need to split it into several new “material” and “body descriptor” classes merely to match terminology. A body owns its mass, inertia and velocity; geometry entries contribute collision shapes. Do not create a body or duplicate mass for every rendered piece.

Behavior is optional. Without a damage-receiving behavior, an ordinary object has no invented health pool and is not registered as a combatant merely to make streaming or rendering work. Keep behavior evaluation/validation in its owning code, outside geometry and rig math.

Move EntityAsset's Movement/Colliders geometry onto the appropriate collision-enabled geometry entries. Behavior selects the movement shape and attack/hurt/guard roles by stable geometry/collider IDs; it does not own another copy of their shape or attachment transform. The character's stable movement collider can remain on the root while animated hurt and weapon entries follow their rig bindings.

### Loading and spawning

A common loader still needs to know the Part schema and resolve shape kinds, references and optional settings. The simplification is one shared schema and instantiation path, with no object-specific C# class for a dumbbell, crate or new decorative object.

Instantiation creates the resources requested by the definition: visual geometry, optional colliders, optional physics body, and optional behavior state. A static decoration, dynamic dumbbell and rigged character can use the same definitions without all paying for every capability.

Start physical instantiation with root-level passive objects. An animated attachment with a sensor is not a simulated limb. More elaborate per-bone physical simulation is outside this consolidation.

Dynamic physics owns a body's resulting pose. Animation/path motion may drive a kinematic body or a nonphysical part. Do not let physics and animation independently overwrite the same transform.

## 3. Animation: unify the useful pieces

Reuse [KeyframeSampling](../App2d.Core/Animation/KeyframeSampling.cs), existing MotionClip/channel bindings, [AnimationClip2D](../App2d.Core/Animation/AnimationClip2D.cs), [AnimationPlayer2D](../App2d.Core/Animation/AnimationPlayer2D.cs) and existing playback-clock rules. They already supply continuous key sampling and generic timed frame sequences.

| Target | Sampling rule | First useful example |
| --- | --- | --- |
| Joint/local pose and exposed dimension | Existing numeric/angle/parameter sampling | A walking arm or a changing bone length |
| Material tint/alpha and selected appearance values | Numeric/color sampling | A fading or flashing geometry entry |
| Texture frame | Held frame selection over frame durations | Images or atlas regions animated on one rectangle |
| Geometry/attachment visibility and collider activation | Held state plus existing interval-crossing rules | Swapped artwork and a weapon sensor's active interval |
| Named part/animation selection | Existing held selection bindings | Another glove or a face expression clip |
| Path position/orientation | Sample a motion source at a defined time | A moving platform or part following a curve |

Expose only implemented useful targets. Do not build reflection-based animation of every serialized property.

Texture animation must:

- Work on a plain textured rectangle and on one attached to an animated rig.
- Use a stable rectangle and anchor as images change; texture pixel dimensions do not silently resize the geometry.
- Support timed texture references first; a frame can also reference a source rectangle/UV region when atlas support is added in the same path.
- Share a clip clock with bone motion by default, with the existing explicit local/mapped clock option for independent playback.
- Sample the selected frame from time during seeking, rather than advance a separate mutable player during each draw.
- Reuse texture loading/caching. A frame change does not create new geometry assets, reload the same texture every tick or dirty a document.

Continuous values interpolate; frame/selection values hold. Sharing timing does not require forcing every value into a float or duplicating the generic frame-animation implementation.

The shared image draw path maps either the full texture or the sampled frame's normalized source region onto the same authored rectangle. Source-region selection changes UVs, not world dimensions or placement.

For curves, reuse existing control-point bindings. Distinguish a point the curve passes through from a Bezier handle that influences its shape. Curve deformation and optional effects belong to the geometry operation, not the definition of every Part.

Reuse [IKinematicMotion2D](../App2d.Core/Physics/Motion/IKinematicMotion2D.cs) and [KinematicEntity2D](../App2d.Core/Physics/Motion/KinematicEntity2D.cs) where their semantics fit. A path produces motion, not rendering/physics material. Preserve fixed-step velocity calculation for physical kinematic objects; editor seeking is pure sampling.

## 4. Current duplication to retire

| Current route | Destination | Condition for deletion |
| --- | --- | --- |
| PuppetPart as a second “part” concept | Geometry entry inside the shared Part definition | Converted callers use the geometry entry directly; no generic wave/face field is retained merely for a demo |
| PartAsset as a distinct public concept | The one shared Part definition | Catalog, instance references and Studio use one resource identity and document path |
| PropAsset / PropShape / independently handled solids | Common Part geometry and named locations | Weapons/clothing keep their appearance, grips and collision bindings through the shared path |
| Equipment-specific art placement | Ordinary part instances on locations | Existing game equipment selects instances/behavior, without a second geometry renderer |
| PhysicsPropAsset and physics-props catalog/codec | Part with optional collision/physics | Dumbbell and another passive object load/spawn through the shared schema |
| PhysicsProp2D's bespoke runtime and fake combatant role | General part instantiation with optional body/behavior | Placement, streaming, state and presentation work without fake health or enemy registration |
| HitVelocityScale / HitAngularKick | Removed experiment | No supported asset/consumer requires them; tests are revised to verify ordinary physics rather than the old artificial kick |
| EntityAsset's game fields scattered around appearance/geometry | One optional Behavior aggregate | Consumers and documents read the aggregate; no parallel copies remain |
| EntityAsset's separate Movement/Colliders geometry collections | Collision-enabled geometry entries and stable role references | Existing sensor/body registration uses those entries without a second geometry owner |
| Hardcoded prop point fields | Shared named locations | Grip/tip/muzzle/second-grip bindings preserve meaning and all consumers use lookup |
| Separate texture playback timing added for parts | Existing frame clips and clock context | Seeking and joint/texture synchronization use one defined time source |
| Demo-only face/wave content in ordinary catalog | Removed or isolated test fixtures | Reference audit preserves game faces and any deliberately retained reusable data |

“One class” means one authored Part definition, not deleting the few small value/settings types needed to describe its contents. Do not keep both old and new versions indefinitely under different names.

## 5. Implementation order

Each stage is a small group of reviewable changes. Update its runtime path, file conversion and Studio commands together. Checked items below record completed, verified stages; later capabilities are not implied by the public type rename.

S1 establishes the shared representation. S2 and S3 then address separate consumers: the passive-physics proof can ship after S1 without waiting for every weapon to convert. S5's image/material animation can also begin once the shared geometry targets exist; it need not wait for all Behavior consumers to move. S6 closes the conversion and deletion work.

### S0. Finish the preview bug checkpoint and clarify the asset list

- [x] S0a. Fix Appearance selection so a selected library part is not combined with a fallback prop, and fallback prop selection actually enables its preview. Two regressions failed before the change; the 28 focused library-part, appearance and editor-session tests passed afterward.
- [x] S0b. Make the browser/preview distinguish a whole module, a reusable part and a study/demo. Previewing a full lower-body module on a wearer must be an explicit, clearly labeled operation rather than look like saved corruption.

Current fix: [EditorSession.SetMode](../App2d.Core/Characters/Editing/EditorSession.cs) and [LibraryPartEditingTests](../App2d.Tests/Authored/LibraryPartEditingTests.cs). It is in the working tree; no asset JSON was changed. The All-list clarification did not reproduce the user's original report as game-art corruption.

Gate: beard and module selections have one consistent active asset and preview. Switching workspaces does not silently change the subject or save a preview attachment.

### S1. Establish Part plus geometry entries

- [x] S1a. Evolve the current PartAsset into the shared Part definition and narrow PuppetPart into geometry-entry data. Preserve the existing instance-reference and resolved-resource roles.
- [x] S1b. Route model artwork, library parts, picking and their inspectors through that entry representation. Avoid copying rig fields into another new parallel document type.

Keep CharacterModel as the working rig envelope during this step. Where a Part needs a rig, reuse the rig data/representation instead of duplicating its evaluator. A root character can ultimately be a rigged Part; changing that envelope is justified only when it removes a real remaining wrapper/path.

Gate: an existing character, one repeated glove/shoe asset and a multi-piece static part render, edit, undo and save/reopen unchanged. Namespaced animation/variant target IDs remain stable.

Delete: duplicate per-entry placement/material/binding handling. A rename without a shared execution path does not complete this stage.

### S2. Absorb props and named points

- [x] S2a. Convert clothing and weapons to Part geometry, with arbitrary named locations for grip, tip, muzzle and other points.
- [x] S2b. Move equipped-prop rendering, attachment preview and geometry editing onto the shared instancing path; retain equipment selection and attack rules under Behavior.

Gate: the same asset can appear on two sockets; beard/hair/clothing remain correct; sword sheathing, both facings and weapon tilt/twist preserve grip/tip/muzzle positions. Physical mesh Z and character layer remain distinct.

Delete: PropAsset's separate codec/catalog route, PropShape's duplicate placement and the extra prop drawing/preview path once their last supported consumers convert. Preserve geometry-building helpers such as extrusion when still useful.

### S3. Replace the physics-prop special case

- [x] S3a. Load and instantiate a Part with collision and PhysicsBodySettings2D through the shared catalog and world-object path.
- [x] S3b. Convert dumbbell.json and its world placement, presentation and state handling. Remove HitVelocityScale, HitAngularKick and its fake combatant health.

The retired `PhysicsProp2D` was an enemy/combatant with a million-point health pool and TakeDamage that directly changes velocities. That is the concrete special case to remove, not an API to preserve under a different name.

Physics still requires the existing runtime body and collider. Shared instantiation creates them from settings; there is no Dumbbell class or another per-object actor class.

Passive objects respond through ordinary physical contact. If an attack must impart an impulse, use an explicit gameplay action applying the physics engine's impulse at a contact point. Do not retain the two experimental gains just to preserve a test.

Gate: a dumbbell and another differently shaped JSON part fall, collide, rotate and obey friction without new object classes. Rendered and physical poses agree; disable/re-enable, streaming, disposal and state restoration retain the configured motion type and do not leak registrations. An object without Behavior has no health or AI.

Move passive-object lifetime/state handling to the common world-object path rather than preserving IEnemyActor2D just to retain the old registration API. Reuse existing world ownership/streaming infrastructure; this does not require redesigning every game actor or introducing another entity framework.

Delete: PhysicsPropAsset, its dedicated loader/catalog collection and PhysicsProp2D-specific spawner/view/state branch after their usages convert. Old TumbleProp world placements can map to a generic asset reference at the loading boundary.

S3 implementation review (2026-10-27):

- Parts now own optional collision, physics, surface and path settings. The existing `AuthoredObject2D` and `ObjectPlacement2D` use Part directly. `PhysicsPropAsset`, `PhysicsProp2D`, their separate catalog and enemy view/state branch are deleted. The former `ObjectAsset2D` schema is also deleted, avoiding parallel Part and platform payloads.
- The dumbbell and a new rectangular crate share the same world runtime, with no health or AI. All eight platform JSON files also use Parts, preserving paths, one-way surfaces and rider carrying. These resources retain world units and scale 1; character resources are unchanged.
- Rendering uses shared Part geometry/material/animation drawing. Collision uses rigid shapes with the same pivot and scale, or an explicit collision override. Mesh/animated physical Parts require that override; per-geometry gameplay collision remains S4 work.
- Streaming, pause/resume, disposal and object state restoration preserve configured motion type, velocities and clock. Passive objects live in world content/state, and old TumbleProp references map to dumbbell at the loading boundary. Texture roots survive snapshot transport. No experimental hit gains remain in normal runtime code.
- Originals are backed up under `artifacts/part-consolidation/s3-original/Assets`; explicit offline conversions and reports are under `s3-converted-authored` and `s3-converted-world`. All nine source files convert repeatably; ordinary loading rejects the retired formats.
- Verification: 1,254 affected authoring/gameplay/physics/presentation/loading tests passed, followed by 14 focused simulation/lifecycle checks after simplifying the legacy placement adapter. Studio built; native editor checks passed 158/158. The object rendering study produced five frames, inspected at the initial and settled poses: the two passive bodies fall/rotate/stack while the platform and orbital path still run. Physical tests compare rough/slippery contact, scaled pivots, all three motion types, clock restore, streaming/reload/disposal and allocation-free steady presentation.
- Production C# is 35 lines smaller, including the offline converter; additional regression coverage increases the total C# diff by 90 lines. S4 Behavior/collider ownership and S5 texture/material/path tracks remain pending.

### S4. Gather game-specific configuration under Behavior

- [x] S4a. Evolve EntityBehaviors into the one optional Behavior aggregate, moving existing controller, action/role, strike, guard, reaction and event configuration under it.
- [x] S4b. Update game consumers and the existing typed inspector. Collapse EntityAsset's remaining wrapper only as its useful responsibilities move onto Part/Behavior references.

During this stage, finish moving Movement/Colliders shapes and placement onto geometry entries. Behavior retains stable references and activation/interaction policy, including attack windows; the common collision path retains shape construction, attachment, filtering and registration.

Reuse useful nested records and collections. Different attacks can still have different strikes; “one Behavior class” need not flatten several attacks into one damage field. Keep body mass distinct from a gameplay knockback-resistance multiplier.

No new key/value framework, plugin registry or universal behavior interpreter is required. Do not move damage, sounds and controller rules back onto geometry to reduce the apparent class count.

Gate: current player/enemy attacks, guards, projectiles, reactions, accepted impact sounds and replay/state behavior work. Generic geometry/rig evaluation ignores Behavior. Passive objects do not inherit default character health or combat registration.

Delete: duplicate game configuration on appearance/prop/geometry definitions and obsolete forwarding wrappers. Retain game-specific algorithms inside their owning behavior/controller implementation.

S4 implementation review (2026-10-27):

- Characters are Parts with a model reference, equipment and optional `Behavior`. The 11 shipped character definitions now live under `parts/`. `EntityAsset`, `EntityBehaviors`, `MovementDef`, `ColliderDef`, `GuardDef`, the entity file kind and the separate entity document/catalog store are removed. `Entities` remains the compiled gameplay view of Parts that opt into Behavior.
- Movement, hurt, attack and guard shapes live in `Part.Geometry`; Behavior selects stable geometry IDs and owns activation, controller, action, strike, reaction and sound policy. Geometry collision supplies enabled state and mask. Missing material makes a collider invisible. Passive Parts retain no Behavior, invented health or combat registration.
- Drawing and collision use the same geometry attachment frame, including socket/equipment points, local offsets, uniform stretch and both facings. Draw depth remains separate from collision position and physical Z. Body mass remains in Physics; the old gameplay mass is now named `KnockbackResistance`.
- Studio's existing entity workspace edits the same Part document, including collision geometry, typed behavior, equipment, undo and save. Character Parts retain their entity browser category; the ordinary part/module preview list excludes them. No second authoritative asset or universal behavior interpreter was introduced.
- Explicit offline conversion preserves IDs, defaults, controller behavior and evaluated legacy placements. The report identifies the pterosaur beak's old ignored socket offset; new geometry offsets apply consistently. Original files and the validated conversion report are retained under `artifacts/part-consolidation/s4-original` and `s4-final-converted`. Ordinary load/save does not accept the retired entity format.
- Verification: 1,264 of 1,265 affected regression tests passed together; one audio decoder timing test missed its startup deadline during the concurrent native editor run, then all five audio performance tests passed in isolation. All 29 focused seed/part/reference checks passed with existing reference data unchanged. The final seed cleanup passed seven focused checks, and root-frame/movement validation passed 14 focused checks. Native Studio editor checks passed 158/158 and acceptance passed 46/46; collider attachment artwork was inspected. New tests cover shared Part identity, conversion defaults, passive noncombat behavior and matching drawn/collision frames.
- Production C# outside offline conversion is 63 lines smaller. Conversion support adds 86 lines, making the complete production C# change 23 lines larger; regression tests add 152 lines. The reduction is in independent definitions and runtime/editor paths, not a net deletion once migration coverage is included.

### S5. Share animation binding and add texture frames

- [x] S5a. Bind the useful existing pose/parameter, material and enabled/selection targets through shared sampling/playback rules.
- [x] S5b. Add a timed image sequence on a rectangle, then exercise it on a moving bone. Reuse AnimationClip2D and existing clock semantics; add source-region UV selection through the same frame binding when required.
- [x] S5c. Reuse the existing kinematic motion interface for a path-driven Part/body and expose it as motion configuration, not a material.

Gate: a bone-animated object can simultaneously change texture frames and material alpha. Seeking to a given time always produces the same pose/frame/color without advancing simulation or playing sound. Rect size/anchor remains stable across different image sizes; looping/holding and both facings work. A kinematic path retains its correct fixed-step velocity.

Delete: duplicated frame clocks, selection sampling and material/geometry timing branches introduced during the consolidation. Do not rewrite the generic animation library or attempt animation of all fields.

S5 implementation review (2026-10-27):

- `tint` (RGB) and `alpha` (X) are ordinary geometry-targeted clip channels, including exposed `instance/piece` targets. Values in 0..1 multiply the authored material and blend through existing masked layers. Fit, visibility, poses and normalized bend parameters keep their existing bindings. Held visibility and expression selection now share `KeyframeSampling.Held`; continuous tracks still share the existing easing/curve sampler.
- A material can declare `textureAnimation.frames` with relative image paths, durations in seconds and optional normalized `source` regions. `AnimationClip2D<TextureFrame>` owns frame selection; `PlaybackClock.SampleTime` supplies the same loop/hold mapping to frames, poses, layers and path motion. No per-texture player or render-time clock was added. A pose carries its sampled time, and standalone static Parts use their supplied instance time. Texture dimensions do not change authored geometry. An empty frame list explicitly clears an inherited sequence.
- Material tint and alpha apply to images, shapes, curves, paint and meshes through the shared drawing path. Atlas frames alter UVs only. The existing texture cache still loads image resources; static geometry with animated textures now refreshes its shader on supplied time changes.
- Studio edits frame paths/durations/regions and material values in the shared geometry inspector. Geometry rows in Animate expose tint/alpha, fit and visibility with the existing key/curve/undo tools, separately from a bone with the same name. Standalone texture/path previews use the existing transport with an unsaved timeline descriptor. Part path controls edit the existing `Motion` definition; the world runtime still computes kinematic velocity from fixed-step displacement.
- This stage adds optional fields and track kinds; existing Part/clip files need no conversion, and shipped artwork/reference data is unchanged. Verification covers repeat/hold boundaries, seeking backwards, both facings, shared-material overrides, namespaced layers, editor save/reopen/undo and fixed-step path velocity. Native proof images use different pixel sizes and an atlas crop on a moving bone; the proof files stay in the smoke workspace.
- Verification: 1,111 affected regression tests passed, including eight new S5 cases. The final 55 focused checks also passed, adding coverage for held visibility/material defaults in clip excerpts and texture sampling on a resting rig's mapped layer clock. Native editor checks passed 170/170 and acceptance passed 46/46; the first frame, cropped second frame and fading/moving bone were inspected in `artifacts/part-consolidation/s5-editor`. The final Studio build passed.

### S6. Remove unwanted studies and finish the cutover

- [x] S6a. Audit actual references and remove the unused curve-face and wave demonstrations. Preserve face-person and other assets used by game models, variants, profiles and clips.
- [x] S6b. Remove unused wave-specific character integration, demo inspectors/generators and any remaining obsolete formats/paths; retain generic curve math that still has a supported use.
- [x] S6c. Complete the Studio walkthrough, migrate the rest of the authored bundles and update documentation/dependency listings.

The 2026-10-26 cleanup completed the wave-specific portion of S6b: the character wave modifier, its inspector, the demo conversion command and the unused payload interface are removed. Core's generic normal-offset curves remain. S6 closes the remaining format and collision paths below.

A generated module or study is not automatically unused. In particular, centaur currently references person-upper and quadruped-lower. Keep deliberately supported composition examples or move the entire dependent example to test content together.

Gate: one browser and editing workflow handles a passive prop, static decorative part, textured/animated rectangle, weapon and rigged character. No duplicate authoritative definition, rendering or spawning route remains for converted content.

S6 implementation review (2026-10-27):

- Removed `FormatConversion`, `PartGeometryConversion`, `PropPartConversion`, `WorldPartConversion`, `EntityPartConversion`, `--convert-formats`, and the now-unused tolerant authored reader. Conversion-only tests are retired; current-format geometry/placement tests remain. Supported puppet/library imports, rig authoring, module extraction and game-face generation remain.
- Removed the separate `Part.Collision` shape and implicit collision from every rendered shape. A body's enabled `geometry[].collision` entries now supply its rigid shape through the shared placement, pivot and scale. Decorative shapes/curves/meshes do not collide automatically; collision-only geometry has no material. One body keeps one mass and mask; enabled entries must share that mask, intersected with the spawning world's mask. Animated artwork does not move a rigid body's bind-pose collision. Gameplay's pose-driven colliders retain their existing role-specific registrations.
- Part format **4** makes that participation change explicit. Migrated all **58** committed Part files: **50** in the authored library and **8** level-local platforms. The **10** physical assets mark their existing geometry with `collision: {}`. An inverse comparison against the saved originals verifies that only versions and collision participation changed: IDs, geometry, units, animation, materials, body settings, paths and references are preserved. Originals and the per-file migration ledger are under `artifacts/part-consolidation/s6-original` and `s6-asset-migration.json`; Git retains every earlier format.
- Studio's shared Part inspector edits body settings and geometry collision/render participation. Invisible collision geometry has an editor outline and no game visual. All edits use existing document transactions, undo and save/reopen. The walkthrough now includes passive body edits, noncolliding decoration, invisible collision geometry and a reopened library containing a dumbbell, boot, texture/path Part, sword and character in the same Part document format.
- Dependency audit: **7** models, **7** variants, **87** external clips, **50** Parts, **6** templates and **1** game profile resolve. `face-person` remains the game face; centaur still references `person-upper` and `quadruped-lower`. CharacterModel/ModelVariant/MotionClip remain the shared rig/variation/animation envelopes. `AuthoredCatalog.Entities` is a compiled view and Studio's Entity category is a filter over Parts. Level-local lookup is a path cache over `Part.Load`, and all passive bodies use `AuthoredObject2D`; there is no separate level-object schema. Noodle remains outside this consolidation.
- Verification: **1,039/1,039** affected regression tests passed, including unchanged character reference captures and new body participation/mask, animated bind-pose, composite collision-only, strict format and save/undo cases. Native Studio passed **182/182** editor checks and **46/46** acceptance checks. The full solution build passed; the five-frame game object study exited successfully. Passive-body, invisible-collider, world-object and centaur frames were visually inspected. Proofs are under `artifacts/part-consolidation/s6-*`.
- The C# change removes **386 net lines**, including the new tests and native walkthrough. All completion examples below are covered; no further stage remains in this plan.

## 6. Verification and conversion policy

Keep existing assets working while each route converts. Old serialized semantics are handled at an explicit, temporary conversion boundary; do not add silent upgrades on ordinary open/save or permanent parallel runtimes.

Convert a dependency bundle and record any changed asset IDs, named points, channel targets, units, overrides and state references. Preserve originals until conversion validates. Character assets currently use model units, while dumbbell geometry is in game world units: preserve dimensions using an explicit conversion/instance scale, not an accidental global scale assumption.

Existing tests of the dumbbell's exact 0.8 velocity multiplier / 4.5 angular kick protect the experiment being intentionally removed. Replace them with ordinary-body behavior checks; do not preserve the hack just to keep those expectations green.

Use existing test groups and extend only meaningful boundaries:

| Area | Starting points |
| --- | --- |
| Preview/document selection | LibraryPartEditingTests, AppearanceAuthoringTests, EditorSessionTests |
| Parts, placement and art | PartAssetTests, PartAttachmentDrawingTests, WeaponOrientationTests, material and rendering tests |
| Variants/animated parts | AnimatedPartTests, appearance/build tests, module/variant tests and character reference captures |
| Passive physics and loading | PhysicalPartAssetTests / PhysicalPartTests / AuthoredObject2DTests / PartCutoverTests |
| Gameplay behavior | EntityBehaviorTests, ContactResolutionTests and existing gameplay/state round trips |
| Frame/material/path animation | Existing generic animation, key sampling and kinematic-motion tests plus a combined rig/texture test |

Each stage records actual checks, observed differences and what was deleted. Inspect representative Studio/game renders when placement or appearance changes. Do not rerun a large unrelated test suite for documentation-only edits.

## 7. Completion test

The consolidation is complete when these examples need data and shared runtime capabilities:

1. Load a rectangle with a render material as an ordinary decoration.
2. Add frame animation to its texture; attach the rectangle to an animated bone and independently key its alpha.
3. Load a multi-shape dumbbell with collision, finite mass, rotation enabled and friction; instantiate it through the same path with no custom class.
4. Instantiate the same glove/shoe Part twice with independent fitting/placement.
5. Use a weapon Part's named grip/tip/muzzle locations for drawing and gameplay.
6. Use the existing game face through ordinary animated geometry; no unused demo feature is required to load unrelated parts.
7. Add Behavior only to an object that needs game rules.
8. Edit, save, reopen and undo each through Studio; demonstrate fewer independent schema/loader/placement/spawn branches than the current system.

Track reduction by the old paths removed and the ease of authoring these examples, not by forcing every helper value type into a single enormous class.

### S1 conversion and review notes

- Part format **3** replaces its `parts` and `solids` collections with `geometry`. Entry IDs and animation/variant targets are unchanged. The three meshes in each of `boot` and `sandal` are now `mesh-1`, `mesh-2`, `mesh-3`, bound to `origin`. All seven shipped part files were converted; model, clip, variant and entity schemas did not change. Units, physical vertex Z, layer offsets and instance/state IDs are preserved.
- Conversion is explicit through `--convert-formats <source> <scratch-output>` and `PartGeometryConversion`. Normal loading rejects the old schema. Original files and the validated scratch bundle are retained in `artifacts/part-consolidation/original/parts` and `artifacts/part-consolidation/converted`. Existing entry IDs win collisions with generated mesh names. An old animated part's root-level solids receive an independent origin control.
- Removed separate library-solid rendering, picking, selection-overlay and inspector branches. `PartMesh` holds geometry only; the old prop wrapper reuses it until S2. Model and library geometry share material resolution and an inspector. Meshes follow rig nodes and receive existing fit/show tracks. Extrusion remains one algorithm. Standalone previews cache their resolved instance, use the part's clock and texture root, and never enter a document.
- Studio distinguishes reusable parts, whole rig modules, and named studies/demos. Modules open alone. **Preview on wearer (temporary)** is explicit; **Attach** is the saved operation. Instance layer and per-entry layer remain planar draw order; mesh vertex Z and thickness remain physical distances.
- Still pending after S1 (S2 is now completed below): passive world objects and removal of fake combatant health (S3), Behavior/collider ownership (S4), texture/material/path animation (S5), and the final single-workflow cutover (S6).

### S2 conversion and review notes (2026-10-27)

- All **30** clothing and weapon files now live in `parts/` as ordinary Part format 3 resources; the catalog contains **37** shared parts. IDs, equipment bindings, clip targets, state references and units are unchanged. Existing models, variants, animations, entities and profiles needed no JSON changes.
- `PropAsset`, `PropShape`, `PropSolid`, their document/catalog route, the separate prop drawing loop, the clothing mesh inspector and the appearance-to-part conversion UI are removed. Extrusion and OBJ helpers now build `PartGeometry` with `PartMesh`. Both equipment and library previews draw through shared instancing and geometry code. Existing equipment selection, draw/sheath markers, hair/back-view selection, strikes and projectiles remain in gameplay; the Behavior aggregate is still S4.
- Grip, tip, second-grip and muzzle are conventional names in arbitrary `locations`. A location has physical `z` separately from `layer`; rigid shape/curve vertices can carry physical `depths`. The generic `pivot` location and `scale` preserve the old grip-relative coordinate system without rewriting geometry. Physical rotation happens before instance/location/entry layer offsets are added. Collider location validation and the editor accept arbitrary names.
- Geometry editing, named locations, OBJ import, material controls, cutout insertion/removal, viewport dragging, undo and compact save/reopen remain available. Undo snapshots retain derived mesh data; persisted extrusion files omit it. Entity equipment users now appear in the shared part's wearer list.
- Conversion is explicit via `PropPartConversion` and the existing `--convert-formats <source> <scratch-output>` command. It refuses unknown source fields and colliding destination IDs. Original files are retained in `artifacts/part-consolidation/s2-original/props`; the verified dependency bundle and report are in `artifacts/part-consolidation/s2-converted`. Normal loading does not accept legacy prop files.
- Validation: **789** character/geometry/entity/enemy integration tests passed, including unchanged migration reference captures and added checks for arbitrary collider locations, mirrored tilted instances, independent repeated instances, physical Z versus layers, conversion collisions and mesh undo. Native Studio: **158** editor checks and **46** acceptance checks passed. Clothing and tilted-weapon captures were visually inspected. Logs/captures are under `artifacts/part-consolidation/s2-*`.
- The C# diff removes **446 net lines**, including new converter code and regression tests. This does not count moved JSON files as code reduction. Next: S3's common passive-body path; S4 Behavior/collider ownership and S5 texture/material/path animation remain pending.

## 8. Continuation record

| Date | Work | Evidence / next step |
| --- | --- | --- |
| 2026-10-26 | Preview fix implemented; consolidation plan written | S0a: two failures reproduced before fix; 28 focused tests passed afterward in the previous work pass. Current source still contains that fix. This planning pass adds documentation only. Next: S0b and S1a. |
| 2026-10-26 | Requested demo cleanup completed ahead of consolidation | S6a complete; wave-specific portion of S6b complete. Removed the curve-face model/template, nine demo clips, face-curve part, character wave modifier, unused payload interface and demo conversion command. Game faces and Core normal-offset curves retained. Seven demo reference cases removed; all 74 retained cases unchanged. Verification: 866 character/curve/rendering tests passed; Studio built in a separate output directory because the open editor locked its normal output; editor smoke 155/155 and acceptance 46/46, with curve/game-face frames inspected. These results cover the cleanup snapshot before concurrent consolidation edits. |
| 2026-10-26 | S0b and S1 complete | 726 affected authored/geometry tests passed, then 24 focused preview/session/mesh tests passed after the final clock adjustment; Studio editor smoke 158/158 and acceptance 46/46. Inspected outfit, centaur, standalone module and mesh-inspector frames. Existing character reference data stayed unchanged. Studio and the Noodle bridge built. Removed separate library-solid handling; next is S2 or the independent S3 passive-object proof. |
| 2026-10-27 | S2a/S2b implemented | 30 props converted into the shared Part library; separate codec/catalog, drawing and clothing inspector removed. 789 integration tests, 158 editor checks and 46 acceptance checks passed. Next: S3. |
| 2026-10-27 | S3a/S3b implemented | Unified passive bodies and moving platforms on Part and the existing world-object runtime. 1,254 integration tests, 158 native editor checks and the five-frame physics rendering study passed. Next: S4. |
| 2026-10-27 | S4a/S4b implemented | Unified character definitions on Part with optional Behavior and collision-enabled geometry. Converted all 11 characters; removed the entity format/runtime wrapper and separate collider/movement definitions. 1,264 regression checks passed together, the remaining audio timing check passed in isolation, and native editor/acceptance checks passed 158/158 and 46/46. Next: S5. |
| 2026-10-27 | S5a/S5b/S5c implemented | Shared pose/frame/path timing, geometry tint/alpha channels, texture sequences with atlas regions, and Studio editing/preview. 1,111 regression checks, 55 final focused checks, 170 native editor checks and 46 acceptance checks passed. Different-size images preserve the animated rectangle's dimensions. Next: remaining S6 cutover. |
| 2026-10-27 | S6 complete; plan closed | Removed temporary format converters and separate root collision; 58 Parts moved to v4 with explicit body collision entries. Shared Studio body/geometry editing, dependency audit and walkthrough completed. 1,039 regression tests, 182 native editor checks and 46 acceptance checks passed; five-frame world study passed. Net C# reduction: 386 lines including new coverage. |
| 2026-10-28 | Seed-only helpers moved out of Core | Reference audit found only Seeds and test callers for ModuleConversion, TemplatePublication and PersonWardrobeDepths. Moved all three into App2d.CharacterSeeds and its namespace: 794 original lines leave Core, with implementations unchanged. RigConversion and PuppetMotionConverter remain in Core because Studio authoring/imports use them. Core, Seeds, game and Studio built; all 20 module extraction, template publication, wardrobe and seed-generation tests passed. Across the consolidation commits before this move, non-test C# shrank by 516 lines while tests grew by 708 lines. |
| 2026-10-28 | Passive Parts falling through terrain fixed | Props use the World layer, but streamed terrain and tree-branch masks still accepted only Player/Enemy. Included World in both surface filters. Game-simulation regressions reproduced all six old/new placement and solid/one-way floor failures before the fix. The new checks also exposed a one-way rotation error: previous support used the current rotation. It now uses the previous pose and exact composite support. Seven landing cases now pass, together with all 250 affected world/session/collision/physics and physical-asset tests. The existing unrestricted-floor unit test had not covered game collision wiring. |
