# Kenney Pixel Platformer

- Source: [Pixel Platformer](https://kenney.nl/assets/pixel-platformer)
- Creator: Kenney (https://kenney.nl)
- Pack version: 1.2
- License: Creative Commons Zero 1.0 (CC0)
- Original archive: `pixel-platformer.zip`

The archive is a durable third-party source input. The asset pipeline adapts selected
18 by 18 pixel terrain, platform, rock, and spike tiles into the semantic files used by
App2d's `kenney-grassland` terrain interface. Runtime output remains generated and
disposable.

Grippable walls use `Tiles/tile_0006.png`, the inset stone block, scaled with
nearest-neighbor sampling. Both the terrain and editor palette use this graphic.

Ladders use `Tiles/tile_0051.png` for the top and `Tiles/tile_0071.png` for
repeating segments, scaled with nearest-neighbor sampling. Other tilesets reuse
these ladder images when they do not provide their own.

Run `python tools/ArtPipeline/build_runtime_assets.py` from the repository root after
downloading the official archive to this folder as `pixel-platformer.zip`.
