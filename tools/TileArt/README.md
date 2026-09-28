# TileArt

Procedural, original terrain art for App2d, in the "ink" style: dark hand-drawn outlines with
a gentle wobble and flat, slightly saturated colour, chosen to sit beside the stick-figure rigs.

There are six tilesets: a natural **ground** and a built **wall** for each era.

| Era | Ground | Wall | One-way | Spikes | Ladder | Grip cue |
| --- | --- | --- | --- | --- | --- | --- |
| `medieval` | dirt, pebbles, grass | brick | plank on brackets | red-tipped stakes | wood | ivy |
| `prehistoric` | dirt, roots, bones, lush grass | dry-stone boulders | lashed log | flint and bone | lashed branches | hanging vines |
| `wasteland` | dry dirt, rubble, rust | cracked concrete slabs | painted girder | rebar and barbed wire | painted steel | rebar staples |

The IDs are `ink-<era>-<ground|wall>`. The output lives in `Assets/Static/environments/tilesets`,
because it is authored content. The runtime build copies it into `Assets/Runtime`.

## Regenerate

```bash
python tools/TileArt/generate.py --also-runtime
```

The generator is deterministic. Building `App2d` copies changed tilesets from `Assets/Static`
into `Assets/Runtime`, where Debug loads them, and Release packages them from `Assets/Static`
directly. So a git pull or a regenerate needs no art-pipeline run. `--also-runtime` also writes
`Assets/Runtime`, for tools that load tilesets without building the game first.

## Judge it in the engine

```bash
dotnet run --project App2d -- --tileset-smoke <dir> ink-medieval-ground ink-medieval-wall
```

The smoke builds a fixed test layout (ground, a wall cliff with a grippable face, a one-way
plank, a ladder, a spike pit and a floating block). It renders the layout through the real
terrain renderer with the player and the game's procedural grass, then writes three images:
an overview at 1080p game size and two 2× close-ups. Pass ground/wall pairs to render several
tilesets at once.

## Rules the art follows

The rules come from `SideScrollerTerrainTileset2D`.

- `fill.png` repeats on a world-anchored grid every `fillPeriod` (128 px, four tiles), so it
  must wrap seamlessly. It is drawn slightly darker than the lighter lip in `surfaces/top.png`.
- Edge strips are `surfaceThickness` (12 px) deep and overlay the fill on exposed sides. They
  repeat identically every tile, so each wobble is pinned at the tile edges. `left-grip.png` and
  `right-grip.png` replace the side strips on grippable walls. Grippable tiles otherwise draw
  exactly like other solids.
- One image serves all four outer corners and one serves all four inner corners. The fill
  cannot be cut, so outer corners stay square. The inner corner is a small ink join.
- Interactive props (one-ways, ladders) stay warm orange in every era, so they read against grey
  and blue backgrounds.

`concepts/` holds the throwaway style-exploration renderers that picked the look. They are kept
for reference, and `concept_eras.py` shows the full scene mock-ups.
