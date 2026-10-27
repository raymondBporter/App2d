# Part consolidation plan

Date: 2026-10-26.

Status: reviewed and in progress. S0, S1 and S2 are implemented. S3-S5 and the remaining S6 cutover are pending; the earlier demo cleanup is recorded below.

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
| Collision settings | Enabled/sensor state, category, mask and owner identity at runtime. | Collider2D, ColliderDef's useful settings and shared shape collision |
| Physics settings | Motion type, finite positive mass/inertia, rotation lock, friction, restitution and gravity settings for a body. | PhysicsBodySettings2D and PhysicsWorld2D |
| Behavior | Controller/action/role bindings and game-specific damage, receiving damage, guarding, sound/event configuration. | Existing EntityBehaviors and typed action/controller records |

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

The current character image draw path maps the entire texture onto the quad. Source-region selection needs an explicit UV mapping change there; changing a texture filename alone does not implement atlas animation.

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

- [ ] S3a. Load and instantiate a Part with collision and PhysicsBodySettings2D through the shared catalog and world-object path.
- [ ] S3b. Convert dumbbell.json and its world placement, presentation and state handling. Remove HitVelocityScale, HitAngularKick and its fake combatant health.

The current [PhysicsProp2D](../App2d/Gameplay/World/PhysicsProp2D.cs) is an enemy/combatant with a million-point health pool and TakeDamage that directly changes velocities. That is the concrete special case to remove, not an API to preserve under a different name.

Physics still requires the existing runtime body and collider. Shared instantiation creates them from settings; there is no Dumbbell class or another per-object actor class.

Passive objects respond through ordinary physical contact. If an attack must impart an impulse, use an explicit gameplay action applying the physics engine's impulse at a contact point. Do not retain the two experimental gains just to preserve a test.

Gate: a dumbbell and another differently shaped JSON part fall, collide, rotate and obey friction without new object classes. Rendered and physical poses agree; disable/re-enable, streaming, disposal and state restoration retain the configured motion type and do not leak registrations. An object without Behavior has no health or AI.

Move passive-object lifetime/state handling to the common world-object path rather than preserving IEnemyActor2D just to retain the old registration API. Reuse existing world ownership/streaming infrastructure; this does not require redesigning every game actor or introducing another entity framework.

Delete: PhysicsPropAsset, its dedicated loader/catalog collection and PhysicsProp2D-specific spawner/view/state branch after their usages convert. Old TumbleProp world placements can map to a generic asset reference at the loading boundary.

### S4. Gather game-specific configuration under Behavior

- [ ] S4a. Evolve EntityBehaviors into the one optional Behavior aggregate, moving existing controller, action/role, strike, guard, reaction and event configuration under it.
- [ ] S4b. Update game consumers and the existing typed inspector. Collapse EntityAsset's remaining wrapper only as its useful responsibilities move onto Part/Behavior references.

During this stage, finish moving Movement/Colliders shapes and placement onto geometry entries. Behavior retains stable references and activation/interaction policy, including attack windows; the common collision path retains shape construction, attachment, filtering and registration.

Reuse useful nested records and collections. Different attacks can still have different strikes; “one Behavior class” need not flatten several attacks into one damage field. Keep body mass distinct from a gameplay knockback-resistance multiplier.

No new key/value framework, plugin registry or universal behavior interpreter is required. Do not move damage, sounds and controller rules back onto geometry to reduce the apparent class count.

Gate: current player/enemy attacks, guards, projectiles, reactions, accepted impact sounds and replay/state behavior work. Generic geometry/rig evaluation ignores Behavior. Passive objects do not inherit default character health or combat registration.

Delete: duplicate game configuration on appearance/prop/geometry definitions and obsolete forwarding wrappers. Retain game-specific algorithms inside their owning behavior/controller implementation.

### S5. Share animation binding and add texture frames

- [ ] S5a. Bind the useful existing pose/parameter, material and enabled/selection targets through shared sampling/playback rules.
- [ ] S5b. Add a timed image sequence on a rectangle, then exercise it on a moving bone. Reuse AnimationClip2D and existing clock semantics; add source-region UV selection through the same frame binding when required.
- [ ] S5c. Reuse the existing kinematic motion interface for a path-driven Part/body and expose it as motion configuration, not a material.

Gate: a bone-animated object can simultaneously change texture frames and material alpha. Seeking to a given time always produces the same pose/frame/color without advancing simulation or playing sound. Rect size/anchor remains stable across different image sizes; looping/holding and both facings work. A kinematic path retains its correct fixed-step velocity.

Delete: duplicated frame clocks, selection sampling and material/geometry timing branches introduced during the consolidation. Do not rewrite the generic animation library or attempt animation of all fields.

### S6. Remove unwanted studies and finish the cutover

- [x] S6a. Audit actual references and remove the unused curve-face and wave demonstrations. Preserve face-person and other assets used by game models, variants, profiles and clips.
- [ ] S6b. Remove unused wave-specific character integration, demo inspectors/generators and any remaining obsolete formats/paths; retain generic curve math that still has a supported use.
- [ ] S6c. Complete the Studio walkthrough, migrate the rest of the authored bundles and update documentation/dependency listings.

The 2026-10-26 cleanup also completed the wave-specific portion of S6b: the character wave modifier, its inspector, the demo conversion command and the unused payload interface are removed. Core's generic normal-offset curves remain. The remaining obsolete formats/paths still belong to the consolidation stages above.

A generated module or study is not automatically unused. In particular, centaur currently references person-upper and quadruped-lower. Keep deliberately supported composition examples or move the entire dependent example to test content together.

Gate: one browser and editing workflow handles a passive prop, static decorative part, textured/animated rectangle, weapon and rigged character. No duplicate authoritative definition, rendering or spawning route remains for converted content.

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
| Passive physics and loading | PhysicsPropAssetTests / PhysicsProp2DTests during conversion; generic object/body tests afterward |
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
