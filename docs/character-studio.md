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

`--convert-studies <authored-dir>` regenerates the Person model, walk, run and starter content from the prototype
studies; `--write-player-moves <authored-dir>` regenerates the player move set.

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
