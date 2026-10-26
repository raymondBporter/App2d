# Geometry

## Solid mass properties

`ShapeMassProperties2D.Calculate(shape, density: 1d)` in
`App2d.Core.Geometry.Operations.MassProperties` returns `MassProperties2D` with
double-precision `Area`, `Mass`, `CenterOfMass` (`Vector2d`), and `MomentOfInertia`.
The input is a solid 2D lamina with uniform mass per unit area; constant thickness
is absorbed into density. Geometry stays in the shape's local coordinates.

The defining integrals are:

- `mass = density * integral(dA)`
- `center = integral(position * dA) / area`
- `I = density * integral(|position - center|² * dA)`

`I` describes rotation about the axis perpendicular to the plane through the center
of mass. Mass farther from that center contributes in proportion to distance squared.
The [OpenStax derivation](https://openstax.org/books/university-physics-volume-1/pages/10-5-calculating-moments-of-inertia)
explains the integral and parallel-axis theorem;
[Box2D's geometry implementation](https://github.com/erincatto/box2d/blob/main/src/geometry.c)
provides an independent reference for disk, capsule and polygon mass properties.

| Solid shape | Center | Centroidal polar inertia, total mass `m` |
| --- | --- | --- |
| Circle / disk, radius `r` | Circle center | `m * r² / 2` |
| Ellipse, semi-axes `a`, `b` | Ellipse center | `m * (a² + b²) / 4` |
| Rectangle / axis-aligned rectangle, width `w`, height `h` | Rectangle center | `m * (w² + h²) / 12` |
| Triangle, side lengths `a`, `b`, `c` | Mean of its three vertices | `m * (a² + b² + c²) / 36` |
| Convex or concave simple polygon | Area-weighted centroid | Exact signed edge integrals, either winding |
| Capsule | Spine midpoint | Exact rectangle plus two semicircles |
| Rounded rectangle | Outer rectangle center | Exact inner rectangle, edge strips and quarter disks |
| Additive composite (`IncludeOverlap = true`) | Mass-weighted part centroids | Sum of part inertias, shifted to the combined center |

Polygon integration shifts coordinates near the shape before summing, then integrates
inertia directly about its centroid. Translation therefore does not introduce the
large-origin cancellation of subtracting `mass * |center|²` from an origin inertia.
Curved primitives use formulas, with no outline sampling. Compound pieces combine via
`I = sum(I_part + mass_part * |center_part - center|²)`. Additive overlap counts as
multiple layers of material, matching the composite's additive area semantics.

`properties.WithMass(mass)` chooses a new uniform density without moving the center.
`properties.MomentOfInertiaAbout(point)` uses `I_point = I_center + mass * distance²`.
Infinite half-spaces, unknown custom shapes, and composites requesting overlap-counted-once
union mass (`IncludeOverlap = false`, more than one part) throw `NotSupportedException`.
Use a simple polygon outline or a decomposition into non-overlapping additive parts for
those unions in this first implementation. No bounding-box or sampled inertia is substituted.

`RigidBody2D.SetMassFromDensity` and `SetMassFromShape` apply these results to live
physics and include the current uniform scale. At fixed density, scaling lengths by
`s` scales mass by `s²` and inertia by `s⁴`; at fixed total mass, inertia scales by `s²`.
Rotation and mirroring preserve scalar inertia. Nonuniform scale and shear retain the
existing collision restriction; recalculate mass properties after changing size.

## Geometry organization

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
(anchors, containment, distance, `TransformedBy`, for any type with `Min`/`Max`), `OrientedFrame2D` (a center and a
unit axis that oriented shapes project every query through), `Interval<T>`, `Line2D`, `Ray2D`, `RayHit2D`, `IGeometry2D`.

Rectangle `Contains(point)` includes edges. `ContainsInterior(point)` explicitly excludes
all edges and corners; a rectangle with a collapsed dimension has no interior. The raw
equivalents are `Containment2D.Rectangle` and `Containment2D.RectangleInterior`.

`Rectangle2D`, `RoundedRectangle2D` and `Ellipse2D` carry a unit `Axis` for their local +X and so rotate in local space;
an unset axis reads as +X. Their JSON writes `center`, `size` (or `radii`) and `angleRadians`, and the two rectangle kinds
also read the axis-aligned `min`/`max` spelling, never mixed with `center`/`size`. `AxisAlignedRectangle2D` keeps `Min`/`Max`
and is the one shape that never rotates. `ShapeTransforms2D.Posed(shape, Similarity2D)` carries any built-in shape through
a translation, rotation, uniform scale and optional mirror and returns a shape of the same kind.

`IInterval<T>` supplies ordered scalar `Min`/`Max` endpoints. `Interval<T>`, authored `Limit` values
and physics `ConstraintLimit1D` share `IntervalMath<T>` operations without converting structs to interfaces.
The contract is closed and permits infinite endpoints; it does not impose domain-specific validation.
`Length` is an endpoint difference, including for integer ranges; it is not an inclusive element count.
Arguments enable convenient calls such as `limits.Contains(3f)` and `limits.Overlaps(new Interval<float>(2, 5))`.
For queries where C# cannot infer the scalar from arguments, name it once: `IntervalMath<float>.Length(limits)`
or `IntervalMath<float>.TryIntersect(limits, authoredLimit, out var shared)`.
Rectangles expose `XInterval` and `YInterval` while retaining their existing vector storage.

`ISegment<TSelf, TPoint>` supplies `Start`/`End` and a static abstract `Create` factory.
`LineSegmentCurve2D` and Noodle's bone segment implement it with `Vector2` endpoints.
`SegmentMath2D` adds the computed `segment.Midpoint` property, `segment.Length()`,
clamped `segment.PointAt(amount)`, and `segment.Reversed()` for `Vector2` endpoints.
The midpoint uses double intermediates to avoid overflow or underflow at finite float extremes.
These operations need no additional vector interface. Reversing calls the concrete
type's factory and returns that same type.

`GiantTree2D(root, settings, seed)` creates immutable, seeded tree-level geometry. `GiantTreeSettings2D`
uses world units; `Branches` gives exact flat-topped platform bounds. `GiantTreeVisual2D` adds bark, moss,
and foliage using a separate random stream. Changing foliage never moves a platform. The trunk is scenery;
the side-scroller makes each branch a static one-way platform that survives root-chunk unloading.

The cavern grove is authored in `Assets/Static/levels/cavern/giant-trees.json`. That file uses **tiles**
(currently 32 world units): ranges such as `branchLengthTiles: [4, 12]` mean random lengths beyond the trunk.
Each tree has `xTiles`, `heightTiles`, `trunkWidthTiles`, and a stable `seed`. Omit `rootYTiles` to plant it on
the contiguous ground column; supply it to choose an exact base row. Edit the file and run `reload_trees`
in the developer console to rebuild visuals and collision. An empty `trees` list removes the grove.
`--giant-tree-study <output-directory>` renders an overview and checks a real player standing on a branch.

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

## Ray queries

`GeometryRaycast2D.TryIntersect` in `Geometry.Operations.Intersection` is the shared
nearest-hit entry point for `IGeometryValue2D` values: shapes and curves. It dispatches
to `ShapeRaycast2D` or `CurveRaycast2D`, with no physics or renderer dependency.
`Raycast2D.TryLine`, `TryRay`, and `TrySegment` also query the corresponding linear
primitives directly, including unbounded lines and rays without fabricated finite boxes.

```csharp
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Geometry.Primitives;
using System.Numerics;

var curve = QuadraticBezier2D.Builder.Create(new(0, 0), new(1, 2), new(2, 0));
var ray = new Ray2D(new(-1, .75f), Vector2.UnitX);
if (GeometryRaycast2D.TryIntersect(ray, curve, 10f, out var hit))
{
    // Nearest point is (.5, .75); hit.Normal is the curve's directed unit normal.
}
// An optional Matrix3x2 localToWorld argument accepts an invertible affine pose.
// GeometryRaycast2D.TryIntersect(ray, curve, localToWorld, 10f, out hit);
```

| Boundary | Query representation |
| --- | --- |
| Circles, ellipses, capsules, half-spaces | Direct analytic primitive intersections |
| Rectangles, triangles, convex/concave polygons, baked heightfields | Their actual straight boundaries |
| Rounded rectangles | Exact straight sides and circular corners, including collapsed circle/capsule cores |
| Arcs | Both supporting-circle roots, restricted by the signed angular sweep |
| Quadratic and cubic Beziers | Polynomial roots on the original curve; no flattening |
| B-splines | Cached Bernstein spans extracted by knot insertion, preserving degree and knot intervals |
| Polylines and rounded polylines | Actual line spans and quadratic fillets |
| Curve slices, including nested slices | Restricted source parameter intervals before nearest-hit selection |

Bezier control-hull bounds reject irrelevant spans before root solving. Degrees up
to four reuse `PolynomialRoots`; higher-degree spline spans use derivative root
isolation and bisection in Bernstein form. Roots, knot insertion, points and normals
use double intermediates; result vectors and distances retain the engine's float
API. This intersects the represented curve numerically, rather than a polyline
approximation. Floating-point root accuracy still applies, especially near tangencies.
Prepared spline/rounded-polyline spans are cached against immutable source data.
These local queries do not yet provide a scene acceleration structure.

Queries return the nearest nonnegative hit, including tangencies and endpoints,
within `maxDistance`. Vector-origin/direction overloads preserve the original ray
parameter for non-unit directions. `Ray2D` overloads report coordinate-space distance.
Solid normals point outward; open-curve normals point counterclockwise from the
increasing local curve tangent. Cusps use the first nonzero one-sided derivative;
point curves and collinear overlaps have no isolated surface crossing and miss.
A ray starting inside a solid reports its exit, following the existing shape API.

The affine overload transforms the ray without renormalizing its local direction,
retains world distance, and transforms normals with the inverse transpose. This
preserves the authored normal side under mirroring; it need not be counterclockwise
from the mirrored world tangent. Singular/nonfinite poses are rejected.

`NormalOffsetCurve2D` and arbitrary custom/procedural curves are explicitly unsupported
and throw `NotSupportedException`; there is no hidden sampled fallback. A deliberate
approximation remains possible by explicitly converting to `PolylineCurve2D` first.
Composites retain their existing nearest-part semantics, rather than computing a
new constructive-solid-geometry union boundary.

## Curves

`ICurve2D` evaluates a geometric path and its derivative at a normalized parameter. It does not own a clock:
animation can pass eased progress to `Evaluate`, or move the curve's control points to change a face or contour.

```csharp
var mouth = CubicBezier2D.Builder.Create(start, firstHandle, secondHandle, end);
var point = mouth.Evaluate((float)Easing.EaseInOutSine.Sample(phase));
var (left, right) = mouth.Split(.5f);
var middle = mouth.Slice(.25f, .75f);
```

`Split` and `Slice` use Bezier parameters, not distance or elapsed time. Subdivision preserves the original path;
each result maps its own parameter range to [0, 1]. A slice with equal endpoints is a constant point curve.
The methods return value types without allocating arrays.

For any `ICurve2D`, import `App2d.Core.Geometry.Operations.Curves` to use the same `Split(t)` and
`Slice(from, to)` extension names. Their `CurveSlice2D<TCurve>` results retain the original curve and remap
its parameter without resampling the path. Derivatives include the parameter scale; collapsed intervals have
zero derivative. Distance queries use a polyline approximation of the restricted path. Concrete cubic calls
keep returning concrete `CubicBezier2D` pieces. `CubicBezier2D.FromHermite` converts endpoint tangents with
respect to normalized progress into the same shared cubic representation.

`FindParametersAtX(x, parameters)` writes all intersections into a span of at least three doubles, sorted in [0, 1].
It includes tangencies and endpoints and uses the shared `PolynomialRoots` solver. A curve lying entirely on that X
has infinitely many matches and throws. `ParameterAtX(x)` is the convenience query when exactly one result is expected;
it throws for missing or ambiguous results.

Animation's `KeyCurve2D.ToCurve(start, end)` represents a segment as a cubic in `(normalized time, channel value)`.
Its X controls guarantee a unique time lookup. Its `Slice` first converts time bounds to Bezier parameters, then
uses the same subdivision as drawing curves. Relative/absolute value handles remain authored data; absolute handles
can move away from and return to equal endpoint values. The existing keyframe JSON fields are unchanged.

`KeyCurve2D.Split(time, start, end)` returns the two time segments; `Slice(from, to, start, end)` extracts one
interval. Both use sampled values for the new key endpoints. Handles are stored as two `Vector2` values;
motion keys store `Vector3`, and tint keys store `Vector4`. `AuthoredJson` maps these vectors to the existing
scalar file fields, including their sparse defaults. `KeyframeSampling` owns the shared ordered-key lookup,
and `ClipEase.Interpolate` owns the shared scalar interpolation policy. Component timing curves stay independent.

## Shapes

`IShape2D` classes delegate their `Area`, `ContainsPoint` and `GetSupportPoint` to the raw operations and keep no
bounds cache. The switch tables map a shape to raw parameters once:

- `WorldShape2D` turns a shape plus `Similarity2D` into world parameters and writes perimeters and convex cores.
- `ShapeBounds2D.Calculate(shape)` gives local bounds; `SpatialObject2D` caches local and world bounds.
- `ShapeDistance2D` gives point/shape, shape/shape and object/object distances. Finite convex pairs use
  `ShapeConvexQuery2D`, shared with collision contacts. Ellipses use analytic support mappings; point and circle
  queries use the bracketed closest-point solver.
- `RoundedRectangle2D` is an inset rectangle expanded by a radius. Point distances and pair distances against
  circles, capsules and polygonal convex shapes use that core exactly, as do collision contacts. Ray casts use
  its exact straight sides and circular corners.

`TriangleMesh2D` is a mesh, not a shape; `ToCompositeShape()` bridges to collision. `CompositeShape2D` is the
non-convex shape and always resolves per part.

Composite area queries sum part areas by default, including overlap; use
`CompositeShape2D.Builder.Create(parts, includeOverlap: false)` to count the covered region once. `ShapeArea2D.Union(parts)`
also calculates that area directly. Circles, ellipses in any orientation and capsules stay analytic, and rectangles, triangles and
convex polygons use their exact edges. The shared boundary integral keeps only exposed edges and conic arcs, handling multiple
overlaps, disconnected regions and holes. Two axis-aligned rectangles use a direct formula.
`ShapeArea2D.Intersection(first, second)` returns the overlap of two convex shapes from the same walk by selecting the arcs
and edges of each shape that lie inside the other, exact for every kind the union is exact for and precise for thin slivers,
with a direct formula for two axis-aligned rectangles. Each conic part carries its own frame, so a rotated ellipse projects
into the other part's coordinates before the crossings are solved.
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

Built-in shapes and serializable curves are readonly record structs. Their constructors only store source parameters
and snapshot collection inputs; they can represent invalid geometry. `geometry.Validate()` checks that same value.
The nested `Builder` types are static factories with no instance properties or fields: `Builder.Create(...)`
constructs a value, validates it, and returns it. Half-space factories also normalize the normal and offset.
Analyzer `APP2D001` warns on unchecked construction and offers a factory fix, or explicit `Validated()` for an object
initializer. An intentional unvalidated draft can suppress that warning while editing and validate later.

The geometry value is also the JSON data. `geometry.ToJson()` writes its kind and source fields;
`GeometryJson2D.FromJson(json)`, `ShapeFromJson(json)`, and `CurveFromJson(json)` read the actual values without
geometric validation. `JsonSerializer.Deserialize<Circle2D>(json, GeometryJson2D.JsonOptions)` also reads a circle
directly. Missing required members, unknown kinds, and unknown JSON properties remain deserialization errors.
Fields accepting either shapes or curves use `IGeometryValue2D`; shape-only and curve-only fields use `IShape2D`
and `ICurve2D`. Editors change these same values with `with` expressions, including collection snapshots.
There is no definition hierarchy, stateful builder hierarchy, or builder-to-runtime conversion cache.

Polygon triangulation, convex pieces, spline knots and derivatives, and composite union area are derived query data.
They are calculated on demand and cached against immutable source snapshots. Source changes through `with` use
the new parameters instead of stale constructor caches. Computed properties such as area, diameter, and arc endpoints
are excluded from the JSON schema. `ShapeKinds2D` and `CurveKinds2D` list the kind tags.
`Rectangle2D` uses `"kind": "rectangle"`; `AxisAlignedRectangle2D` uses `"kind": "axis-aligned-rectangle"`.
The kind alone selects the rectangle type, including inside composite parts; there is no `axisAligned` flag.
Geometry values use `System.Numerics.Vector2` directly. `IO.Vector2JsonConverter` is registered in geometry and character authoring
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
