# Runtime asset pipeline

`Assets/Runtime` is disposable output that the game reads in Debug and packages in
Release. This pipeline rebuilds it from the durable inputs described in
`Assets/README.md`: curated files under `Assets/Static` and original or third-party
inputs under `Assets/Sources`.

## Building

From the repository root:

```powershell
.\tools\setup.ps1
```

`setup.ps1` creates an ignored `.venv`, installs Pillow (the only dependency), checks
for the Maaot cave packs, and runs `build_runtime_assets.py`. Once the environment
exists you can also run the build directly:

```powershell
.\.venv\Scripts\python tools/ArtPipeline/build_runtime_assets.py
```

The build stages a fresh tree under `Assets/Work`, copies `Assets/Static`, runs each
importer below, validates required files, writes `Runtime/content-manifest.json` with
sizes and SHA-256 hashes, and only then swaps the new tree into `Assets/Runtime`. A
failed build leaves the previous `Runtime` untouched. Deleting `Assets/Runtime` and
rebuilding is the supported clean rebuild. `App2d.csproj` refuses to build when the
manifest is missing and prints the command to run.

### Inputs that are not in git

Maaot's license forbids redistributing the cave packs, so every clone must download
them once and save them as:

- `Assets/Sources/third-party/maaot/dark-cave.zip` from
  [2D DarkCave Assets](https://maaot.itch.io/2d-browncave-assets)
- `Assets/Sources/third-party/maaot/mossy-cavern.zip` from
  [Mossy Cavern](https://maaot.itch.io/mossy-cavern)

The build stops with these instructions before doing any work if either is missing.
Every other input is committed.

## What the build produces

Each step is a standalone script that takes `--content-root`, so any one can be re-run
against `Assets/Runtime` while iterating.

| Script | Output under `Assets/Runtime` | Source |
| --- | --- | --- |
| `import_stick_figure.py` | `characters/player-sword`, `player-gun`, `player-unarmed`, `characters/player-geometry.json`, `ui/hud/weapons/*.png`, `effects/bullet/orange.png` | CC0 RGS Dev stick-figure pack in `Sources/third-party/rgs-stick-figure` |
| `import_blender_character.py` | Authored sword clips (balance and downward attack) added to `characters/player-sword` | Cached Blender renders in `Sources/characters/player-sword` |
| `import_maaot_caves.py` | `environments/tilesets/dark-cave`, `mossy-cavern` | Maaot zips (see above) |
| `import_kenney_pixel_platformer.py` | `environments/tilesets/kenney-grassland` | CC0 `Sources/third-party/kenney/pixel-platformer.zip` |
| `build_gun_effects.py` | `effects/gun/*`, `ui/hud/gun-charge/*`, `audio/sfx/gun-*.wav` | Procedural; design notes are in the script's docstring |
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
