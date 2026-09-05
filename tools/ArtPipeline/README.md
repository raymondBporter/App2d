# Runtime asset pipeline

`Assets/Runtime` is disposable output. The build copies curated files from
`Assets/Static`, imports source packs from `Assets/Sources`, validates required files,
and writes a size/hash manifest before replacing the previous runtime tree.

From the repository root, run:

```powershell
python -m pip install -r tools/ArtPipeline/requirements.txt
python tools/ArtPipeline/build_runtime_assets.py
```

The build imports and normalizes three baked 512 by 512 character sets from the CC0
RGS Dev stick-figure source pack:

- `Assets/Runtime/characters/player-sword`
- `Assets/Runtime/characters/player-gun`
- `Assets/Runtime/characters/player-unarmed`

It also creates the sword, gun, and unarmed HUD icons and imports the pistol projectile.
It also imports the Maaot cave tilesets. All generated output, including character
manifests, HUD icons, projectile, player geometry, terrain slices, and
`content-manifest.json`, is ignored by Git and reproducible from durable inputs.

The pipeline also imports the CC0 Kenney Pixel Platformer archive as the
`kenney-grassland` tileset. Its original 18 by 18 tiles are normalized to App2d's
32-unit semantic terrain interface without depending on the source atlas layout.

The source-to-runtime mappings and scale live in `import_stick_figure.py`. The importer
measures the generated idle pose and writes
`Assets/Runtime/characters/player-geometry.json`. Runtime presentation and collision
load the aspect-preserving visual size, foot anchor, collider size, and mirrored
horizontal offset from that manifest.

The pipeline builds in `Assets/Work` and replaces `Assets/Runtime` only after success.
Deleting `Assets/Runtime` and rerunning the command is the supported clean rebuild.
