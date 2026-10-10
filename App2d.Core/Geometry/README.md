# Geometry

`App2d.Core` geometry is layered so that each kind of question has one home:

| Namespace | Holds | Depends on |
| --- | --- | --- |
| `App2d.Core.Mathematics` | Vectors, cross products, polar/direction values, rotation, `Similarity2D`, `Affine2D`, easing | `System.Numerics` |
| `App2d.Core.Geometry` | Raw 2D math on numbers plus small value types. Never references `IShape2D`. | `Mathematics` |
| `App2d.Core.Shapes` | `IShape2D` classes and the per-shape switch tables (`ShapeBounds2D`, `ShapeDistance2D`, `WorldShape2D`) | `Geometry` |
| `App2d.Core.Meshes` | `TriangleMesh2D`, `Triangle3D`, `TriangleMeshAnalysis3D`, `TriangleMeshBuilder3D`, `Orientation3D` | `Geometry`, `Shapes` |
| `App2d.Core.Collision` | `ShapeCollision2D` (contacts over convex queries and composites), ray queries over objects, the broad phase and system | `Shapes`, `SpatialObject2D` |

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
| `ClosestPoint2D` | Which point of it is nearest? (`OnEllipsePerimeter` brackets Eberly's monotone root; no polygonizing) |
| `Distance2D` | How far, signed or not, squared where that saves a root? Partials: `.Linear` lines/rays, `.Convex` convex cores with radii |
| `Gjk2D` | Convex gap, overlap, escape depth and boundary witnesses through vertex cores or `IConvexSupport2D` |
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
var convex = Gjk2D.Query(new ConvexProxy2D(first, firstRadius), new ConvexProxy2D(second, secondRadius));
var ground = Line2D.Horizontal(0);
if (Raycast2D.TryCircle(origin, direction, center, radius, 100, out var rayHit)) { /* rayHit.Point, .Normal, .Distance */ }
```

## Shapes

`IShape2D` classes delegate their `Area`, `ContainsPoint` and `GetSupportPoint` to the raw operations and keep no
bounds cache. The switch tables map a shape to raw parameters once:

- `WorldShape2D` turns a shape plus `Similarity2D` into world parameters and writes perimeters and convex cores.
- `ShapeBounds2D.Calculate(shape)` gives local bounds; `SpatialObject2D` caches local and world bounds.
- `ShapeDistance2D` gives point/shape, shape/shape and object/object distances. Finite convex pairs use
  `ShapeConvexQuery2D`, shared with collision contacts. Ellipses use analytic support mappings; point and circle
  queries use the bracketed closest-point solver.
- `RoundedRectangle2D` is an inset rectangle expanded by a radius. Point distances and pair distances against
  circles, capsules and polygonal convex shapes use that core exactly, as do collision contacts. Ray casts still
  sample its curved perimeter.

`TriangleMesh2D` is a mesh, not a shape; `ToCompositeShape()` bridges to collision. `CompositeShape2D` is the
non-convex shape and always resolves per part.

Composite area is cached at construction. The default sums part areas, including overlap; use
`new CompositeShape2D(parts, includeOverlap: false)` to count the covered region once. `ShapeArea2D.Union(parts)`
also calculates that area directly. Circles, ellipses and capsules stay analytic, and rectangles, triangles and convex polygons use
their exact edges. The shared boundary integral keeps only exposed edges and conic arcs, handling multiple
overlaps, disconnected regions and holes. Two axis-aligned rectangles use a direct formula.
Ellipse/segment intersections are quadratic in normalized ellipse coordinates; ellipse/ellipse and circle/ellipse
intersections are quartic and can have four boundary crossings. Two bounded half-angle charts avoid an infinite
parameter at PI. `PolynomialRoots` isolates real roots using derivatives and bisection; the exposed arcs have an
elementary Green's-theorem integral. Calculations use double intermediates and return float area, without perimeter
samples for circles, ellipses or capsules. Nearly coincident or tangent roots remain subject to floating-point precision.
A capsule contributes its spine rectangle and two endpoint circles to this same union calculation; internal edges
and arcs disappear through the existing coverage rules. Reversed spines share a canonical rectangle, zero-length
spines reduce to a circle, and generated rectangle coordinates remain in double precision relative to the spine start.
Primitive vertices and bounds keep their own coordinate frames; coverage tests translate between these frames,
and corner contacts within rounding precision provide the cuts where straight edges join circular arcs.
This decomposition is internal to area calculation; the composite's public part list and JSON retain the original capsules.
Rounded rectangles use inscribed polygon outlines; `areaOutlineSegments` (default 64) controls this approximation,
including custom convex shapes sampled through their support mapping. This sampling affects area only.
The area mode and sample count survive shape-definition JSON round trips; older definitions retain summed area.

`ShapeDefinition2D` is the editable, JSON-tagged form of every built-in shape, mirroring `CurveDefinition2D`:
`FromShape(shape).ToJson()` writes it, `FromJson(json).Build()` reads it back through the validating constructors,
and `ShapeKinds2D` lists the kind tags, also available as `IShape2D.Kind` on runtime shapes. Both definition families
use `System.Numerics.Vector2` directly. `IO.Vector2JsonConverter` is registered in geometry and character authoring
settings and preserves the existing `{ "x": ..., "y": ... }` coordinate format. It can also be registered in other
`JsonSerializerOptions.Converters` collections.

`ShapeVertices2D` exposes exact local convex cores and sampled drawing outlines without allocating:

```csharp
Span<Vector2> buffer = stackalloc Vector2[shape.GetVertCount()];
ReadOnlySpan<Vector2> core = shape.GetVerts(buffer, out float radius);
```

The core is a point for circles, a segment for capsules, inset corners for rounded rectangles (possibly collapsing
to a segment or point), or the original vertices for rectangles, triangles and convex polygons. The radius expands
the core into the shape. `GetVertCount()` returns zero and `GetVerts()` throws for ellipses, concave polygons,
composites, half-spaces and custom shapes, which have no known exact finite polygonal convex core.
`GetOutlineVertCount(roundSegments)` and `GetOutlineVerts(buffer, roundSegments)` provide drawing perimeters,
including ellipses and concave polygons. Returned spans refer to the caller's buffer; unused entries stay untouched.
`WorldShape2D.WriteWorldConvexCore` also exposes only exact polygonal cores. Its ellipse count is zero;
`Ellipse2D.DefaultOutlineSegments` controls its default drawing perimeter, not collision accuracy.

`ConvexProxy2D` accepts a caller-owned vertex span plus radius and pose, or an `IConvexSupport2D` plus pose.
`IConvexShape2D` inherits that support contract. GJK finds the closest core features; overlapping cores use 2D EPA
to obtain escape depth. Small overlapping polygonal cores use the cheaper SAT projections, with the same normal
and witness result. Circles and point-versus-segment cores keep their direct closest-feature solution.
Applying round radii after the core query avoids iterating over rounded arcs.
Queries return a unit normal, boundary witnesses and a residual `ErrorBound`. Float precision and iteration caps
bound accuracy; no tessellation count controls contacts. Tied escape directions are arbitrary but chosen in a
consistent geometric argument order. `Distance` and `Intersects` skip EPA when cores overlap.
The raw query allocates no managed memory; built-in shape adapters borrow polygon buffers and use small stack buffers.

## Collision

`ShapeCollision2D.cs` dispatches composites and concave polygons over their convex pieces, half-spaces through
a direct support-plane query, and every finite convex pair through `ShapeConvexQuery2D`. Signed distances and
contacts therefore use the same core and radius or analytic support, including ellipses and rounded rectangles.
Touching counts as intersection but produces no penetration contact. A contact has one boundary witness;
this is not a multi-point contact manifold. A custom convex shape only needs the existing support contract for
pair distances and contacts, without a perimeter entry or pair-specific collision row.
`RayIntersection2D` takes a world ray into object space and dispatches to `Raycast2D`.

## Adding a primitive

1. Value primitive (like `Line2D`): add the struct to `Geometry` and raw functions to the operation classes you need.
2. Finite convex shape: implement `IConvexShape2D.GetSupportPoint` using raw geometry. Pair distances and contacts
   work immediately. Add drawing outlines, specialized bounds, point queries and ray queries as needed. An exact
   vertex core plus radius in `ShapeVertices2D` is optional and accelerates round shapes.
3. Concave shape: expose convex pieces through a composite rather than a support map of the entire union.
