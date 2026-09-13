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
`build_gun_effects.py` then bakes the blue bolt, muzzle charge and flash textures,
61 continuous radial HUD frames, and four deterministic PCM sound cues. Run that
script by itself to iterate on these assets.
The charging muzzle glow uses 24 pre-baked shimmer frames at 30 fps (0.8 seconds),
with a breathing core, rotating wisps, and small orbiting glints. Playback uses
simulation time, starts with charging, and stops on automatic fire or cancellation.
The charge sound lasts 0.6 seconds, matching `GunPersonWeapon2D.ChargeSeconds`;
playback uses a fixed rate and its voice stops immediately on cancellation or fire.
The standing pistol muzzle socket is pixel (418, 206) on the normalized 512px canvas.
The weapon converts it through the player geometry manifest; presentation holds that
pose while charging and through the flash before playing recoil.
The bolt's thin ghost trail covers 1/30 second of flight with a 1.35x visual stretch
(about 56 world units at the current speed). It grows from the muzzle, remains
separate from collision geometry, and fades in place over 0.05 seconds on impact.
Eight pre-baked opacity variants avoid generating or modifying textures during play.
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

Authored Blender character clips are imported after the pre-rendered character
pack. The two eight-frame sword balance clips live in
`Assets/Sources/characters/player-sword`, with an editable `.blend`, a render
recipe, and cached transparent PNGs. See that folder's `README.md` for the
original pack's camera/NLA settings, comparison results, and editing workflow.

After editing and saving that Blender source, run:

```powershell
& .\tools\ArtPipeline\render_sword_balance.ps1 -BuildRuntime
```

This renders the saved actions and runs the normal asset build. Normal builds
consume the cache without launching Blender, verify its source/frame hashes,
and apply the same scale and anchor transform as the pre-rendered art. The
one-time `create_sword_balance.py` authoring script must not be used for ordinary
re-renders because it regenerates the initial poses.

The airborne downward sword attack has its own editable source and recipe in
`Assets/Sources/characters/player-sword/downward-attack`. Re-render saved edits
with `render_sword_attack.ps1 -BuildRuntime`. The importer includes the parent
sword recipe and immediate child recipe directories; the attack is six frames,
0.25 seconds, non-looping, and uses the same canvas normalization as the balances.
It starts in the downward stab pose without a wind-up; the slash flash spans frames 1–2.
