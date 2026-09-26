# Geometry

Use `App2d.Core.Geometry` functions directly with `System.Numerics` values when you need math without constructing a shape. The folders separate algorithms from objects; their public namespace stays the same.

| Location | Purpose |
| --- | --- |
| `Functions/VertexGenerator2D` | Circle, ellipse, arc, rectangle, rounded rectangle and capsule contours into a caller-owned `Span<Vector2>` |
| `Functions/PrimitiveGeometry2D` | Areas, containment, support points, segment distance and normalized picking scores |
| `Functions/Projection2D` | Polygon, circle and capsule intervals on an arbitrary axis; polygon offsets avoid transformed copies |
| `Functions/PolygonGeometry2D` | Area, containment, support, closest perimeter point, edge normals and convex SAT overlap |
| `Functions/ClosestPoint2D` | Point-to-segment and segment-to-segment closest points |
| `Shapes/` | `IShape2D`, `IConvexShape2D` and concrete shapes with validated parameters and cached local bounds |
| `Bounds2D`, `Interval1D` | Value types for bounds and projected intervals |

```csharp
Span<Vector2> outline = stackalloc Vector2[48];
VertexGenerator2D.WriteEllipse(outline, center, radii);
var interval = Projection2D.Polygon(outline, axis);
var inside = PrimitiveGeometry2D.EllipseContainsPoint(point, center, radii);
```

Contour angles are radians, increasing counter-clockwise in Y-up coordinates. Closed perimeters omit a repeated closing vertex. Circle/ellipse buffer length determines tessellation; arcs include both endpoints. Rectangle, rounded-rectangle and capsule writers return the number of written vertices and leave the rest of the buffer untouched. Rounded rectangles clamp radius to half the shorter side and retain their corner samples even at zero radius. Screen-space Y-down coordinates naturally reverse the visual winding.

Primitive arithmetic queries assume finite inputs, nonnegative radii and ordered box bounds; ellipse radii and normalized box half-extents must be positive. Shape constructors retain their validation. Vertex writers additionally validate their dimensions and buffer sizes. Polygon queries expect ordered convex perimeters; overlap accepts either winding, skips repeated adjacent vertices and includes touching edges. Projections support non-unit axes and project everything to zero on a zero axis.

`DistanceToSegment` returns a Euclidean distance in input units. `NormalizedEllipseRadius` and `NormalizedRectangleRadius` return dimensionless scores: zero at the center, one at the boundary. An ellipse's normalized radius is **not** the shortest distance to its boundary. `PartGeometry.Distance` keeps its existing picking semantics, including bounding-box picking for rounded boxes and the minimum stroke tolerance.

`PartGeometry` owns attachment frames and depth; the renderer owns screen-dependent tessellation and triangle emission. Both use the shared generators. `EntityRegion` delegates overlap to polygon math. General XY rotation lives in `Mathematics/Rotation2D`; `PoseEvaluator.RotateXY` remains a compatible entry point.

Add raw geometry algorithms here and let shape methods delegate to them. Keep authoring, rendering, caching and gameplay policy in their respective callers.
