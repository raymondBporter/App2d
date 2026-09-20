# Point character pipeline review

Reviewed 2026-09-20 against the sibling `sprite-renderer` checkout and current
App2d presentation code. This is a proposed integration and animation mapping;
it does not switch the game renderer or claim visual acceptance of every motion.

## Recommendation

Adopt the sampled-point pipeline for the player and creatures. It fits crisp,
stylized artwork with a small shared motion library. Preserve the exporter’s
XYZ controls and actual depth testing: drawing the same points as flat ordered
2D lines would lose the sword/body occlusion that makes the preview work.

The useful separation is **motion library + anatomy drawing + appearance +
gameplay animation bindings**. Many appearances can share one motion library.
Different anatomies share decoding, playback and rendering primitives, but need
their own geometry bindings. A dinosaur cannot simply use the humanoid drawing.

This preserves fidelity to the approved fixed-camera ink artwork and sampled
motion, rather than reconstructing the original fully surfaced 3D character.

## Measured baseline

The active source pointer is
`sprite-renderer/output/universal/point-library/latest.json`, selecting
`runs/01fb8fb83882db2b`. The packaged runtime pointer selects
`game-assets/stick-figure/c67d96c223e30257`; its library matches the source run.

| Item | Current measurement |
|---|---:|
| Humanoid motions | 250 |
| Stored samples | 60,971 |
| Controls per sample | 24 XYZ points |
| Packed coordinate bytes | 8,779,824 (8.37 MiB) |
| Library JSON on disk | 1,199,932 bytes |
| Full seven-file browser runtime package | 10,009,944 bytes (9.55 MiB) |
| Largest reported midpoint landmark error | 0.107503 export pixels |
| Largest reported quantization landmark error | 0.013422 export pixels |
| Maximum triangles in the humanoid test's exercised cases | 2,452 |

These are storage sizes and sampled landmark checks, not total application RAM,
continuous-time pixel error bounds, or crowd performance measurements.

The browser retains packed bytes and up to 12 decoded clips. Decoding the entire
coordinate library to float32 would add 16.75 MiB. Each humanoid XYZ pose is only
288 bytes; two sampled poses need 576 bytes before appearance/blending scratch.
For a C# runtime, first try sampling directly from packed data: find the two
adjacent timestamps and decode only their 24 controls into reusable pose buffers.
Profile before adding a shared decoded cache; if needed, bound it by bytes.

The browser humanoid renderer reserves 512 KiB of CPU vertex storage and uploads
to a separately allocated GPU buffer. Share reusable geometry buffers across
actors in App2d. Do not allocate a renderer, decoded library, or framebuffer per
actor. Metadata, depth/MSAA surfaces, optional creature face textures, vertex
uploads and runtime allocations still need separate accounting.

## App2d integration

1. **Sampled animation data in Core.** Add a graphics-independent point clip and
   sampler alongside existing animation types. Format v2 stores little-endian
   uint16 XYZ coordinates using `origin[axis] + value * step`, with authoritative
   nonuniform timestamps. Validate version, offsets, lengths, finite metadata,
   increasing timestamps, and endpoint inclusion. Existing texture-frame clips
   are not the right container for adaptive point samples.
2. **Asset loading and bindings in Game.Presentation.** Load immutable shared
   libraries and resolve semantic roles (idle, fall, wall attack, etc.) through
   an explicit per-anatomy/equipment map. Keep clip IDs and appearance settings
   out of simulation. Validate required roles when loading an appearance.
3. **Geometry rendering in Rendering.** Port the depth-tested line/capsule,
   rounded torso, domed head, face and rigid weapon-plane behavior from
   `spin-renderer.js` and `point-depth-renderer.js`. Use reusable triangle buffers,
   unlit colors, consistent ink width, and antialiasing at device resolution.
   Curves and head tessellation must remain adequate at supported camera zooms.
4. **Ordered scene integration.** `Renderer2D.Flush` currently disables depth and
   every vertex has Z=0. `GraphicsSurface2D` also requests `DepthFormat.None`,
   although it already requests 4x MSAA. Add a depth-capable path and flush when
   switching between sprite and character materials. Preserve `Scene2D` ordering
   while resolving depth inside each character. A correctness-first option is
   to clear only depth between ordered character submissions, preserving color;
   benchmark that before choosing depth ranges or another batching strategy.
   The browser's whole-canvas color/depth clear must not be copied into each game
   character draw. Restore the state needed by terrain, effects and HUD.
5. **Preserve observational presentation.** Replace texture selection in
   `PersonPresentation2D` with semantic role and pose sampling. Continue taking
   position, facing and attack phase from immutable observations. Apply the same
   view architecture to `EnemyPresentation2D`, including Rival's shared person
   view. Keep physics, damage and rollback independent of artwork.

Use the library pivot and one standing-height calibration, not per-clip fitted
bounds. The humanoid source has Y down and depth positive away; App2d world Y is
up. The source's two-unit standing height must be converted to the game's chosen
standing height in world units. Culling must include animated limbs, weapons and
appearance changes, not just the collision rectangle.

Root motion is retained in the exported points. Choose and validate an in-place
policy for physics-driven movement before using jumps, dodges or lunges. Do not
add baked travel on top of simulated travel or remove all body bob by blindly
subtracting a moving torso point. The present format does not supply a separate
gameplay root-motion track; author/export that information where necessary.

`TraversalMetrics2D.GunMuzzleOffset` still derives from the old sprite's fixed
512px socket. The point rig supplies sword grip/tip/width controls but no general
gun/shield attachment contract. Author those sockets and reconcile their visual
placement with the configured simulation muzzle and shield. A moving visual
socket must not silently become the authority for projectile or hit timing.

## Proposed player mappings

All mappings below are candidates pending in-game visual review. Existing App2d
roles are shown verbatim. Equipment can choose different candidates for the same
role; sword support in `hands` is capability metadata, not an instruction to draw
a sword on an unarmed actor.

| Current role | Candidate source clip(s) | Required treatment |
|---|---|---|
| `idle` | `idle`, `sword_idle`, `pistol_idle`, `kaykit_melee_unarmed_idle` | Select per equipment; charging currently holds the gun idle's first pose. |
| `walk` | `walk`, `jog`, `sprint` | Match gait to current movement speed; start with one approved gait. |
| `jump-start` | `jump_start`, `kaykit_jump_start` | Review takeoff timing and retained root travel; current role lasts throughout ascent. |
| `fall` | `jump`, `kaykit_jump_idle` | No dedicated fall ID; audition airborne candidates or author a distinct descending pose. |
| `land` | `jump_land`, `kaykit_jump_land` | Preserve the current terminal-velocity landing trigger; fit recovery deliberately. |
| `wall-grip` | `wall_grip` | Reconstructed hold exists; authored wall is on the left, so verify facing and contact alignment. |
| `climb` | `climb` | Explicit placeholder; verify rung spacing and contacts, pause when hanging still. |
| `dash` | `kaykit_dodge_forward`, `ual2_sword_dash`, `ual2_shield_dash` | Different actions and durations; choose a pose that fits the game's dash and remove duplicate travel. |
| `hit-a` | `hit_chest`, `hit_head`, `kaykit_hit_a` | Audition the reaction and interruption behavior. |
| `death` | `death01`, `kaykit_death_a` | Hold the final pose; preserve respawn/reset behavior. |
| `sword-attack` | `sword_attack`, `ual2_sword_regular_a` | Align visible contact to authoritative attack phase; the latter has no authored trail window. |
| `wall-sword-attack` | `wall_sword_attack` | Explicit placeholder, 0.35 seconds; verify wall-hand/foot contact. |
| `sword-down-attack` | `sword_down_attack` | Explicit placeholder, 0.25 seconds; verify downward pose and trail pre-roll. |
| `magic-shot` | `pistol_shoot` (gun), `spell_simple_shoot` (magic) | Choose by actual equipment behavior; gun attachment is additional work. |
| `wall-shot` | No dedicated clip | Needs authored wall-compatible shooting or a contact-preserving upper-body treatment. |
| `shield-block` | `kaykit_melee_blocking`, `ual2_idle_shield_loop` | Held loops exist; shield artwork/socket still required. |
| `punch` | `punch_jab`, `punch_cross`, `kaykit_punch` | Choose and fit contact phase; hide equipment. |
| `kick` | `kaykit_melee_unarmed_attack_kick` | Fit contact phase; hide equipment. |
| `balance-left-foot`, `balance-right-foot` | `balance_forward`, `balance_backward`, possibly `teeter` | First two are placeholders. Resolve by edge direction and support foot in both facings; no name-only left/right swap. |

App2d selects balance using `BalanceDirection * Facing`, whereas the new metadata
describes platform side. Wall-side conventions also need explicit translation.
Five clips are flagged as placeholders: climb, wall sword attack, downward attack,
and both balance clips. No dedicated fall or wall-shot clip is present.

Two ranged aiming loops have large source seams (63.74 and 79.81 export pixels).
Do not choose them as default idle loops without repair or deliberate pose holds.
Authored slash windows currently exist for only five attacks. They are decorative
and must not replace gameplay hit windows. Uniformly rescaling a full attack is
not sufficient if its contact moment lands outside the gameplay active phase;
bindings may need explicit windup/contact/recovery timing.

## Creatures

| Anatomy | Available motion | Integration status |
|---|---|---|
| Person | 250 clips | Only anatomy in the current game export; suitable for player and Rival. |
| Quadruped/hound | 12 wolf actions | Separate geometry/appearance binding; 240 Hz source sampling; experimental preview. |
| Blob | 9 actions | Separate geometry binding; preview only. |
| Flying | 8 actions | Separate geometry binding, including wings; preview only. |
| Candidate inventory | 11 distinct bases, 59 clips | 2.79 MiB packed plus metadata; generic audition drawings, not finished game types. |

The inventory report says 28 of 59 clips meet its current midpoint/seam checks.
The other 31 remain review candidates, including large Rat/Run and Spider/Death
outliers. Passing decoder tests does not remove these source/sampling flags.
Creature faces currently include small image assets; retaining those is compatible
with low RAM because they are shared faces, not per-frame animation textures.

Current gameplay enemies are Shieldback, GreenDinosaur, BoilerBrute, Rival and
TumbleProp. Anatomy and behavior should be separate: use Person for Rival;
audition a dinosaur drawing for GreenDinosaur; choose Shieldback's body deliberately;
and give BoilerBrute a suitable heavy humanoid action plus hammer attachment.
`kaykit_large_melee_2h_slam` is a candidate motion, not a finished hammer conversion.
TumbleProp can keep its existing rigid geometry.

Extend packaging to include each selected creature's motion, geometry binding,
appearance defaults, optional faces and provenance. Copy versioned runtime data
into App2d content; do not depend on sibling checkout paths at runtime.

## First implementation slice and acceptance

Start with one player and one Rival sharing the humanoid library. Prove idle,
walk, a depth-sensitive sword spin, attack, wall grip and both facings before
switching every role. Then resolve the player gaps and port one non-humanoid
anatomy to verify the shared architecture.

Acceptance should cover parity with the browser at matched pose times; internal
occlusion and actor/terrain ordering; ground/wall/ladder alignment; held endpoints;
mid-action snapshot attachment; resize/device recreation; and supported zooms.
Measure CPU time, GPU time, allocations and resident CPU/GPU memory at 1, 25 and
100 visible actors, including mixed clips and appearances. Those crowd counts are
proposed test points, not established capacity. Keep fidelity settings unchanged
while measuring the baseline.

Validation run during this review:

- `node scripts/test_point_library.cjs`: passed all 250 depth motions and 60,971
  samples, provenance, interpolation, rigid swords, endpoints and generated meshes.
- `node scripts/test_point_player.cjs`: passed packed sampling and playback checks.
- `node scripts/test_creature_inventory.cjs`: passed 64 source clips / 59 distinct
  clips and 1,728 finite drawing cases.

These checks validate the existing exporter/runtime mechanically. No App2d renderer
port, GPU parity capture, crowd benchmark or full-catalog visual review was performed.
