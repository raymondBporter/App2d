# RGS stick figure: Universal animation adapter

This is an adapter of Raphael Gonçalves / RGS Dev's original Grease Pencil
artwork, using Quaternius Universal motion. Both sources are CC0. The original
file in App2d is read only; this directory contains the generated adapted rig.

Run `./render.ps1 -Config config/rgs-stick-figure.json` from the repository root.
This builds `adapted.blend` and renders the entire current review catalog:
85 animations, including both Universal libraries and teeter, plus 72 edited
versions (157 sheets / 2,897 frames). Exact source sample times, durations,
loop flags, and Base/Edited relationships match the original model. Quarter
frame baking also makes the adapted Blender actions editable between samples.

The approved head size is option D: `head_size: 0.7` in the render config,
70% of the initial head width and height. Head and face scale together about
the neck attachment; body, sword, animation controls, camera, and pivot retain
their previous scale. The original size comparison remains in `head-options/`
under the output directory.

Then run `python scripts/verify_rgs_stick_figure.py` followed by
`python scripts/build_main_preview.py`. The main preview at
`output/universal/preview.html` has a **Character model** selector for Universal
Base and RGS Stick Figure. `?model=rgs` opens the latter directly. Model changes
carry over animation phase, pause/final-pose state, playback speed,
zoom, search, queued transitions, and active sequences. Each model uses its own
camera and shared framing across that model's full catalog. The standalone
RGS preview is still available in `output/universal/rgs-stick-figure/preview.html`.

The stick figure uses original motions (`pose_variants: "base"`), without the
Universal model's 25/35-degree chest/head visibility corrections. Its front-facing
face artwork already provides readability. The preview hides Base/Edited for RGS
and maps old edited links, queued clips, and sequence entries to original poses
at the same animation phase. Universal remembers its own Base/Edited choice.
Previously rendered edited sheets remain available as source/review assets;
normal RGS playback, face overlays, sword angles, and swooshes use the base clips.

The original rig has independent 2D controls, Grease Pencil layer parenting,
bone envelopes, and legacy expression drivers. It cannot accept Universal
skeletal actions directly. The adapter retains the original head outline,
first facial expression, torso polygon, and limb strokes/materials. It builds
an independent weighted control rig and bakes the projected Universal joint
positions into actions. Torso, limb, and head proportions are adjusted to
retain the large-headed cartoon appearance. It does not simply rename bones.

The first idle/walk test was visually accepted; the expanded catalog remains a
**visual candidate**, not full compatibility. The face remains a
flat drawing, head rotation uses projected tilt, and depth turns are lost.
Foreshortening and fixed front/back drawing order can cause limb overlap.
There is no contact-solving IK, finger animation, expression switching, or
3D sword-depth integration. The original RGS sword can now be equipped for
59 compatible animations (108 Base/Edited clips). Teeter is
retargeted as body motion; its original pixel-specific platform edge is not
reused on a model with different feet and proportions.

Rebuilds read the sibling App2d source path in `config/rgs-stick-figure.json`.
The generated blend is self-contained and uses no external scripts or drivers.
The source artwork is available at
<https://rgsdev.itch.io/animated-stick-figure-character-2d-free-cc0>.
Original provenance and license are retained in App2d's RGS source directory.

The verifier (Pillow) checks every frame's visible bounds, atlas pixel equality,
and complete motion-metadata parity, and makes representative contact sheets
and idle/walk GIFs. `node scripts/test_model_select.cjs` verifies model coverage
and playback transfer, including paused edited poses, sequences, and completed
one-shots. Numerical checks do not establish visual approval of every motion.

## Equipped sword

Run Blender with `--background --factory-startup --disable-autoexec
--python-exit-code 1 --python scripts/render_rgs_sword.py`, then
`python scripts/verify_rgs_sword.py` and `python scripts/build_main_preview.py`.
The preview's Sword picker offers **None** and **Stick-figure sword**; use its
Animation with sword menu for supported motions. Equipping carries across model
changes, with the destination model's first sword substituted when needed.

The equipped catalog contains 2,123 frames across sword combat, walking,
running, crouching, jumps, slides, rolls, reactions, sitting, and selected
gestures, interactions, reaching, chopping, braced poses, and defensive motions.
The added utility and shield motions reuse their body motion while holding the
sword; they do not add a shield or other props, or solve contact with objects.
`config/rgs-sword.json` explicitly classifies every base animation;
edited versions inherit their base's decision. Punches, pistol motions,
spellcasting, conflicting props, swimming, and climbing stay unarmed. The
picker retains the equipped sword during these motions and restores it on
the next compatible animation. New catalog entries require a compatibility
decision before rebuilding.

The source `sword1` Grease Pencil strokes are normalized around their handle,
scaled to the character, and attached to `arm_r_2`. Blade angle follows the
projected Universal right-hand orientation, calibrated from the same curled
finger grip used by the existing sword renderer. A minimum direction threshold
keeps edge-on projection defined. The cartoon blade retains its full length.
Attachment is baked at quarter source frames plus every export sample.

Jog, sprint, and idle talking use a 30-degree downward sword-angle offset around the grip,
including their edited versions, to reduce face overlap. Offsets are configured
in `config/rgs-sword.json` and applied by full sword rebuilds. For angle-only
changes with current equipped and face exports, run Blender with
`--python scripts/update_rgs_sword_angles.py`; this updates just changed actions,
their composed sprites, and their face-system sword foregrounds. Then run the
sword verifier, `verify_rgs_faces.py --clips jog sprint idle_talking`, and the preview builder.

### Sword swoosh

The preview has an independent swoosh on Sword Attack and the full regular/heavy
combos, including edited versions. `config/rgs-sword-trails.json` separates swing
windows (30 fps source-frame coordinates), sampling accuracy, and visual style.
The **Tune swoosh** controls change trail history, post-swing fade, blade coverage,
tail taper, and opacity immediately, including while paused. These are preview
adjustments; put chosen defaults in the config's `style` object to save them.

Run Blender with `--python scripts/export_rgs_sword_trails.py` after changing the
equipped rig or the sampling/window config, then `python scripts/build_main_preview.py`.
Style-only config edits only need the preview builder. Paths are in
`output/universal/rgs-stick-figure/sword/trails/paths.json`; the builder rejects a
stale rig hash or mismatched timing. No character sprite rerender is needed.

The exporter evaluates the actual Grease Pencil blade base/tip under the equipped
bone transform. It recursively subdivides motion using quarter/midpoint screen
error probes (0.25 px tolerance), retaining exact sprite times and swing boundaries.
The runtime preserves those curve samples and subdivides long edges by pixel
distance. Width/taper use cumulative tip travel, so variable swing speed does not
bunch the shape into the slow section. History and smooth fade remain time-based.
Each swing is separate; idle, recovery outside the fade, and unsupported motions
have no trail. Pause, seek, speed changes, and clip switches reconstruct the same
effect without carrying a trail from the previous motion.

With separate faces, draw order is body, face, swoosh, sword/grip. In the baked-face
or layer-load fallback, the swoosh sits behind the full sprite. This is a 2D effect,
using RGS blade paths rather than the Universal model's depth paths. The Universal
model's existing slash renderer is unchanged. `node scripts/test_rgs_sword_trails.cjs`
checks all six clips, spatial subdivision, distance-based taper, fade, controls,
paused redraws, seeking, unequip, and stale-source protection.

These RGS exports are equipped character compositions with foreground sword
art, using exactly the unarmed camera, frame times, and pivot. They do not claim
per-pixel 3D depth or reuse the original model's weapon textures. The off-hand
is not repositioned for two-handed grips. The original model's depth weapons
and slash effects retain their existing renderer and controls.

Outputs are under `output/universal/rgs-stick-figure/sword/`, including the
editable `equipped.blend`, attachment report, timing/clipping/pixel verification,
contact sheets, and review GIFs. `node scripts/test_rgs_sword.cjs` checks equipped
playback, image-cache limits, failures, and unequipping.

## Independent face textures

### Doodle face pack

The preview defaults to **Relaxed** from ten custom doodle faces: relaxed, blink,
look up, look down, focused, effort, startled, wince, dead (X eyes), and smug.
They stay mostly front-facing with a subtle screen-right bias. Original pack
faces are grouped separately for comparison. The selector also offers an idle
blink loop and a ten-expression tour, independent of body playback.

Edit `assets/rgs-stick-figure/doodle-faces.json`, then run
`python scripts/build_main_preview.py` to regenerate and publish the pack.
`python scripts/build_doodle_faces.py` updates just the assets and face manifest.
Both use Pillow; no Blender or body re-render is needed for face-only edits.
Full `render_rgs_faces.py` exports also reinstall this pack automatically.

`output/universal/rgs-stick-figure/faces/doodle/` contains ten transparent
256×256 PNG textures and matching editable SVGs. The JSON recipe is the source
of truth; generated SVG edits are overwritten on rebuild. No head outline or
background is included: use the existing per-frame UV transforms and body/face/
sword draw order from `faces/faces.json`. IDs such as `doodle-dead` are stable.
`defaultExpression`, expression `group`/`vector`, and `doodlePack` provenance are
additive metadata. The PNGs work with the existing face renderer. These assets
are integrated into the RGS authoring preview; the Universal game-export format
is separate. Expressions are selected explicitly, not inferred from gameplay.

Run Blender with `--background --factory-startup --disable-autoexec
--python-exit-code 1 --python scripts/render_rgs_faces.py`, then run
`python scripts/verify_rgs_faces.py` and `python scripts/build_main_preview.py`.
Rebuild the adapted and equipped rigs first when changing those assets. The
export checks both rig hashes so stale attachment data cannot be published.
For a small export, append `-- --clips idle walk sword_attack roll`; the verifier
then needs `--allow-partial`. Omit that option for full-library validation.

The face export covers all 157 body clips and the 108 equipped variants. Four
256-square transparent textures reuse the source expression drawings: focused,
concerned, laughing, and knocked out. These are static drawings; the expression
cycle is a playback demonstration, not authored speech or lip sync. New facial
sequences can reference those textures (or additional artwork) without rendering
each body/expression combination.

The preview's **Face expression** selector offers the four textures, an independent
expression cycle, and the original baked face for comparison. **Pause face
animation** controls only the face; the normal body Pause and speed controls do
not reset or advance its clock. Body transitions and Base/Edited switches preserve
the facial phase. The head outline remains in the body render at approved size D.

`output/universal/rgs-stick-figure/faces/faces.json` describes the layers. Asset
paths are relative to `output/universal/rgs-stick-figure/`. Draw the faceless body
frame first, the expression texture second, and the optional sword foreground
last. The foreground includes the grip hand, so expressions stay behind the blade.
Unsupported equipment motions have no foreground. The old composed sprites remain
available as a load-failure fallback and comparison.

Each body's `transforms[i]` is `[a,b,c,d,e,f]` and maps expression texture UVs
to the logical 512-square frame: `x=a*u+c*v+e`, `y=b*u+d*v+f`, where `(0,0)` is
the texture's top left and `(1,1)` its bottom right. These transforms come from
the evaluated head bone at the exact body sample time. Apply the same global
sprite pivot, scale, and mirroring to all layers. Facial sequences use their own
elapsed time and per-expression durations; select the attachment using the body
frame, independently of the expression frame.

The verifier reconstructs the original expression over every body and equipped
pose and compares premultiplied pixels with the baked reference. Small texture
filtering differences are allowed; position, rotation, timing, atlas contents,
and sword occlusion are checked. `node scripts/test_rgs_faces.cjs` covers layer
order, independent playback/pause, bounded caching, and load-failure fallbacks.
This adds face composition to the authoring preview; the separate Universal
game-export format has not been changed.
