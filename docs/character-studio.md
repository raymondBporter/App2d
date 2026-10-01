# Character Studio

The studio is the character editor: one window, one viewport and one document workflow over the authored assets in
`Assets/Characters/authored`, which the game plays. Its design and history are in
[Character editor replacement](character-editor-replacement.md).

## Run

```powershell
dotnet run --project App2d.CharacterStudio
```

Requires Windows and .NET 10. ImGui.NET 1.91.6.1 is pinned; MonoGame WindowsDX is the same version as the game. Startup
exceptions are written to `character-studio-error.log` beside the executable.

The transport's **Preview end** menu follows the clip's loop setting by default. Hold stops on the final pose,
Loop wraps to the beginning, and Ping pong plays to the end and back. A ping pong trip from start to end takes
one clip duration. Preview speed and end mode are session settings; they do not change the saved clip.

## Weapons and 3D rotation

Open **Sword backhand** (`player-sword-backhand`) in **Animate**. Under **Weapon preview**, choose Sword and the
`sword-hand` socket, then **Edit weapon rotation**. Opening a prop in the browser also opens these controls. An entity
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
automatic conversion of the old imported weapon motion. Characters keep their existing XY rig and depth controls.

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

In **Model → Edit rig**, **Add root bone** creates a bone with an origin, rest angle, and length; **Add child bone** starts at the selected bone's tip. Drag the origin to move it, drag the tip to change its rest angle and length, or edit those values in the inspector. Turn off **Edit rig** and use **Add shape to selected bone** to attach an ellipse, box, capsule, or polygon in that bone's local frame. The shape's offset and angle remain editable. **Save** writes these as controls and parts in the ordinary model JSON; **Animate** uses a `rotate` track on the bone control. Existing point-control models and their animations continue to use the same editor. The existing two-bone IK tool still applies to point controls; bone-frame IK is a separate constraint step.

- **Model**: controls, IK chains, measures, drawing parts, sockets, motion sets, hurt layouts, groups and looks on a base
  model. On a variant: build values, part overrides with **Reset to base**, and looks. **Edit rig** shows control handles.
- **Appearance**: reusable hair, beards and clothes, editable cutout silhouettes, colors, ink, depth and thickness,
  with wearer preview and equipment binding.
- **Animate**: clips compatible with the subject's base. Dragging keys the channel it drives (autokey), contacts,
  markers, face keys, easing and onion skin. **Compare** pins two more builds at the same phase and world scale.
- **Entity**: model and motion set with per-role overrides, controller, movement box, hurt regions, equipment and the
  selected action's clip, mask, blend, hit windows and events.
- **Test** plays the arena from a snapshot of the current drafts.

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

The game has two more: `dotnet run --project App2d -- --render-smoke <dir>` draws authored entities through the game
presentation, and `-- --face-smoke <dir>` draws every expression on the Person.

## Code

| Where | Holds |
| --- | --- |
| `App2d.Core/Characters/Authored` | Schema, resolution, pose evaluation, entity runtime and conversions. No graphics. |
| `App2d.Core/Characters/Editing` | Documents, workspace, transport and the editor session. No ImGui. |
| `App2d.Rendering/Characters` | `PuppetDrawing`, faces and depth-tested submission. |
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
