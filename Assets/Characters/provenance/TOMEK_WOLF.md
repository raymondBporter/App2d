# Tomek wolf animations

Nine motions by **tomek**, from
[3D wolf animation for game](https://opengameart.org/content/3d-wolf-animation-for-game).
Downloaded 2026-09-20 from https://opengameart.org/sites/default/files/wolf_1.blend.
The source page offers CC0 alongside GPL 2.0 and GPL 3.0. This import selects
[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).

`tomek-wolf.json` records source and exporter hashes, timing, calibration,
sample errors and per-clip metadata. `tomek-wolf-mapping.json` records the exact
donor bone mapping. The original scene is retained at
`Assets/Sources/characters/tomek-wolf/wolf.blend`.

These are evaluated donor landmarks mapped into the existing Hound drawing,
not new hand-authored gaits. The donor's simpler spine, neck, paws and tail are
subdivided to cover the 76-point contract. Ear guides follow the head; the source
has no ear bones. The drawing does not animate the source jaw/nose. One uniform
standing calibration is shared by all clips. Root motion is retained, with no
per-frame grounding, contact IK or loop repair.
