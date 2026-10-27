# Character Studio

Library parts now use the same geometry inspector as model artwork, including mesh placement, fit, layer, material, physical Z and thickness. The browser distinguishes **Part**, **Module**, and named **Study** entries. Whole rig modules open alone; **Preview on wearer (temporary)** places one on the selected wearer for fitting. **Attach** creates the saved instance. Standalone previews use the part's animation clock without changing the wearer or its documents.

Clothing and weapons are now ordinary entries under **Parts**, with the same geometry editor, arbitrary XYZ locations, pivot, size, material and OBJ controls. Weapon tilt/twist remains available in Animate. **Attach** writes a model instance; **Equip on an entity** writes a gameplay equipment binding. Saved meshes keep editable outlines, and undo retains their rebuilt geometry.

For the remaining consolidation of passive physics, Behavior and texture-frame animation, see [Part consolidation plan](part-consolidation-plan.md).

The proposed staged simplification of rigs, parts, variants, animation, collision and Studio is documented in [Character system and Studio migration plan](character-system-migration-plan.md). The workflow below describes the current implementation.

The studio is the character editor: one window, one viewport and one document workflow over the authored assets in
`Assets/Characters/authored`, which the game plays. Its design and history are in
[Character editor replacement](character-editor-replacement.md).

The reusable resource boundaries and staged migration are described in [Character resources and migration](character-resources.md).
Model templates come from `Assets/Characters/authored/templates/*.json` and from nowhere else. The New menu discovers their names and copies
the referenced model and animations into independent drafts. Add a recipe and its source assets, then reopen Studio;
no C# menu entry is needed. Person includes seven starter clips, Quadruped and Triceratops eight each, Pterosaur nine and Stalker five.
A published template (version 2) shows its contract in the create dialog: the part slots with their usage and default, the
dimensions with a **Start from preset** choice (the preset's values become the new model's parameter defaults), and the
default animation library (roles, masks, measure mappings); a contract that does not hold on the source model is listed with
its reference paths and the creation is refused. Use **Save all** to save the created bundle. Existing model, clip and entity files are unaffected by template creation.


## Run

```powershell
dotnet run --project App2d.CharacterStudio
```

Requires Windows and .NET 10. ImGui.NET 1.91.6.1 is pinned; MonoGame WindowsDX is the same version as the game. Startup
exceptions are written to `character-studio-error.log` beside the executable.

The transport's **Preview end** menu follows the clip's loop setting by default. Hold stops on the final pose,
Loop wraps to the beginning, and Ping pong plays to the end and back. A ping pong trip from start to end takes
one clip duration. Preview speed and end mode are session settings; they do not change the saved clip.

## Rounded drawing paths

Person arms and legs use the shared `rounded-polyline` curve with 20 base sections and softness 1.
Each curve point pins to an existing pose control through the part's `bindings`; drawing paths do not
create or solve IK chains. The same path can use more points for a tail. In Model, select a limb to edit
its ordered controls, insert/remove points, or change **Bend softness** and **Curve sections**. An ordinary
stroke can become a path with **Make rounded control path**. These edits use normal undo, save and reopen.

The curve library joins straight spans with quadratic Bezier fillets, limiting each corner's trim to
48% of its shorter neighboring span. It samples exact fillet boundaries in addition to the base sections;
section count is not an exact vertex count. Pose depth uses the same interpolation weights as XY.
An intentional reverse turn must come from the controls; rounding adds no wobble. The Person torso and
animation controls are unchanged. Torso deformation remains future work.

`RoundedPolylineCurve2D` and its typed definition also work without characters. In a character part,
unbound points retain the usual normalized A-to-B stroke frame, while bound points use their named
control's XYZ position (curve bindings cannot use `toward`). Existing cutout bindings retain their
rest-offset behavior. Drawing, selection and bounds share `PartGeometry.Contour`.

Run `dotnet run --project App2d.CharacterStudio -- --smoke-rounded-paths artifacts/rounded-paths`
for 24 before/after frames of walk, run, landing and sword motion through the game renderer, with close-up
and game-scale figures. The report includes triangle counts.

## Character layers

Model controls, variant rest positions, appearance attachment origins, and animation translations are edited as
screen-plane X/Y plus a **character layer**. Front, Middle, and Back are quick order presets. The numeric order lets
shapes fit between other shapes or move between layers during an animation; smaller values draw closer to the viewer.
Each shape has a layer offset from its attached control. The existing `z` and `depth` fields in saved assets keep their
exact values, so opening and saving an older character does not rearrange its artwork.

These layers order parts **within one character**. The game's scene `ZIndex` still orders the character as one world
object. Solid weapon meshes and their socket orientation continue to use three-dimensional coordinates.

## Weapons and 3D rotation

Open **Sword backhand** (`player-sword-backhand`) in **Animate**. Under **Attachment preview**, choose Sword and the
`sword-hand` socket, then **Edit attachment rotation**. Opening a prop in the browser also opens these controls. An entity
action's **Animate this action with weapon** button selects its clip and equipment together.

- **Turn** aims in the screen plane, **Tilt** points toward/away from the camera, and **Twist** rolls the broad face onto
  its edge. These are offsets from the socket's existing frame; changing them does not move the arm.
- Drag the gold handle around the grip to turn, the blue handle up/down to tilt, or the green handle left/right to twist.
  Shift slows adjustment. Numeric degree fields and Broadside / Edge-on / Reverse face presets are also available.
- Autokey writes at the playhead. With autokey off, **Key pose** keeps the previewed pose; seeking discards it. The
  **Weapon rotation** timeline row supports moving, copying, easing and deleting keys. Key the starting pose before
  adding later poses. Angles are unwrapped: 0 to 360 makes a full turn rather than taking a shortest path.
- Preview equipment is independent of entity equipment. Pick the same socket the entity equips. **Save animation**
  saves motion; **Save weapon** saves the shared art asset. Ctrl+Z/Redo includes both while the weapon inspector is open.

Expand **Weapon asset / import OBJ** for size, grip, tip, muzzle, ink width and solid colors. **Replace geometry from
OBJ** imports a triangulated mesh into the current prop document as an undoable edit. Export applied transforms with
+X along the blade/barrel, +Y across the broad face, Z for thickness, and outward face winding. Pick import scale to
convert into character model units. UVs, normals and materials are ignored; assign flat color in the editor. The imported
mesh is stored in the prop JSON, with no runtime dependency on the OBJ or modeling application. Check grip, tip and
muzzle after import; they remain explicit markers. Asset changes affect every entity using the prop.

The starter sword, sheath, pistol, spear and hammer are simple closed meshes. Sword attacks, sheathing, gun poses and
the hammer slam have initial orientation keys for experimentation. These are rough starting performances, not an
automatic conversion of the old imported weapon motion. Characters keep their existing XY rig and character layers.

Rendering uses the same orthographic depth buffer as characters, with flat facet colors and visible silhouette/crease
ink on solid props. Coplanar triangulation edges are suppressed. Legacy stroke/polygon props still load. Socket
orientation is evaluated before both drawing and gameplay point projection; gameplay collision remains XY.

## Quadrupeds and Triceratops

Use **New > Model: Quadruped template** for a simple four-legged creature, or **Model: Triceratops template**
for the concept-art starter: mint body, broad scalloped frill, three horns, tail, dark spots and chunky clawed feet.
Both create an independent base and eight unsaved animations with IDs prefixed by the model ID. Use **Save all**
to keep the model and clips together. The shipped `quadruped` and `triceratops` assets are also available in the browser.

Each has four two-bone IK chains with locomotion-frame foot targets, a body/root, and separate head and tail controls.
In **Model > Edit rig**, move the rest controls to change anatomy. In **Animate**, drag feet to key their targets;
rotate the head or tail to pose their attached artwork. Walk/run/rush have travel and planted contact intervals.
The clips are idle, walk, run, scrape, head-down, rush, brake and recover. These are editable animation studies;
charge AI, damage, dust effects and a view-changing slow turn are separate gameplay/artwork work.

Drawing parts now include **polygon** cutouts. Select a cutout in Model, expand **Edit cutout silhouette**, and enable
**Drag silhouette points in viewport** to drag its gold handles. Coordinates are relative to the part's width/height;
insert or remove perimeter points in the inspector. Self-crossing/degenerate outlines retain the last valid shape.
Cutouts attach to a control and use **Point toward** for orientation, just like other shapes. Concave silhouettes
are triangulated for fill and picking respects their actual perimeter. **Surface paint** reuses clothing's normalized
color patches, clipped to the outline. Variants can resize/recolor/hide these parts; perimeter edits belong to the base.
No texture or deformable mesh is needed for this starter.

```powershell
dotnet run --project App2d.CharacterStudio -- --smoke-quadrupeds artifacts/quadrupeds
```

This renders pose samples and both facings through the game drawing path. `--write-quadrupeds <authored-dir>`
explicitly regenerates the two starter models and their clips, overwriting edits to those files.

## Pterosaur and attached cutout points

**New > Model: Pterosaur template** creates a winged starter and nine unsaved clips: hover, fold, dive, brake,
low-recovery, reset, swoop, hit and death. **Save all** keeps the bundle. The shipped `pterosaur` model is also in the browser.
Both membranes use normal shoulder/bend/tip controls; the lower beak uses a jaw control. In Animate, pose these controls
with the ordinary handles and rotation channels. The fold clip has `screech` and `commit` markers for later action bindings.
Flight paths, target commitment, hit windows and the low-pass gameplay guarantee belong to Entity/controller work.

In a base model's **Edit cutout silhouette**, each point has **Follow** and optional **Point toward** choices. The default
follows the whole part. Selecting a control makes that point follow its frame, preserving its drawn rest position;
without Point toward it follows translation only. Numeric coordinates describe the rest drawing. Gold handles work
while previewing a pose by inversing the same transform. Insert/remove keeps attachments aligned with their points.
Detach points before deleting their controls or changing the cutout into a primitive. Variants inherit the attachments.

Surface paint follows membrane deformation through the same rest triangles as the fill. Neither paint nor picking
retriangulates the folded screen silhouette. Ordinary rigid cutouts and clothing retain their existing authoring path.
The design boundary and acceptance criteria are recorded in [Creature-driven editor design](character-editor-replacement.md#creature-driven-editor-design-2026-10-01).

```powershell
dotnet run --project App2d.CharacterStudio -- --smoke-pterosaur artifacts/pterosaur-review
```

This renders all six poses and both facings. `--write-pterosaur <authored-dir>` explicitly regenerates the starter model
and clips, overwriting edits to those files.

## Human proportions

Humans use legs at 80% of the original study length. This is baked into the shared Person rest pose;
the tall, broad, brute and cinder builds retain their relative proportions on that base. Head, torso
and arm dimensions are unchanged. Walk, run and heavy walk retain their original reference measures,
so stride, foot lift and pelvis motion retarget to the shorter legs; distance-based playback follows
the shorter stride. `PersonTemplate.StudyReference()` preserves the source proportions for converting
and regenerating the original motion studies. The player's grid-aligned standing collider is 52 px
(previously 56 px), and the authored human movement boxes and Hero sword-hit rectangles follow the lower stance.

## Hair and clothing

The Hero wears short tousled hair. Maul brute is bare-chested with an ochre hide skirt; Cinder gunner
wears a russet one-shoulder tunic and hide wrap. Both enemies keep wild hair and caveman beards.
The tunic is the torso's fill plus normalized `paint`
patches for the spots and exposed shoulder. Paint follows the torso dimensions and frame and is clipped
to its contour. Head and torso inherit the same thick outline as the rest of the character, and
the hair and clothing use matching ink. Fabric spots have no separate border.
Hair, beards and wraps are editable prop assets equipped on `head-art` and `body-art`.
`PersonWardrobeDepths` derives named depth slots from each build's rest pose. From far to near:
far arm, wrap back, far leg, torso, near leg, wrap front, belt/pattern details, near arm.
The belt and pattern have their own thickness budget inside the leg-to-arm gap, including clearance
for their outlines. This avoids belt details cutting across the near forearm on narrower builds.
The wrap encloses both hip strokes; animation keeps its authored XYZ rather than being flattened
onto these slots. These are indexed meshes, not animation frame images.
The body socket's `toward: "chest"` follows the evaluated hips-to-chest direction. The head socket uses
`frame: "locomotion"` to match the current screen-facing head drawing. The player's runtime loadout
keeps hair when unarmed and selects its back silhouette using the existing view markers.

Use **New > Appearance: hair / clothing** to start from hair, beard, skirt, or a blank cutout.
Right-click an existing appearance asset to duplicate it. The **Appearance** workspace previews the
selected asset on a wearer; choose the socket and drag the gold silhouette points, or edit their
coordinates. Each piece has fill, outline, depth and thickness controls. The waist depth guide shows
where the arms, legs, belt and wrap surfaces sit. **Match body ink** copies the model's stroke style.
**Equip on wearer** adds the asset; remove the old style under **Entity > Equipment** when replacing it.
**Save all** saves both artwork and equipment. Undo/redo covers both. The player reads its appearance
from the Hero's equipment, including an optional back-view asset, independently of carried weapons.

For a fitted tunic, select the torso in **Model**, set its fill, then use **Fabric paint > Edit fabric paint**
to add colored patches and edit their normalized polygon points. Save a look on the base to reuse it
on other variants. Cutout outlines are saved as editable source; their meshes are rebuilt when loaded.
When launched from the repository, the editor saves into source `Assets/Characters/authored`, rather
than the build output copy. Rebuild/restart the game to pick up saved asset changes.

`--write-wardrobe <authored-dir>` seeds missing wardrobe art and updates its sockets, equipment bindings,
and the two enemies' torso paint and outline widths. Existing prop JSON remains editor-owned, including when
`--write-player-moves` runs this step. `--replace-wardrobe-art <authored-dir>` explicitly regenerates that art.
`--smoke-wardrobe <dir>` renders idle, run, attacks, and climbing/death samples in both
facings, including game-size views. Clothing follows the rig rigidly; this first pass has no cloth simulation.

## Workspaces

In **Model → Edit rig**, **Add root bone** creates a bone with an origin, rest angle, and length; **Add child bone** starts at the selected bone's tip. Drag the origin to move it, drag the tip to change its rest angle and length, or edit those values in the inspector. Turn off **Edit rig** and use **Add shape to selected bone** to attach library geometry in that bone's local frame. The menu and **Geometry** field use the geometry library's own names: rectangle, rounded rectangle, capsule, polygons and curves. Curves connect two controls. Shapes and curves share the same fill and outline material fields. The shape's offset and angle remain editable. **Save** writes these as controls and parts in the ordinary model JSON; **Animate** uses a `rotate` track on an unconstrained bone control. Existing point-control models and their animations continue to use the same editor.

To add bone IK, create a root, child and endpoint, select the endpoint and press **Make IK constraint**. The first two bones supply the segment lengths; the endpoint may itself have artwork and child bones. Bone children must meet their parent's +X tip. The new constraint appears under **IK constraints**, with a bend direction, target frame and reference measure. In **Animate**, drag the endpoint to key the constraint's target. Both segments turn, carrying attached shapes and descendants. The root still has translation and uniform-scale channels; its rotation belongs to IK. On a run of point controls **Make IK constraint** converts the run to bone nodes as it makes the constraint (P11c); there is no position-only solver.

Choose the **locomotion** target frame to use the existing **Plant** contact controls. Contacts pin any endpoint; they are not restricted to feet. A free control frame makes a bone target follow that frame's translation, rotation, scale and reflection. A constrained frame cannot drive another constraint. Bone length edits carry the connected IK joint to the new tip. Since P3c a control's inspector shows its declared node kind: a point control's character-frame rest, a bone node's derived origin, direction, length, local setup (rotation, uniform scale, reflect, layer offset) and link (position t along the parent, offset, orientation, bind/unbind); a bound origin refuses a drag with its reason, and **Convert to bone chain** turns a run of point controls into bone nodes without moving any rest position. Uniform scale and reflection are supported; collapsed, sheared or nonuniform segment frames produce a named diagnostic, including when encountered during animation. Floor/wall queries, self-collision and stateful physics remain later extensions. See [constraint resources and compatibility](character-resources.md#second-migration-typed-ik-and-contact-evaluation).

- **Model**: controls, IK chains, measures, drawing parts, sockets, motion sets, groups and looks on a base
  model, and its declared **Parameters** (P4a: units, default, ranges, bindings; add a parameter, bind a property of the
  selected control, part or attachment to it, remove either; a bound field says which parameter produces it), and its
  **Library parts** (P5c: the instances of shared part assets on its sockets; attach one from the library with a socket
  and an optional mirror, select an instance in the list or by clicking it in the viewport to edit its asset, socket,
  placement, size, mirror, layer, fit, fill and visibility, or remove it; **Edit shared part** opens the asset itself,
  whose edits reach every wearer). On a variant: parameter values with the base's presets, part overrides with
  **Reset to base**, looks, and per-instance attachment overrides (swap the asset, restyle, re-place, hide; click a
  marked label to clear and follow the base again). **Edit rig** shows control handles.
- **Appearance**: reusable hair, beards and clothes, editable cutout silhouettes, colors, ink, depth and thickness,
  with wearer preview and equipment binding; and the shared **library parts** (P5c: pieces of shapes, curves, images and
  solids on named locations, shared materials, the wearers that instance the part, preview on any model or variant at a
  chosen socket with mirror, **Attach to** the wearer's base, uses / used by). The browser lists library parts first
  (`[L]`), filters them with **Parts**, creates one from a shape or from a hair/clothing prop (New menu or right-click
  the prop), duplicates one and lists its wearers from the right-click menu. A part's own outline points are edited as
  coordinates in this pass; viewport dragging of library-part outlines arrives with the P7 cutout tools.
- **Model → base model → Root motion** (P10c): which instance's clips carry the model's travel (**Owner**; "this model's clips" otherwise). The attachment inspector shows each instance's **Connection** (the socket and the wearer control or the other instance's control it sits on, and the usage the socket takes) and **Owns the root motion**. In Animate, the outline's Parts rows say where each instance mounts, whether it owns the root motion and whether this clip keys it; the timeline marks the owner's play row with "(root motion)". **New → Model: Centaur template** creates the composed study.
- **Model → attachment instance of a rig module** (P10b): the inspector lists the module's inputs with this instance's values
  (drag to set, **Part default** to clear; a bound input shows its parameter) and the part's contract (connection, inputs,
  points, targets, chains, words). The Animate outline lists each instance's exposed targets as `instance/target` rows.
- **Animate**: clips compatible with the subject's base. Dragging keys the channel it drives (autokey), contacts,
  markers, part expression keys, easing and onion skin. **Compare** pins two more builds at the same phase and world scale.
- **Entity**: model and motion set with per-role overrides, controller, movement box, **Colliders** (P8c: the entity's
  attached hurt, attack, guard and trigger colliders; add one with an id and a use, select it to edit its use, binding (the
  actor, a control with frame/toward/stretch/offset/angle, a socket or an equipped prop point, along the axis), default
  enabled state and shape; removing one a window names is refused by name), **Guard** (on or off, naming one of the guard
  colliders), equipment and the selected action's clip, mask, blend, activation windows (each names its attack collider;
  adding a window needs an attack collider to enable) and events. **Health** and a window's **Damage** edit the typed
  behaviors (`behaviors.damage.health`, `behaviors.strikes.<window>`); adding a window adds its strike, removing the
  last window of a hit group removes it. The **Behaviors** section (P9c) is the typed inspector: receiving damage
  (health, mass, hurt and stagger seconds), one strike per hit group (damage, sound, impact; add one for a group striking
  with the defaults, remove one), the guard's open event and block sound, and custom properties (add, edit, remove), each
  field with its schema range and help. Since P11c an entity file is version 5 and carries nothing else: no hurt layout,
  no inline window or guard geometry, no loose combat fields, and no conversion commands. The viewport draws the colliders the game registers, placed from the preview pose: blue hurt,
  red attack (filled while its window is open, faint otherwise), green guard, orange trigger, the selected one thicker
  and labelled; the timeline labels each window with its collider.
- **Test** plays the arena from a snapshot of the current drafts; its overlays read the arena's registered colliders and
  its event log lists every accepted contact (a hit with its strike and sound, a block with the guard's sound) beside
  the animation events, with the hit, block and trigger-contact counts.

Every asset is a document with its own undo. **Save** writes the open document; **Save all** writes each dirty one in
turn and is not atomic. The browser's context menus create variants, animations and entities, duplicate them, open
bases and discard assets that were never saved.

## Imported motion and puppet files

Both conversions are explicit. They create new unsaved drafts, record where they came from in the asset's `source`,
and never change the source file.

- **New → Import .puppet.json...** converts a prototype puppet into a base model and one animation per motion. Chains
  whose ends have contacts are keyed from the ground and share one reach measure; the others are keyed from their root.
- **Sources** lists the imported libraries (`Assets/Characters/catalog.json`). Choosing a clip opens a dialog: the base
  model to convert onto, a reference clip whose first frame stands for the model's rest (the library's `idle` by
  default), and the mapping from each control to source points. For the Person template the default mapping pairs limbs
  by depth: the export's left side is the far side, which the template calls right.

A library clip is transferred as offsets from the reference frame, scaled by each measure, so the model keeps its own
proportions. IK joints are solved with the model's lengths, and any reach shortfall is shown in the viewport. The
converted clip is sampled at 30 fps and has no contacts or travel: plant the feet, set travel and check depth in
Animate. **Show source points** overlays the mapped source at the same time for comparison.

The conversion code is `App2d.Core/Characters/Authored/SourceImport.cs` (`PuppetImport`, `LibraryImport`,
`SourceLibraries`).

## Headless renders

| Command | Writes |
| --- | --- |
| `--smoke-editor <dir>` | The acceptance walk through the editor on a scratch copy of the assets: a frame per step and `editor-smoke.txt`. |
| `--smoke-motion <dir>` | Shared walk, run and heavy walk on three Person builds, with `motion-proof.txt`. |
| `--smoke-entities <dir>` | Arena frames with collision overlays, with `entity-proof.txt`. |
| `--review-moves <dir>` | Player move set frames, manifest and report for `tools/MoveReview`. |
| `--smoke-weapons <dir>` | A five-weapon turntable and sword swing in both facings. |

`--convert-studies <authored-dir>` regenerates the Person model, walk, run and starter content from the prototype
studies; `--write-player-moves <authored-dir>` regenerates the player move set.
`--write-weapons <authored-dir>` replaces the five starter weapon assets and their sample orientation tracks, keeping
body keys and entity settings. It is a regeneration command and overwrites edits to those weapon assets/tracks.

The game has two more: `dotnet run --project App2d.CharacterSeeds -- --render-smoke <dir>` draws authored entities through the game
presentation, and `-- --face-smoke <dir>` draws every expression the baked Person face part binds.

## Code

| Where | Holds |
| --- | --- |
| `App2d.Core/Characters/Authored` | Schema, resolution, pose evaluation, entity runtime and conversions. No graphics. |
| `App2d.Core/Characters/Editing` | Documents, workspace, transport and the editor session. No ImGui. |
| `App2d.Rendering/Characters` | `PuppetDrawing`, attached part instances (faces among them) and depth-tested submission. |
| `App2d.CharacterStudio/Editor` | The ImGui shell and workspace views. |
| `App2d.CharacterStudio/Proof.*.cs` | The headless proof renders. |

## Refreshing imported libraries

The libraries are read-only sources. After exporting changes in the sibling `sprite-renderer` checkout:

```powershell
node tools/CharacterPipeline/import.cjs ../sprite-renderer
node tools/CharacterPipeline/import.cjs ../sprite-renderer --person-only
```

The importer checks packed-data hashes and preserves source metadata and licenses. Quadruped includes nine
[mapped Tomek wolf motions](wolf-animation-mapping.md). Already converted clips keep their keys; convert again to pick up
a changed source.
