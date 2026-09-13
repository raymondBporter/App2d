# Downward sword preview

- `downward-sword.gif`: six-frame immediate airborne stab at 24 fps, with a review pause.
- `downward-sword-slow.gif`: the same poses at quarter speed.
- `contact-sheet.png`: all six poses in order.
- `downward-strike.png`: the immediate downward strike on frame one.
- `checks.json`: frame count, timing, sweep frames, and normalized image bounds.

The editable Blender source and export instructions are in
`Assets/Sources/characters/player-sword/downward-attack`. The actual clip lasts
0.25 seconds and does not loop; previews repeat it with a 0.6-second pause.
The slash flash is visible on frames 1–2. The earlier swing is in `swing-version`.
