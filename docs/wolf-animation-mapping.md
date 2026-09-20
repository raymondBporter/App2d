# Wolf animation mapping

The existing **Quadruped** library includes nine additional motions from
[tomek's wolf](https://opengameart.org/content/3d-wolf-animation-for-game), using
the source page's CC0 option. Existing clips and saved entities keep their IDs
and bindings. The game and Character Studio use the baked data without Blender.

In Character Studio, choose **Entities → Browse source motions → Quadruped**,
then choose **Tomek / Wolf** or search for `Wolf`. Entity actions can select the
same clips through **Action → Source motion**. Open
`Assets/Characters/looks/tomek-wolf.json` for a look with the semantic bindings
already assigned. Bindings are suggestions; they do not replace entity behavior
or combat timing. Fly is the pack's airborne cycle and is suggested for Fall.

| Role | Runtime clip | Source action | Playback |
|---|---|---|---|
| Idle | `tomek_wolf_idle` | `wolf_idle` | Loop |
| Walk | `tomek_wolf_walk` | `wolf_walk` | Loop |
| Run | `tomek_wolf_run` | `wolf_run` | Loop |
| Jump | `tomek_wolf_jump` | `wolf_jump` | One-shot |
| Fall | `tomek_wolf_fly` | `wolf_fly` | Loop |
| Land | `tomek_wolf_land` | `wolf_land` | One-shot |
| Cry | `tomek_wolf_cry` | `wolf_cry` | One-shot |
| Attack | `tomek_wolf_attack` | `wolf_attack01` | One-shot |
| Death | `tomek_wolf_die` | `wolf_die` | One-shot, hold final pose |

`tools/CharacterPipeline/wolf-mapping.json` explicitly maps the source's generic
bone names into the existing 76-point Hound contract. Spine and neck segments
are subdivided, five tail bones provide eight target segments using fixed rest
distances, and paw segments fill the foot controls. Left/right preserve donor
X signs. A single uniform rest-height scale and fixed origin apply to every
clip. The donor proportions remain; no target joint rotations are fabricated.
Ear guides follow the head using fixed rest offsets because this donor has no
ear bones. The Hound drawing does not expose jaw or nose articulation.

The exporter uses the scene's 24 fps and full action ranges (including the
99-frame death), samples evaluated poses at 240 Hz, and records midpoint and
loop seam errors. One-shot endpoints are preserved. Root motion remains in the
points, including jump height and death motion; entity movement must account
for that when binding them to physics-driven actions. No contact IK, per-frame
grounding or loop repair is applied. Attack's maximum measured midpoint error
is approximately 0.0012 model units and is surfaced in the studio.

## Rebuild

The original source is stored unmodified in `Assets/Sources/characters/tomek-wolf`.
Run from the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python tools/CharacterPipeline/export_wolf.py
node tools/CharacterPipeline/import-wolf.cjs
node --test tools/CharacterPipeline/tests/wolf-pack.test.cjs
dotnet run --project App2d.CharacterStudio -- --check
dotnet run --project App2d.CharacterStudio -- --smoke-wolf artifacts/wolf-smoke
```

Export writes an ignored intermediate pack in `Assets/Work/tomek-wolf`; import
replaces just this pack's clips and updates the catalog and provenance. Both
reimporting the wolf pack and refreshing the full sprite-renderer import retain
the other source's clips. The full importer preserves the installed wolf pack,
so Blender is needed only when rebuilding its mapping. Provenance and the exact
mapping are packaged alongside the runtime library.

The wolf smoke command captures all nine motions facing both directions. The
head and bitmap face reflect horizontally with the body, keeping the face
upright when looking left. The studio checks also verify face reflection.
