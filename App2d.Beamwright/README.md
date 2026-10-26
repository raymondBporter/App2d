# Beamwright desktop

A native C# Windows port of Beamwright 1.2.0 by Jalapeño Seed Productions, in the
App2d solution. It references `App2d.Core` and runs through `GameHost`, `Game2D`,
`InputState`, and `Renderer2D`. It has no browser, WebView, JavaScript runtime, or
external assets at runtime.

```powershell
dotnet run --project App2d.Beamwright
```

The first playable version includes all 36 campaign benches, all 14 parts,
inventory and target requirements, colored dispersion, total internal reflection,
joined fibers, apertures, profile sensors, mirage zones, hints, undo, a level picker,
and an unlimited free bench. Campaign completion and best part counts are saved to
`%LOCALAPPDATA%/App2d/Beamwright/progress.json`. The current arrangement is not saved.

Click a part in the rack, then an empty cell to place it. Drag existing parts to
move them, or drag them back into the rack to return them. Click a selected part,
use **Q/E**, or scroll over the board to rotate by 15 degrees. **Delete** and
right-click return a part. **Ctrl+Z** undoes a move; **R** resets; **H** reveals the
next hint as a translucent part. **L** opens the level picker; **B** opens the free
bench; **Page Up/Down** changes levels; **N** advances after completing a level.
Scroll over the rack to see more sandbox parts. **Esc** closes the game.

## Geometry and optics

The original `disc` tag describes a filled circle, not a ring. Both droplets and
cat's eyes use `Circle2D`; the material determines how light interacts with it.
Blocks and straight fibers use `Rectangle2D`. Prisms, diamonds, the critical-angle
block, and the open quarter-annulus fiber bend use `SimplePolygon2D`. The bend is
one concave outline without a hole, retaining the original polygon approximation.
Mirrors, splitters, lenses, and profile sensors use `LineSegmentCurve2D`.

`OpticalScene` stores every shape or curve as `IGeometryValue2D` and casts `Ray2D`
through `GeometryRaycast2D`. `Frame2D` supplies local-to-bench poses; normals use the
inverse transpose. All bench boundaries, apertures, and hot plates are finite.
The upstream `slab()` is a rectangle-intersection algorithm; no new infinite slab
shape or additional shape-wide distance/containment/serialization APIs are needed.

Reflection uses `Vector2.Reflect`. Core's `Vector2.TryRefract` implements Snell's
law through `Vector2.Reject`, explicitly returning false for total internal reflection.
Authored angles use `Frame2D.FromRotationDegrees`; emitter offsets use `TransformPoint`.
The optical tangent and normal are the frame's two axes. Lens projection uses their
basis transpose. Fiber ports compute their bench-space midpoint from the segment's
shared `Midpoint` property and frame. Hot-air stepping uses `AirBounds.ContainsInterior`
to retain the original exclusion of points exactly on the zone boundary.
Cell coordinates use Core's `Vector2i`; dimensions remain plain `W` and `H` fields,
and the original campaign JSON format is retained. The original thin
lens and curved mirror rules remain optical interactions, independent of geometry.
Trace segments retain power, color, path distance and event names for a later
audio implementation. Refraction depends on the incident direction and material
indices, so it is not represented as a single affine transform.

HUD descriptions use Core's `Renderer2D.DrawScreenTextWrapped`, with measured glyph
widths, word wrapping, long-word splitting, and a two-line limit with an ellipsis.

## Verification and upstream import

```powershell
dotnet run --project App2d.Beamwright -- --verify
dotnet test App2d.Tests --filter FullyQualifiedName~Beamwright
dotnet run --project App2d.Beamwright -- --render-smoke artifacts/beamwright
```

The 72 reference traces cover each campaign bench both empty and solved. They
compare win states, received power, target quality, and profile strips against the
original JavaScript engine. Core coordinates are floats rather than JavaScript
doubles: a strip's reference tolerance only permits redistribution of power from
reference rays lying exactly on its neighboring strip seams. Total accepted power,
quality, and completion are still checked independently.

`Content/campaign.json` and `Content/reference-traces.json` are embedded snapshots.
To regenerate them from the separate upstream checkout (Node is needed only here):

```powershell
node App2d.Beamwright/Tools/import-beamwright.cjs C:/path/to/beamwright
```

This first port uses native rendering with layered beam glow. The upstream WebGL
metal/bloom compositor, synthesized music and rhythmic sound, custom bench maker,
interactive solver, share-code import/export, and community browser are not yet
ported. See [LICENSE](LICENSE) for the original MIT license and
[the upstream notice](ThirdPartyNotices/Beamwright.txt) for attribution.
