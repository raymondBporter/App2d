# Kevin Iglesias free human animation collection

Downloaded from the creator's official itch.io pages on September 20, 2026.
The eight packs cover basic movement, melee, archery, crafting, dance, soldier,
spellcasting and throwing. Both masculine and feminine variants are retained.

## Sources and license

`assets/kevin-iglesias/sources.json` records official pack URLs, original archive
sizes and SHA-256 checksums, plus hashes of Blender scenes and included manuals.
Original archives are at `assets/kevin-iglesias/<pack>/source.zip`; extracted FBX
files and PDFs are in `source/`, and authoring scenes in `blender/`.

These assets use the **Standard Unity Asset Store EULA**, including downloads
from itch.io. They are not CC0. The author's embedded README states royalty-free
commercial use, no resale, and optional attribution. The original manuals and
embedded README text are preserved; see the author's
[license information](https://www.keviniglesias.com/#license).
Source binaries are excluded from Git. Do not publish the raw collection or
an independently reusable motion library as an asset download.

## Import and playback

The pipeline reads clip metadata from the included `.blend` files and motion
from the author's baked FBX exports. Embedded scripts remain disabled. FBX bones
are centimetre-sized with a 0.01 object scale; both rest and posed translations
are normalized by the same measured leg-length ratio. This avoids re-evaluating
the authoring rig's cyclic hand constraints during random-access sampling.
The runtime receives only
the same 24 XYZ points used by the existing human player, with adaptive sampling
starting at 120 Hz; no sprite sheets are generated.

`inspect_kevin_source.py` inventories the authoring rigs and action properties.
`import_kevin_catalog.py` adds 412 named clips to `config/point-library.json`,
bringing the human catalogue to 662. Repeated action names across packs are
listed once per M/F variant; all original files remain available, and the
338 skipped pack entries are recorded in `catalog-report.json`. This is a count
of named clips including root-motion variants and poses, not 412 distinct attacks.

| Pack | Added clips after shared-name deduplication |
| --- | ---: |
| Basic movement | 116 |
| Melee | 58 |
| Archer | 38 |
| Spellcasting | 42 |
| Throwing | 26 |
| Crafting | 50 |
| Dance | 24 |
| Soldier | 58 |

These counts include 206 masculine and 206 feminine clips, with 64 root-motion
variants across the collection. Shared movement clips are assigned to Basic first.

Loop flags come from the author's `animation_loop`. Playback limits and duration
come from the packaged FBX key times: a few Blender `animation_end_frame` values
differ from the baked release files. Unspecified FBX timebases use Blender's
25 fps import fallback while preserving the exact duration in seconds.
Blender authoring action bounds include extra editing keys and must not be used
as playback limits. See the author's
[Blender file guide](https://www.keviniglesias.com/animationBlenderFiles.html).

`kevin_point_source.py` maps rest-relative deform-bone rotations onto the existing
Universal reference proportions, then uses the existing cartoon human projection.
The source rest-ankle reference preserves jumps and vertical motion.
Original X depth and hand-prop orientation survive in the point data.
Legacy FBX 7.3 poses are normalized for their object rotation and pose-to-bind
unit ratio before retargeting; this keeps archer, soldier, spellcasting and
throwing motions upright at the same size as the newer packs.

The preview has Kevin filters for the whole collection and each pack. M/F denotes
the author's movement variants. Bow, gun, shield, thrown-object and crafting-tool
props are not yet drawn; those clips currently preview body motion. Melee sword
and polearm motions can use the existing generic weapon display. Retargeting to
cartoon proportions can require later contact adjustments for two-handed props.

`test_kevin_retarget.py` checks sample motions from all eight packs in forward and
reverse seek order, including both attack hands and unspecified FBX timebases.
It also checks evaluated standing poses from both variants of the four legacy
packs for upright orientation, scale and ground placement.
The shared `test_point_library.cjs` checks the complete packed result, interpolation,
weapon anchors, runtime geometry, playback, and source checksums.

## Rebuild

After obtaining the free Godot/Unreal archives through each official pack page:

```powershell
python scripts/catalog_kevin_sources.py
& $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/inspect_kevin_source.py
python scripts/import_kevin_catalog.py
& $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/inspect_kevin_fbx.py
python scripts/import_kevin_catalog.py
./render.ps1
```

The normal point export uses the saved baked FBX motions directly. It does not need
Unity, an Adobe account, a mesh skin, or an intermediate raster render.
