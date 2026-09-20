# Character Studio

## Run

```powershell
dotnet run --project App2d.CharacterStudio
```

Requires Windows and .NET 10. ImGui.NET 1.91.6.1 is pinned; MonoGame WindowsDX is
the same version as the game. The checked-in runtime assets make the studio
independent of Blender and the original checkout. The application has its own
executable and does not replace App2d.Noodle or the game.

The studio now starts with **entity types**: Player, Needle, Maul, Cinder and Scrap
Hound. See [Entity authoring and playtest](entity-authoring.md) for the action,
collision, saving and playable-arena workflow. **Entities → Browse source motions**
opens the original motion/appearance interface described below.

The left panel selects an anatomy/library and searches its clips. The center
provides playback, sample stepping, a scrubber, speed, one-shot repeat, queued
switches and three-clip sequences. Scrubbing to a loop's endpoint explicitly
holds its last stored pose. The right panel edits the relevant appearance values,
faces, weapons and optional game animation bindings. Source review warnings remain
visible below playback controls.

Scroll over the viewport to zoom, middle-drag to pan, or double-click/Fit motion
to fit sampled poses across the action. Ordinary playback never auto-fits. Space
toggles playback when a text field/widget is not active. Ctrl+S saves a look;
Ctrl+Z/Ctrl+Y undo/redo appearance edits. Open/Save use standard file dialogs.
Look files are versioned JSON containing library ID, selected clip, appearance
and semantic role-to-clip bindings. Closing offers to save unsaved edits. Unsaved documents
are retained when switching libraries during a session.

The interface follows Windows per-monitor DPI scaling. The initial window uses
logical dimensions bounded by the monitor's working area; resizing changes the
available layout space, never the size of text or controls. The top-right **UI**
menu adjusts interface size relative to the Windows default and saves that
preference in `%LOCALAPPDATA%/App2d/CharacterStudio/settings.json`. Moving between
monitors rebuilds the font atlas at the new DPI. Small windows scroll instead of
shrinking the controls.

## Body proportions

The Person Appearance panel has **Leg length**, **Arm length** and **Hip width**
sliders. They are per-look values, not baked motion variants: every sampled pose
is retargeted in export space before the ordinary drawing transform. Limbs scale
segment by segment from the hip or shoulder, so foreshortening and weapon grips
follow; hips spread the torso bottom edge and the two leg roots about their
midpoints, which from the side mostly separates the legs in depth. Longer legs
raise the whole figure by a constant measured from the library's rest pose, so
standing feet stay on the pivot and gait bob is preserved. Anatomy-mode hurt
regions read the retargeted points automatically. Contact clips (wall grips,
ladders, two-handed props) drift with large changes; keep edits moderate.

## Head workshop and weapons

The Person Appearance panel includes **Face animation**: choose **Expression tour**,
**Hit reaction**, **Long fall**, or **Victory** to preview short performances. **Pause
face** and **Replay face** work independently of body playback. These previews do not
change the saved look. Choose **Saved expression** to return to the face picker;
it now offers 16 vector expressions, also available in the edited-head face picker.
Most expressions use solid ink eyes, simple mouths, and no brows. Anger, worry,
and confusion introduce brows; surprise opens white eyes with small pupils,
and panic adds raised brows and larger eyes. These details blend in and out
with the reaction. Teeth are omitted throughout.
The game player blinks and reacts to charging, attacking, damage, sustained falls,
wall grips, hard landings, low health, death, and reaching the goal. Faces blend on
their own clock; damage and death take priority. This first pass does not yet add
enemy personalities or hit/miss detection for facial acting.

`dotnet run --project App2d -- --face-smoke artifacts/faces` exports expression
contact sheets using the real character renderer, for original and edited heads.

**Head editor** near the top of the Appearance panel opens the workshop for Person
and Quadruped. Enable **Use edited head**, or make an edit, to attach that shape
to the current animation. **Original head** restores the original drawing; undo
can bring the edited head back. Head size still scales the attached shape.

Outline mode provides the source workshop's skull, muzzle, brow, jaw and softness
controls, ten draggable control points, arrow-key nudges, point reset and clear
hand edits. Edits that fold the outline are rejected; concave shapes use a
triangulated fill. Face mode provides independent face dragging, position, size,
tilt and expression. Scroll over the workshop canvas to zoom; Fit includes the
face. The same shared renderer attaches the shape to the person's head direction
or the quadruped's animated head/neck, including shortened necks and flipped looks.

Four starting variations and Explore provide alternate shapes. **Keep variation**
retains up to eight during this session. **Export head / Import head** use the
original workshop's version 1 JSON format for portable reuse between anatomies.
**Save look** includes the current head definition, weapon choice and weapon head
size. Older looks default to the original head and sword. Named character templates
and a permanent variant library remain a future workflow decision.

The Person weapon selector includes Sword, Rapier, Great mace and Great hammer.
Mace and hammer have separate handle-length and head-size controls. Weapons follow
the authored hand attachments and stay hidden on unarmed clips. Sword slash trails
are disabled for the other weapons, matching the source preview. Weapon artwork is
imported from `point-library.js`; it does not add animation data or per-weapon bakes.

## Kevin Iglesias motions

Person includes 662 clips, with 412 added clips from eight free Kevin Iglesias packs.
Counts include masculine/feminine and root-motion variants. In **Entities → Browse
source motions**, choose **Person**, then **Kevin Iglesias / All** or an individual
pack in the source filter. Existing entity actions can select these clips through
their **Action → Source motion** picker; search for `Kevin` or a motion name.
Existing gameplay bindings and saved entity definitions are preserved.

The importer preserves the creator's source manifest, README/license notice and
eight manuals under `Assets/Characters/provenance`. These packs use the Standard
Asset Store EULA, not CC0. Bow, gun and crafting-tool motions currently preview
body movement; their prop drawings remain separate work.

Refresh just Person without rewriting creature libraries or saved entities:

```powershell
node tools/CharacterPipeline/import.cjs ../sprite-renderer --person-only
```

## Tomek wolf motions

Quadruped includes nine mapped wolf clips in addition to its twelve original
motions. Select **Tomek / Wolf** in the source filter, or search `Wolf` in an
entity's Source motion picker. Open `Assets/Characters/looks/tomek-wolf.json`
for a look with suggested role bindings. See [Wolf animation mapping](wolf-animation-mapping.md)
for the rig mapping, source limitations, provenance and rebuild commands.

## Refresh assets

After exporting changes in sprite-renderer:

```powershell
node tools/CharacterPipeline/import.cjs ../sprite-renderer
dotnet run --project App2d.CharacterStudio -- --check
```

The importer reads the current humanoid pointer and active creature exports,
checks packed-data hashes, preserves source metadata/licenses, normalizes the
library schema, and captures reference poses and vertices from the original
JavaScript renderers. Existing uint16 bytes are copied without recompression.
Quadruped and monster JSON frame arrays become float32 binary, with maximum
conversion error recorded in each manifest. The 15 libraries contain 759 clips.
The catalog records current packed coordinate sizes. The studio loads
libraries on selection; clean inactive libraries can be reclaimed.

`capture-reference.cjs` may also be run separately to refresh the comparison
fixtures after deliberately changing the source drawing code. It does not change
the C# renderer. A mismatch is a reason to review the port, not silently adjust
tolerances or regenerate expectations from C#.

## Boundaries

The studio ports the active sprite-renderer previews to C# and Dear ImGui. Its
renderer is a reusable engine component; the studio is a consumer, not the owner
of character drawing behavior. Existing game presentation remains a separate
consumer to migrate after visual review.

- `App2d.Core/Characters`: packed motion loading, sampling, playback and appearance
  values. No graphics, UI or game simulation dependency.
- `App2d.Rendering/Characters`: depth-tested geometry, anatomy bindings and GPU
  submission. No ImGui or editor state.
- `App2d.CharacterStudio`: MonoGame desktop host, ImGui integration, document state,
  motion browsing, playback controls and appearance editing.
- `tools/CharacterPipeline`: repeatable import of current exported assets, plus
  the local Tomek wolf mapping exporter. Other Blender authoring stays in
  sprite-renderer; the application has no sibling-repo dependency.
- `Assets/Characters`: imported packed data, drawing definitions and provenance.

Person, Hound, Blob and Flying retain their distinct anatomy builders. Candidate
creatures retain their rest-space bindings. Shared primitives do not imply that
unrelated skeletons are interchangeable. Legacy raster experiments and archived
preview pages are not copied into the runtime.

## Data and fidelity

Import existing uint16 data without recompression. Convert preview-only numeric
frame arrays to float32 binary without resampling, changing duration or inventing
loop repairs. Keep explicit times, source warnings, authored geometry and license
information. Read two samples directly from packed bytes into caller-owned poses;
do not decode entire animations per actor. Load libraries on demand in the studio.
Unsaved documents deliberately retain their library until saved or closed.

The shared renderer owns no playback clock, window, camera policy or framebuffer.
The host renders into a reusable depth/MSAA target and displays that texture in
ImGui. This permits overlays and resizing without native child-window composition.
Appearance changes and scrub time are editor values, separate from widgets.
Reuse one `CharacterGeometry` workspace per library when drawing game actors
sequentially; actors need their playback/appearance values, not individual large
mesh buffers. The primitive buffers grow only when needed. Anatomy construction
still has temporary CPU allocations, especially for curved hounds and ribbons;
crowd throughput and allocation optimization remain separate from this preview port.

One-shot endpoints, queued changes and sequences retain source playback semantics.
Root motion stays visible for authoring. Game integration will need an explicit
root/contact policy and action-time mapping; neither is inferred by the studio.

## Verification

```powershell
dotnet run --project App2d.CharacterStudio -- --check
dotnet run --project App2d.CharacterStudio -- --smoke artifacts/character-studio
dotnet test App2d.slnx
```

`--check` checks every stored sample/interpolation interval, 4,500 finite geometry
cases covering all 750 clips, playback boundaries, look-file roundtrips and
appearance undo/redo. It compares 91 source reference cases, including altered
proportions, facing, all four weapons, hound contact deformation and flying wings.
Seven head cases compare the original JS control points and quadratic corners;
checks also cover concave fill area, portable JSON, head/weapon undo and save/load,
and custom heads on every person/quadruped clip in both facing directions.
The reference comparison checks all pose points, vertex counts and a distributed
subset of generated vertices. It is not a pixel-for-pixel GPU comparison.

`--smoke` runs the actual native MonoGame/ImGui renderer, writes a screenshot for
each library, then verifies window resizing, user scaling and resetting the font
atlas. It also captures all new weapons and custom heads, including the head editor
on both anatomies, plus Kevin melee, dance, archer, soldier, spellcasting and throwing poses. It writes `dpi-checks.json`, then exits.
Captures use depth testing and an MSAA preview target.
Results from `--check` are also written beside the executable in
`character-studio-checks.txt`; startup exceptions go to `character-studio-error.log`.

The ordinary xUnit suite includes synthetic nonuniform-time and corrupt-data
tests, sequence/endpoint tests, and an allocation check for the packed sampler.

Publish a standalone application folder (requiring the .NET 10 desktop runtime):

```powershell
dotnet publish App2d.CharacterStudio -c Release -o artifacts/CharacterStudio
```

The first release is an audition/appearance tool, not a Blender replacement or a
keyframe editor. Preserve source review warnings as useful information while the
content continues to evolve.
