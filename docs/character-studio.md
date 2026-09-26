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

## Weapons and 3D rotation

Open **Sword rising slash** (`player-sword-slash`) in **Animate**. Under **Weapon preview**, choose Sword and the
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

## Workspaces

- **Model**: controls, IK chains, measures, drawing parts, sockets, motion sets, hurt layouts, groups and looks on a base
  model. On a variant: build values, part overrides with **Reset to base**, and looks. **Edit rig** shows control handles.
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
