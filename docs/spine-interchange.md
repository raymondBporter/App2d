# Spine 4.2 interchange

Character Studio can export an existing model or variant to Spine 4.2 JSON, then import the edited region rig as a new
native model with editable animations. This is an initial interchange subset, not complete Spine runtime compatibility.
The original authored library remains usable; existing templates and game entities do not need to be converted.

## Editor workflow

1. Open a model, variant, or its animation in Character Studio. Choose **Export Spine**, saving into a dedicated folder.
   The package includes all compatible animations for that base, JSON, loose PNG images, a text atlas, conversion notes,
   and a `.app2d.json` native backup. Duplicate animation display names include their native clip IDs.
2. In Spine 4.2, use **Import Data** to create a **new project** from the JSON. Keep the adjacent `images` directory.
   The export uses 100 pixels per App2d model unit. Save the Spine project normally after import.
3. Edit bones, region attachments, skins, and the supported animation channels in Spine. Procedural two-point parts
   have helper bones named `app2d-part-*`: these carry the attachment's position, rotation, and stretch.
4. Export JSON from Spine 4.2 with **Nonessential data** enabled. Keep its loose images, or place a matching `.atlas`
   beside the JSON. The images-directory override in Studio is useful when Spine's saved image path no longer exists.
5. In Studio choose **New > Import Spine 4.2 JSON** and supply a new asset ID. Import creates unsaved model and clip
   drafts and copies their images under `Assets/Characters/authored/images/<model-id>`. Use **Save all** to keep the
   model and animations together. Import does not replace an existing ID.

The viewport skin selector chooses the active skin. Model mode edits local bone translation, rotation, scale, and shear,
plus attachment dimensions, offsets, and angle. Animate mode keys bone transforms, slot attachment selection (including
hidden), and slot draw order. Original external bone, slot, skin, and animation names are retained for export, while native
asset IDs are normalized for the library. The slot inspector also keys RGBA tint, RGB, and opacity. Expand a motion or
color component's curve to edit its two time/value controls. Value controls in channel units allow overshoot between
equal endpoint values; fractional controls retain the usual normalized easing. Rotation and shear controls use radians.
Spine JSON does not carry a loop flag; enable Loop on imported clips when needed.

Command-line export uses the same converter:

```powershell
dotnet run --project App2d.CharacterStudio -- --export-spine person artifacts/spine/person.json
dotnet run --project App2d.CharacterStudio -- --export-spine quadruped artifacts/spine/quadruped.json
```

## What the terms mean here

| Concept | Native representation | Meaning |
| --- | --- | --- |
| Skeleton | `CharacterModel` and its bone hierarchy | General rig; anatomy names are authored data. |
| Bone | `ModelControl.Transform` / math `Affine2D` | A parent-local affine frame; length is independent of the transform. |
| Joint | Bone origin / parent-child connection | A rig connection, not a mandatory separate Spine object. |
| Constraint | Existing `ModelChain`, with Spine constraint support still pending | A solver relationship, separate from animation channels. |
| Slot | `SkeletonSlot2D` | A named location on a bone, with attachment selection, tint, blend, and draw order. |
| Attachment | A skin entry referencing a `PuppetPart` image region | Visible artwork selected by a slot. |
| Skin | `SkeletonSkin2D` | A slot-to-attachment lookup. Missing entries fall back to the default skin. |
| Material | `RenderMaterialDefinition2D` | Texture, tint, fill, and outline settings for artwork. A skin is not a material. |
| Socket | Existing `ModelSocket` | A placement frame for equipment and gameplay; it does not itself select visible artwork. |
| Animation | `MotionClip` | Bone channels, slot colors, discrete attachment/order keys, and named events. |

The existing class names remain for native asset and gameplay compatibility. The new transform and appearance path
does not require arms, legs, a head, a weapon, or Person-specific IDs. Templates, motion-role tables, loadouts, and game
actions remain higher-level authored/gameplay concepts rather than requirements of a skeleton.

Native models/variants use format version 3 and clips use version 3. Existing version 2 models/variants and version 1/2
clips upgrade in memory on load, with their point and animation behavior preserved. Saving writes the new version;
older Studio builds reject it rather than opening an affine/image rig with incomplete behavior.

## Supported subset

- Spine **4.2.x JSON**, normal transform inheritance, hierarchical local translation/rotation/scale/shear, reflections,
  and zero scale during evaluation. World transforms use full `Matrix3x2` composition rather than summed angles.
- Region image attachments, multiple skins with default fallback, setup tints, and normal/additive slot blending.
- Translate, rotate, scale, and shear timelines with linear, stepped, and cubic Bezier interpolation. Combined X/Y
  timelines and one isolated axis are supported; separate X and Y timelines for the same channel are currently rejected.
- Slot attachment timelines, attachment hiding, draw-order timelines, and repeated named events with int/float/string
  payloads. Imported event data is retained; binding those events to gameplay is separate work.
- RGBA, RGB, and alpha slot timelines, including independent RGB/alpha key times, setup values before the first key,
  stepped changes, and per-component cubic curves. Tint multiplies the attachment/material color at rendering time;
  evaluation leaves setup colors unchanged. Colors clamp to [0,1] when displayed, and Spine color keys use 8-bit hex values.
- Cubic value controls can overshoot or animate between equal endpoints. Native X/Y/Z components can have separate
  curves. Move/copy/delete, retiming, save/reopen, and excerpts include attachment, color, and draw-order keys.
- Loose PNG artwork and text atlases with modern `bounds`/`offsets` or legacy `xy`/`size`/`orig`/`offset` fields,
  trim restoration, packing rotation, and premultiplied-alpha recovery. Export currently uses one atlas page per image;
  packing several regions into a shared page is a later optimization. Atlas pixels are extracted to loose native images.
- Native affine image rigs preserve their supported keys and curves through interchange. Exported native clips retain
  their duration even when their last meaningful key is earlier. Repeated interchange does not keep adding motion roots.

Unsupported features fail import explicitly: weighted/unweighted mesh and linked-mesh attachments, deform timelines,
paths/clipping/bounding-box/point attachments, IK/transform/path/physics constraints, alternate inheritance modes,
skin-dependent bone/constraint activation, image sequences, two-color tint, multiply/screen blends,
and event audio. Native clip duration is limited to 60 seconds.
A visible part in a slot rig must belong to a skin for export; image artwork needs a bone frame. Mixed unmanaged procedural
artwork is rejected instead of silently omitted. Collapsed parent frames cannot be inverted for world-space editing or baking.

## Conversion tradeoffs

Existing point rigs export as editable bones. Point IK, contacts, proportion retargeting, and two-point drawing relationships
are evaluated and sampled at 60 Hz, including authored key and contact boundaries. The resulting Spine keys reproduce
the sampled motion, but do not reconstruct the original solver or retargeting relationships. Procedural silhouettes, paint,
outlines, and face choices become PNG regions at 100 pixels per model unit. They can be transformed in either editor;
their original vector shapes and paint operations are retained in the backup rather than reconstructed from pixels.

App2d's depth values become per-attachment slot order. This preserves ordinary front/back ordering; intersecting surfaces
whose depth varies across a part can render differently. Game scene depth continues to order the actor as a whole.
Three-dimensional equipment and socket orientation have no equivalent in this region subset. Export targets the model
and its clips; entity equipment and game action bindings remain in the native asset library.

The `.app2d.json` companion contains the original base model, selected variant, and animations. **It is a backup, not an
automatic merge format.** Spine does not preserve or update it. Importing edited Spine JSON creates a new image rig and
new clip IDs; it does not transplant edits into the original model's contacts, motion sets, equipment, or game bindings.
Preserving stable identities and merging compatible edits back into the original rig is a separate follow-up.

## Validation and next steps

Automated tests cover affine hierarchy/reflection/shear/collapse, setup-before-first-key behavior, cubic curves, skins,
attachment hiding, draw order, repeated events, native image round trips, conversion of procedural point IK, duration,
name collisions, native variant editing, and atlas extraction. The editor smoke workflow exports a headless tripod,
imports and renders its image rig, then saves/reopens it through the ordinary asset catalog.

The converter is written against the published format and 4.2 example data. No Spine runtime library is embedded.
Actual import into the installed Spine editor still needs manual verification; this workstation has no Spine editor installed.

The next architectural steps are generic constraint definitions and solvers, image/slot/skin creation tools, independent
axis timelines, weighted meshes/deforms, efficient atlas packing, and a stable-identity merge workflow. Keep
Person templates and game actions above the reusable skeleton layer as these features are added.

References: [JSON format](https://esotericsoftware.com/spine-json-format),
[Import Data](https://esotericsoftware.com/spine-import), [atlas format](https://esotericsoftware.com/spine-atlas-format),
[versioning](https://esotericsoftware.com/spine-versioning), and
[official 4.2 Spineboy example](https://github.com/EsotericSoftware/spine-runtimes/blob/4.2/examples/spineboy/export/spineboy-ess.json).
The current timeline field layout is also checked against the
[official 4.2 JSON reader](https://github.com/EsotericSoftware/spine-runtimes/blob/4.2/spine-ts/spine-core/src/SkeletonJson.ts).
