# Point library file format

## Current editable storage

App2d point libraries now use version 2 JSON storage. `library.json` contains clip
metadata and sample `times`; `points/<clip-id>.json` contains the coordinates for
that clip. Each named point has one `[x, y, z]` entry for each timestamp, in order:

```json
{
  "head": [
    [0, 1.8, 0],
    [0.02, 1.82, 0],
    [0, 1.8, 0]
  ]
}
```

Edit the coordinates directly. Keep every point listed in `pointNames`, with
exactly three finite coordinates per position and one position per timestamp.
Point property order does not matter. If you add or remove samples, update the
tracks and `times` together. Times start at zero, increase strictly, and end at
the clip's `duration`.

There are no packed offsets, quantization settings, sample-count fields or data
checksums to maintain. Studio reads plain vectors; looping, endpoint holds
and interpolation still use the original sample times. Reopen the library to
load file edits. Authored clips made through the import command are independent
copies; re-import if you want changed source samples in an authored clip.

`tools/CharacterPipeline/import.cjs` and `import-wolf.cjs` write this format, and
`export_wolf.py` produces it directly. The sibling `sprite-renderer` exporter may
still supply binary input, which is decoded only at the import boundary. Binary
packing and streaming can be added later without making the source files opaque.

Studio reads these sources through `Importing/Library/` and imports independent
authored clips. See [the character system guide](character-system.md) for the
current asset, editor, and runtime flow.
