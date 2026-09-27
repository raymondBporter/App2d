# Rendering passes

`GameHost` calls `Game2D.RenderFrame` between the renderer's `BeginFrame` and
`EndFrame`. The frame is cleared once, then draws in this order:

1. `Render`: game graphics, including backgrounds, terrain, vegetation,
   characters, and world effects. Scene Z indices control order within the game.
2. `RenderWorldDebug`: grid, collision geometry, traversal/ballistics curves,
   zone boundaries, and editor world guides. These use the game camera and
   overlay the finished game image.
3. `RenderUI`: HUD, feedback text, diagnostic labels, editor palette, and FPS.
   Screen UI draws above all world graphics and diagnostics. Native editor and
   console controls sit above the rendering surface.

Override the passes needed by a game. Call the base debug/UI methods to retain
the shared grid, collision, and FPS options. Put clears in the frame entry point,
not inside a pass; override `BackgroundColor` to choose the game background.
Render captures should also call `RenderFrame` so they include the same passes
as the host.

Console options:

```text
draw_grid true             # World debug grid; off by default
toggle draw_grid
draw_collision_shapes true
draw_traversal_metrics true # Side scroller jump diagnostics
draw_zones true             # Side scroller zone boundaries
draw_fps true               # Screen-space diagnostic UI
draw_graphics false        # Hide game graphics; keep debug and UI
```

Parallax affects background motion, not these pass boundaries. The debug grid
is an overlay, not a scene object or a tilemap layer. Sparse tile storage and
tilemap scene composition are separate future work.
