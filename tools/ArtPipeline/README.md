# Runtime asset pipeline

Playing the game requires no asset tools: normal Visual Studio builds copy the
committed runtime-ready resources from `Assets/Static` into `Assets/Runtime`, and
Release/publish packages `Static` directly. Pull changes and press Run.

## Optional authoring build

Only asset authors need Python and Pillow. From the repository root:

```powershell
.\tools\setup.ps1
```

This creates an ignored `.venv`, installs Pillow, and regenerates imported art and
procedural effects. The pipeline validates a staged tree before replacing Runtime,
then promotes generated player/dinosaur sprites, geometry, gun effects, HUD images,
and gun/healing sounds into `Assets/Static`. Commit these baked files with source
changes. Contributors receive the results without running the pipeline.

The unused Maaot cave packs are no longer imported or required.

## What the build produces

Each step is a standalone script that takes `--content-root`, so any one can be re-run
against `Assets/Runtime` while iterating.

| Script | Output under `Assets/Runtime` | Source |
| --- | --- | --- |
| `import_stick_figure.py` | `characters/player-sword`, `player-gun`, `player-unarmed`, `characters/player-geometry.json`, `ui/hud/weapons/*.png`, `effects/bullet/orange.png` | CC0 RGS Dev stick-figure pack in `Sources/third-party/rgs-stick-figure` |
| `import_blender_character.py` | Authored sword clips (balance and downward attack) added to `characters/player-sword` | Cached Blender renders in `Sources/characters/player-sword` |
| `build_gun_effects.py` | `effects/gun/*`, `ui/hud/gun-charge/*`, `audio/sfx/gun-*.wav` | Procedural; design notes are in the script's docstring |
| `build_spell_audio.py` | `audio/sfx/gun-charge.wav`, `audio/sfx/heal-*.wav` | Procedural; uses `Static/gameplay/player-spells.json` durations after the gun bake |
| `import_green_dinosaur.py` | `characters/green-dinosaur` | `Sources/user/green-dinosaur/walk-cycle.png` |

`import_stick_figure.py` owns the shared character normalization: it scales and
root-aligns the 512 by 512 source frames, then measures the idle pose to write
`characters/player-geometry.json`. Presentation and collision read the visual size,
foot anchor, and collider from that manifest. Every character clip that enters the
runtime, authored or imported, goes through the same transform so anchors line up.

Static audio comes from CC0 Kenney packs; `Sources/third-party/kenney-audio` holds the
licenses and a manifest mapping each runtime cue to its source file.

## Authoring new sword animations in Blender

The player's sword clips beyond the pre-rendered pack are authored in Blender and
cached as PNGs so ordinary builds never launch Blender. The editable sources, render
recipes, and cached renders live in `Assets/Sources/characters/player-sword`; that
folder's `README.md` covers the original pack's camera and NLA setup and the pose
editing workflow.

After editing and saving a `.blend`, re-render and rebuild with:

```powershell
.\tools\ArtPipeline\render_sword_balance.ps1 -BuildRuntime   # sword-balance.blend
.\tools\ArtPipeline\render_sword_attack.ps1 -BuildRuntime    # downward-attack/sword-downward.blend
```

Both wrap `render_blender_character.py`. The importer verifies source and frame hashes
and fails the build if a `.blend` changed without a re-render. `create_sword_balance.py`
and `create_downward_sword.py` are the one-time scripts that authored the initial
poses; do not run them for re-renders because they regenerate the poses.

`preview_sword_balance.py` and `preview_downward_sword.py` write review GIFs, contact
sheets, and pixel checks under ignored `Assets/Work/previews/player-sword`.

## Tests

```powershell
.\.venv\Scripts\python -m unittest discover -s tools/ArtPipeline/tests
```
