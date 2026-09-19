# Assets

Every non-code game asset belongs under this directory, including images, audio,
text, fonts, level data, and source files used to produce runtime content.

The first folder describes the asset lifecycle:

- `Static` contains curated, runtime-ready inputs that the pipeline copies without
  transforming. This directory is durable and committed.
- `Sources` contains original and third-party inputs with their licenses and
  provenance. Importers transform these into runtime assets.
- `Runtime` is the complete generated game-facing tree. Debug reads it directly;
  Release builds and publishes package it as `Assets`. It is ignored and disposable.
- `Work` contains regenerable output: pipeline staging, previews, validation
  reports, and caches. It is ignored by Git, so nothing durable may live there.

From a clean clone, run `tools/setup.ps1` from the repository root before starting the
game; see `tools/ArtPipeline/README.md`. The pipeline stages a fresh tree, copies
`Static`, runs every importer from `Sources`, validates required assets, writes
`Runtime/content-manifest.json` with file sizes and SHA-256 hashes, and only then swaps
the completed tree into place. A failed build leaves the previous `Runtime` untouched.

Delete `Runtime` whenever you want a clean checkout-like state; running the pipeline
recreates it. Durable or hand-edited files must never live only in `Runtime`.

Runtime content is organized by game concept rather than file format. Asset IDs
use lowercase letters, digits, and hyphens. A canonical ID and its folder name are
the same: the `walk` animation lives at `animations/walk`, and the
`dark-cave` tileset lives at `tilesets/dark-cave`.

Levels live at `levels/<id>/level.db` — one SQLite file per level, holding the tile grid,
thing definitions, placed instances, and their typed pieces. They are durable authored content, so they are committed under
`Static` and read from there directly in Debug builds; the pipeline copies them into
`Runtime` like any other static asset. The `.db-wal` and `.db-shm` files SQLite leaves
alongside are transient and are not committed.

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
