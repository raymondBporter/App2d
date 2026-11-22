# Geometry

## Solid mass properties

`ShapeMassProperties2D.Calculate(shape, density: 1d)` in
`App2d.Core.Geometry.Measurements` returns `MassProperties2D` with
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

## Segment continuous collision with rotation

`SegmentCcd2D.FirstContact` in `Geometry.Intersection` takes two local
`Segment<Vector2f, float>` values and each segment's start/end `RigidTransform2D`.
It returns the earliest touching fraction in [0, 1], zero for initial overlap,
or null for a miss. Tangencies, endpoint contact, collinear sliding, and segments
collapsed to points are included.

```csharp
var a = new Segment<Vector2f, float>(new(0, 0), new(1, 0));
var b = new Segment<Vector2f, float>(new(0, -1), new(0, 1));
var startB = new RigidTransform2D(new(2, 0));
var endB = new RigidTransform2D(new(0, 0), .5f);
double? time = SegmentCcd2D.FirstContact(a, default, default, b, startB, endB);
if (time is double t)
{
    Vector2d endpoint = SegmentCcd2D.TransformPoint(b.Start, startB, endB, t);
}
```

Translation is linear. Rotation linearly interpolates the half-angle vectors
`z0 = (cos(theta0/2), sin(theta0/2))`, `z1 = (cos(theta1/2), sin(theta1/2))`,
then applies the complex rotation `z(t)^2 / |z(t)|^2`. Trig is used once to convert
the endpoint angles; polynomial construction and solving use arithmetic only.
Use `TransformPoint` to sample this same path: interpolating the angles linearly
has a different angular speed and can give a different contact time.
Unwrapped turns retain their sign and may exceed 180 degrees. A full revolution
passes through `z=0`; turns approaching that singularity, or spanning a full
revolution or more, must be split. Subdividing an existing rational path must also
preserve its interpolated half-angle vectors and time parameter; sampling angles
and restarting with unit half-angle endpoints generally changes its timing.

The transforms' translations are the world positions of the rotation pivots.
A point `P0 + velocity*t + R(t)*(p + localPoint)` is represented by adding `p`
to the local segment endpoints before querying.

In B's local frame, set `D_A = |z_A|^2`, `D_B = |z_B|^2`, and let `N_A`, `N_B`
be the quadratic rotation numerators. For an endpoint a and segment b0,b1,

```
q_num = D_A N_B^T (P_A - P_B) + N_B^T N_A a - D_A D_B b0
d = b1 - b0
F = cross(q_num, d)
0 <= dot(q_num, d) <= |d|^2 D_A D_B
```

The first term has degree at most five; the other terms have degree at most four.
Thus there are four endpoint-on-line **quintics**, with dot-product bounds checked
at their roots. Parallel means `cross(U,V)=0`; `dot(U,V)=0` means perpendicular.
Identically collinear motion uses the projection-boundary roots. Root solving
reuses `Mathematics.RootFinding.PolynomialRoots`, including repeated roots and
the endpoints of the time interval. There is no temporal sampling in the query.
The equations describe the rational path algebraically; double-precision roots
and tolerance-based predicates are numerical, with no certified exactness guarantee.

For comparison, an offset circle against a moving line has two signed quintic
equations (one per side); two offset circles have a degree-six distance equation.
Squared-distance constraints explain the higher degree. Polygon boundary CCD can
take the earliest edge-pair contact, with an initial solid-containment check;
this query does not itself sweep polygon solids or exact curved boundaries.

## Geometry organization

Every namespace is its folder: a file in `App2d.Core/Geometry/Distance` declares `App2d.Core.Geometry.Distance`.
Each kind of question has one home:

| Folder and namespace (under `App2d.Core`) | Holds |
| --- | --- |
| `Mathematics` | Vectors, `Interval<T>`, polar/direction values, clamping, polynomial roots. `Mathematics.Transforms` has rotation, `Similarity2D`, `Affine2D`; `Mathematics.Progress` has easing |
| `Geometry` | `IGeometryValue2D` and `GeometryValidation2D` |
| `Geometry.Primitives` | Rects, segments, triangles, lines, rays, conics, and vertex/polygon math on raw points |
| `Geometry.Shapes` | `IShape2D` classes, each next to its `X.Builder.cs`, plus `ShapeConversions2D`, `ShapeEditing2D`, `ShapeVertices2D` |
| `Geometry.Curves` | Curve types, each next to its builder, plus `Curve2D` and control-point editing |
| `Geometry.Bounds`, `Geometry.Distance`, `Geometry.Intersection`, `Geometry.Containment`, `Geometry.Measurements` | One question each: bounds, distance and closest points, ray casts and intersections, containment, area/diameter/mass |
| `Geometry.Convex`, `Geometry.Clipping`, `Geometry.Construction`, `Geometry.Transforms` | GJK and support queries, polygon clipping, circle/conic construction, posing shapes and vertices |
| `Geometry.Meshes`, `Geometry.Meshes.Tessellation` | Triangle meshes and their analysis; triangulation and stroke/shape tessellation |
| `Geometry.Serialization` | JSON converters for shapes and curves |
| `Collision`, `Collision.Contacts`, `Collision.Queries`, `Collision.BroadPhase`, `Collision.Filtering` | `ShapeCollision2D` contacts, ray queries over objects, the broad phase and the system |
| `Spatial` | `SpatialObject2D` and distance/ray casts against placed objects |

## Geometry

Value types: `Rect2f` (float bounds; `Unbounded` covers the plane), `IRect2f` + `Rect2DExtensions`
(anchors, containment, distance, `TransformedBy`, for any type with `Min`/`Max`), `OrientedFrame2D` (a center and a
unit axis that oriented shapes project every query through), `Interval<T>`, `Line2D`, `Ray2D`, `RayHit2D`, `IGeometry2D`.

`IRect2<TVector, TReal>` describes bounds using a concrete floating-point vector.
`TVector` is unmanaged and implements `IVector2<TVector, TReal>`; `TReal` is unmanaged
and implements `IFloatingPointIeee754<TReal>`. `IRect2f` is a global
alias for `IRect2<Vector2f, float>`; `IRect2d` selects `Vector2d` and `double`.
The aliases change spelling, not storage or conversions.

`Rect2<TVector, TReal>` supplies generic floating-point bounds and holds every one of its formulas as an
ordinary member; nothing forwards to a helper class. `Rect2d` aliases `Rect2<Vector2d, double>`. For example:

```csharp
var precise = Rect2d.Builder.Create(new(16_777_217, -4), new(16_777_220, 8));
double width = precise.Width; // 3, with no float rounding
Vector2d center = precise.Center;
bool contains = precise.Contains(center);
```

Another type with `Min`/`Max` in the same vector type converts with `Rect2<TVector, TReal>.From(value)`,
which copies and validates the corners without boxing. `Rect2<TVector, TReal>.Unbounded` is the whole plane,
and `DistanceTo` uses the coordinate type's `Hypot` so large finite offsets do not overflow.

`Rect2f` retains the renderer's packed float vector storage and float-specific factories and transforms.
Its queries come from `Rect2DExtensions`, which serves every `IRect2f` (including `AxisAlignedRectangle2D`).
It shares the same bounds contract as `Rect2<Vector2f, float>`.

Rectangle `Contains(point)` includes edges. `ContainsInterior(point)` explicitly excludes
all edges and corners; a rectangle with a collapsed dimension has no interior. The raw
equivalents are `Containment2D.Rectangle` and `Containment2D.RectangleInterior`.

`Rectangle2D`, `RoundedRectangle2D` and `Ellipse2D` carry a unit `Axis` for their local +X and so rotate in local space;
an unset axis reads as +X. Their JSON writes `center`, `size` (or `radii`) and `angleRadians`, and the two rectangle kinds
also read the axis-aligned `min`/`max` spelling, never mixed with `center`/`size`. `AxisAlignedRectangle2D` keeps `Min`/`Max`
and is the one shape that never rotates. `ShapeTransforms2D.Posed(shape, Similarity2D)` carries any built-in shape through
a translation, rotation, uniform scale and optional mirror and returns a shape of the same kind.

`Interval<T>` (in `App2d.Core.Mathematics`, next to the vectors) is a closed `[Min, Max]` range and owns every interval
formula as an ordinary member: `limits.Contains(3f)`, `limits.Length`, `limits.Overlaps(Interval<float>.Builder.Create(2, 5))`.
Nothing forwards to a helper class. Physics and rig limits use `Interval<float>`.
Use `Interval<float>.Builder.Create(min, max)` for a finite range and `Interval<T>.Singleton(value)` for a single value.
`Builder.CreateUnbounded(min, max)` also accepts outward infinite endpoints. Both builders check endpoint order and reject NaN.
The JSON constructor stores both endpoints. After loading, call `Validate()` for a finite field or
`Validate(allowUnbounded: true)` for an open-ended field.
The floating-point factories `Interval.Entire<float>()`, `Interval.AtLeast(2f)` and `Interval.AtMost(8f)` construct
unbounded ranges; `IsBoundedBelow`, `IsBoundedAbove`, `IsEntire` and `IsSingleton` describe their bounds.
Interval clamping follows the shared numeric arithmetic, including NaN propagation. Constraint-specific finite-coordinate
validation, activation tolerance and active-side reporting live in `ConstraintMath1D.EvaluateLimit`.
`Length` is an endpoint difference, including for integer ranges; it is not an inclusive element count.
Numeric arithmetic uses `T`'s operations, so the same struct serves float, double, integer and decimal endpoints.
Rectangles expose `XInterval` and `YInterval` while retaining their existing vector storage.

`IInterval<T>` is only the two endpoints, `Min` and `Max`. A type that carries a range plus its own data (an authored
`Limit` with a soft range, for example) implements it and converts with `Interval<T>.From(value)` to use the queries.
`From` copies and validates the two endpoints; a struct is not boxed.

```csharp
public readonly record struct Band(double Min, double Max) : IInterval<double>;
```

```csharp
var band = Interval<double>.From(new Band(2, 5));
bool contains = band.Contains(3d);
double length = band.Length;
bool overlaps = band.Overlaps(Interval<double>.Builder.Create(5, 8));
```

`Distance(other)` is the nearest-point distance, returning zero for touching or overlapping intervals.
`MaxDistance(other)` uses the farthest cross-interval endpoints. `SignedDistance(other)` is positive for a gap,
zero at contact, or negative by the smallest translation needed to reach touching contact. This includes containment:
`[0, 10]` and `[3, 4]` have signed distance `-4`, not `-1`. Because these intervals are closed, strictly disjoint
intervals require moving past contact. An interval that cannot be separated by a finite translation has signed distance
negative infinity when the endpoint type supports it.

`Lerp(t)` evaluates `Min + t*(Max-Min)`, allowing extrapolation; `LerpClamped(t)` first clamps t to `[0, 1]`.
`InverseLerp(value)` recovers the parameter, and `InverseLerpClamped(value)` clamps that result to `[0, 1]`.
These operations require finite endpoints. Singletons evaluate to their endpoint and return parameter zero.
Parameters and results use `T`, including integer division for integer intervals. Floating-point interpolation handles
opposite-sign endpoints without overflowing the endpoint difference, and parameter recovery rescales overflowing
floating-point differences. NaN parameters/values propagate for nonsingleton floating-point intervals.

`Translate(offset)`, `Reflect()`, `Reflect(center)`, `Scale(scale)` and `Scale(scale, center)` return validated intervals.
Affine parameters must be finite. Reflection reverses and negates the endpoints; negative scaling reorders them.
Zero scaling maps any interval, including an unbounded one, to the singleton at its center of scaling.
`Include(value)` expands the interval only as needed, and `ToString()` shows `[Min, Max]` with invariant formatting.
The new distance, interpolation and affine calculations use checked generic arithmetic; integer or decimal intermediate
overflow throws rather than silently wrapping. Unsigned types cannot represent negative penetration distances.
The nonempty closed-range representation remains unchanged; a disjoint intersection still returns false, not an empty sentinel.

`IIntervalTests` checks that the interface is only the endpoints and that endpoint-only types with float, double, int and
decimal endpoints reach every query through `From`, plus BigInteger/decimal precision and zero managed allocations after warmup.
`IntervalOperationsTests` covers reusable validation, distances (including exhaustive small-integer searches),
parameterization, affine operations, unsigned/overflow cases, and unbounded ranges.
To inspect optimized machine code on x64, run `tools/IntervalJitProbe/Verify.ps1` in PowerShell. It builds the Release
probe, disables tiered compilation and ReadyToRun for that process, and writes disassembly plus `verification.json`
under `artifacts/interval-jit`. Verification checks for boxing/runtime-dispatch helpers, requires basic scalar queries
to inline completely, and permits only resolved decimal arithmetic, interval construction/validation, and invalid-clamp throw calls
in the other probes. This checks generated code, rather than assuming that constrained calls in generic IL are free.

`ISegment<TSelf, TPoint>` supplies `Start`/`End` and a static abstract `Create` factory.
`LineSegmentCurve2D` and Noodle's bone segment implement it with `Vector2` endpoints.
`Segment<TVector, TReal>` provides `Midpoint`, `Length()`, and clamped `PointAt(amount)` through
`IVector<TVector, TReal>` for any supported dimension and precision. `SegmentMath2D` exposes those
same calculations to segment types with `Vector2` endpoints and handles `Matrix3x2` transforms.
Reversing a segment returns the same concrete type with its endpoints swapped.

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
| `ShapeBounds2D` | What encloses this shape or point set? |
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

`ShapeBounds2D.GetContainingCapsule(points)` fits a capsule around finite points in any order.
It searches angles and radii on the convex hull within a fixed iteration budget and checks all
input points against the stored spine. Pass `minimumRadius` to set a thickness floor, especially
for collinear points or a single point.

`ConicPencil2D` constructs the family of conics through four points with no three collinear.
Sweep `pencil.At(knob)` from 0 to 1, or use `AtWeights` to set the ratio of the two line products
directly. `ThroughPoint(q)` sets that ratio so the conic also passes through `q`.
The coefficients use coordinates relative to the first point. `Evaluate(p)` accepts points in
the original coordinate space.

```csharp
using App2d.Core.Geometry;

var box = Rect2f.FromSize(new(20, 10), center: new(5, 3));
var hit = box.Contains(point) ? box.SignedDistanceTo(point) : box.DistanceSquaredTo(point);
var world = box.TransformedBy(matrix);
var onEllipse = ClosestPoint2D.OnEllipsePerimeter(point, center, radii);
var gap = Distance2D.SignedDistanceBetweenConvexPolygons(first, second, firstRadius, secondRadius);
var convex = Gjk2D.Query(new ConvexProxy2D(first, firstRadius), new ConvexProxy2D(second, secondRadius));
var ground = Line2D.Horizontal(0);
if (Raycast2D.TryCircle(origin, direction, center, radius, 100, out var rayHit)) { /* rayHit.Point, .Normal, .Distance */ }
```

## Ray queries

`GeometryRaycast2D.TryIntersect` in `Geometry.Intersection` is the shared
nearest-hit entry point for `IGeometryValue2D` values: shapes and curves. It dispatches
to `ShapeRaycast2D` or `CurveRaycast2D`, with no physics or renderer dependency.
`Raycast2D.TryLine`, `TryRay`, and `TrySegment` also query the corresponding linear
primitives directly, including unbounded lines and rays without fabricated finite boxes.

```csharp
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Intersection;
using App2d.Core.Geometry.Primitives;
using System.Numerics;

var curve = QuadraticBezier2D.Builder.Create(new(0, 0), new(1, 2), new(2, 0));
var ray = new Ray2D(new(-1, .75f), Vector2f.UnitX);
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
| Rectangles, triangles, convex/concave polygons | Their actual straight boundaries |
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
var easing = Easingd.Sine.WithDirection(EasingDirection.InOut);
var point = mouth.Evaluate((float)easing.Sample(phase));
var (left, right) = mouth.Split(.5f);
var middle = mouth.Slice(.25f, .75f);
```

`Split` and `Slice` use Bezier parameters, not distance or elapsed time. Subdivision preserves the original path;
each result maps its own parameter range to [0, 1]. A slice with equal endpoints is a constant point curve.
The methods return value types without allocating arrays.

For any `ICurve2D`, import `App2d.Core.Geometry.Curves` to use the same `Split(t)` and
`Slice(from, to)` extension names. Their `CurveSlice2D<TCurve>` results retain the original curve and remap
its parameter without resampling the path. Derivatives include the parameter scale; collapsed intervals have
zero derivative. Distance queries use a polyline approximation of the restricted path. Concrete cubic calls
keep returning concrete `CubicBezier2D` pieces. `CubicBezier2D.FromHermite` converts endpoint tangents with
respect to normalized progress into the same shared cubic representation.

`FindParametersAtX(x, parameters)` writes all intersections into a span of at least three doubles, sorted in [0, 1].
It includes tangencies and endpoints and uses the shared `PolynomialRoots` solver. A curve lying entirely on that X
has infinitely many matches and throws. `ParameterAtX(x)` is the convenience query when exactly one result is expected;
it throws for missing or ambiguous results.

Animation's scalar time/value functions are constructed by `Easingf.Bezier` or `Easingf.TimedBezier` and sampled
as `ProgressMapf` values. The factories share the scalar/vector `BezierMath` basis with geometric curves.
Scalar interpolator recipes read and write their Bezier controls in `Mathematics/Serialization`.

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

`ConvexPolygon2D.Builder.CreateRegularHexagon(radius, center, angleRadians)` creates a six-vertex
convex polygon that uses the existing polygon queries and JSON format. The radius is measured
from the center to a corner. Zero rotation gives horizontal top and bottom edges.
`VertexGenerator2D.WriteRegularHexagon` writes the same corners into an existing buffer.

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
are excluded from the JSON schema. Each built-in shape and curve declares its own `Kind` constant.
`GeometryJson2D.ShapeKinds` and `CurveKinds` enumerate them in their serialized catalog order.
`Rectangle2D` uses `"kind": "rectangle"`; `AxisAlignedRectangle2D` uses `"kind": "axis-aligned-rectangle"`.
The kind alone selects the rectangle type, including inside composite parts; there is no `axisAligned` flag.
Geometry values use `Vector2f`. The geometry profile registers `Mathematics.Serialization.Vector2JsonConverter` for the `{ "x": ..., "y": ... }` coordinate format. It can also be registered in other
`JsonSerializerOptions.Converters` collections.

`ShapeVertices2D` exposes exact local convex cores and sampled drawing outlines without allocating:

```csharp
Span<Vector2f> buffer = stackalloc Vector2f[shape.GetVertCount()];
ReadOnlySpan<Vector2f> core = shape.GetVerts(buffer, out float radius);
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
