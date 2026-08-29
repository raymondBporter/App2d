# Character asset pipeline

This folder contains the repeatable inputs and validation tooling used to turn a
licensed humanoid walk cycle into fixed-canvas 2D pose guides for the player art.

The generated guide frames and final sprites intentionally remain separate from
gameplay collision. The player's collider and visual size continue to come from
`TraversalMetrics2D`.

Third-party source material is stored under `Assets/Sources/third-party` with its
original license.
The current source is [KayKit Character Animations](https://kaylousberg.itch.io/kaykit-character-animations),
Free 1.1, retrieved 2026-08-26 under CC0. The untouched license is retained at
`Assets/Sources/third-party/kaykit-character-animations-1.1/License.txt`.

## Clean-clone runtime build

Only the source models, plans, licenses, manifests, and small hand-selected game assets
are versioned. Generated player frames, equipment layers, and sparse packages are
ignored. Rebuild everything required by the game from the repository root with:

```powershell
python -m pip install -r tools/ArtPipeline/requirements.txt
python tools/ArtPipeline/build_runtime_assets.py
```

The command renders both character facings, renders and validates every equipped weapon,
renders the shield fallback used by `shield-block`, and packs the sparse runtime
loadouts. It exits on the first failed render or validation. Pass `--skip-sparse` to
stop after the playable full-canvas fallback assets when iterating on the source
renderers.

The committed GLB and glTF inputs are sufficient for this build. Duplicate FBX, OBJ,
MTL, and shortcut files from the downloadable source packs are intentionally ignored.

## Current pipeline

Run these commands from the repository root with the bundled Python runtime or
any Python containing Pillow and NumPy:

```powershell
python tools/ArtPipeline/render_walk_guides.py `
  --profile tools/ArtPipeline/walk-profile.json `
  --output Assets/Work/art-pipeline/WalkB

python tools/ArtPipeline/normalize_generated_frames.py `
  --input Assets/Work/art-pipeline/WalkB/GeneratedFrames `
  --profile tools/ArtPipeline/walk-profile.json `
  --output Assets/Work/art-pipeline/WalkB/Normalized

python tools/ArtPipeline/validate_walk_frames.py `
  --frames Assets/Work/art-pipeline/WalkB/Normalized `
  --profile tools/ArtPipeline/walk-profile.json `
  --motion-review Assets/Work/art-pipeline/WalkB/motion-review.json `
  --report Assets/Work/art-pipeline/WalkB/validation-report.json
```

## Deterministic mannequin render

For a coherent placeholder that does not involve an image model, render the
skinned KayKit mannequin directly from the animation GLB:

```powershell
python tools/ArtPipeline/render_mannequin_walk.py `
  --profile tools/ArtPipeline/walk-profile.json `
  --output Assets/Work/art-pipeline/MannequinWalk30 `
  --fps 30 `
  --supersample 2
```

This uses an orthographic side camera, locks horizontal motion to the animated
hips, aligns the mesh sole to the configured ground line, and chooses one fixed
scale from the maximum posed mesh height over the entire loop. It emits native
RGBA PNG frames plus a dark-background GIF preview. Mesh parts receive simple
distinct colors and faceted lighting so limb depth remains readable without
committing to final character art.

## Equipped weapon layers

Weapons remain independent from the character so a character or armor set does
not need to be rerendered for every weapon. At load time the runtime combines
four synchronized images for each frame: character color, character depth,
weapon color, and weapon depth. It compares the two depth images per pixel and
caches an ordinary in-memory sprite. This lets the hand correctly pass in front
of the grip while the blade remains behind or in front of the body as the 3D
pose requires, without relying on GPU-only shaders on the raster game canvas.

Only combinations listed in the render plan are generated. The current proof
renders `sword-b` for `idle` and `walk`:

```powershell
python tools/ArtPipeline/render_attached_weapon_layers.py `
  --plan tools/ArtPipeline/weapon-render-plan.json
```

The renderer reuses the existing character color sprites. It renders only the
missing character depth stream plus the much smaller weapon color/depth streams.
Add another clip to `weapon-render-plan.json` only when gameplay actually needs
that weapon/animation pair.

The proof uses the `directional-all` facing policy. Character color/depth and
weapon color/depth are rendered from the same true 3D orientation for both
`right` and `left`. This preserves near/far arm occlusion and shows the model's
actual back instead of assuming the character is symmetric. A genuinely
symmetric future character/equipment set can opt into a single mirrored result
to halve its directional assets.

Render every currently used player animation in true screen-left and
screen-right orientations with:

```powershell
python tools/ArtPipeline/render_directional_character_layers.py `
  --plan tools/ArtPipeline/character-direction-plan.json
```

The plan currently covers the nine gameplay animations plus `shield-block`.
For every existing animation, the renderer rejects the run if its generated
right-facing pixels differ from the already promoted canonical frames. This
keeps model-native axes and earlier gameplay-facing corrections from becoming
another accidental mirror operation.

## Multi-item loadouts

The sword-and-shield proof renders each item independently against the same
directional character pose:

```powershell
python tools/ArtPipeline/render_loadout_equipment_layers.py `
  --plan tools/ArtPipeline/loadout-render-plan.json
```

The runtime compositor accepts any number of color/depth pairs and sorts them
per pixel, so the authoring model is character + main hand + offhand rather than
a baked sword/shield cross-product. Because the current game uses a raster
canvas, the declared proof loadout also gets an ordinary derived sprite cache:

```powershell
python tools/ArtPipeline/build_loadout_composite_cache.py `
  --plan tools/ArtPipeline/loadout-render-plan.json
```

That cache is disposable build output derived from the independent layers; it
does not replace them as the reusable source of truth. Gameplay textures load
lazily by frame. Hold `B` in the current proof build to play the looping
shield-block animation.

## One-handed right-hand batch

The project-selected right-handed presentation attaches its weapon to
`handslot.l`. KayKit's one-handed attacks animate `handslot.r`, so those three
clips set `mirrorPose: true` to exchange the skeletal left/right motion before
attaching the weapon. Do not solve this by moving only the attack weapon to
`handslot.r`: that produces a coherent but visibly left-handed attack and makes
the weapon switch hands across animation transitions. Each facing is still
rendered from its real 3D yaw, so the attachment rule is:

`facing transform * animated hand socket * fixed equipment grip`

The current one-handed batch renders every player animation in both true
facings, with independent color and depth layers. The batch and gameplay
animation catalogs must stay in lockstep; runtime loadout validation fails when
a clip is missing rather than allowing presentation code to substitute another
loadout:

```powershell
python tools/ArtPipeline/render_loadout_equipment_layers.py `
  --plan tools/ArtPipeline/right-hand-weapon-batch-plan.json

python tools/ArtPipeline/build_individual_equipment_previews.py `
  --plan tools/ArtPipeline/right-hand-weapon-batch-plan.json

python tools/ArtPipeline/validate_equipment_batch.py `
  --plan tools/ArtPipeline/right-hand-weapon-batch-plan.json
```

Included models are axes A-C, daggers A-B, hammers A-C, swords A-E, and wand A.
Shields, bows/arrows, fist weapons, staffs, spear, and halberd are intentionally
excluded from this one-handed batch. `sword_E` uses a `0.7` model-local grip
scale so it remains inside the existing fixed canvas. This scales only the 3D
weapon about its grip before rasterization; it does not resize the character,
canvas, or final image.

The one-handed attack extension reuses `sword-attack` for the diagonal slice and
adds `melee-chop` and `melee-stab` for every listed weapon. Its focused plans are
`one-handed-attack-character-plan.json` and
`one-handed-attack-equipment-plan.json`; keeping them separate avoids rerendering
unrelated locomotion frames when only attack poses change. All three attack
clips mirror the skeletal pose; finished pixels are never mirrored.

The mirrored stab extends farther forward than the centered canvas allows for
the longest swords. It therefore renders the complete character and equipment
with directional roots at X=210 (right-facing) and X=302 (left-facing) inside
the unchanged 512-pixel canvas. `character.json` stores those roots, and runtime
applies the opposite horizontal presentation offset. The world-space player,
feet, collision geometry, and weapon scale remain unchanged.

### Deferred shield profile

Shield rendering remains supported but is not part of the current batch. The
verified shield-A grip for this rig is:

```json
{
  "translation": [0.0, 0.039429, 0.144725],
  "rotationAxis": [0.0, 0.262863, 0.964833],
  "rotationDegrees": -90,
  "forearmRollDegreesByClip": { "shield-block": 90 }
}
```

The fixed grip rotation is around the forearm-aligned local axis, not a screen
axis or a global Euler rotation. The `shield-block` clip additionally needs the
90-degree forearm roll shown above. Keep front and back facings as genuine 3D
renders; flipping a completed shield sprite makes its top/bottom and hand
occlusion wrong.

### Directional pivots and compact canvases

Render plans may declare `rootXByFacing` when a long forward-facing item would
otherwise reserve the same empty distance behind the character. The image still
has one stable world root; the per-facing value is the root's pixel coordinate
inside that directional frame. Runtime drawing must subtract that stored pivot
instead of assuming the character is centered.

The native-scale two-handed idle proof uses a 320x384 canvas with right/left
pivots at 96 and 224. Sword E, spear A, halberd, and staffs A/B all pass without
clipping. This compact idle size is not a safe universal two-handed size: native
Sword E occupies 356-396 pixels in chop/slice/stab, 551 in the short spinning
attack, and 588 in the full spin. A production pipeline should therefore render
onto a generous offline canvas, depth-composite the selected character and
equipment, then alpha-trim the derived cached sprite and retain its root offset.
That avoids drawing transparent symmetry padding without forcing every clip or
loadout into the worst-case spin dimensions.

### Sparse rooted layer packages

`build_sparse_layer_package.py` converts existing full-canvas character and
equipment renders into a runtime-oriented package without changing their
placement. It samples one shared animation timeline, finds each layer's nonzero
alpha bounds, adds transparent filter padding, and stores the cropped frame's
integer offset from the known character root. Color is packed as RGBA and the
matching depth data as an R16 grayscale atlas using the same rectangle.

`sourceFps` describes the fidelity of the source renders. Uniform plans can set
`targetFramesPerSecond` per clip, with `targetFps` as the plan-wide fallback. Production
plans instead use `sampling.mode: screen-space-motion`. The analyzer re-evaluates the
same skinned character and every declared equipment mesh without rasterizing, projects
corresponding vertices through the production orthographic camera, and accumulates the
largest displacement in final-canvas pixels. A sample is retained when that curve
distance reaches `maxPixelsPerSample`, or when the hold would exceed
`1 / minimumFramesPerSecond`.

Selected poses always remain points on the existing source-render grid. Their playback
times are calculated from the selected source indices, including animations with an
explicit gameplay `durationSeconds`; they are never redistributed at equal intervals.
Per-sample durations therefore preserve the original pose timing and exact total clip
length. Both facings and every compatible equipment package use the same timeline.

The source render rate remains a hard ceiling. If one adjacent source pair already moves
farther than `maxPixelsPerSample`, the builder retains both and records
`maximumSourceStepPixels` in the timeline/package metadata. Meeting a tighter bound in
that case requires rerendering the source curve at a higher measurement rate rather than
inventing an interpolated raster frame downstream.

### Native KayKit character parts

Rig Medium exposes six skinned mesh nodes directly: `ArmLeft`, `ArmRight`,
`Body`, `Head`, `LegLeft`, and `LegRight`. Build the isolated 10 FPS idle/walk
proof with:

```powershell
python tools/ArtPipeline/render_character_part_layers.py `
  --plan tools/ArtPipeline/native-character-parts-proof-plan.json `
  --replace
```

The renderer maps those exact nodes to `arm-left`, `arm-right`, `body`, `head`,
`leg-left`, and `leg-right`; it does not infer or invent additional regions.
Every part uses the same skinned pose, global hips, ground, directional camera,
and root. Each receives independent RGBA color and encoded depth source frames,
then automatic alpha trimming, root-relative origins, and paired RGBA/R16 atlas
packing. The work output includes combined animation previews and six-panel QA
sheets. Sparse layer reconstruction is required to be pixel-exact. Recomposition
against the monolithic character render is reported separately because
independent antialiasing produces small expected differences along native mesh
boundaries.

The compositor calculates the root-space union of any number of cropped layers,
compares depth only inside that union, alpha-composites back-to-front, and trims
the derived result again. No crop or attachment offset is authored by hand.

Run the quick Sword A idle/walk proof at 10 FPS with:

```powershell
python tools/ArtPipeline/build_sparse_layer_package.py `
  --plan tools/ArtPipeline/sparse-layer-sword-a-proof-plan.json `
  --replace
```

The proof is intentionally written below
`Assets/Work/art-pipeline/SparseLayerSwordAProof`; it does not replace production
content. Its validator reloads the written color/depth atlases, reconstructs
every selected source layer at 512x384, and requires exact color and 16-bit depth
equality. It also root-composites character plus weapon and requires that result
to match the existing full-canvas compositor exactly. `package.json` contains
the atlas rectangle, root-relative origin, source frame, sample time, and frame
duration needed to consume the package. `validation-report.json` records file,
disk, and decoded-size comparisons.

Build the complete shipping Sword A package with:

```powershell
python tools/ArtPipeline/build_sparse_layer_package.py `
  --plan tools/ArtPipeline/sparse-runtime-sword-a-plan.json `
  --replace
```

The runtime plan writes only the package manifest, packed atlases, and validation
report below `Assets/Content/sparse-loadouts/right-hand-sword-a`; proof composites and
preview GIFs remain exclusive to the work-area plan.

To rebuild the complete production package for every right-hand equipment set, run
`python tools/ArtPipeline/build_sparse_runtime_loadouts.py`.

To render the complete unique `Rig_Medium` library with four parallel workers:

```powershell
python tools/ArtPipeline/render_animation_library.py `
  --profile tools/ArtPipeline/walk-profile.json `
  --source Assets/Sources/third-party/kaykit-character-animations-1.1/Animations/gltf/Rig_Medium `
  --output Assets/Work/art-pipeline/AnimationLibrary30 `
  --fps 30 `
  --supersample 2 `
  --workers 4
```

Add `--resume` to validate and skip completed animation folders after an
interrupted run. The batch renderer deduplicates repeated animation names such
as `T-Pose`, records progress in `library-manifest.json`, and writes a linked
Markdown animation index.

While building the cycle one frame at a time, add `--allow-incomplete` to the
normalization command to process and review only the candidates that currently
exist. Without that flag, all configured frames remain required.

The model-facing step sits between guide rendering and normalization. Generate
one frame at a time: give the model the matching `guide-XX.png` as authoritative
pose/depth input, the existing player art as identity input, and only the
previous approved frame as a continuity reference. The previous frame is never
allowed to override the current guide. The complete prompt template is stored
in `walk-image-prompt.txt`.

The original contact-sheet paint attempt is rejected. It averaged six distinct
leg poses into a weak gait, and it returned a painted checkerboard rather than
native transparency. The selected runtime walk is promoted separately to
`Assets/Content/characters/player/animations/walk`; pipeline output never ships
directly.

`motion-review.template.json` defines the semantic review that must be completed
against the normalized preview before validation can pass. This is intentionally
separate from pixel registration: a perfectly centered bad walk is still bad.

`walk-profile.json` owns all provisional choices: canvas size, frame count,
registration anchor, ground line, target visual height, and tolerances. It
explicitly does not validate the art against collision geometry.

The current normalization anchor is the median of blue armor pixels in the
upper 48% of the character, which is a reliable helmet proxy for this design.
That is deliberately profile-level policy, not a general claim that the helmet
is the gameplay root. A later character can replace this with a different
detector or explicit model-produced anchor mask.

The image model used for the first proof returned a visible checkerboard rather
than real alpha. That output is rejected. `normalize_generated_sheet.py` requires
a native alpha channel containing transparent pixels and will not synthesize one:
background removal after rendering contaminates antialiased edge colors and can
leave an unrecoverable halo.

The built-in image generator can return native alpha, but compliance is not
reliable: visually correct frames may still arrive as opaque RGB files with a
painted transparency grid. Every candidate is therefore inspected before it is
accepted. Normalization ignores near-invisible alpha haze when measuring the
foreground and resizes in premultiplied-alpha space so RGB hidden beneath fully
transparent pixels cannot bleed into the sprite edge.

Registration checks are necessary but insufficient. A candidate must also pass
a semantic motion review: alternating near/far leg order, two readable legs,
distinct contact and passing poses, forward-only weapon orientation, and no pose
averaging. The first proof passed registration but correctly fails this newer
motion gate.
