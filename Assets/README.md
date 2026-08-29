# Assets

Every non-code game asset belongs under this directory, including images, audio,
text, fonts, level data, and source files used to produce runtime content.

The first folder describes the asset lifecycle:

- `Content` is the only shipping boundary. The game project packages everything
  below it into the executable's `Assets` directory. Small selected assets and
  manifests are committed; reproducible character frames, equipment layers, and
  sparse packages are ignored and rebuilt locally.
- `Library` contains useful, reviewed alternatives that are not currently shipped.
- `Sources` contains original and third-party inputs with their licenses and
  provenance.
- `Work` contains regenerable output, previews, validation reports, rejected
  attempts, and caches. It is ignored by Git.

From a clean clone, run `python tools/ArtPipeline/build_runtime_assets.py` from the
repository root before starting the game. Its inputs are the committed GLB/glTF source
models, plans, licenses, and `Content/characters/player/character.json`. Alternate FBX,
OBJ, and MTL copies from the source packs are intentionally not versioned because the
pipeline does not consume them.

Runtime content is organized by game concept rather than file format. Asset IDs
use lowercase letters, digits, and hyphens. A canonical ID and its folder name are
the same: the `walk` animation lives at `animations/walk`, and the
`rust-cyberpunk` tileset lives at `tilesets/rust-cyberpunk`.

Character animation folders contain contiguous four-digit files beginning with
`frame-0001.png`. The adjacent `character.json` records timing and looping but
does not repeat folder paths. Similarly, a tileset manifest records dimensions;
conventional paths such as `surfaces/top.png` and `corners/outer.png` carry their
own meaning.

Source pack names and production history belong in library metadata and
provenance, never in runtime IDs. Promote one chosen variant into `Content`; keep
other useful variants under the same semantic ID in `Library`.
