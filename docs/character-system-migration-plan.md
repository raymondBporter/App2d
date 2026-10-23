# Character system and Studio migration plan

Date: 2026-10-23.

Status: implementation in progress. P0 (baseline, reference set and conversion decisions) is complete and P1a/P1b are done; see the progress record in section 11 and [Character migration baseline](character-migration-baseline.md). Next: P1c.

This is the full migration proposal from the character-system design discussion. It supersedes the proposed future sequence in [Character resources and migration](character-resources.md), while that document remains a record of implemented behavior. [Character Studio](character-studio.md) describes the current editor.

The objective is to simplify the existing system into reusable rigs, parts, attachments, animation, collision geometry and gameplay behaviors. Keep the working mathematics, geometry, renderer, animation sampler, collision/physics engine and editor document infrastructure. Migrate in reviewable stages, with explicit conversion and removal of superseded paths.

Scope: App2d.Core, App2d.CharacterStudio, the game, CharacterSeeds, their tests and authored assets. Noodle is excluded at the user's request. This plan requires no Noodle integration, migration, parity checks or preservation work, and does not authorize deleting it.

## 1. What the finished system must let the author do

These requirements carry forward the discussion. A phase is useful only if it advances these capabilities.

| ID | Required capability | Completion evidence |
| --- | --- | --- |
| R01 | Start from reusable person, quadruped and flying-creature rigs and animation libraries. | All three run through the same evaluator; anatomy is resource data. Empty/custom rigs remain possible. |
| R02 | Change individual bone lengths and useful proportions such as body width, head size and limb thickness. | Changes carry the appropriate joints, artwork and attachments without editing animation keys or using inherited nonuniform rig scale. |
| R03 | Set joint limits and optionally animate normalized parameters such as bend in 0..1. | The channel's units and mapping are explicit; limit-aware solving reports residuals honestly. |
| R04 | Place attachments at normalized positions along a bone. | Positions at 0, .5 and 1 follow the origin, midpoint and tip as length changes. |
| R05 | Select feet, legs, arms, body, hands, head, gloves, hair, beard, clothing and weapons from a parts library by asset reference. | A shared part can be instantiated more than once, with independent placement and local overrides. |
| R06 | Use animated curves/shapes as eyes, mouth and other facial features attached to named locations. | Ordinary geometry and animation implement a face; the shared part type and evaluator require no fixed facial vocabulary. |
| R07 | Save variants that change lengths, limits, exposed parameters, part selections, materials and animation bindings. | Reset and inheritance work per field; a variant never silently edits its base or the shared part asset. |
| R08 | Combine compatible rig modules, including a humanoid upper body and quadruped lower body. | A centaur study uses existing modules and separate animation bindings without a centaur-specific evaluator. |
| R09 | Author one local hierarchy and inspect local, character and world coordinates clearly. | Accumulated frames are derived; editor drags convert back to the authored local representation. |
| R10 | Avoid accidental shear from animated hierarchies. | Inherited rig frames are similarities; geometry fitting is explicitly separate and does not become an implicit parent scale. |
| R11 | Combine planar artwork, character draw order and existing 3D weapons without ambiguous Z semantics. | Layer order, physical local Z, planar-to-3D lifting and gameplay projection each have a defined owner. |
| R12 | Attach ordinary collision geometry, including sensors, through the same placement mechanism. | Attack, hurt and trigger geometry uses the existing collision system and filtering rather than a parallel region-query system. |
| R13 | Configure gameplay properties without putting every behavior's fields on every character/part. | Behaviors own typed property schemas; damage and impact sound do not belong to rig or geometry definitions. |
| R14 | Actually perform these operations in Studio. | Every capability includes selection, editing, preview, undo, save/reopen and useful diagnostics. No visible handle silently does nothing. |
| R15 | Port existing assets and preserve working behavior through the transition. | Each migrated bundle has a conversion report and comparable motion/render/gameplay evidence. Intentional changes are identified. |
| R16 | Reuse the existing engine and remove superseded code. | Each phase names the paths it replaces and the condition for deleting them; no permanent CharactersV2 runtime is added. |
| R17 | Retain collision and physics properties as reusable capabilities. | Existing body settings and shapes remain usable; animated sensors do not require a simulated body per bone. |
| R18 | Make many characters through combinations rather than copied assets and new code. | Repeated part choices and continuous proportion changes share rig, animation and art resources; unsupported combinations give actionable errors. |

Three choices in six independent categories give 729 selections before proportion changes. Independence is a declared compatibility property, not a promise that every arbitrary library item fits every socket. A different-looking enemy also does not automatically acquire different AI or attacks.

## 2. What the code currently tells us

This inventory was read from the checkout on 2026-10-23. It is planning evidence, not a test-run baseline.

- Authored folders contain 7 models, 7 variants, 91 animations, 30 props, 11 entities, 5 templates and 1 physics prop.
- The shipped models have no explicitly declared positive bone lengths. Most use model-space point controls; curve-face has two explicit affine controls.
- Person, quadruped, triceratops, baby-triceratops-rig and stalker use point IK chains. Pterosaur uses ordinary animated controls and point-bound artwork.
- Curve-face contains the wave-enabled curve. Person contains the procedural face-bearing part.
- Animation tracks include translation, rotation, IK targets, socket orientation and scale. The checked-in scale tracks occur in four curve-face clips. NativeRigTests also deliberately exercises inherited affine scale/shear, so affine behavior cannot be dismissed without inspecting its intended replacement.
- The current schemas are model 5, clip 3, variant 4, entity 2, prop 3, template 1 and physics-prop 1. Sparse JSON omits defaults; inventories and converters must apply schema defaults rather than infer types from missing fields.

| Current responsibility | Evidence and problem | Reuse / migration direction |
| --- | --- | --- |
| Rig definition | [CharacterModel](../App2d.Core/Characters/Authored/CharacterModel.cs) combines model-space Rest/RestAngle with optional parent-local Transform, and keeps Chains alongside typed Constraints. | Retain the document envelope initially; resolve into one local rig representation and one ordered constraint list. |
| Resolution | [ResolvedModel](../App2d.Core/Characters/Authored/ResolvedModel.cs) applies Person build rules, absolute rest overrides and a narrow part override schema. | Keep the compile boundary; extend it to resolve recipes, parts, parameters and transitive dependencies. |
| Pose evaluation | [PoseEvaluator](../App2d.Core/Characters/Authored/PoseEvaluator.cs) branches between point and affine control semantics and maintains Points, Angles and Bones. | One canonical evaluated frame per node; position/angle accessors become derived views or consistently maintained caches. |
| Placement | [PartAttachment2D](../App2d.Core/Characters/Geometry/PartAttachment2D.cs), [PartFrame2D](../App2d.Core/Characters/Geometry/PartFrame2D.cs), sockets and prop grips calculate related placements differently. | Evolve the existing frame/attachment path instead of adding another unrelated transform family. |
| Part ownership | [PuppetPart](../App2d.Core/Characters/PuppetDefinition.cs) contains WaveAmplitude, WaveCycles, WavePhaseControl, Face and FaceX. | Specialized curve deformation and face appearance move into their owning feature/asset. |
| Part selection | [SkeletonAppearance2D](../App2d.Core/Characters/Authored/SkeletonAppearance2D.cs) has slots/skins; [PuppetDrawing](../App2d.Core/Characters/Drawing/PuppetDrawing.cs) restricts slot-managed attachments to images. Props/equipment follow another route. | Generalize the existing attachment selection mechanism to supported geometry, curves, images, meshes and animated parts. |
| Variants | [ModelVariant](../App2d.Core/Characters/Authored/ModelVariant.cs) cannot override lengths or local transforms; moving a subtree writes absolute rest positions. | Typed property/parameter overrides and slot selections, resolved before evaluation. |
| Proportions | [PersonBuild](../App2d.Core/Characters/Authored/PersonBuild.cs) contains humanoid-specific geometry rules in Core. | Retain its existing results during conversion; move reusable proportion bindings and presets into template data. |
| Faces | [FacePose](../App2d.Core/Characters/FacePose.cs) and [FaceDrawing](../App2d.Core/Characters/Drawing/FaceDrawing.cs) implement a fixed expression vocabulary/style. | Preserve the style during transition, then supply it through an optional asset implementation or authored geometry/animation. |
| Collision | [EntityRegion](../App2d.Core/Characters/Authored/EntityRegion.cs) already reuses shapes and narrow-phase collision, but its regions do not join the persistent collision system. | Register and update attached colliders through [CollisionSystem2D](../App2d.Core/Collision/CollisionSystem2D.cs). Reuse [Collider2D](../App2d.Core/Collision/Collider2D.cs) sensor, ownership and filter fields. |
| Combat | [HitWindow](../App2d.Core/Characters/Authored/EntityAsset.cs) bundles timing, anchoring, shape, damage and sound. [CombatSystem2D](../App2d/Gameplay/Combat/CombatSystem2D.cs) separately scans authored hurt regions. | Separate attached collider, activation interval and gameplay behavior; converge on shared collision queries and accepted-contact handling. |
| Editor | [EditorSession](../App2d.Core/Characters/Editing/EditorSession.cs) and [AssetDocument](../App2d.Core/Characters/Editing/AssetDocument.cs) already provide tested transactions, selection and preview. Variant bone-tip dragging explicitly returns without acting. | Keep the shell/session/document infrastructure and migrate commands and inspectors with each feature. |
| Templates/profiles | Model templates are already resources. PersonLoadout, appearance templates, proportions and player clip IDs still have code-specific bindings. | Keep specialized controller algorithms in code; put anatomy, asset choices and animation-role assignments in resources. |

There is no evidence here that another collision engine, renderer, editor application or general-purpose scripting system is needed.

## 3. Target contracts

The concepts below are commitments for the migration. Type names and file boundaries are provisional; evolve existing types where that removes duplication.

### 3.1 Definitions, resolution and instances

Keep three distinct lifetimes:

1. Authored resources: editable local setup, exposed parameters, references, clips and behavior configuration.
2. Resolved resources: validated structure, stable target bindings, fitted geometry and shared read-only resource data.
3. Actor instances: animation clocks, pose buffers, contact anchors, enabled colliders, attack identities and physics state.

Swapping a glove on one actor must not mutate the shared glove asset. Editing that shared asset intentionally must invalidate every dependent preview. Rendering and sampling must not mutate setup transforms.

Keep the existing catalog, workspace and document concepts. Extend their reference graph to cover part assets, nested modules, clips and their transitive dependencies. Versioning only the immediate base and variant will be insufficient once a shared part changes.

### 3.2 Rig nodes, dimensions and frames

- Every rig node has one parent-local setup pose. Root setup is relative to the character frame. Character placement in the world is an additional explicit frame.
- Inherited rig poses use translation, rotation, uniform scale and optional reflection. Composed poses retain that invariant.
- A node may expose a bone length; zero-length/ordinary controls remain useful for curve handles, sockets and targets.
- A child origin or attachment can bind to a parent-bone position t, an offset and an orientation policy. A tip binding derives its translation from the parent's final length.
- Bone length is a dimension. It is not implemented as nonuniform scaling of the inherited pose.
- A driven child origin must not also carry an independent authoritative absolute rest position. Choose a binding or an explicit local offset, with provenance visible in Studio.
- Retain authored translation/rotation/scale channels for editing and animation. Matrices are evaluated results; matrix interpolation and repeated decomposition are not the animation model.
- Derived positions, directions and angles must be updated together after constraints. Consumers do not independently reconstruct a different pose from stale dictionaries.

For the existing row-vector convention:

~~~text
nodeInCharacter = nodeLocal * parentInCharacter
nodeInWorld     = nodeInCharacter * characterInWorld
~~~

Topology does not imply behavior by itself. A point-moving constraint and a rigid-bone IK constraint have different orientation effects; that difference must be declared rather than inferred from whether a nullable Transform exists.

### 3.3 Planar placement, layer order and real 3D

Evolve the current frame/placement representation toward a composable planar frame plus relative character-layer offset. Absence of a layer offset means zero.

~~~text
combined.XY    = local.XY * parent.XY
combined.Layer = local.Layer + parent.Layer
~~~

A Matrix3x2 can participate as a placement with zero layer offset. XY rotation/scale does not transform the layer value. Identity and default-value behavior must be explicit.

The three meanings remain separate:

| Value | Meaning | Owner |
| --- | --- | --- |
| XY pose | Planar placement and orientation | Rig/attachment evaluation |
| Character layer | Relative drawing order within a character | Appearance/attachment evaluation |
| Physical local Z | Mesh thickness and true 3D orientation | 3D geometry and prop placement |

The renderer maps character layer into its depth convention at one boundary and preserves existing solid-mesh depth behavior. Slot order, continuous layers and local mesh depth need one documented ordering policy; they must not each silently overwrite the others.

Keep character-layer composition in the character/appearance placement wrapper. Pure Frame2D, Matrix3x2 and general geometry APIs do not need character draw-order state added to them.

Existing control Z values often behave as absolute character layers. Migration derives local layer offsets from those values so adding parent offsets does not double-count depth. Entity scene ZIndex remains a separate world-object ordering concept.

Existing weapon tilt/twist/turn remains genuinely 3D. A single defined bridge lifts the attachment's planar similarity into the 3D frame used for mesh vertices, grip, tip and muzzle. Gameplay projection to XY is explicit and initially preserves existing foreshortening/facing behavior. A planar matrix plus layer offset cannot represent arbitrary 3D rotation.

### 3.4 Artwork fitting and deformation

Separate geometry definition coordinates, fitting and animated placement:

~~~text
geometryPoint * geometryFitAffine * attachmentPose * rigPose * characterPose
~~~

- A fitting affine can stretch a shin guard or widen a torso. It can intentionally shear artwork when the feature explicitly permits that.
- Fitting is not inherited as an implicit nonuniform rig scale by child bones.
- Sockets can derive positions from dimensions or fitted geometry while receiving a separately defined similarity orientation.
- A composite asset must declare whether fitting deforms the whole artwork/animation or repositions its internal attachment locations. The default for new modular rigs is recomputed layout with similarity pose inheritance.
- Geometry deformation is an appearance operation, distinct from skeletal pose. Existing cutout point bindings, curve bindings, material paint and intentional eye squash must retain a supported owner.
- Wave is typed curve-deformation data. It does not become a field on every part.
- Generic code does not assume that a head contains two eyes, that a face uses a fixed expression enum, or that clothing must be an extruded prop.
- Stroke/outline sizing has a declared fitting policy. Preserve current approved appearance during migration rather than accidentally stretching line thickness.

Native inherited-affine scenes cannot always be factored into one static leaf fitting transform. Their conversion must classify intentional deformation, preserve it through a scoped representation or bake, and report unsupported cases. Silently dropping shear or decomposing it into an approximate rig pose is not an acceptable conversion.

### 3.5 Parameters, limits and channels

Parameter declarations carry a stable ID, type, default, units, hard range, optional suggested editor range and simple target bindings. Start with direct assignment, multiplier/reference-size binding and linear range mapping. Avoid introducing an expression language.

Examples:

- Forearm length: a positive length or an explicit multiplier of a reference length.
- Torso width: geometry/layout dimension, not inherited nonuniform pose scale.
- Eye spacing: positions of eye attachment locations.
- Bend: normalized value mapped through a declared angular range.
- Attachment position: normalized t along a bone.

Channel semantics are explicit: angle, normalized parameter, local translation, IK target, uniform pose scale, artwork fit, material value, attachment selection or enabled state. Reuse the existing keyframe sampler and curve rules. Discrete channels use held values.

Normalized bend does not mean the same thing as a fixed angle. Changing limits intentionally changes a normalized performance; ordinary angle tracks preserve authored angles. Full turns, independent component curves, setup-before-first-key behavior and exact endpoints remain meaningful.

Constraints evaluate in a deterministic declared order with explicit target spaces. Joint limits on IK require a limit-aware solve that can return a closest valid pose and residual. Clamping an analytical IK result afterward must not continue reporting that the target was reached.

Reuse [ConstraintLimit1D](../App2d.Core/Constraints/ConstraintLimit1D.cs), [ConstraintMath2D](../App2d.Core/Constraints/ConstraintMath2D.cs), existing IK math and interpolation where their semantics fit. Editor slider ranges and active physical/kinematic limit state have different responsibilities; sharing primitives does not require merging those responsibilities.

Parameter/layout dependencies are validated for cycles. Explicit property overrides win over parameter-produced values for the same exposed property, then downstream attachment/layout calculations use that final value. Studio shows when an override masks a parameter binding.

### 3.6 Part assets, attachment slots and modules

A part definition owns reusable geometry/materials and may include a small local rig, attachment locations, animation and declared capabilities. An attachment instance owns the asset reference, local placement, fitting, local overrides and animation bindings.

Distinguish:

- Asset ID: which library resource is referenced.
- Attachment/slot ID: the named role or connection location.
- Instance ID: this occurrence of an asset inside a character.

The same glove or shoe asset can be used twice. Current equipment uniqueness by prop asset ID must not force separate left/right library assets. Mirroring and fitting belong to instances where supported.

Contracts declare the connection frame, dimension inputs, optional named points and exposed animation targets. Part types differ in payload, not in fundamental placement semantics. Shapes, curves, images, meshes and animated parts use the same attachment mechanism; invisible collision attachments do not need fake hidden artwork.

Keep geometry/material definitions embedded until reuse warrants a separate resource. Reuse RenderMaterialDefinition2D and shared geometry serializers rather than introducing another material or shape schema.

Nested animated parts and rig modules use the same instancing mechanism. Resolve their instance namespaces and target mappings into one runtime graph. Reject cycles, duplicate public IDs and incompatible bindings with a reference path to the offending resource.

### 3.7 Character recipes and variants

Keep CharacterModel as the document envelope where practical. A character recipe selects its rig/modules, parameters, attachment instances and animation-role bindings. A variant is one live base reference plus overrides expressed in the same declared property system.

Resolution order:

1. Resolve base recipe and resource defaults.
2. Apply parameter overrides and module/part selections.
3. Compute parameter bindings and apply explicit exposed-property overrides.
4. Derive child/socket positions, geometry fitting, measures and collider dimensions from final values.
5. Validate graph/contracts/channel compatibility and compile bindings.
6. Sample animation and constraints for each instance.

Supported overrides include bone length, joint limits, setup pose, layer, fitting, material, part selection and animation binding. Values have explicit inherit/set/clear semantics. Resetting an override removes that leaf override; it does not write a copy of the current base value.

Stable IDs, not array indices, address overrides. Resolve strings and typed property IDs into runtime bindings once. A validated property catalog is acceptable; parsing strings and reflecting across arbitrary objects every frame is not the target.

Structural composition is expressed by declared module/slot contracts. Arbitrary reparenting or deleting public controls still needs an explicit base/module edit and animation-compatibility checks. “Everything can vary” means the useful declared properties and selections can vary, not that any JSON mutation is automatically compatible.

Keep one inheritance level initially. Duplicating a variant creates a sibling; making it independent explicitly flattens the resolved recipe. Transitive part references provide composition without requiring long variant inheritance chains.

### 3.8 Animation composition and faces

- Clips address stable declared targets and channels. Templates supply default role assignments, masks and parameter mappings.
- A face asset can contain ordinary curve controls, shape visibility and blink/smile clips. Expression names are asset/profile bindings.
- Gameplay can request a semantic expression or action through a profile mapping without teaching the generic evaluator a fixed face vocabulary.
- Independent parts have explicit clock ownership: shared phase, mapped phase or an instance-local clip time. Seeking derives pure pose without dispatching gameplay effects.
- Masked action ownership, blending, contacts and root travel remain explicit. Generalize only the layer capabilities required for body, face and module composition.
- A centaur has one owner of world/root motion. Its horse gait and humanoid upper-body animation own different target sets; the torso attachment follows the lower module. Reusing clips does not automatically solve gait synchronization or performance quality.
- Existing presentation hitstop/retiming is intentionally distinct from authoritative simulation time. Both use the shared evaluation machinery, but delayed display samples must not drive combat colliders or replay effects.

### 3.9 Collision, physics and gameplay properties

An attached collider has shared shape geometry, a placement binding, sensor/enabled state, collision category/mask, owner and stable attachment identity. It follows the authoritative evaluated pose.

- Attack, hurt, trigger and guard uses share collider placement, indexing and overlap infrastructure.
- A sensor does not need a dynamic RigidBody2D. Reuse CollisionSystem2D and Collider2D directly for animated attachments.
- Keep the stable terrain movement body as a deliberate movement capability. It need not be identical to animated hurt geometry.
- Shape dimensions can depend on character parameters. Bake nonuniform fitting into supported local collision geometry before similarity placement, or report an unsupported shape conversion. Drawing-layer values never change XY collision.
- Timeline channels control collider activation. Preserve half-open intervals and attacks whose whole active interval is crossed by one simulation step; a final enabled boolean alone is insufficient.
- Collision filtering excludes the owner and respects both masks/categories and gameplay rules. Existing overlap queries and collider-pair collection currently have different filter entry points; reconcile that deliberately.
- Deduplicate by attack instance, configured hit group and target entity, not by individual hurt collider. Multiple intended attack pulses may have different hit-group identities.
- Register/unregister and pool colliders at defined lifecycle boundaries; do not recreate a persistent collider every rendered frame.
- Preserve the existing fixed-step order initially. Define the pose/collider synchronization point before combat queries and ensure physics movement cannot leave stale collider frames.

The engine supplies geometry, placement, collision results and timing. Gameplay owns damage, factions, invulnerability, blocking, cooldowns and accepted effects. A behavior schema owns its types, defaults, validation and editor metadata.

An extensible key/value store can carry custom authored properties, but known behavior properties are typed and validated at resolution. Damage remains an integer, sound an asset reference, and unknown properties are reported rather than silently parsed during combat.

Retain accepted-contact facts as the source of impact audio and presentation. A missed or rejected attack must not produce a damage effect merely because an enabled sensor overlapped something. Swing sounds remain independently authored timeline events.

Reuse PhysicsBodySettings2D and the existing physics world for physical parts/props. Per-bone rigid-body simulation, ragdolls and secondary dynamics are separate capabilities with fixed-step instance state and explicit editor seeking rules; they are not required to make part swapping and sensors work.

### 3.10 Editor contract

Every implementation phase includes its Studio commands and usable UI, rather than postponing the editor until the runtime is finished.

- Distinguish editing the shared part asset, a character's attachment instance and a variant override.
- Display where a value came from: base, parameter binding, local override or animation.
- Offer local/character coordinate displays; convert entered/dragged positions to local authored values.
- Provide bone length/tip and normalized attachment handles for base models and supported variants.
- Show only applicable feature inspectors. A glove has no wave or face fields unless its actual payload includes those capabilities.
- Select and replace compatible library parts, preview them on the wearer, and navigate “uses / used by”.
- Animate nested-part channels and collider enabled intervals in the existing timeline.
- Retain autokey, pending-pose behavior, compare, transport, undo/redo and save/reopen semantics.
- Disallow unsupported edits visibly with a reason; do not leave an apparently usable handle that silently returns.
- Preserve editable invalid drafts and show actionable reference/property errors. If retaining a last-valid preview, label it as stale.
- Refresh all dependent drafts when shared geometry, materials, clips or parameters change; extend cache dependencies accordingly.

## 4. Migration rules

1. No replacement engine or all-at-once asset rewrite. Keep the current application runnable after every integration.
2. New canonical types live in the existing pipeline. Temporary legacy behavior has a named conversion/compatibility boundary and a deletion condition.
3. Legacy files remain unchanged until explicitly converted. No automatic format upgrade on open or silent rewrite on ordinary save.
4. Bump a format only when serialized meaning changes. New canonical assets can coexist with explicitly supported legacy inputs during migration. Writers must not pretend an old format can represent a new feature.
5. Convert a dependency bundle together: model/recipe, variants, clips, referenced parts and entity bindings. Preserve IDs where meaning is unchanged; report every remap.
6. Conversion is transactional on a scratch output tree first, includes a dry-run/report, preserves originals, and can be rerun without duplicating assets or overwriting subsequent editor work.
7. Legacy point rigs need semantic conversion, not a field rename. Preserve position-only constraint behavior until assets and bindings are deliberately converted to orientation-propagating bones.
8. General-affine animation requires an explicit deformation/bake decision. No “best effort” loss of shear, reflection, full turns or socket orientation.
9. Shared code that still has a supported caller is not deleted just because a new route exists. Conversely, a migration is unfinished while unnecessary duplicate routes remain.
10. Working tree edits from unrelated work are preserved. A source snapshot identifies the exact baseline; historical documentation/test counts are not substitutes for a fresh run.

## 5. Phases and dependency order

Each phase spans multiple reviewable passes. The passes below are task boundaries, not a promise to finish a whole phase in one session.

Dependencies are at pass level. Part swapping needs P4a's dimension/property bindings; it must not wait for P4b's more involved limit-aware IK work. P4b/P4c still have to finish before final cutover. Likewise, collider work can start when attachment placement and activation-channel contracts are ready, without waiting for every facial asset to be converted.

~~~mermaid
flowchart TD
    P0["P0: baseline and conversion cases"] --> P1["P1: common evaluated placement"]
    P1 --> P2a["P2a: curve modifier ownership"]
    P2a --> P2bc["P2b/c: appearance ownership"]
    P1 --> P3["P3: canonical local rig"]
    P3 --> P4a["P4a: dimensions and bindings"]
    P4a --> P4bc["P4b/c: limits and motion checks"]
    P2a --> P5["P5: library parts and attachments"]
    P4a --> P5
    P5 --> P6["P6: general variants"]
    P5 --> P7["P7: animated parts and faces"]
    P2bc --> P7
    P4a --> P7
    P6 --> P8["P8: attached colliders"]
    P7 --> P8
    P8 --> P9["P9: gameplay behaviors and profiles"]
    P6 --> P10["P10: templates and centaur composition"]
    P7 --> P10
    P9 --> P11["P11: full content cutover and retirement"]
    P10 --> P11
    P4bc --> P11
~~~

| Phase | Primary requirements |
| --- | --- |
| P0 | R15, R16: establish behavior and conversion evidence |
| P1 | R09, R10, R11: placement, fitting boundaries and dimensional meaning |
| P2 | R06, R16: give special features an owner and shrink the common part |
| P3 | R02, R04, R09, R10: local rigs, dimensions and attachment links |
| P4 | R02, R03, R04: parameters, limits, units and reusable motion |
| P5 | R05, R14, R18: reusable library parts and usable instance editing |
| P6 | R07, R14: general variants with real inheritance and reset |
| P7 | R06, R08, R14: animated parts, generic faces and module instancing |
| P8 | R12, R17: shared collision geometry, sensors and physics boundaries |
| P9 | R12, R13, R17: shared contact handling and typed gameplay configuration |
| P10 | R01, R08, R18: anatomy templates and composed creatures |
| P11 | R14, R15, R16: complete authoring, content conversion and retirement |

### P0. Establish a trustworthy baseline and conversion cases

Passes:

- [x] P0a. Record checkout revision/diff and schema-aware asset/reference inventory. Run the applicable existing character, editor, rendering and combat checks; record existing failures separately.
- [x] P0b. Capture reference poses, sockets, rendered silhouettes/order and combat outcomes for the representative bundles below.
- [x] P0c. Write the local-frame/layer/affine-conversion decisions against those fixtures. Identify every intentional conversion difference and the evidence required to accept it.

Use Person with tall-thin/short-broad, Hero, Cinder and Maul brute; a quadruped/triceratops gait; pterosaur wing bindings; curve-face; and a small nonhumanoid mechanical linkage fixture. Include both facings, zero-target/full-reach IK, foot contacts, sheathing and a tilted/twisted weapon. The linkage should live in shared tests, not depend on an experimental app.

Primary homes: existing Authored tests, rendering tests, CharacterStudio smoke paths and CharacterSeeds contact/wardrobe diagnostics.

Gate: the reference set is reproducible, existing failures are identified, and every general-affine/point-specific case has a stated conversion disposition. Record numeric tolerances before comparing outputs.

Removal obligation: no new general abstraction yet. Retain only reference evidence and tests that protect actual behavior, not copies of implementation details.

### P1. Converge evaluated placement and layer composition

Passes:

- [x] P1a. Evolve the current frame/placement value and define identity, composition, Matrix3x2 participation, local/entity/world conversion and layer rules.
- [x] P1b. Route rigid artwork placement, picking/editor inverse placement and ordinary sockets through the shared result.
- [ ] P1c. Centralize planar-to-3D prop placement and XY gameplay projection; keep existing serialized inputs while comparing against P0.

Primary homes: PartFrame2D, PartAttachment2D, PartPicking2D, PartContours2D, ActorPose.Socket/PropPoint, Frame2D/Frame3D and relevant viewport handles.

Gate: rest/animated artwork, picking, grip/tip/muzzle and both facings match references. Layer order survives parent-child composition and world scaling; physical mesh thickness remains correct. Similarity pose plus leaf nonuniform fitting adds no unexpected shear.

Removal obligation: delete duplicate offset/rotation composition from migrated consumers. Keep one narrow adapter for unmigrated inputs, with callers listed in the progress record.

### P2. Remove specialized feature ownership from the common part

Passes:

- [ ] P2a. Make wave a typed curve modifier with its own evaluation, validation, dependency references and inspector.
- [ ] P2b. Move procedural facial appearance behind an explicit optional appearance payload/asset implementation. Retain the old visual style temporarily.
- [ ] P2c. Route source-field conversion through the compatibility boundary and remove wave/face assumptions from common authoring and part-edit operations.

Primary homes: PuppetPart, PartCurves2D, FacePose, FaceDrawing, ModelAuthoring, ModelCurveInspector, ModelPartInspector, source serialization.

Gate: the wave example and current Person face still render and animate, while unrelated part data/inspectors contain no wave/face requirements. Deleting a referenced control is handled by the owning feature's reference validation.

Removal obligation: canonical shared part types no longer own WaveAmplitude/WaveCycles/WavePhaseControl/Face/FaceX. Legacy source fields may remain only in a versioned reader/DTO until their inputs migrate. Procedural expression vocabulary retirement completes in P7/P11.

### P3. Introduce one parent-local rig representation

Passes:

- [ ] P3a. Compile parent-local similarity setup nodes, bone dimensions, normalized child/socket links and one authoritative evaluated frame graph.
- [ ] P3b. Adapt existing point and affine inputs with explicit constraint/deformation semantics. Convert one representative limb and the mechanical linkage.
- [ ] P3c. Migrate edit commands for move, rotate, reparent and resize; derive point/angle views from the canonical pose and propagate constraint updates consistently.

Primary homes: ModelControl, ResolvedModel, PoseEvaluator, RigConstraintEvaluator, BoneFrameIk, ModelAuthoring, BaseModelInspector and model/animation drag handlers.

Gate: rotating a parent carries children/attachments correctly; changing length moves t-bound children without stretching their rotating frames. Round trips and inverse drags work under reflection and uniform scale. The Person point-IK reference remains explainable and stable during conversion.

Removal obligation: migrated nodes never select semantics from nullable Transform. Consolidate Chains/Constraints into a canonical list. Retain a named legacy point-constraint adapter only for listed unconverted assets; schedule its retirement in P11.

### P4. Add dimensions, parameter bindings, joint limits and explicit channel units

Passes:

- [ ] P4a. Declare parameter/property/channel metadata and simple bindings; implement length, layout/width, fitting and normalized bone attachment positions. This is the dependency needed for the first part-library/variant slice.
- [ ] P4b. Add angular limits and explicit angle-versus-normalized channels. Integrate a limit-aware IK path with residual reporting.
- [ ] P4c. Preserve measure-scaled target/travel behavior, add Studio units/range/provenance displays and exercise proportion extremes while animations play.

Primary homes: current BuildRule/PersonBuild path, ModelMeasure, constraint definitions, ClipTrack/ClipAuthoring, shared key sampler bindings and rig inspectors.

Gate: the same walk/run keys remain shared across supported long/short limbs. t=.5 follows a changing midpoint. A constrained elbow remains in range and reports unreachable targets correctly. Negative facing/reflection does not silently invert the declared limit convention.

Removal obligation: new anatomy needs no Core switch for proportions. Convert Person build formulas into data-backed bindings/presets with equivalent results; retire PersonBuild geometry logic once its shipped users migrate.

### P5. Make reusable parts and general attachments real

Passes:

- [ ] P5a. Add catalog/workspace support for reusable part definitions and stable attachment instance IDs, including transitive references and dependency invalidation.
- [ ] P5b. Generalize slots/skins and equipment placement to shapes, curves, images and meshes; use the P1 placement and P4 fitting contracts. Establish the payload boundary for animated parts, whose rig/clock instancing is delivered in P7.
- [ ] P5c. Deliver library selection, replace/clear, edit-shared versus edit-instance, preview-on-wearer, reference navigation and save/undo for parts.

Primary homes: AuthoredCatalog, AuthoringWorkspace, AssetDocument kinds/serialization, SkeletonAppearance2D, PuppetDrawing, PropAsset/EquipmentBinding and AssetBrowser/AppearanceView.

Gate: one glove/shoe asset can occupy both hands/feet with distinct instances. Swapping it does not mutate another wearer. Shape/image/curve/mesh attachments share placement and picking; material inheritance/clear behavior remains correct. A shared asset edit refreshes every dependent preview.

Removal obligation: remove the image-only slot restriction. Migrate props/clothing/ordinary parts onto the common attachment path rather than adding another independently rendered equipment collection.

### P6. Replace narrow variant overrides with recipe overrides

Passes:

- [ ] P6a. Implement declared typed property/parameter overrides and part/module selections, with stable IDs, provenance, reset and explicit clear.
- [ ] P6b. Migrate current build/rest/PartOverride data for one Person family, preserving existing results and inheritance.
- [ ] P6c. Complete variant length/tip, fitting, limits and animation-binding editing; repair cache invalidation, duplication and flattening for the new references.

Primary homes: ModelVariant, PartOverride, ResolvedModel, ModelAuthoring.MoveRest/EditPartOverride/Flatten, VariantModelInspector, ModelControlDrag and workspace dependency tracking.

Gate: lengthening a variant forearm and swapping its glove leaves the base unchanged; an unoverridden base edit propagates; resetting one property restores inheritance. “Move children” changes the intended local/bound setup instead of freezing every descendant's absolute rest position.

Removal obligation: delete the parallel per-property override lists for migrated data. Remove the silent variant bone-tip no-op. New supported properties register once rather than adding matching fields and switches to several override classes.

### P7. Support independently animated parts and generic faces

Passes:

- [ ] P7a. Instance local part rigs/channels with stable namespaces, exposed targets and explicit clock bindings, using the same rig/module graph needed later for P10.
- [ ] P7b. Convert the curve-face example into a reusable face part; create reusable expression clips/poses and profile bindings.
- [ ] P7c. Convert the current Person facial style and gameplay expression requests; preserve blink, back-view hiding and action/face ownership through ordinary bindings.

Primary homes: MotionClip channels/appearance tracks, PoseEvaluator/PoseLayer, SkeletonAppearance2D, part resolution, FaceDrawing/FaceExpressions callers and Studio animation outline/inspector/timeline.

Gate: a walking character independently blinks/smiles, swaps face assets and changes head width. The chosen fitting policy either stretches artwork or repositions features explicitly. Seeking and onion skins produce no damage/audio/events. The same geometry/animation primitives work for a non-face animated attachment.

Removal obligation: remove fixed face fields/tracks and built-in expression validation from the generic rig/part evaluator after their assets convert. Retain a procedural generator only if explicitly used as an optional feature or authoring tool.

### P8. Move attached collision geometry into the existing collision system

Passes:

- [ ] P8a. Resolve collider attachments from shared shapes/placement; give them stable ownership, IDs, layers/masks and lifecycle management.
- [ ] P8b. Update colliders from authoritative fixed-step poses and support animation-controlled activation intervals, including intervals crossed within one step.
- [ ] P8c. Convert one attack/hurt/guard set plus a trigger, expose shape/activation editing in Studio, and compare against legacy query outcomes.

Primary homes: EntityRegion/EntityCollision, HitWindow geometry and anchoring, Collider2D, CollisionSystem2D, EntityAnimator.ActiveHits, Studio EntityView/ArenaTest and collision overlays.

Gate: hurt geometry extending outside the movement body is found by the ordinary broad phase; self/ally filtering is correct; sensors cause no physical pushback; the movement body remains stable. A short active interval is not lost. Tip, collider, art and editor handles agree under mirrored and tilted weapon policies.

Removal obligation: remove duplicate character region placement and the separate authored-hurt scan after all its users are registered. Read-only debug outlines may remain as views of actual colliders, not another authoritative collision representation.

### P9. Separate gameplay behavior schemas, profiles and accepted impacts

Passes:

- [ ] P9a. Define small typed behavior configurations for damage-on-contact, receiving damage, guarding and existing projectile behavior; reuse current health/body settings and contact facts.
- [ ] P9b. Bind animation activation to attack instances/hit groups; unify player, authored enemy and Studio-test contact resolution and deduplication.
- [ ] P9c. Move character-specific role/asset/property bindings into profiles/templates and provide typed property inspectors. Keep movement/controller algorithms where they belong.

Primary homes: EntityAsset, ResolvedEntity, EntityAuthoring, EntityAnimator/HitLedger, AuthoredArena, CombatSystem2D, IAuthoredHurt2D, AuthoredEntityEnemy2D, player authored-action adapters, PersonLoadout and PersonMoves bindings.

Gate: miss, hit, block, invulnerability, kill, multiple hurt colliders, multiple targets, repeated overlaps and intentional repeated pulses produce the configured outcomes exactly once. Accepted impacts trigger the right sound/presentation; rejected contacts do not. Existing projectiles, contact damage, reactions and replay/state capture remain supported.

Removal obligation: geometry/timing no longer carry arbitrary combat fields. Remove duplicated enemy-versus-player attack handling and obsolete IAuthoredHurt2D query plumbing when all supported callers use the common path. Do not replace typed behavior configuration with repeated string parsing.

### P10. Prove reusable anatomy templates and module composition

Passes:

- [ ] P10a. Publish migrated person, quadruped and flier templates with part slots, exposed dimensions, attachment contracts and default animation libraries.
- [ ] P10b. Extract compatible upper-body/lower-body modules and their animation target mappings using the P7 instancing mechanism.
- [ ] P10c. Build a centaur study entirely from those modules/resources; expose module connections, target ownership and root-motion ownership in Studio.

Primary homes: ModelTemplate, template resources, module/part catalog and resolver, animation bindings/masks, CharacterSeeds starter content and Studio creation/browser/outline tools.

Gate: three anatomy families and an arbitrary machine use the same evaluator. The centaur lower body runs a gait while the upper body performs an independently bound action; only one module drives root motion. Two instances of the same module do not collide in target IDs. Library part combinations require no generated C# character classes.

Removal obligation: delete anatomy-specific runtime/template switches and redundant module instancing logic. Humanoid constants may remain conveniences for built-in content, not prerequisites of the generic evaluator.

### P11. Finish content conversion and retire the compatibility routes

Passes:

- [ ] P11a. Convert the remaining model/variant/clip/part/entity bundles with reports and reviewed motion/render/gameplay comparisons.
- [ ] P11b. Move retained seed generators and diagnostics onto the canonical definitions; confirm explicit generation does not overwrite editor-owned content unexpectedly.
- [ ] P11c. Complete the editor acceptance walkthrough, remove unused compatibility fields/evaluators/queries/adapters, and update format/workflow documentation.

Gate: the complete authored library resolves without legacy runtime evaluation; Studio can create, vary, animate, equip, collide, save and reopen a character using the intended workflow. Game and Studio share the canonical resolution/placement/collision paths. External importers may still convert source formats, but do not retain a second playback engine.

Removal obligation: the retirement ledger below is closed or each retained item has a specific supported purpose. A renamed legacy class behind a permanent wrapper does not satisfy this phase.

## 6. Reference workflow and final acceptance

The full workflow is the integration test for the design:

1. Create a person from a template and play its shared walk.
2. Make a variant; change one forearm length, shin length, torso width and head size.
3. Place an attachment at the shin midpoint and confirm it follows length edits.
4. Fit a shin guard; rotate knee/ankle through animation and verify no unintended shear.
5. Choose gloves, hair, beard, shirt, weapon and shoes from library assets. Reuse one asset on two sockets.
6. Edit an instance override, reset it, then deliberately edit the shared asset and observe all dependent wearers refresh.
7. Attach a curve face; blink while walking and switch expression through a profile binding.
8. Adjust a joint limit and animate both an explicit angle and an explicitly normalized bend.
9. Attach a sensor to the weapon; animate activation and set its gameplay behavior properties.
10. Test hits against an actor with several hurt colliders, then block, miss, kill and strike multiple targets.
11. Confirm audio/effects come from accepted outcomes and are absent during timeline seeking.
12. Save all dependent assets, restart Studio/game and verify the same results; exercise undo across shared asset and instance edits.
13. Repeat the applicable steps on quadruped and flier templates.
14. Compose the centaur and bind upper/lower animations without copying the entire rig or adding a dedicated runtime type.

This is not a requirement to produce polished new artwork. Simple visibly distinct library parts are sufficient to prove resource reuse, fitting and combinations.

## 7. Verification and compatibility gates

Use existing tests first and extend them at meaningful behavior boundaries.

| Area | Existing starting points | New behavior to protect |
| --- | --- | --- |
| Rig/IK | BoneAuthoringTests, RigConstraintTests, PoseEvaluatorTests, ResolvedModelTests, NativeRigTests | Similarity invariants, length-bound links, limits, conversion, constraint order and unchanged authored setup |
| Proportions/motion | AppearanceAndBuildTests, HumanProportionsTests, PersonVariantMotionTests, PersonStudyConversionTests | Shared clips on varied lengths, intentional normalized mappings, no accidental absolute-rest freezing |
| Geometry/appearance | CurveAttachmentTests, CutoutAttachmentTests, PartPaintTests, ModelDrawingTests, SlotTimelineTests | Common placement, nested animated parts, point bindings, waves, material inheritance and non-image slot selection |
| Parts/variants | ModelTemplateTests, ModelEditingTests, RepeatedAuthoringTests, AppearanceAuthoringTests | Repeated asset instances, leaf reset, missing contracts, transitive invalidation, save/reopen and duplication |
| 2D/3D/rendering | WeaponOrientationTests, PointCharacterProjectionTests, CharacterBufferRenderingTests, CharacterActorRenderingTests | Layer versus physical depth, both facings, grip/tip/muzzle alignment, picking and deliberate XY projection |
| Collision/combat | EntityRegionShapeTests, EntityRuntimeTests, existing collision/physics and game combat tests | Shared broad phase, filtering, interval crossing, multiple hurt colliders, deduplication and accepted effects |
| Editor | EditorSessionTests, AnimationEditingTests, ModelEditingTests, EditorSmoke, EditorSurfaceSmoke | Actual authoring commands, no silent handles, clear edit ownership, undo and transitive dependency saves |
| Runtime/state | EntityRuntimeTests and gameplay simulation/state round-trip tests | Deterministic activation, attack/contact state, reset/removal, restored ownership and no duplicated replay effects |

At the start, run the relevant tests to establish current status. During a phase, run its targeted checks; broaden to the affected integration suites when references or runtime behavior cross boundaries. For final cutover, build Core, Studio, the game and their dependencies, run the complete relevant test suite and inspect the representative visual/gameplay gates. A solution-wide build does not add Noodle to the acceptance scope. Documentation-only planning does not require rerunning all runtime tests.

Useful existing commands, with outputs directed to a new disposable artifacts subdirectory:

~~~powershell
dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"
dotnet run --project App2d.CharacterStudio -- --smoke-editor <new-output-directory>
dotnet run --project App2d.CharacterStudio -- --smoke-editor-surface <new-output-directory>
dotnet run --project App2d.CharacterStudio -- --smoke-motion <new-output-directory>
dotnet run --project App2d.CharacterStudio -- --smoke-weapons <new-output-directory>
dotnet run --project App2d.CharacterStudio -- --smoke-wardrobe <new-output-directory>
dotnet run --project App2d.CharacterStudio -- --smoke-quadrupeds <new-output-directory>
dotnet run --project App2d.CharacterStudio -- --smoke-pterosaur <new-output-directory>
dotnet run --project App2d.CharacterSeeds -- --contact-study <new-output-directory>
~~~

The existing editor smoke prepares a scratch workspace and deletes its previous workspace subfolder. Always use a verified disposable output directory. Review generated frames; a successful process exit alone does not establish visual parity.

Set numeric tolerances by observable quantity and scale, not one permissive global epsilon. Do not require bitwise matrix identity where representation intentionally changes; compare final positions, silhouettes, attachment frames and behavior. Record approved differences rather than updating every expected result until tests pass.

Measure representative compilation time, per-actor pose/collider allocation, collider registration counts and broad-phase candidate counts. Optimize only when the migration adds a measured regression. Keep immutable shared assets and per-instance state separate; avoid accidental per-frame recompilation or collider churn.

## 8. Retirement ledger

| Existing path | Replacement | Retirement condition |
| --- | --- | --- |
| Repeated offset/angle frame construction in art, sockets, props and handles | Common evaluated placement plus explicit 3D bridge | All consumers use it; P1 frame/picking references pass |
| Generic PuppetPart wave fields and unrelated validation/dependency code | Typed curve modifier | P2 converted inputs no longer require these fields outside a legacy reader |
| Generic PuppetPart face fields, fixed face tracks and expression validation | Animated face part / optional procedural asset plus profile mapping | Current Person/clip/gameplay callers converted; P7/P11 comparisons pass |
| Nullable Transform selecting point versus affine setup semantics | Canonical local rig and explicit conversion/feature semantics | No canonical node depends on that branch |
| Parallel Chains and Constraints collections | One resolved ordered constraint representation | Source conversion complete and references preserve order |
| Position-only legacy rig evaluation | Canonical nodes with explicitly migrated constraint/attachment semantics | All supported bundles converted; source importers no longer need legacy playback |
| Core PersonBuild geometry formulas | Parameter bindings and template presets | Equivalent migrated Person proportions and shared-motion gates pass |
| Rest/PartOverride property whitelist and descendant absolute-position freezing | Typed recipe/property overrides | Existing variant families converted; reset/inheritance and length handles work |
| Image-only slot renderer restriction | Common attachment payload dispatch | Shapes, curves, images and meshes use the same selection/placement path |
| Separate clothing/prop/ordinary-part placement and asset-ID uniqueness per wearer | Attachment instances with shared asset refs | Same asset can occupy multiple sockets; all old equipment consumers migrate |
| EntityRegion as an authoritative parallel collision representation | Registered attached colliders / read-only debug views | All authored hurt/attack users enter shared collision queries |
| CombatSystem2D authored-hurt scan and IAuthoredHurt2D query workaround | Ordinary collider indexing and target ownership | Extended hurt shapes are found without special scans |
| HitWindow bundling geometry, anchors, timing, damage and sound | Collider attachment + activation track + typed gameplay behavior | Player, enemy and Studio-test attacks use common contact outcomes |
| Separate attack/contact/deduplication routes in enemy, player and arena | Shared combat behavior/contact policy | Existing combat scenarios and state handling pass |
| Person asset-ID/profile switches in generic/Core paths | Resource role/property/template bindings | Content changes need data edits; specialized algorithms retain only genuine game responsibilities |
| Temporary legacy loaders/runtime adapters | Explicit source converters | Canonical authored library and supported import path no longer need duplicate runtime evaluation |

## 9. Risks and decisions to settle at the relevant gate

| Risk / question | Required handling |
| --- | --- |
| Old point IK moves positions without rotating attachments; rigid bones rotate frames. | Capture affected visuals/children in P0. Convert constraints and attachment orientations deliberately in P3; preserve approved results or identify intentional improvements. |
| Native affine scenes contain inherited shear that a similarity hierarchy cannot express. | Classify in P0; implement a scoped appearance deformation/bake where justified. Do not silently approximate or reopen unrestricted rig scale. |
| Old Z values combine absolute layers, relative offsets and real prop depth. | Document conversions in P1 and compare ordering plus tilted solid geometry, not just XY points. |
| A generic variant schema can become an untyped JSON patch language. | Limit overrides to declared typed properties and selections. Compile bindings and show provenance; keep structural validation. |
| A generic component system can become larger than the original engine. | Start with the actual geometry, collider, deformation and gameplay behaviors used by the acceptance workflow. No general ECS rewrite or expression language is required. |
| “Normalize everything” changes existing performances. | Preserve explicit angle/target channels; normalization is an opted-in mapping with visible units. |
| Registered sensors expose multiple hurt shapes per actor. | Deduplicate by actor/attack group and reconcile collision masks before removing legacy queries. |
| Boolean activation misses short intervals or repeated pulses. | Preserve interval crossing and attack-instance identity in the fixed-step animation/runtime boundary. |
| Sharing parts introduces stale caches and surprising global edits. | Transitive dependency versions, visible shared-versus-instance editing and multi-document undo/save gates ship with P5. |
| Swapping a rigged part changes required animation targets. | Validate public contract compatibility and explicit remapping. Arbitrary topology substitutions are not automatic retargeting. |
| Applying artwork fit to a whole face/module reintroduces shear under child rotation. | Define layout/refit versus intentional deformation policy; inherit similarity poses and compute attachment positions separately by default. |
| Stage count grows while duplicate code stays. | Close each phase's removal obligation and maintain the ledger. Temporary adapters have named callers and a phase that removes them. |
| Existing presentation time differs from simulation time. | Keep authoritative colliders on simulation poses; preserve accepted-contact facts and presentation-only retiming separately. |

## 10. Work deliberately outside this migration

- Noodle integration or preservation.
- A general 3D character skeleton, perspective animation system or replacement renderer.
- A replacement physics/collision engine, automatic physical simulation of every visual bone or a full ragdoll system.
- Arbitrary weighted skinning, cloth, fluid simulation and general self-collision. Existing point-bound cutouts/curves remain supported; new deformers can be added through their owning capability later.
- Arbitrary retargeting between unrelated skeletons or automatically producing a convincing centaur gait from any two clips.
- Infinite variant inheritance, a general scripting language, a mandatory ECS rewrite or automatic animation of every serialized property.
- A large new artwork-production pass. Use existing assets and small proof parts to establish the authoring model first.

These exclusions do not remove existing supported game features. They keep the simplification bounded while delivering all requirements in section 1.

## 11. Starting package and how to continue across sessions

Start with P0 and P1a/P1b, not a new universal asset schema. The first reviewable package should establish the reference fixtures, define the existing frame type's composition/layer rules and route one artwork/socket/picking path through it without rewriting the library.

The first visible capability package after that is the long-forearm / midpoint attachment / fitted shin guard / replaceable glove example. Carry it through the necessary P3-P6 passes before expanding the whole library. The face example then proves P7; combat sensors prove P8-P9; centaur composition proves P10.

Recommended first capability sequence: P0, P1, P2a, P3, P4a, P5 and P6. Finish the other ownership/conversion and joint-limit passes alongside the later capability packages, with their individual gates still enforced. This sequence gets the central length-and-part workflow into the author's hands before the more involved solver and full-library work.

For every implementation session:

1. Read this plan and the progress record, then inspect current code/status; do not assume an earlier chat's implementation state.
2. Select a specific unchecked pass and list its dependencies, supported conversion inputs and removal obligation.
3. Implement runtime, serialization and Studio commands together where the feature requires them.
4. Run the relevant behavior checks and inspect required visual evidence.
5. Record changed files, actual checks/results, intentional differences, remaining adapters and the next smallest pass.
6. Mark a pass complete only when its gate is satisfied. A phase remains open if the replacement exists but its removal obligation is unfinished.

Progress record:

| Date | Pass | Status | Evidence / next action |
| --- | --- | --- | --- |
| 2026-10-23 | Planning | Complete | Source inspection and schema-aware inventory informed this proposal. No implementation phase, runtime baseline run or asset conversion was performed in this planning pass. Next: P0a. |
| 2026-10-23 | P0a | Complete | Baseline recorded in [Character migration baseline](character-migration-baseline.md): checkout main at 769d60b5, clean tree. Schema-aware inventory via the real readers: 7 models, 7 variants, 91 clips, 30 props, 11 entities (all compile), 5 templates, 1 physics prop, all at current schema versions, zero catalog errors; reference graph, code-only bindings (14 player clips, hair-short-back, sheath, short-broad) and conversion-relevant facts (all 23 chains point IK, no declared lengths, scale tracks only in 4 curve-face clips, orient tracks in 15 weapon clips, face tracks in 28 clips, absolute part depths) listed there. Full App2d.Tests: 2337 passed / 7 failed of 2344; the 7 are pre-existing (6 missing dark-cave tileset manifest, 1 TriangleMeshBuilder3D count) and outside the character suites. Every section 7 class passes (Authored 434/434, Rendering 163/163, Collision 53/53, Gameplay.Combat 11/11, Editor 8/8). Seven Studio smokes and the Seeds contact study all exit 0 with frames and reports in a disposable directory; frames were not reviewed against a reference. No conversion, no new abstraction. Next: P0b, capture reference poses, sockets, silhouettes/order and combat outcomes for the representative bundles (asset IDs mapped in the baseline), add the mechanical linkage fixture to shared tests, and record tolerances per quantity before comparing. |
| 2026-10-23 | P0b | Complete | Reference set in shared tests: `MigrationReferenceTests` plus `App2d.Tests/Authored/References/character-migration-references.json` (75 pose/socket/silhouette/collision cases over Person + tall-thin/short-broad, Hero, Cinder, Maul brute, spear-guard, quadruped/triceratops/baby, pterosaur, curve-face, stalker and the new `TestModels.Linkage()` crank fixture; both facings, foot contacts, sheathing, tilted/twisted sword, zero-length and full-reach IK) and 8 `AuthoredArena` combat scenarios with exact hits, events and end states. Tolerances recorded per quantity before comparing (section 6.1 of the baseline). Observed: all authored chains/contacts at residual <= 2.8e-8; the linkage shows the solver reporting `reached` with a 5.6e-3 residual just past full reach (input for P4b) and a 1e-3 minimum for a zero-length target; slam and club attacks miss in the arena at their test distances (input for P8/P9). Full App2d.Tests 2339/2346, the same 7 pre-existing failures as P0a. No code outside App2d.Tests changed; no conversion. Next: P0c, write the local-frame/layer/affine conversion decisions against these fixtures, with the evidence each intentional difference needs. |
| 2026-10-23 | P0c | Complete | Conversion dispositions for every general-affine and point-specific case in the reference set, in section 7 of [Character migration baseline](character-migration-baseline.md): preserve exactly (point IK via a named legacy adapter, absolute Z to local layer offsets, animated Z, weapon 3D bridge, foot contacts, hit/hurt placement, arena outcomes, build proportions, facing); scoped deformation/bake (curve-face eye scale as artwork fit, sheathing as a discrete attachment channel, faces as profile bindings, wave as a typed modifier); intentional difference (residual-based `reached` in P4b, zero-length fold, rotating hurt shapes and newly landing attacks in P8/P9, each needing its own evidence row); unsupported/report (inherited reflection/shear of the native wheel, the shear channel, retained on the explicit-bone evaluator as the legacy affine adapter until P11). Each row names its acceptance evidence and tolerance. Added only fixtures P0b lacked: the NativeRigTests wheel scene moved to `TestModels.Wheel()` and captured with full bone matrices (6 new cases, 81 total); extended capture compared against the P0b file first: 13 differences, all additions. Full App2d.Tests 2339/2346, the same 7 pre-existing failures. P0 gate met. Next: P1a, evolve the frame/placement value with identity, composition, Matrix3x2 participation, local/entity/world conversion and the layer rule from section 7.4, accepted against the P0b references. |
| 2026-10-23 | P1a | Complete | The composable placement value is `LayeredFrame2D` (`App2d.Core/Characters/Geometry/LayeredFrame2D.cs`): a `Frame2D` plus a relative character layer, with `Identity`, `FromMatrix` (zero layer), `FromRotation`/`FromYAxis` at a layered origin, `Local(scale, rotation, offset, layer)` as the one offset-and-rotation form, `Then` (frames multiply, layers add), `At`, `TryToLocal` and `TryInverse`; pure `Frame2D`/`Frame3D`/`Matrix3x2` carry no layer state. Layer rule (baseline 7.4): `ResolvedModel.LayerOffsets` holds `restZ - parentRestZ` per control (root keeps its rest Z) and `RestPlacement(id)`; `PoseEvaluator` composes every control as one local placement on its carrier (`EvaluatedPose.Placement(id)` for explicit bones, the parent's rotation-since-rest frame for point controls) with the translate-Z key as a further local offset, so explicit bones now take the same local layer meaning (all shipped values are 0; no reference moved). `ActorPose.Placement` (mirrored entity-to-world frame, layer 0), `Placement3D`, `ControlPlacement(id)`, and `Place`/`ToLocal` now go through it. Checks: `dotnet test --filter Authored|Geometry|Mathematics` 1177 passed / 1 failed (the pre-existing TriangleMeshBuilder3D count), including `MigrationReferenceTests` (81 cases, 8 scenarios, no drift) and the new `LayeredFrame2DTests` (identity/default, composition, world scaling and mirroring keep layer order, inverse, similarity carrier + nonuniform leaf adds no shear, actor round trip, layer offsets reproduce authored Z and an animated parent layer carries point and explicit children alike). No format change: authored `rest.z` stays absolute in files; the split is derived at resolve time. Intentional differences: none. Adapters: none yet (PartFrame2D consumers migrate in P1b). Next: P1b, route artwork placement, picking, editor inverse placement, sockets and Studio handles through `LayeredFrame2D` and delete `PartFrame2D`. |
| 2026-10-23 | P1b | Complete | `PartFrame2D` deleted; artwork is `PartAttachment2D.Attachment(part).Then(Carrier(...))` (one `LayeredFrame2D.Local` attachment on a bone or A-to-B carrier; the three hand-written offset/rotation branches are gone), bound points and cutout/curve rest depths are leaf layer offsets through the same value, picking (`PartPicking2D`), contours (`PartContours2D`), image slots, paint (`PartPainting`) and the face overlay read it, and `ArtworkPoint` inverts it for the Studio cutout drag. Ordinary sockets: `ActorPose.SocketPlacement` (authored offset/angle/layer on the control's carrier, carried by `ActorPose.Placement`), `SocketTurn` for the weapon editor's turn handle (the hand-summed `base + angle + orient.Z` is gone), and `Socket` lifts the planar placement through `PropPlacement3D.Lift` (P1c completes the bridge). Studio: `ModelOverlay.BoneTip`, `Viewport.DrawingPoints`, `ModelViewport` picking, `ModelCutoutEditor` and `AnimationWeaponEditor` use `EvaluatedPose.Placement`/`ActorPose` and the new `(model, pose)` overloads; the four-lambda `Angles/Bones/Rest/RestTransforms` call shape is gone from Core, Studio, Noodle's bridge check and the tests. Checks: `dotnet build App2d.slnx` 0 errors; Authored, Geometry, Rendering, Editor, Mathematics, Gameplay.Combat and Collision suites 1420 passed / 1 failed (pre-existing TriangleMeshBuilder3D count), `MigrationReferenceTests` unchanged (rest/animated artwork, picking frames, sockets, both facings, Z ordering), new `PlacementRoutingTests` (3) and `LayeredFrame2DTests` (7) pass; Studio smokes `--smoke-weapons`, `--smoke-editor-surface`, `--smoke-motion`, `--smoke-editor` all exit 0 in a fresh disposable directory: the weapon turntable (twist 0..180, tilt 45) and the six-time sword swing in both facings were viewed and show the same foreshortening, grips and facing mirror as the baseline description, the 34 editor-surface frames render (model rig/part/cutout, animation control/slot/weapon handles, curve-face studies), motion-proof strides and residuals are identical to the P0a numbers (0.400/0.480/0.328 walk, 1.280/1.536/1.050 run, contact <= 2.8e-8, reach <= 4.5e-8), and the editor smoke reports 50 PASS / 0 failures over its 40 frames including the cutout and bone-tip drags. Intentional differences: none (baseline 7.4 and the facing row annotated as exercised). Remaining adapter: the `Func<string, Vector3> world` + optional `bone`/`rest`/`restBone` overloads of `FrameOf`/`Contour`/`Distance`/`ArtworkPoint` are the narrow adapter for unmigrated inputs (prototype `PuppetDefinition`/`PuppetPose` drawing in `PuppetDrawing.Build(PuppetDefinition, PuppetPose)`, seed/template rest lookups in `CharacterGeometryTests`, `PuppetAuthoringTests`, `RoundedRectangle2DTests`, `CurveAttachmentTests`, `CutoutAttachmentTests`, `RoundedPathTests`, `QuadrupedTests`, `HumanoidFootwearTests`, and `Proof.RoundedPaths` in Studio); everything with a `ResolvedModel` + `EvaluatedPose` uses the typed overloads. Next: P1c, finish `PropPlacement3D` (prop points, XY projection for muzzle and attack regions, the editor's prop-plane inverse) and compare against the P0 references. |

