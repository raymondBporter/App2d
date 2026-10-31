# Character geometry and drawing order

Spatial geometry and compositing order are separate data:

| Data | Meaning |
| --- | --- |
| `PartMesh.Vertices` / `Outline`, `PartLocation.Point` | Real `Vector3f` positions; Z is a distance |
| `PartGeometry.SpatialCurve` | A generic quadratic or cubic Bézier with real XYZ control points |
| `PartGeometry.VertexZ` | Physical Z for the samples of rigid planar geometry |
| `PartGeometry.Layer`, `PartInstance.Layer`, `PartLocation.Layer`, `ModelSocket.Layer` | Drawing-order offsets |
| `PartContour` | Physical positions with a separate uniform or interpolated layer attribute |
| `CharacterVertex` | Physical position, color, UV and drawing layer |

`PartPlacement3D.Extend` and `Lift` embed the planar frame at physical Z=0, ignoring its layer.
`GeometryFrame` adds an explicit physical Z before composing the spatial frame. `PartContour.TransformedBy`
transforms positions and leaves layers alone. Changing a layer cannot move a prop, alter a normal or change a 3D pick.

`PuppetPoint` is now confined to planar rig authoring. That existing format still packs XY and drawing layer
into its serialized x/y/z values, as do the planar evaluator's coordinate lookups. `LayeredFrame2D` names that
domain explicitly. Its packed points must not be treated as spatial geometry; character bindings resolve them
into a contour's positions and layer attributes. Spatial rig joints are a separate future change.

The fixed-camera `PointCharacterRenderer` uses the existing actor-local depth convention. At its GPU submission
boundary, `CharacterMesh.Buffer` encodes `physicalZ + layer` as draw depth. Stored geometry and bounds retain real
positions. `CharacterMesh.CopySpatialTo` exports only physical positions for `Renderer3D`; arbitrary 3D compositing
groups are not implemented by adding layer offsets to world Z.

The field migration in authored assets is `depth` → `layer`, `depths` → `vertexZ`, and socket `offsetZ` → `layer`.
Physical point JSON remains x/y/z. All checked-in authored assets use the new field names. Older drafts with those
removed names are rejected by strict JSON loading rather than silently losing ordering information.

`PartCurves2D` resolves character-specific control bindings and rest offsets. Curve control-point editing lives in
`Geometry.Operations.Curves`; Bernstein evaluation lives in `Geometry.Curves.BezierMath`. Layer values use scalar
Bernstein evaluation, not a second XY curve. Rounded paths retain their geometric parameterization when interpolating
attributes, so changing layer order does not change their silhouette or sampling density.

The `spatial-curve-study` library asset demonstrates real XYZ control points. Its layer is deliberately 8; the
3D preview exports its physical geometry without moving it eight units away. Open the asset's geometry inspector
to edit its XYZ points. The preview scene is built when Studio opens.
