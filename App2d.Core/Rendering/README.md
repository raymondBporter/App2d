# Spatial rendering

`Camera3D` and `Renderer3D` provide a spatial path independent of characters, rigs, and authored layer order.
World positions use `System.Numerics.Vector3`; `Frame3D` supplies affine placement. With row-vector conventions,
the pipeline is `local * world * view * projection`, followed by the perspective divide and viewport mapping.
Orthographic and perspective cameras share the same projection and picking API.

```csharp
var camera = Camera3D.Perspective(new(3, 2, 4), Vector3.Zero, Vector3.UnitY, width / (float)height);
renderer.Clear(background); // once for the scene, on a depth-enabled target
renderer.Draw(mesh, Frame3D.Identity, camera);
renderer.Draw(otherMesh, placement, camera); // same depth buffer
```

Meshes are the existing `TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b>` values. The renderer retains
GPU buffers by mesh identity and version, so camera and placement changes do not upload vertices. It restores blend,
depth and rasterizer state after drawing, and trims its buffer cache to a configurable byte budget.
The first path uses opaque vertex colors. Alpha blending and depth writes are explicit draw options; callers own
transparent ordering. Texture materials, lighting and a scene graph can be added independently.

`TryProject` returns viewport-local pixels (Y down) and normalized depth. `ScreenRay` starts at the near plane.
`Ray3D` normalizes direction, reports world-unit distances, and intersects two-sided `Triangle3D` values or
`System.Numerics.Plane` values (`normal dot point + D = 0`). To drag on a plane, retain the offset between the initial
hit and the object origin; intersect each new mouse ray with that same plane and add the retained offset.

## Studio preview

Run `dotnet run --project App2d.CharacterStudio -- --preview-3d`, or select **3D preview** in the editor toolbar.
The existing sword, calibration cube and `spatial-curve-study` asset share a camera and depth buffer. The curve's XYZ
control points use the generic Core Bézier evaluator; its drawing layer does not become spatial Z. Drag empty space or use
the right mouse button to orbit, use the wheel to zoom, and middle-drag to pan. Click an object to select it and drag
on a view-aligned, XY, XZ, or YZ plane. The inspector edits temporary position and rotation, with a reset action.
An empty workspace still shows the calibration cube. Camera and preview transforms do not write assets.

`--smoke-3d <output-directory>` renders perspective, orthographic and side views through this same scene and renderer,
and verifies that a camera ray selects the calibration cube. Camera/ray tests cover inverse mapping and plane dragging;
GPU tests cover shared depth, retained buffers and device-state restoration.

Rig placement still has its existing planar frames and drawing layers. The preview reads spatial vertices from a prop
without reinterpreting rig layer values as physical depth. Future spatial joint chains can compose `Frame3D` values;
the camera projection belongs only at the rendering and picking boundary.
