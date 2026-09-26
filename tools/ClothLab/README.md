# Puppet cape experiment

Interactive cloth study using the existing `PuppetTemplates.StepStudy()` and
`RunStudy()` characters. The exporter calls `PuppetPose.Sample`, including the
project's IK and authored foot contacts, at 120 evenly spaced samples per cycle.
It embeds those poses and the cape simulator into one visualization fragment.
The character's primitives are reconstructed in Three.js from its part definitions.

From the repository root:

```powershell
dotnet run --project tools/ClothLab
node tools/ClothLab/check.cjs
```

The default output is `artifacts/cloth-lab/puppet-cape.html`. Pass an output path
as the first application argument to export elsewhere. The second optional
argument is the template source directory (default `tools/ClothLab`). The HTML is
a fragment intended for the Codex visualization surface; the visualization skill's
`render.py` can wrap it for standalone browser testing. Three.js 0.128.0 loads from
jsDelivr; the poses and simulation are embedded and require no data requests.

## Motion and airflow

- Walking: 0.5 world units / 1.2 seconds = 0.416667 units/second.
- Running: 1.6 world units / 0.72 seconds = 2.222222 units/second.
- Animation rate multiplies both cycle frequency and implied travel speed.
- Relative air points opposite travel, plus an adjustable headwind. Drag uses
  relative particle velocity, with a small procedural lateral flutter term.
- Switching gaits blends sampled poses and speeds over 0.5 seconds. The alternate
  mode walks, runs, then walks on a repeating 12-second schedule.
- Simulation runs at 120 Hz in a translating character frame. Root acceleration
  supplies an inertial force during speed changes. Horizontal root position resets
  never enter the cloth state; shoulder bob/lean remain in the sampled poses.
- The cloth continues across gait and animation-loop boundaries. Pausing freezes
  both character and cape.

## Cloth and rendering

A simulation-vertex slider selects eight grids from 3 × 4 (12 vertices) to
17 × 25 (425 vertices), including the original 9 × 13 grid (117 particles).
Changing resolution resamples positions and velocities without restarting the
animation. These approximate constraints are not calibrated to keep identical
material stiffness across resolutions. Its top row forms a narrow curved neckline,
positioned from the shoulder controls and torso orientation. A flared pattern opens
out from the neckline across both shoulders; all rows below the neckline are free
to drape. The collar covers 225 degrees, extending toward the chest, and the
1.6-unit cape length brings the resting hem near the feet. The shoulder flare
retains its physical height as length changes. Verlet prediction feeds XPBD
distance constraints for stretch, shear, and inexpensive two-edge bending.
Eight constraint passes run per fixed step. These bend constraints and drag
coefficients are artistic approximations, not material-calibrated cloth physics.

Collision uses head/torso and leg capsule proxies, two shoulder supports, four
animated arm capsules (upper arms and forearms), and the floor. The arm collision
toggle allows comparison. Three additional samples along stretch/shear edges help
coarse meshes meet arms between vertices. The torso proxy ends below the collar
so its rounded cap does not obstruct the neckline. Cloth-against-cloth collision
is omitted. Capsule projection is discrete and particle/edge-sample based;
it cannot guarantee triangle-level nonpenetration or continuous contact. The rendered
surface optionally uses bicubic interpolation to 33 × 49 vertices (1,617), with
additional contact projection, one flat red color, and ink on boundaries and
view silhouettes. The smooth rendering grid stays at 1,617 vertices regardless
of physics resolution. Turning off smoothing displays the simulation grid directly.

The prototype renderer lives here rather than in the engine's render loop.
The original character templates and Character Studio remain the source of poses.

## Validation

The C# exporter checks finite pose samples and continuous local loop seams.
`check.cjs` runs 15-second cases for walking, running, disabled airflow, gait
transitions, and extreme control settings. It checks finite/bounded positions,
bounded per-step movement, exact attachments, the authored travel speeds, and
greater trailing distance with running/airflow. This is a stability and behavior
check, not a benchmark or aerodynamic validation. The checks also cover the
lowest/highest resolutions and live remeshing through every slider setting.
Arm checks exercise each capsule, compare animated cloth with arms enabled versus
disabled, and verify the pattern widens from the neckline over the shoulders.
