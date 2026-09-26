# Character editor replacement

Status: in progress, 2026-09-26. Phases 1 to 3 are implemented, and the in-game player is drawn from authored assets.
All of this is on branch `claude/character-editor-phase-3-cb7a61`, which is not yet merged to `main`. See
[Where we are](#where-we-are-2026-09-26) for the summary; each phase below carries its own progress note.

## Product goal

Make convincing, readable game characters from simple shapes, without requiring
drawing skill. Creating the fiftieth person should mostly mean choosing existing
pieces and adjusting a few values. Creating a creature with different anatomy
should use the same tools, with a new model structure.

The quality bar is movement, silhouette, contact, expression and attack readability.
The existing walk and run studies are useful visual reference assets. Preserve
their performances while replacing the surrounding prototype architecture.

The editor should answer three questions clearly:

1. **Model:** What is it made of, and what can move?
2. **Animate:** How does it move?
3. **Entity:** What does it do in the game?

One application, one viewport, one document workflow. Continue using C#, MonoGame
and ImGui. Reuse the renderer and math; replace the competing editor workflows.

## Main decisions

| Area | Decision |
| --- | --- |
| Character structure | Named controls, connections, optional IK chains, drawing parts and sockets. No mandatory head, hands or legs. |
| Sharing | A base model owns structure. A variant references that base and stores explicit overrides. One inheritance level. |
| Animation | Separately named assets compatible with a particular model structure. Motion sets assign clips to roles. |
| Gameplay | Entities reference a model or variant, select a motion set, and define available actions, collision, equipment and sounds. |
| Coordinates | Orthographic XY posing with Z for depth. Local 3D orientation for props. No general 3D character editor. |
| Evaluation | One evaluated pose supplies drawing, sockets and animated collision. |
| Presets | Build/look presets apply values. Motion sets remain shared references. No multiple inheritance of presets. |
| Editing | Dragging in Model changes rest geometry; dragging in Animate changes motion. Selecting a variant never silently edits the base. |
| Imports | Existing imported libraries remain source assets. Conversion is explicit and selective. |

## Assets and ownership

There are four main asset types in the browser. Props are supporting assets exposed
through equipment and attachments, rather than another mandatory workflow.

### Base model

A named reusable structure, for example `person`, `hound` or `walking-lantern`.
"Family" is a useful informal name for a base model and its variants, not another
asset type. Person and quadruped are starting templates, not engine categories.

A base model contains:

- Stable IDs and editable labels for controls, connections and drawing parts.
- Rest geometry, optional two-bone IK chains and explicit bend directions.
- Stroke, ellipse and rounded-box parts initially; existing outline/head geometry
  can become another shape implementation without defining a separate head editor.
- Named sockets with position and orientation, usable by faces, equipment and collision.
- Optional named control groups, such as `upper` and `lower`.
- Exposed build values, such as height, torso width, arm length and leg length.
- Named motion sets, consisting of role-to-animation assignments.

Controls and drawing parts remain separate. A headless person can hide its head
part and face while retaining a compatible control structure. An extra arm that
needs independent motion changes the structure and calls for a new base model.

Expose a small useful set of build values for the Person template first. Implement
their geometry changes explicitly and deterministically; do not build a scripting
language or arbitrary parameter-dependency graph. Custom models remain authorable
by moving rest controls and editing shapes directly.

Base models may include named hurt-region layout presets for reuse. These are
geometry suggestions, without damage or controller behavior. An entity explicitly
selects a preset and may override or disable individual regions. This avoids
redrawing a person's collision for every variant.

### Model variant

For example `needle-person`: references `person`, then overrides proportions,
shape dimensions, colors, visible parts and face placement/default expression.
Variant-specific decorative parts may attach to existing controls. Variants do
not add, remove or reparent controls or change IK topology.

The base is the only inherited source. Build and look presets are authoring
shortcuts that write override values, not additional live parents. Duplicating a
variant creates a sibling referencing the same base, not a chain of variants.

Every overridden field has a visible indicator and **Reset to base**. The editor
shows the base name and offers **Open base**. Changes to unoverridden base values
propagate. A base topology edit reports affected animations, variants and entities;
missing references are errors, not silently discarded data.

**Make independent model** is an explicit operation that flattens the resolved
variant into a new base. Animation references still require compatibility checking
or an explicit copy/conversion; matching labels alone do not establish compatibility.

### Animation

An animation names its base model and structural revision, and owns motion channels,
duration, loop behavior, travel, contact intervals and named timing markers.
It does not own colors, body proportions, damage, sound asset choices or permissions
to perform an action.

Animations can be authored against a chosen variant for preview while retaining
the base structure as their compatibility contract. Store reference measurements
with the clip so changing the base does not silently reinterpret its authored units.

A motion set is a small named table inside the base model:

| Role | Standard | Heavy | Deliberate |
| --- | --- | --- | --- |
| idle | person-idle | heavy-idle | person-idle |
| walk | medium-walk | large-walk | guard-walk |
| run | person-run | heavy-run | person-run |
| hit | person-hit | heavy-hit | person-hit |

Sets may share clips. Start a new set by copying assignments, with no set-inherits-set
resolution. Missing roles remain visibly unassigned. Names such as "large walk"
describe an artistic choice; size does not force a particular animation.

An entity selects one set and can override individual role assignments. Effective
assignment is **entity override, then selected set**. The editor displays that source.
An action can also directly name a specialized animation such as a spear thrust.

### Entity

For example `spear-guard`: references `needle-person`, chooses `Deliberate`, equips
a spear and defines game behavior. Another entity can use the same visual variant.

Entity-owned data:

- Controller selection and its supported configuration, including movement values.
- Explicit enabled actions, with their animation role or direct clip reference.
- A stable movement shape; selected hurt layout and region overrides.
- Equipment/socket bindings and default face behavior configuration.
- Action timing, active hit regions, damage and sound/effect bindings.

A shared library containing jump or spin never enables those actions automatically.
Controller requirements replace the current universal required-action list. A
stationary object should not need a walk or attack. Unsupported requested actions
are rejected or handled by an explicit controller fallback, not an accidental idle.

Start with entity templates that copy gameplay defaults, and duplicate existing
entities while retaining model/animation references. Do not add entity inheritance
in the first version. Extract shared action definitions later if repeated tuning
across many entities demonstrates the need.

## Authoring a character

The common path for another person should be:

1. **New variant → Person**; choose Tall/thin as a starting build.
2. Adjust silhouette while the shared walk or run plays.
3. Choose colors, visible parts, face and name; save the variant.
4. **Create entity from this model**; choose a guard gameplay template.
5. Select Deliberate movement, equip a spear, select its thrust animation.
6. Adjust movement/hurt geometry and attack timing in context; test and save.

No rig reconstruction, copied walk keys or mandatory visit to every workspace.
The New entity dialog may perform steps 1–4 together, but the resulting assets and
references must remain clear.

For an unusual creature, **New model → Empty** opens the same Model workspace.
Add controls and shapes, pose it, save clips, and make an entity. No conversion to
Person or Hound is required for collision or gameplay.

## Proportions and reusable motion

This is the first technical risk to resolve. The current workshop stores absolute
local joint positions in every key. Changing rest lengths then re-solving knees
does not make the complete motion suitable for the new body.

Use a small explicit channel system for newly authored, reusable clips:

- Ordinary controls use a parent-local rest transform plus animated translation
  and XY rotation offsets. Child frames follow the parent. Z remains an explicit
  depth value, not a perspective projection parameter.
- IK limbs key an end target in a declared frame and a bend choice. The middle
  joint is a solved result, not a competing position channel.
- Translation offsets declare their scale source: fixed model units or a named
  measurement, such as arm reach or leg reach. Rotation is in radians. Scale
  choices are model defaults shown in advanced track settings, not a decision
  required for each key.
- Shape size/visibility channels allow limited squash, breathing and changing
  silhouette. They do not change IK lengths or the movement collider implicitly.
- Root travel is a separate channel with an explicit scale source. Contacts use
  the same locomotion frame and travel scale.

For an ordinary local offset, the basic rule is:

`resolved offset = variant rest offset + authored delta * variant measure / reference measure`

The channel's frame supplies its origin and XY rotation. An IK target uses its
declared frame and scale, then solves with the variant's actual two segment lengths.
Foot targets use a ground-relative locomotion frame; hand swings can use a shoulder
frame. Do not normalize every coordinate by overall height, or scale a planted
world target each frame.

Changing proportions cannot guarantee a good performance at every extreme. Report
reach errors, retain limb lengths and make it easy to select a different walk.
The first supported range is the reviewed standard, tall/thin and short/broad
Person variants. Widen that range with examples, not a promise of universal retargeting.

Before committing the animation schema, reproduce the existing walk and run at
reference proportions and show both clips on those three builds. If the channel
scheme cannot preserve the reference performance, revise it before building a
large timeline UI.

### Travel and contacts

Keep two distinct playback contexts:

- **Authored preview:** use clip travel and authored contact targets. This is
  deterministic and supports random-access scrubbing and repeated cycles.
- **Gameplay locomotion:** the controller owns actor movement. Advance gait phase
  using actual ground distance and the resolved stride length. Capture contact
  anchors at touchdown from the evaluated foot and terrain; retain them in world
  space until release. Airborne phases use clip timing and controller state.

Do not apply clip translation and controller translation to the actor together.
Root-driven action movement can be added later as an explicit action mode; initially
actions use controller-owned travel and separate local body bob/lean.

Use half-open contact intervals at runtime, with explicit treatment of the held
animation endpoint in the editor. Walks can have support by both feet; runs can
have no support. Loop transitions, seek, facing changes, interrupted actions and
teleports reset or transfer contact state deliberately. Constraint ownership follows
the winning animation layer; never blend two world anchors into a sliding foot.

Flat-ground distance matching and planting are required in the first gameplay
slice. Slope adaptation, ledge balancing and procedural recovery are separate work.
Expose residuals so reach problems are visible rather than hidden by limb stretching.

### Composition and faces

Keep clips whole. A named group determines which channels an overlay owns; there
is no need to physically split the model into upper/lower drawings.

First composition support is one base motion plus one override with a group mask
and short blend in/out. Each IK chain is owned as a unit. In the Person template,
pelvis/travel/leg targets belong to locomotion; chest and arm channels may belong to
an upper action. Ancestors still move child frames normally. Solve constraints
after channel composition, not independently on two poses and then blend knees.

Faces are separate appearance channels attached to a part/socket. The evaluator
accepts an explicit gameplay expression override; fallback order is gameplay,
animation, then model default. Gameplay owns reaction priority and duration.
No face data is baked into limb keys.

## Depth and weapons

Keep the existing orthographic depth-tested renderer. Model parts expose simple
front/back ordering controls backed by numeric Z. Ordinary editing remains XY;
there is no character orbit camera or mandatory 3D manipulation.

Props need more information than a hand point. A socket has a position, an axis
and an across direction defining an orientation frame. A prop defines local art,
grip, optional second grip, tip and optional muzzle. Equipment references a prop
and a model socket. Explicit orientation tracks allow foreshortening and swings
through depth; a 2D hand angle alone cannot recover that information.

Reuse existing weapon art and geometry through an adapter. Normalize orientation
frames, apply size separately and handle degenerate guide vectors deterministically.
Weapon hit regions and muzzle output derive from the same resolved prop transform
as its rendering. Collision projects that transform into the game's XY plane;
visual Z does not introduce a new gameplay depth axis.

Ship one-handed attachment first. Keep the second-grip field available, but defer
the coupled two-hand constraint solve. A general 3D modeling tool is out of scope.

## Collision and action authoring

Keep three distinct objects with distinct overlays:

| Object | Ownership and behavior |
| --- | --- |
| Movement shape | Entity, stable body-local box initially; unaffected by animation. Fit only on an explicit command. |
| Hurt region | Named box or convex hull of selected controls/shape geometry, optionally attached to a socket. Evaluated from the final pose. |
| Attack region | Action-owned attached box initially, active for a defined interval; can bind to a prop socket. |

No fixed body/head/legs/arms vocabulary in generic geometry. A template may create
those names. Compiling a model variant resolves shared hurt-layout geometry using
its proportions; the editor shows any explicit entity overrides.

Animation markers name visual moments such as `strike`, `recover` and footsteps.
Actions bind gameplay events and sounds to these markers or explicit normalized
times. Retiming uses one mapping for pose sampling, hit windows and sound events.
Changing a clip reports missing required markers rather than retaining unrelated
timestamps silently. Numeric timing remains available for imported clips.

The Entity timeline shows anticipation, active and recovery intervals together
with hit geometry. The existing attack trim/contact remapping is reusable math,
but its full set of knobs should sit behind an advanced timing section.

Editing or scrubbing never triggers gameplay events. Simulation dispatches events
when advancing across their times, including skipped-over markers and loop wraps.
Preserve per-action hit deduplication and deterministic fixed-step simulation.

## Editor layout

```text
Asset search / New       Open document · saved state        Model | Animate | Entity    Test
-----------------------------------------------------------------------------------------
Assets / model tree      Shared viewport                    Selection inspector
                        grid, contact and collision        relevant properties only
                        overlays when requested
-----------------------------------------------------------------------------------------
Animation / action       Play · scrub · speed · loop         Compare variants
timeline when relevant
```

- The asset browser filters by Models, Variants, Animations and Entities; search
  works across names/tags. Imported motions live under Sources.
- Model shows a parts/control tree and contextual inspector. Default selection is
  drawing parts; **Edit rig** exposes control handles and IK setup.
- Variant editing prioritizes exposed build values and appearance. Base controls
  remain visible for preview but structural editing requires opening the base.
- Animate shows only compatible clips, pose tools, selected tracks, contacts and
  markers. Dragging captures affected channels; **Key pose** is an explicit command.
  Include key move/copy/delete, easing, onion skin and visible autokey state.
- Entity shows model/motion assignments, actions, equipment, collision and sounds.
  Selecting an action brings its timing and hit-region inspector into focus.
- Face and outline editing use the same viewport and inspector, with a focused
  edit tool and a clear way back. Avoid additional independent editor windows.
- Test enters the existing arena through the shared evaluator; restart applies an
  explicit snapshot of current drafts. Returning preserves document selection.

Playback, camera, selection, overlays and comparison are session state, not asset
fields. One transport handles playback; preview speed is separate from clip duration
and gait speed. Scrubbing to an endpoint holds that endpoint. Fit is explicit.

**Compare** pins two or three variants in the same viewport at the same world scale.
Default to synchronized normalized phase for evaluating proportion changes, with
an authored-speed option for comparing performances. Switching variants preserves
phase, camera and selected clip. Include a game-size preview: enlarged authoring
views alone are a poor test of readability.

Shared animation edits display the asset's name and number of dependents. **Duplicate
animation** makes a deliberate specialized version. Merely previewing a variant
does not create private animation copies or correction tracks.

## Implementation boundaries

Keep existing project boundaries. Add small concrete components as needed rather
than starting with a generalized plugin architecture.

| Component | Responsibility |
| --- | --- |
| Core definitions | Versioned models, variants, clips, props and entity references; strict structural validation. |
| Asset resolver/compiler | Resolve shallow overrides and references; compile stable IDs to indices; validate controller and motion compatibility. |
| Pose evaluator | Sample channels, compose layers, build frames, solve IK/contacts and emit final controls/sockets/shape values. No graphics. |
| Collision builder | Derive hurt/attack regions from that final pose and resolved entity configuration. |
| Rendering | Build character/prop/face meshes from the supplied final pose. No independent animation sampling. |
| Gameplay animation state | Phase, transitions, world contact anchors, expression input and event dispatch. No ImGui dependency. |
| Editor document/session | Per-asset drafts, undo, save, selections and preview state. |
| Editor workspace views | Model, Animate and Entity tools using the same session and viewport. |

Runtime flow:

```text
resolved model + clip channels + controller inputs
  → sample and compose local channels
  → evaluate hierarchy and IK/contact corrections
  → final pose and socket/prop transforms
  → rendering and animated collision
```

Gameplay derives collision from the authoritative tick pose. Rendering can consume
that pose initially. Any future interpolated display pose must be an explicit
interpolation of authoritative results, not a second independent motion clock.

Compile references when loading/changing assets, not per actor per frame. Immutable
resolved assets are shared; each actor owns playback/contact state and a reusable
pose buffer. Start from the working dictionary-based evaluator for the first proof,
then move hot evaluation to indexed arrays without changing authoring IDs.

### Files and editing semantics

Use readable versioned JSON under a new authored asset directory, for example
`Assets/Characters/authored/{models,variants,animations,entities,props}`. One asset
per file; IDs do not depend on filenames or display labels. Build the catalog by
scanning these directories initially, without another manually synchronized index.

Give structure a compatibility revision separate from its file-format version.
Proportion/appearance changes do not change structure revision. Adding/removing or
reparenting controls, changing chain membership, or changing channel frames does.
Compatibility checks may accept additive changes when all required IDs, parents,
frames and chains still match; never infer compatibility from anatomy names alone.

Reuse strict field validation and atomic per-file writes. Report semantic errors
against asset IDs and fields. Drafts with unresolved references may be saved for
repair, but cannot compile for playtest/game use; surface these errors in the UI.
Structural edits show dependents before applying and are undoable. Do not copy the
prototype's automatic deletion of unrelated animation data into shared assets.

Undo is per document, with a drag treated as one transaction. Snapshot-based undo
is sufficient initially; capture at edit boundaries instead of serializing the
entire asset every display frame. Opening related assets retains drafts. Save
clearly identifies which documents are dirty; Save All is explicit and is not
claimed to be atomic across multiple files.

External/model-authored JSON uses the same schema, validator and evaluator. Provide
a command-line validate/render-preview path using the existing smoke infrastructure.
Clean files can reload from disk; a dirty draft gets a conflict choice. Reuse these
mechanisms rather than adding an LLM chat integration to the editor.

## Reuse and replacement map

| Existing code | Treatment |
| --- | --- |
| `CharacterMesh`, `PointCharacterRenderer`, shader and render targets | Reuse geometry submission and depth rendering. |
| `PuppetDrawing` | Evolve into the generic model renderer; replace baked face selection with evaluated appearance inputs. |
| `TwoBoneIk2D` | Reuse math and reach reporting. |
| `PuppetPose` | Reuse contact/IK ideas and reference behavior; replace absolute-key evaluation for reusable native clips. |
| `PuppetDefinition` | Reuse controls/parts concepts and validation patterns; split model, clip and variant ownership. |
| `PuppetTemplates.StepStudy/RunStudy` | Preserve as reference fixtures; explicitly convert and visually compare. |
| `FacePose`, `FaceDrawing`, `HeadShape`, `HeadDrawing` | Reuse expression/shape implementations behind generic part attachments. |
| `WeaponDrawing` | Extract library-independent prop art/frames; retain current visual results. |
| `PointLibrary`, `PointClip`, import provenance | Keep as read-only source storage and sampling. Convert selected clips through explicit mappings. |
| `PersonPose`, `HoundPose`, anatomy renderers | Retain for legacy source preview/migration comparison; do not make them the new model API. |
| `EntityAction` time mapping, `EntityRegion` collision math | Reuse applicable math; replace fixed clip/library/attachment vocabularies. |
| `EntityPose` | Replace anatomy branches and internal clip sampling with collision/attachment evaluation from a supplied final pose. |
| `EntityPlaytest` | Reuse arena and simulation behavior; adapt asset/pose inputs and explicit capability checks. |
| `PuppetDocument`, `StudioDocument` | Consolidate useful undo/save behavior into the new document/session layer. |
| `StudioGame` host, DPI, camera and dialogs | Reuse infrastructure; replace the separate source/entity/workshop screen routing. |
| Smoke checks and visual exports | Retain as regression tools, extending them with variant comparisons and a generic creature. |

Preserving imported libraries does not require converting all 759 clips now.
Mapping an imported pose into a native rig is an explicit authoring operation:
select source and target, map controls/frames, bake a new clip, review contacts and
depth, then save with provenance. Do not promise automatic equivalence between
arbitrary 3D rigs and the native 2.5D controls.

## Where we are (2026-09-26)

| Phase | State |
| --- | --- |
| 1. Shared motion proof | Done. Walk and run reproduce the prototype and play on three Person builds. The visual verdict on the rendered frames was never recorded. |
| 2. Model and Animate editor | Done (`App2d.CharacterStudio --editor`). One acceptance check in `--smoke-editor` failed once and passed in every run since; the cause is unknown. |
| 3. One entity playable end to end | Done. The spear guard, jumping player and stalker work in the arena and through the editor's **Test**. The guard and stalker replace two enemy placements in the game. |
| 4. Repeated-authoring workflow | Not started, apart from the masked upper-body overlay (see below). |
| 5. Cut over | Started. The in-game player is drawn from authored assets. The rest remains. |

**In the game today.** The player is drawn by `AuthoredPersonPresentation2D` using the player move set
(`player-*` clips plus `person-walk` and `person-run`, and the `sword`, `sheath` and `pistol` props). Movement, hit boxes
and attack timing still come from `Person2D` and the legacy `entities/player.json`. Shieldback and green-dinosaur
placements spawn the authored `spear-guard` and `stalker-pest`. Maul, cinder and needle are still legacy point-library
enemies. The game refuses to start if any authored asset fails to load or compile. Design and gaps for the move set are
in [the player move set spec](superpowers/specs/2026-09-25-player-move-set-design.md).

**Added since the phase 3 note.**
- A masked overlay: `PoseInput.Overlay` with `PersonLoadout.UpperBody`. The gun shot and aim play on any legs. There is no blend in or out yet, and `EntityAnimator` does not use it.
- `ContactHold` for in-place playback outside `EntityAnimator`.
- `PersonLoadout`, the move set's prop rules, shared by the game and the move review.
- A blended gameplay `FacePose` drawn on authored models.
- Per-frame props on `AuthoredCharacterShader`.

**Open issues, most important first.**
1. **Run speed versus stride.** The game's run (430 px/s, about 13 model units/s at the player's drawn size) is roughly six times the run clip's authored pace (2.2 units/s). The player's gait is capped at 2.5 times its authored pace, and the feet slide beyond that. It needs a longer run stride, a sprint clip, or a different drawn scale.
2. **Climb art** (reviewed from `45026b90`):
   - A gripping hand descends 0.50 per half cycle while the feet and the ladder descend 0.55.
   - The knees splay outside the rails, because the IK bends only in the screen plane.
   - Hands and feet sit in front of the torso in depth, when the ladder is behind it.
   - The turn onto the ladder flings the arms to about ±0.8 at mid-turn.
   - The face hides one frame before the sheath switches to the back view.
   - The turn off the ladder is reused at the top of a ladder.
3. **Sword draw.** The draw starts at the front hip, not from the sheath on the back.
4. **Knees on ladders and turns** need a depth-aware bend or leg foreshortening. That is an engine decision, not just keys.
5. **Moves outside the spec's scope.** Punch, kick and wall melee reuse the slash.

**Next up.**
- Merge this branch to `main`.
- Fix the climb keys and settle the run stride question.
- Then phase 4:
  - an Entity workspace, since entities are still hand-edited JSON;
  - presets, entity duplication and motion-set editing;
  - the masked upper action inside entities, with blend in and out.
- Then move the player's gameplay timing off `entities/player.json` and convert the remaining enemies.

## Implementation sequence and acceptance gates

### 1. Prove shared motion before replacing the UI

Define the minimal base/variant/animation schema and resolver. Reproduce current
walk/run in the proposed channel representation. Render standard, tall/thin and
short/broad variants side by side using the existing preview harness.

Accept when reference proportions preserve the approved motion, adjusted variants
have stable reachable contacts and expected segment lengths, and loop seams retain
continuous travel. Changing one clip updates all three previews without copied keys.
Review at intended game scale. This is the schema checkpoint.

**Progress (2026-09-25).** Implemented in `App2d.Core/Characters/Authored/`: `CharacterModel`, `ModelVariant`,
`ResolvedModel`, `MotionClip`, `PoseEvaluator`, `AuthoredCatalog`, plus `PersonTemplate` and `PersonBuild`. Assets
live in `Assets/Characters/authored/`; regenerate them from the prototype studies with
`App2d.CharacterStudio --convert-studies Assets/Characters/authored`. Render the comparison with
`App2d.CharacterStudio --smoke-motion <dir>`. `App2d.Tests.Authored` verifies exact reproduction against the prototype
(≤1e-4, except at contact-finish instants, where the half-open runtime rule deliberately releases one instant earlier
than the prototype's closed intervals), preserved limb lengths and joint angles, planted contacts, stride scaled by leg
length and continuous seams on all three builds. Visual review: pending the reviewer's verdict on the rendered frames.
Not yet covered: face/appearance channels (the run study's determined face is not carried over), rotation keys in
authored clips (supported and unit-tested, unused by the converted studies), and build values beyond the Person template.

### 2. Deliver the Model and Animate editor

Build one document session, browser, viewport and inspector. Implement new base,
variant overrides/reset, contextual shape/rig tools, animation editing and compare.
Bring walk/run studies in as named assets. Integrate gameplay-overridable face input.

Accept when a user can make/save/reopen a named tall variant, change its build while
walking, edit a shared clip, undo one drag and see dependent previews update. Also
create and animate a headless three-legged model from Empty, without anatomy branches.

**Progress (2026-09-25).** Run the editor with `App2d.CharacterStudio --editor`; `--smoke-editor <dir>` walks the
acceptance path on a scratch copy of the assets and writes a frame per step, plus `editor-smoke.txt`. Layers:

| Layer | Where | Holds |
| --- | --- | --- |
| Schema | `App2d.Core/Characters/Authored/` | Variants store build values (`build`), applied by an explicit `IBuildRule` (`PersonBuild.Rule`) before rest overrides, so base edits propagate. Clips gain per-key `ease`, `markers` and `faces`. Parts gain `hidden`. `PartGeometry` is the one part layout used by drawing and picking. |
| Authoring ops | `ClipAuthoring`, `ModelAuthoring` | Graphics-free edits. `ClipAuthoring.Pose` inverts the evaluator: a dragged world position becomes a delta in the channel's frame and reference units, so a key made on a tall preview reads correctly on every build. Structural edits refuse to discard dependents. |
| Documents / session | `App2d.Core/Characters/Editing/` | `AssetDocument` (transaction undo: a drag is one step, serialized only at edit boundaries), `AuthoringWorkspace` (every asset as a document, resolution over drafts cached on versions, dependents, save), `Transport`, `EditorSession` (all commands). No ImGui. |
| Views | `App2d.CharacterStudio/Editor/` | `EditorShell` layout, `Viewport` (camera, compare, game-size inset), `AssetBrowser`, and `IWorkspaceView` implementations `ModelView` and `AnimateView`. |

Structure revisions are settled at save: saving a model whose structure signature changed raises its revision and moves
each dependent clip that still validates structurally; the others are reported for repair, never edited. In the editor, drafts are checked
structurally (`Validate(model, exactRevision: false)`); files on disk still require an exact match.

`App2d.Tests.Authored.EditorSessionTests` covers the gate through the same session the UI drives: tall variant
save/reopen, build change while walking, a shared-clip drag as one undo step reaching every compare pin, autokey-off
pending poses, variants never editing their base, a headless tripod from Empty, and structure-revision handling.

Extension points for later phases:
- **Entity workspace:** add `Workspace.Entity`, an `IWorkspaceView`, and an `AssetKind` + `AssetDocuments.Of` overload. The shell, browser, undo and save work unchanged.
- **Build values for new templates:** implement `IBuildRule` and register it in `BuildRules`.
- **Motion sets, sockets, hurt layouts:** new fields on `CharacterModel`, with editing in `ModelView`'s inspector. Resolution stays in `ResolvedModel`.
- **Composition layers:** the evaluator already takes `PoseInput`; masked layers belong there, not in the views.

Not yet covered: separate channel rows per track kind in the timeline, dragging contact edges, rotation handles in the
viewport (rotation is edited numerically), control labels separate from IDs, reload-from-disk conflict handling,
deleting assets from the browser, and imported "Sources".

### 3. Make one new entity playable end to end

Integrate motion sets, explicit action capabilities, generic movement/hurt/attack
geometry, sound markers and one-handed props. Adapt both the arena and an actual
game consumer to the same final-pose path. Implement flat-ground travel matching.

Accept with a spear guard that cannot jump, a player using the same Person base that
can, and the non-person creature. Show thrust anticipation, active hit geometry and
recovery; verify the spear grip/tip agree with collision in both facing directions.
Changing face expression must not affect locomotion. Validate marker crossings,
hit deduplication and contact resets at starts/stops and interruptions.

**Progress (2026-09-25).** Starter content (`StarterContent`, written by `--convert-studies`) adds Person idle, spear
thrust and jump clips, a `spear` prop, a three-legged `stalker` model built from Empty with its own clips, and three
entities: `spear-guard` (tall-thin, Deliberate set, walker, cannot jump), `player` (Person, Standard set, platformer,
can jump) and `stalker-pest`.

| Layer | Where | Holds |
| --- | --- | --- |
| Schema | `App2d.Core/Characters/Authored/` | Models gain `sockets` (control + offset, oriented by a frame control's rotation), `motionSets` and `hurtLayouts`. None of them change structure revision. New `props/` (`PropAsset`: art, grip, tip, second grip, muzzle) and `entities/` (`EntityAsset`). |
| Compile | `ResolvedEntity` | Effective roles (entity override, then selected set, with the source recorded), enabled actions with marker times in seconds, equipment, and hurt regions with per-region overrides. Every missing model, set, clip, socket, prop or marker is an error naming the field. |
| Capabilities | `EntityControllers` | `walker`, `platformer` and `stationary` declare their required roles and supported actions, replacing the universal required-action list. An entity lists its actions explicitly; unsupported ones fail validation and are rejected at runtime, never replaced by idle. A jump needs a `launch` event. |
| Runtime | `EntityAnimator`, `EntityCollision` | In-place sampling (`PoseInput.InPlace`), gait phase from ground distance over the resolved stride, world contact anchors held from touchdown to release and reset on role, action and facing changes and on `Reset`. Markers and action events are dispatched once when time crosses them, including skipped spans and loop wraps. `HitLedger` deduplicates hits per attack. Movement, hurt and attack regions and prop points all come from one `ActorPose`. |
| Arena | `App2d.Gameplay/Entities/AuthoredArena` | Fixed-step flat ground. Controllers own travel; the jump leaves the ground on its `launch` event. Hits interrupt attacks. |
| Game | `AuthoredEntityEnemy2D` | Shieldback and green-dinosaur placements spawn `spear-guard` and `stalker-pest` when `SideScrollerSessionDefinition2D.AuthoredCharacters` is set (the game sets it and refuses to start on authored errors). `EnemyState2D.AuthoredPose` carries the tick's pose object, and `AuthoredCharacterShader` draws it without re-sampling. Rollback snapshots the animator. |
| Editor | `Editor/ArenaTest` | **Test** plays the arena from `AuthoringWorkspace.SnapshotEntities()`: entities compiled against copies of the current drafts. |

Verification:
- `App2d.Tests.Authored.EntityRuntimeTests` covers capabilities, set/override sources, error messages, spear grip and tip against the hit region in both facings, mirroring, marker dispatch (skipped spans, loop wraps, no advance means no event), planted feet holding their anchors on all three entities, contact resets, face independence and capture/restore.
- `App2d.Gameplay.Tests` `AuthoredArenaTests` covers the guard that cannot jump, the player's jump, hits inside the strike→recover window, the creature, interruptions and deterministic replay.
- `AuthoredEntityEnemyTests` covers game spawning, the shared pose, damage to the player, rollback replay, pose-derived head hits and spear hitboxes in both facings.
- Renders: `App2d.CharacterStudio --smoke-entities <dir>` draws anticipation, active and recovery frames in both facings, the jump and the stalker, with overlays. `App2d --render-smoke <dir>` writes `entity-*.png` through the game presentation. `--smoke-editor` ends in Test.

Not yet covered:
- An Entity workspace: entities are edited as JSON; Test plays them.
- The in-game player: since 2026-09-26 it is drawn from authored assets (see Where we are). Its gameplay is still the traversal `Person2D` with legacy timing, and its hurt region is still the movement box. The authored jumping `player` entity is proven only in the arena.
- Prop orientation tracks and an adapter for the existing `WeaponDrawing` art. The player move set brings new sword, sheath and pistol art instead.
- ~~The game's blended `FacePose`~~: done 2026-09-26; `PuppetDrawing` takes a gameplay face.
- Hurt regions from shape geometry: they are padded control bounds.
- Interpolated display poses between ticks.
- Terrain beyond flat ground: airborne authored enemies hold their pose.

### 4. Complete the repeated-authoring workflow

Add build/look presets, entity duplication, motion-set editing, explicit role
overrides and dependency navigation. Add one masked upper action over locomotion.

Accept by authoring several visibly different people, assigning Standard/Heavy
motion independently of size, and changing a shared walk once. Verify an upper
attack does not steal leg contacts. Load a representative 50-variant catalog to
check browsing and that clips are shared rather than duplicated in memory/files.

**Progress (2026-09-26).** The masked layer exists in the evaluator (`PoseInput.Overlay`: one override clip owns the
channels in its mask, and base contacts on masked chains are skipped), and the in-game player uses it for gun shots over
locomotion. Not yet: blend in and out, entity actions that declare a mask, and the upper-attack-keeps-leg-contacts gate.

### 5. Cut over and retire prototype screens

Add explicit `.puppet.json` conversion and selective imported-motion conversion.
Preserve original files and source metadata. Convert the chosen game entities,
compare their gameplay behavior, and make the replacement the default studio.
Remove the old edit workflows after their needed functions are covered; retain
source preview only where it supports importing and comparison.

**Progress (2026-09-26).** The in-game player's drawing is cut over: `AuthoredPersonPresentation2D` replaced
`PointPersonPresentation2D`, which is deleted. Still on legacy paths:
- the player's gameplay timing and collider size (`entities/player.json`);
- the maul, cinder and needle enemies;
- the studio's source, entity and workshop screens.

Final acceptance: from a clean checkout, create a model, animate it, make a variant,
make an entity, save, reopen and use it in the game without a 3D import dependency.
Exercise existing regression suites plus targeted new math/runtime tests and real
rendered smoke cases. Do not delete old paths just because the new UI can draw a pose.

## Explicitly deferred

Ragdolls; general 3D skeletal editing; arbitrary node-based animation/behavior
graphs; deep inheritance; procedural texture/stroke styling; automatic retargeting
across unrelated skeletons; nested/coupled IK chains; two-handed grip solving;
terrain-edge balance; live mutation of an active game session; integrated AI chat.

The first implementation target is shared walk/run on three builds. That proves
the central promise before investing in the rest of the editor.
