# Character workshop

Create and animate simple characters without importing a 3D model or motion library:

```powershell
dotnet run --project App2d.CharacterStudio -- --workshop
```

The existing studio also has a **Create / animate** button. **Imported characters**
returns to its existing entity and motion-library workflow, retaining workshop edits.
The workshop can start without `Assets/Characters`; it does not need Blender or
an installed character library.

## First exercise

1. Choose **New → Walk loop study**. Play or scrub the repeating motion. **Loop**
   repeats it; **Follow** keeps the character in view while the ground passes by.
   Use **Speed** to inspect the gait in slow motion.
2. Select `right-foot`: its green contact cross stays in world space while the body
   moves. The floor ticks make travel visible. Later, `left-foot` becomes support.
3. Adjust **Character travel → World X / Y** at a chosen time. The change creates a
   key pose. Reachable planted feet stay fixed, with both IK bone lengths preserved.
4. Move a contact target out of reach. The cross turns orange and the inspector
   reports the remaining distance. The leg does not silently stretch.
5. Drag a hand to pose its two-bone arm. Each chain has an explicit bend direction.
   A solved elbow/knee is a guide; edit the tip or reverse the chain's bend instead.

The shoulder and hip cross-sections share a 30-degree turn toward the camera from
a right-facing profile. Foot paths and planted targets follow the hip offsets.
The shoulder sockets sit slightly behind the anatomical torso center: the left
projects to the near side, and the right sits behind the torso in depth. The arms
swing in opposition. This is a contact/authoring study, not a
finished walk animation or terrain controller.

## Build a character

**New** offers an empty character, a stick figure and the contact study. **Build**
edits rest positions and proportions. Give a control a unique name and choose
**Add control**, or select a parent and **Connect** to create a bone and its stroke.
Attach an **Ellipse** or **Box** to the selected control. Shapes can point toward
another control, carry a local offset, and have their own depth and expression.

Controls, bones and drawing parts are separate. Delete a drawing part to retain
its controls. Deleting a control removes its incident bones, attached parts,
affected IK chains and contact intervals, and its values in every key. Other
controls remain. No head, hand, leg, person or quadruped vocabulary is required.
The headless character smoke case exercises this directly.

Two connected bones can become an IK chain: select root, middle and tip in the
**Two-bone IK** section. Rest XY distances determine their lengths. Chains cannot
share solved controls or nest beneath another solved chain. Other bones provide
relationships for moving descendants; they are not universal length constraints.

Depth is a drawing coordinate: positive is farther away. Projection is
orthographic. IK operates in XY and retains depth for drawing. This is not a 3D
rotation or perspective authoring system.

## Animate

**Animate** edits motions. Drag a control or adjust character travel to create/update
a key at the current time. Moving an ordinary parent moves its descendants; moving
an IK tip solves its two bones. **Key pose** captures the complete evaluated pose.
Click a key to seek; right-click to remove it. **Ghost keys** shows the neighboring
poses. Change **Seconds** to retime the entire motion, including its contact intervals.
**Loop** is saved with the motion. Repeating playback carries travel forward instead
of teleporting the figure back to the start. **Follow** tracks horizontal travel;
turn it off to inspect displacement against the stationary floor. Scrubbing or
restarting returns to the first cycle. The endpoint remains inspectable when paused.

Select an IK tip and **Plant here** to record a world target and contact interval.
Edit its start/end times or world target in the inspector. A planted control is
edited through that target, not by dragging through its constraint. Targets are
explicit: the floor is a visual reference, not automatic terrain detection.
Contacts for a given tip cannot overlap or share an endpoint. Different feet can
have simultaneous contacts. Unreachable contacts produce a visible residual.

Build changes affect rest geometry/IK lengths; existing key poses keep their
authored coordinates. There is no automatic proportion retargeting yet.

## Files and model authoring

Save/Open uses versioned `app2d-puppet` JSON (suggested suffix `.puppet.json`). All
coordinates, keys, constraints and parts are readable and editable outside the
UI. Unknown fields, missing references, bone cycles, invalid dimensions and
ambiguous contacts are rejected before a file replaces the current document.
Undo/redo groups continuous widget drags; closing/replacing a dirty document offers
to save it. The original imported libraries and entity files use their existing
formats and are not rewritten by the workshop.

`PuppetDefinition` in Core owns the independent definition. `PuppetPose` evaluates
key interpolation, travel and contacts without graphics. `PuppetDrawing` consumes
that evaluated pose using the existing depth-tested renderer. This provides an
entry point for later gameplay integration rather than coupling motion sources to
the renderer.

Current limits: workshop files are not yet game entity types; collision editing,
terrain/edge adaptation, animation blending, attack windows and ragdolls are not
integrated here. Existing imported-entity action timing and collision tools remain
available. The next integration should use the same evaluated pose for drawing,
attachments and gameplay geometry, rather than sampling a second independent pose.

## Verification

```powershell
dotnet test App2d.Tests --filter FullyQualifiedName~PuppetAuthoringTests
dotnet run --project App2d.CharacterStudio -- --smoke-workshop artifacts/workshop-smoke
```

The tests cover blank/headless definitions, strict file round trips, contact stability
between keys, bone lengths, bend direction, unreachable targets, deletion, topology,
travel and primitive geometry. The smoke run captures Build, two contact poses,
headless/empty characters and an unreachable contact for visual inspection, followed
by 48 viewport frames spanning two walk cycles.
