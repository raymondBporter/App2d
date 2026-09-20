# KayKit Character Animations 1.1

Author: Kay Lousberg. Official pack:
<https://kaylousberg.itch.io/kaykit-character-animations>

Downloaded the free 1.1 archive from official itch.io upload 15799903 on
2026-09-19. Archive SHA-256:
`65882F31F905AD2E953819648A59287CDEAB8F623908D5EF701971D3758BE20F`.
The extracted pack retains its original `License.txt` (CC0).

The preview uses all eight Rig_Medium and six Rig_Large GLB sets: 131 medium
and 28 large clips, including six static poses. Repeated T-poses are excluded.
Large-rig variants have distinct names, even when their action names match
medium-rig actions. The original five preview IDs are preserved. Source art is
unchanged. Paid Blender source files are not used.

Run from the project root:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/render_rgs_kaykit.py
python scripts/verify_rgs_kaykit.py
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/render_kaykit_swords.py
python scripts/verify_kaykit_swords.py
python scripts/build_main_preview.py
node scripts/test_model_select.cjs
node scripts/test_kaykit_preview.cjs
node scripts/test_rgs_sword.cjs
```

Settings are in `config/rgs-kaykit.json`. The renderer reads the approved RGS
scene and Universal rest skeleton, maps KayKit rest-relative rotations to a
virtual skeleton with the existing limb lengths, and projects it into RGS
controls. Each source rig uses one ground calibration; the medium rig retains
the approved five-clip calibration. This preserves lowest-ankle height,
including airborne motion. It is not a contact-solving IK retargeter.
The pivot, artwork and head size come from the RGS scene. Ordinary motions
retain its camera scale. Outlying poses receive wider framing around the same
ground origin, recorded in `cameraScale` and indicated in the preview. This
avoids cropping spawn effects and displaced skeleton poses.

`scripts/catalog_kaykit.py` inventories the actual GLBs and regenerates the
config with explicit loop semantics. Six zero-duration source poses become
single-frame, one-second holds. Other clips preserve source durations, omit
the repeated endpoint for loops, and include it for one-shots. The catalog
contains 3,133 sampled frames at approximately 12 fps. Source experimental
transforms and skeleton effects are included, but the fixed-limb 2D adapter
approximates disassembly, scaling and depth effects.

For a split bake/render run, pass `-- --bake-only`, then `-- --render-only` to
the Blender renderer command. Rendering can resume completed clips using
scene-hash and camera-scale cache markers. The verifier checks source catalog
coverage, duration, pixel bounds and atlas contents, and writes 20 paginated
contact sheets. Publishing is the separate `build_main_preview.py` step.

The output is separate: `assets/rgs-stick-figure/kaykit.blend` and
`output/universal/rgs-stick-figure/kaykit/`. The main preview appends these
clips to the RGS manifest only, leaving the original RGS scene and shared
catalog intact. KayKit motions use the baked original face. Existing independent
face/sword layers still work on existing clips.
Side-on projection can overlap limbs, and direct cuts between libraries may
show stance differences. These are visual candidates for review.

## Swords

The equipment catalog includes 143 motions and 2,885 rendered frames. It
deliberately includes tool, spell, gesture, and unarmed motions. Only 16 explicit
gun and bow motions hide the sword. Five dual-wield motions carry two swords.
`scripts/kaykit_sword_catalog.py` defines this selection policy.

`render_kaykit_swords.py` reuses the original RGS sword drawing and materials.
Each grip follows the corresponding projected hand, while blade rotation uses
the source `handslot.l/r` local +Y axis projected into the drawing plane. The
renderer samples attachment transforms at quarter-frame intervals plus every
exported frame. It keeps the original durations, sample frames, and loop flags.
Two-handed actions use the right-hand grip; the offhand follows the source
animation without an additional IK constraint. Swords are foreground artwork,
so side-on overlap can obscure a limb or face. Existing strike trails remain
limited to their original motions.

Equipped outputs live in `output/universal/rgs-stick-figure/kaykit/sword/`,
including an editable `equipped.blend`. Three equipped clips need slightly wider
framing than their unarmed versions; all retain the same ground pivot. The
equipment status identifies these while equipped. The renderer supports
`-- --bake-only`, `-- --render-only`, and `-- --render-only --preview-only`.
The sword verifier checks coverage, source hashes, timing, grip attachment,
pixel bounds, and atlas parity, and produces 18 paginated contact sheets.
