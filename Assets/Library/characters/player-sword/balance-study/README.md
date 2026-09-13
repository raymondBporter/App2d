# Sword balance animation experiment

Three editable Blender actions using the original RGS Dev CC0 character drawings:

- `Study - standing`: a still sword stance for comparison.
- `Study - balance-subtle`: three seconds of gentle teetering, with the free arm,
  head, sword wrist, and hanging leg making small corrections.
- `Study - balance-windmill`: three seconds of larger leaning, overshoot, and two
  forearm circles beside the silhouette. An overhead arm circle was obscured by
  this character's large head.

Open `sword-balance-study.blend` in Blender 5.2. The subtle action is selected;
play frames 1–72 at 24 fps. Select the other `Study - ...` actions in the Action
Editor. Frame 73 repeats the starting pose and is excluded from rendered loops.
`comparison.gif` shows the reference and both motions side by side. Individual
GIFs and transparent 512 × 512 PNG sequences are also included.

This is an animation-quality experiment. The platform in the preview is a visual
reference, with its edge touching the planted foot. Game triggers, runtime scale,
collision geometry, transitions from idle, and left-facing variants are not yet
implemented. The camera was reframed for reviewing the drawing; runtime assets
and gameplay code were not changed by this experiment.

The original source file is
`Assets/Sources/third-party/rgs-stick-figure/sticker character.blend` and its CC0
license and provenance remain beside the source pack. The source is not modified.
The Blender 5.2 conversion double-deformed bone-parented face layers. The study
copies bone-parented details into a separate Grease Pencil object without the
armature modifier, retaining the original drawings, layer parents, and limb rig.
It also removes one obsolete expression-offset driver in the working copy.

Rebuild from the repository root:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --disable-autoexec --python tools/ArtPipeline/blender_balance_study.py
& 'C:\Python312\python.exe' tools/ArtPipeline/preview_balance_study.py
```

Checks in `preview-checks.json` verify all 72 frames are present, horizontal
clipping is absent, and supporting-foot contact pixels remain identical in every
frame. `saved-action-checks.json` checks the start/end poses after reopening the
saved Blender file. These checks establish technical consistency, not animation
quality; the motion still needs human judgment.
