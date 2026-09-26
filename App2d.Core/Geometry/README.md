# Geometry

Use `App2d.Core.Geometry` functions directly with `System.Numerics` values when you need math without constructing a shape. The folders separate algorithms from objects; their public namespace stays the same.

| Location | Purpose |
| --- | --- |
| `Functions/VertexGenerator2D` | Circle, ellipse, arc, rectangle, rounded rectangle and capsule contours into a caller-owned `Span<Vector2>` |
| `Functions/PrimitiveGeometry2D` | Areas, containment, support points, segment distance and normalized picking scores |
| `Functions/Projection2D` | Polygon, circle and capsule intervals on an arbitrary axis; polygon offsets avoid transformed copies |
| `Functions/PolygonGeometry2D` | Area, containment, support, closest perimeter point, edge normals and convex SAT overlap |
| `Functions/ClosestPoint2D` | Point-to-segment and segment-to-segment closest points |
| `Functions/Rect2DExtensions` | Shared dimensions, anchors, containment, intersection, union, closest point, movement and resizing for any `IRect2D` |
| `Functions/BoundsGeometry2D` | Bounds from raw primitives/points, union, translation, scaling and affine transforms |
| `Functions/ShapeBounds2D` | On-demand local bounds for shapes, including convex support-point fallback and unbounded half-spaces |
| `IRect2D`, `Rect2D` | A two-property rectangle contract and a lightweight rectangle value independent of shapes |
| `Shapes/` | `IShape2D`, `IConvexShape2D` and concrete shapes with validated geometry; no bounds properties or caches |
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

## Bounds ownership

Shapes describe geometry. Use `ShapeBounds2D.Calculate(shape)` when you need their local bounds without attaching them to anything. For raw data, use `BoundsGeometry2D.FromCircle`, `FromCapsule`, `FromRectangle` or `FromPoints`.

`SpatialObject2D` owns `LocalBounds`, calculated once when its immutable shape is attached, and `WorldBounds`, updated on demand when `Transform.Version` changes. `WorldObject2D` inherits that ownership. Rendering and collision use the spatial object's cache. A shape attached to an object must remain immutable; future geometry replacement must refresh the local bounds and invalidate the world bounds cache together.

`BoundsGeometry2D.Transform` encloses a transformed box. Matrices with no rotation/shear use translation or component-wise scale/translation, correctly ordering mirrored edges. Other matrices transform the four corners. The rotated result is conservative for the underlying shape: for example, rotating a circle's local box can produce a larger box than the circle needs. Tighter shape-specific world bounds can be added later without putting caches back on shapes.

Half-spaces remain shapes and produce `Bounds2D.Unbounded`; transformation preserves that sentinel so they stay broad-phase candidates. Built-in shapes and custom `IConvexShape2D` implementations have bounds calculations. New non-convex shape types must add a calculation to `ShapeBounds2D`; unsupported types fail explicitly. `Bounds2D.FromPoints` and `TransformedBy` remain convenience wrappers around the shared functions.

```csharp
var local = ShapeBounds2D.Calculate(shape); // no cache or object needed
var moved = BoundsGeometry2D.Translate(local, position);
var transformed = BoundsGeometry2D.Transform(local, matrix);
var placed = new SpatialObject2D(shape);
var cached = placed.LocalBounds;
```

## Rectangles without shapes

`IRect2D` requires only ordered `Vector2 Min` and `Vector2 Max` properties. Implement it on a class or struct and import `App2d.Core.Geometry` to get the shared operations directly on your type. C# 14 extension properties provide dimensions and anchors without interface casts; generic receivers avoid boxing value types.

```csharp
var rect = Rect2D.FromSize(new(20, 10), center: new(5, 3));
var corner = rect.TopLeft;
var midpoint = rect.CenterLeft;
var area = rect.Area;
var padded = rect.InflatedBy(2, 1);
var hit = rect.Contains(point);
if (rect.TryIntersect(otherBounds, out var shared)) { /* use shared.Min / shared.Max */ }

// Your existing type needs only these two properties:
public readonly record struct Region(Vector2 Min, Vector2 Max) : IRect2D;
// A Region now has .Width, .TopLeft, .Contains(...), .Intersects(...), etc.
```

The nine anchors are `TopLeft`, `TopCenter`, `TopRight`, `CenterLeft`, `Center`, `CenterRight`, `BottomLeft`, `BottomCenter`, and `BottomRight`. `Left`, `Right`, `Bottom`, `Top`, `MidX`, `MidY`, `Width`, `Height`, `Size`, `HalfSize`, `Area`, and `IsFinite` are also available.

`Bounds2D`, `Rectangle2D`, and `AxisAlignedRectangle2D` implement the contract. A shape's rectangle coordinates are still local to that shape; a rotated world transform does not turn them into a world AABB. `ScreenRectangle2D` stays separate because it uses Y-down coordinates and half-open pixel containment.

Rectangles use Y-up (`Top = Max.Y`) and inclusive edges. Touching edges/corners intersect; zero-area rectangles are valid, and `default(Rect2D)` is the point at the origin. `TryIntersect` returns false and default for disjoint rectangles. `Contains(otherRect)` requires the entire rectangle to fit. `Union` returns the smallest containing rectangle; `ClosestPoint` includes the interior. `TranslatedBy`, `InflatedBy`, `InsetBy` and `ToRect` return new values. Inflation/inset amounts must be nonnegative and finite; oversized insets collapse an axis at its midpoint.

`Rect2D` stores only Min/Max and checks their ordering and absence of NaN. Infinite bounds are supported for broad-phase queries, preserving `Bounds2D.Unbounded`; centers and midpoints can be undefined for infinite extents. Corner writing requires finite coordinates. There is no `LocalBounds` cache or `IShape2D` requirement.
