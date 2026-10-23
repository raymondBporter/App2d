# Rendering passes

`GameHost` calls `Game2D.RenderFrame` between the renderer's `BeginFrame` and
`EndFrame`. The frame is cleared once, then draws in this order:

1. `Background`: parallax layers, farthest first. These draw before `Render`,
   so even a high object Z index in a background cannot cover world trees or actors.
2. `Render`: game graphics, including terrain, vegetation, characters, and world
   effects. Scene Z indices control order within this pass. The side scroller
   draws trees, terrain at Z <= 0, back grass, actors at Z >= 1, then front grass
   and effects.
3. `RenderWorldDebug`: grid, collision geometry, traversal/ballistics curves,
   zone boundaries, and editor world guides. These use the game camera and
   overlay the finished game image.
4. `RenderUI`: HUD, feedback text, diagnostic labels, and FPS. Screen UI draws
   above all world graphics and diagnostics. The ImGui tool UI (editor sidebar and
   debug panel) renders after the frame, above everything.

Override the passes needed by a game. Call the base debug/UI methods to retain
the shared grid, collision, and FPS options. Put clears in the frame entry point,
not inside a pass; override `BackgroundColor` to choose the game background.
Render captures should also call `RenderFrame` so they include the same passes
as the host.

Debug panel toggles (F5), in its Draw section:

- Grid: world debug grid; off by default
- Collision shapes
- Traversal metrics: side scroller jump diagnostics
- Zones: side scroller zone boundaries
- FPS: screen-space diagnostic UI
- Graphics: off hides game graphics and keeps debug and UI

Both backgrounds and game graphics follow the Graphics toggle. The debug grid
is an overlay, not a scene object or a tilemap layer. Sparse tile storage and
tilemap scene composition are separate future work.

## Parallax layers

Every `Game2D` has a `Background` collection. Add as many `ParallaxLayer2D` instances
as needed; depth controls both their camera translation and their order:

```csharp
var mountains = new ParallaxLayer2D(depth: 4f) { RepeatWidth = 1800f };
mountains.Scene.Add(mountainVisual); // ordinary WorldObject2D, sprites or shapes
Background.Add(mountains);
```

Depth is finite and nonnegative. `ScrollFactor = 1 / (1 + Depth)`: depth 0 follows
normal world motion, depth 1 scrolls at half speed, and depth 4 at one fifth.
Larger depths draw first; equal depths retain insertion order. Z indices still
order objects within a layer. Depth affects X and Y camera translation equally;
zoom, rotation, and viewport scaling follow the game camera normally. It does
not shrink the artwork or introduce perspective.

Object transforms stay in layer coordinates. A separate, unbounded layer view
uses the final camera position, including editor panning and shake, at render
time. No per-update repositioning or extra layer update call is needed. Gameplay
camera bounds and physics are unchanged. Optional `RepeatWidth` tiles horizontally
and draws every visible copy, including across seams or while zoomed out; repeated
objects must have finite bounds. Null disables repetition.

Reusable geometry can now be drawn as a mesh, material, and transform:

```csharp
// Keep these objects between frames. Geometry stays in local coordinates.
TriangleMesh2D mesh = /* geometry library output */;
IShader2D material = new SolidColorShader(Color.White);

// Between BeginFrame and EndFrame:
renderer.DrawMesh(mesh, material, localToWorld);
```

`Renderer2D.DrawMesh` retains indexed vertex/index buffers for the built-in solid,
gradient, sprite, and tiled materials. Local coordinates pass through
`localToWorld * camera.WorldToDeviceMatrix` and the viewport projection on the GPU.
Changing the matrix, camera, or solid color reuses the upload. Sprite flips select
a cached UV variant, and texture bindings are selected on every draw. Custom
`IShader2D.GetVertexColor` implementations use the triangle batch so mutable
material data is evaluated on each draw.

`DrawTriangleMesh` uses this path for fills. Regular shape drawing also uses it
for simple polygons with at least 64 triangles; this is a conservative threshold,
not a measured optimum. Smaller shapes remain batched to avoid a draw call per
sprite. Retained draws flush preceding batches and preserve call order, including
transparent layers. Outlines keep their existing pixel-sized stroke behavior.

`PointCharacterRenderer` retains a dynamic vertex buffer for each `CharacterMesh`.
Mesh writes and clears update its version. The renderer uploads once per changed
version, then uses vertex ranges for material batches and matrices for placement
or additional views. Rebuilt poses still upload their complete vertex stream;
pose evaluation, stroke construction, and deformation still run on the CPU.
Reusing an unchanged mesh does not upload it again. Replacement data uses
MonoGame's [discard update mode](https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.SetDataOptions.html)
so earlier submitted draws can finish using their previous data.

Both renderers own their mesh buffers and maintain a least-recently-used cache
with a default `MeshCacheBudgetBytes` of 32 MiB. They release evicted resources
after submission and release all resources on disposal. `ClearMeshCache()` can
release retained meshes when replacing a scene or studio document. A mesh larger
than the budget is submitted and then evicted. `CachedMeshBytes`,
`MeshUploadCount`, and `MeshDrawCount` expose resident buffer bytes and cumulative
submission counts for measurement. These changes reduce repeated uploads; they
do not add GPU instancing or shader-based character deformation.
