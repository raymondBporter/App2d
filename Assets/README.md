# Assets

Every non-code game asset belongs under this directory, including images, audio,
text, fonts, level data, and source files used to produce runtime content.

The first folder describes the asset lifecycle:

- `Static` contains curated, runtime-ready inputs that the pipeline copies without
  transforming. This directory is durable and committed.
- `Sources` contains original and third-party inputs with their licenses and
  provenance. Importers transform these into runtime assets.
- `Characters` contains the character libraries and the durable documents under
  `authored`: models, variants, animations, props, and entities edited in Character
  Studio, plus JSON model recipes in `authored/templates`. Recipes reference existing model/clip IDs and populate Studio's
  New menu; see [Character resources](../docs/character-resources.md). Source libraries and their provenance also live here. These are separate
  from generated sprite animation folders in `Runtime/characters`.
- `Runtime` is the disposable Debug resource tree, automatically populated from
  committed `Static` files by normal builds. Release builds and publishes package
  `Static` directly. Asset generation is only needed by asset authors; generated
  redistributable outputs are baked into `Static` and shared through Git.
- `Work` contains regenerable output: pipeline staging, previews, validation
  reports, and caches. It is ignored by Git, so nothing durable may live there.

## Choosing a home for new content

| Content | Canonical source location | Shared game location |
| --- | --- | --- |
| Levels and zone definitions | `Static/levels/<id>` | `AssetLocations.Levels` |
| Music, manifests, and licenses | `Static/audio/music` | `AssetLocations.Music` |
| Character models, clips, props, and entities | `Characters/authored` | `AssetLocations.AuthoredCharacters` |
| Imported character motion libraries | `Characters/<id>` | `AssetLocations.CharacterLibrary` |
| Curated and baked sound effects | `Static/audio/sfx` | `AssetLocations.SoundEffects` (generated copy) |
| Tilesets and UI images | `Static` or an importer from `Sources` | `AssetLocations.Tilesets` / `UI` (generated output) |
| Third-party originals, licenses, source art | `Sources` | Build tools only |
| Previews, experiments, intermediate files | `Work` | Build tools only |
| Player saves and editor preferences | User's local application data, outside this tree | `UserDataLocations` |

The path definitions live in `App2d.Core/Assets/AssetLocations.cs`; the game host
resolves them once through `AssetPaths.Current`. Character Studio uses the same
module for discovery. Loaders receive roots from their caller rather than searching
upward or choosing a new resource location themselves. Keep asset-specific filenames
and format rules beside their loader; this registry names the major locations.

When adding content, choose a lifecycle folder above first. Hand-authored work goes
in a committed source location, never only in `Runtime`, `Work`, or a `bin` output.
Add a new top-level category deliberately: document it here and add a named location
when application code needs it. Paths resolve without creating directories or moving
assets. Explicit export destinations selected by the user remain valid.

Game Debug builds read `Runtime` art, `Static` authored content, and sibling
`Characters` documents in a checkout. Packaged games use only their executable's
`Assets`, with character documents under `Assets/PointCharacters/authored`.
Character Studio preserves its packaged-first lookup at `Assets/Characters`, then
searches above its working directory. That means a Studio run against copied assets
edits those copies; check the selected document path when intending to edit sources.

For single-file document saves use `App2d.Core.IO.AtomicFile`: it creates the parent,
writes a unique sibling temporary file, and replaces the destination after writing.
Validation and serialization remain in the asset loader/editor. SQLite levels keep
their database transaction handling. This helper is not a multi-file transaction.

## Building and packaging

A normal Visual Studio build prepares `Runtime` from committed `Static` resources,
including on a fresh clone. Release builds and publishes package `Static` directly.
No Python or downloaded cave packs are required. Deleting `Runtime` is safe; the
next build recreates it.

Asset authors can optionally run `tools/setup.ps1`. The pipeline stages imported art
and generated effects, validates the result, and updates the baked outputs in
`Static` for committing. See `tools/ArtPipeline/README.md`. Always commit generated
outputs alongside the inputs or scripts that changed them.

Runtime content is organized by game concept rather than file format. Asset IDs
use lowercase letters, digits, and hyphens. A canonical ID and its folder name are
the same: the `walk` animation lives at `animations/walk`, and the
`ink-medieval-ground` tileset lives at `tilesets/ink-medieval-ground`.

Levels live at `levels/<id>/level.db` — one SQLite file per level, holding the tile grid,
thing definitions, placed instances, and their typed pieces. They are durable authored content, so they are committed under
`Static` and read from there directly in Debug builds; the pipeline copies them into
`Runtime` like any other static asset. The `.db-wal` and `.db-shm` files SQLite leaves
alongside are transient and are not committed.

`levels/<id>/zones.json` adds reusable rectangular world regions beside a level.
`audio/music/soundtrack.json` binds those IDs to music; cue folders contain Ogg
stems, their playback manifests and licenses. Selected music is durable under
`Static/audio/music`, so playing the game never requires Music Lab or its ignored
working files. See [music and zones](../docs/music-and-zones.md).

Character animation folders contain contiguous four-digit files beginning with
`frame-0001.png`. The adjacent `character.json` records timing and looping but
does not repeat folder paths. `characters/player-geometry.json` is generated from the
current player poses and records aspect-preserving visual and collision geometry.
Similarly, a tileset manifest records dimensions;
conventional paths such as `surfaces/top.png` and `corners/outer.png` carry their
own meaning.

Source pack names and production history belong in provenance notes, never in
runtime IDs. Promote runtime-ready files into `Static` and add an importer for source
files that require processing. Experiments that did not ship do not belong in the
repository; keep them under `Work` or outside the tree.
