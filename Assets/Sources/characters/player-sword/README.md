# Editable sword character and compact balance exports

`sword-balance.blend` is the editable source for two small-correction animations.
Each has eight unique poses, held for 0.25 seconds: two seconds per loop. Both
face right and hold the sword in the original hand. Names refer to screen position
in this canonical facing:

| Action / asset ID | Planted foot | Hanging foot | Platform extends |
| --- | --- | --- | --- |
| `balance-left-foot` | Screen left, rig `footR` | Screen right | To the left |
| `balance-right-foot` | Screen right, rig `footL` | Screen left | To the right |

These are separately posed actions, not horizontally flipped copies. A future
left-facing game presentation can flip the whole image; that also flips the
screen-space meaning of the name. The game has the imported clips, but automatic
ledge detection and selection of either clip are not implemented by this work.

The original RGS Dev drawings and original animation actions remain in this file.
Their CC0 license/provenance is under
`Assets/Sources/third-party/rgs-stick-figure`. The downloaded original is unchanged.

To change a motion, open this Blender file and select `balance-left-foot` or
`balance-right-foot` in the Action Editor. Edit frames 1, 4, 7, 10, 13, 16, 19, and
22. Frame 25 repeats frame 1 to close the loop. Playback ends at frame 24, at
12 fps. Constant interpolation displays each pose for three Blender frames,
matching the exported four frames per second. Keep the support bone fixed and
copy any change to the opening pose to frame 25. Save the file normally.

From the repository root, render the saved file and rebuild runtime assets:

```powershell
& .\tools\ArtPipeline\render_sword_balance.ps1 -BuildRuntime
```

Pass `-Blender 'path\to\blender.exe'` for another installation. Without
`-BuildRuntime`, the command only refreshes the cached PNG export. The normal
`python tools/ArtPipeline/build_runtime_assets.py` command also imports the cached
frames, without launching Blender. It rejects a cache if the saved `.blend`,
recipe, or any frame has changed since the export.

The pipeline has three stages:

1. **Author:** `sword-balance.blend` contains the drawings, bones, and editable
   actions. `render.json` records the action names, sampled frames, timing, and
   camera/color settings. Both are durable sources.
2. **Render:** `render_blender_character.py` opens that saved file, applies the
   recorded source camera, and writes 512×512 transparent PNGs to `rendered/`.
   It checks the supporting bone and loop closure, and also renders three
   original poses as calibration references. It does not recreate poses or
   save over the `.blend`. Keep cached exports with the source so builds can run
   without Blender.
3. **Import:** `import_blender_character.py` runs after the pre-rendered pack
   importer. It uses that importer's existing `transform_frame`: 1.35× scale,
   source root `(256,380)`, target root `(256,325)`, on a fixed 512×512 canvas.
   It adds both animation definitions and frames to `Assets/Runtime`, which
   remains disposable build output. The balance foot contacts land at row 324,
   immediately above the existing anchor at row 325.

The recipe can also export edited original actions. For example, an `idle` entry
using action `idleSword`, frames 1–8, `loop: true`, and
`durationSeconds: 0.6666666667` would deliberately replace the pre-rendered idle
at import time. `supportBone` and `loopClosingFrame` are optional checks for clips
that require them; omit them for ordinary moving actions. An idle replacement
also causes player geometry to be measured again. Never crop or individually
center frames: that would break the shared anchor.

`create_sword_balance.py` is the one-time pose authoring recipe used to create
this source. It refuses to replace an existing file without `--replace`.
**Do not run it for ordinary re-renders:** regenerating initial poses discards
manual pose changes. Use `render_sword_balance.ps1` instead.

The first, longer animation experiment remains in `Assets/Library`; neither its
72-frame version nor its windmill animation enters the runtime pipeline.

## What the original pack provides

The downloaded pack has no rendering README and the `.blend` has no embedded
instruction text or export script. The author confirms on the
[asset page](https://rgsdev.itch.io/animated-stick-figure-character-2d-free-cc0)
that it was made in Blender Grease Pencil, and supplies the `.blend` alongside
the PNG archive. The saved project itself provides the useful export recipe:

- File version: Blender 4.1.24; inspected and exported here with Blender 5.2.1.
- Eevee, 512×512 at 100%, transparent film, 8-bit RGBA PNG.
- Camera: perspective, 50 mm lens, 36 mm sensor, at `(0,-10,0)`, facing the X/Z
  drawing plane. Standard view transform, no look, exposure 0, gamma 1, sRGB.
- Scene playback: 12 fps. The game currently assigns its own clip timings.
- Nonlinear Animation tracks `allAnimsSword`, `allAnimsPistol`, and
  `allAnimsFighter` arrange the actions into export strips. Their timeline frame
  numbers match the numbers in the supplied sprite filenames.

For example, the sword timeline contains idle at 1–8, walk at 9–16, run at 17–24,
slide at 25–32, dash at 33–38, climb at 39–42, jump at 43–47, hit at 48–51,
death at 52–61, air attack at 62–64, three sword attacks at 65–75, and wall slide
at 76–77. The saved active timeline/action is not a reliable choice for a new
export, so the renderer explicitly selects each requested action and mutes NLA.

The 5.2 conversion initially double-deformed the face: its layer already
followed a bone and the armature modifier deformed it again. This source keeps
bone-parented face/weapon layers in a separate Grease Pencil object without that
modifier, leaving the limb rig intact. One obsolete expression-offset driver
was removed. No additional drawing plugin was needed for the tested exports.

Re-rendered idle, run, and jump references match the supplied PNG bounds exactly.
Their thresholded alpha masks overlap by 99.88%, 100%, and 99.61%, respectively.
Small antialiasing/overlap differences remain, so this is a closely matching
export path rather than a byte-for-byte recreation of every original frame.
The three samples do not establish fidelity for every action in the pack.

Run `python tools/ArtPipeline/preview_sword_balance.py` to regenerate the comparison
GIF and pixel checks under `Assets/Library/characters/player-sword/balance-compact`.
