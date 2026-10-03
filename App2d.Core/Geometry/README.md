# Geometry

`App2d.Core` geometry is layered so that each kind of question has one home:

| Namespace | Holds | Depends on |
| --- | --- | --- |
| `App2d.Core.Mathematics` | Vectors, cross products, polar/direction values, rotation, `Similarity2D`, `Transform2D`, easing | `System.Numerics` |
| `App2d.Core.Geometry` | Raw 2D math on numbers plus small value types. Never references `IShape2D`. | `Mathematics` |
| `App2d.Core.Shapes` | `IShape2D` classes and the per-shape switch tables (`ShapeBounds2D`, `ShapeDistance2D`, `WorldShape2D`) | `Geometry` |
| `App2d.Core.Meshes` | `TriangleMesh2D`, `Triangle3D`, `TriangleMeshAnalysis3D`, `TriangleMeshBuilder3D`, `Orientation3D` | `Geometry`, `Shapes` |
| `App2d.Core.Collision` | `ShapeCollision2D` (the double-dispatch contact table), ray queries over objects, the broad phase and system | `Shapes`, `SpatialObject2D` |

## Geometry

Value types: `Rect2D` (the only axis-aligned box; `Unbounded` covers the plane), `IRect2D` + `Rect2DExtensions`
(anchors, containment, distance, `TransformedBy`, for any type with `Min`/`Max`), `Interval1D`, `Line2D`, `Ray2D`,
`RayHit2D`, `IGeometry2D`.

Operations, one static class per question, all taking raw parameters:

| Class | Question |
| --- | --- |
| `Area2D` | How big is it? |
| `Containment2D` | Is this point inside? (plus normalized radial picking scores) |
| `SupportPoint2D` | Which point is farthest in a direction? |
| `Projection2D` | What interval does it cover on an axis? |
| `ClosestPoint2D` | Which point of it is nearest? (`OnEllipsePerimeter` iterates on the evolute; no polygonizing) |
| `Distance2D` | How far, signed or not, squared where that saves a root? Partials: `.Linear` lines/rays, `.Convex` convex cores with radii |
| `Intersection2D` | Do they touch? Line/ray clipping, rectangle and convex overlap, swept circle vs rectangle |
| `Raycast2D` | Where does a ray first hit a primitive? |
| `PolygonGeometry2D` | Hull, winding and edge normals |
| `PolygonClipping2D` | Sutherland-Hodgman against a half-space (raw normal/offset) or a convex polygon |
| `VertexGenerator2D` | Perimeters into caller-owned spans |

Conventions: Y up, angles in radians counter-clockwise, closed perimeters omit the repeated closing vertex, touching
counts as intersecting, filled distances are zero inside, signed distances are negative inside. Inputs are finite,
radii nonnegative, rectangle bounds ordered. Raw functions do not validate unless documented.

```csharp
using App2d.Core.Geometry;

var box = Rect2D.FromSize(new(20, 10), center: new(5, 3));
var hit = box.Contains(point) ? box.SignedDistanceTo(point) : box.DistanceSquaredTo(point);
var world = box.TransformedBy(matrix);
var onEllipse = ClosestPoint2D.OnEllipsePerimeter(point, center, radii);
var gap = Distance2D.SignedDistanceBetweenConvexPolygons(first, second, firstRadius, secondRadius);
var ground = Line2D.Horizontal(0);
if (Raycast2D.TryCircle(origin, direction, center, radius, 100, out var rayHit)) { /* rayHit.Point, .Normal, .Distance */ }
```

## Shapes

`IShape2D` classes delegate their `Area`, `ContainsPoint` and `GetSupportPoint` to the raw operations and keep no
bounds cache. The switch tables map a shape to raw parameters once:

- `WorldShape2D` turns a shape plus `Similarity2D` into world parameters and writes perimeters and convex cores.
- `ShapeBounds2D.Calculate(shape)` gives local bounds; `SpatialObject2D` caches local and world bounds.
- `ShapeDistance2D` gives point/shape, shape/shape and object/object distances. Ellipse point and circle queries are
  exact; other ellipse pairings polygonize with `Ellipse2D.CollisionSegments`.
- `RoundedRectangle2D` is an inset rectangle expanded by a radius. Point distances and pair distances against
  circles, capsules and polygonal convex shapes use that core exactly; collision contacts and ray casts sample its curved perimeter.

`TriangleMesh2D` is a mesh, not a shape; `ToCompositeShape()` bridges to collision. `CompositeShape2D` is the
non-convex shape and always resolves per part.

`ShapeDefinition2D` is the editable, JSON-tagged form of every built-in shape, mirroring `CurveDefinition2D`:
`FromShape(shape).ToJson()` writes it, `FromJson(json).Build()` reads it back through the validating constructors,
and `ShapeKinds2D` lists the kind tags. Both definition families share the JSON-friendly `Geometry.Point2D`.

## Collision

`ShapeCollision2D.cs` is the whole dispatch table: `Dispatch` picks the row for the first shape, and each row switches
on the second. Rectangles, triangles, convex polygons and ellipses share the polygon row through
`WorldShape2D.WritePerimeter`. The pair math lives in the `ShapeCollision2D.*.cs` partials.
`RayIntersection2D` takes a world ray into object space and dispatches to `Raycast2D`.

## Adding a primitive

1. Value primitive (like `Line2D`): add the struct to `Geometry` and raw functions to the operation classes you need.
2. Collidable shape: add the class to `Shapes`, its raw functions to `Geometry`, and one arm each in
   `WorldShape2D` (perimeter or core), `ShapeBounds2D`, `ShapeDistance2D`, `RayIntersection2D` and
   `ShapeCollision2D`. If it has a polygonal perimeter, `WorldShape2D.PerimeterVertexCount` is often the only
   collision and distance change needed.
