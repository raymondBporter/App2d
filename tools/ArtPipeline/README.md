# Player art pipeline

The playable character uses the CC0 RGS Dev stick-figure source pack stored at
`Assets/Sources/third-party/rgs-stick-figure`.

From the repository root, run:

```powershell
python -m pip install -r tools/ArtPipeline/requirements.txt
python tools/ArtPipeline/build_runtime_assets.py
```

The build imports and normalizes two baked 512 by 512 character sets:

- `Assets/Content/characters/player-sword`
- `Assets/Content/characters/player-gun`

It also creates the sword and gun HUD icons and imports the pistol projectile.
The two generated player character folders, including their `character.json`
manifests, are ignored by Git and can be rebuilt from the retained source pack at
any time. The generated HUD icons, projectile, and player geometry manifest are
tracked so changes to those runtime assets remain reviewable.

The source-to-runtime mappings and scale live in `import_stick_figure.py`. The importer
measures the generated idle pose and writes
`Assets/Content/characters/player-geometry.json`. Runtime presentation and collision
load the aspect-preserving visual size, foot anchor, collider size, and mirrored
horizontal offset from that manifest.
