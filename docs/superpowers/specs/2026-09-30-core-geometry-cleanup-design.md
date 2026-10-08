# Core Geometry Cleanup Design

**Date:** 2026-09-30
**Status:** Approved in chat (layout, Bounds2D merge, behavior fixes)

## Goal

Make `App2d.Core` geometry coherent: one axis-aligned box type, no grab-bag `Functions`
folder, a clear layering (raw math, then shapes, then collision dispatch), exact ellipse
distance, distance-squared queries where they save work, a complete collision table, and
XML documentation on every public geometry function. Adding a new primitive afterwards
means: a value type or shape class, raw functions in `Geometry`, and (only if it collides)
one arm in each of the named per-shape tables.

## Findings (verified 2026-09-30)

- `Bounds2D` and `Rect2D` were duplicates. `Bounds2D` (positional record) never validated;
  `Rect2D` validated ordering. Box math was spread over `Bounds2D`, `BoundsGeometry2D` and
  `Rect2DExtensions`.
- `Geometry/Functions` held thirteen unrelated static classes under a meaningless name and
  imported `Shapes` for `Line2D`, `Ray2D` and `HalfSpace2D` (layering inversion).
- Point-to-ellipse signed distance polygonized the ellipse into 64 segments.
- No distance-squared queries existed.
- `ShapeCollision2D` had no `ConvexPolygon2D` row: polygon vs polygon, rectangle or capsule
  returned no contact.
- `Vector2Extensions.PositiveInfinity` held negative infinity (unused).
- Forwarders: `Interval1D.ProjectPolygon/ProjectCapsule` -> `Projection2D`,
  `PrimitiveGeometry2D.DistanceToSegment` -> `Distance2D`, and the world half-space
  transform lived in both `CollisionMath2D` and `Distance2D`.
- `SweptCircleAabb2D` was unused pure geometry sitting in `Collision/Intersections`.
- 3D triangle/mesh types lived in the 2D `Geometry` folder.

## Layout

```
App2d.Core/
  Mathematics/   unchanged apart from the infinity constant fix
  Geometry/      raw 2D math + small value types; never references IShape2D
    IGeometry2D, IRect2D, Rect2D, Rect2DExtensions, Interval1D, Line2D, Ray2D, RayHit2D
    Area2D, Containment2D, SupportPoint2D   (PrimitiveGeometry2D split by operation)
    Projection2D, ClosestPoint2D, Distance2D (+ .Linear, .Convex), Intersection2D, Raycast2D
    PolygonGeometry2D, PolygonClipping2D, VertexGenerator2D, LinearGeometry2D (internal)
  Shapes/        IShape2D classes + per-shape switch tables
    WorldShape2D (local shape + Similarity2D -> world parameters)
    ShapeBounds2D, ShapeDistance2D
  Meshes/        TriangleMesh2D, Triangle3D, TriangleMeshAnalysis3D, TriangleMeshBuilder3D, Orientation3D
  Curves/, Grids/ unchanged apart from Rect2D
  Collision/
    Contacts/ShapeCollision2D.cs   the double-dispatch table; every row explicit
    Contacts/ShapeCollision2D.*.cs pair algorithms
    Queries/RayIntersection2D.cs   shape switch over Raycast2D + SpatialObject2D
    Filtering/ gains DefaultColliderPairFilter2D; Intersections/ removed
```

Namespaces follow folders: `App2d.Core.Geometry`, `App2d.Core.Shapes`, `App2d.Core.Meshes`.
`App2d.Core.Geometry.Functions` no longer exists.

## Decisions

- `Rect2D` is the only box type. Properties keep their names (`Bounds`, `WorldBounds`,
  `LocalBounds`). `Rect2D` absorbs `Unbounded`, `FromPoints(span)`, `FromCircle`,
  `FromCapsule`; `TransformedBy(Matrix3x2)` joins the `IRect2D` extensions. `Rect2D`
  validates ordering, so a backwards box now throws instead of misbehaving silently.
- `TriangleMesh2D` stays a mesh, not an `IShape2D`; `ToCompositeShape()` is the bridge.
- Point-to-ellipse closest point uses Chatfield's evolute iteration (four steps, double
  precision). Point queries and circle-vs-ellipse contact/distance are exact. Ellipse
  against polygons, capsules and other ellipses still polygonizes (`Ellipse2D.CollisionSegments`);
  exact contact there needs GJK/EPA and is out of scope.
- Distance-squared functions exist only where they skip a square root: point/point,
  point/segment, segment/segment, point/line, point/ray, point/rectangle,
  point/polygon perimeter. Radius-subtracting and signed queries have no squared form.
- Axis-aligned lines are `Line2D.Horizontal(y)` and `Line2D.Vertical(x)`; no new type.
- Collision contact math is unchanged; the table gains `ConvexPolygon2D` rows by routing
  rectangles, triangles, convex polygons and polygonized ellipses through one polygon row.
- `PolygonClipping2D` takes a raw unit normal and offset instead of `HalfSpace2D`.
- Work happens directly on `main`, committed in phases, never pushed by Claude.

## Authored shapes (2026-10-01)

- `ShapeDefinition2D` is the JSON form of every built-in shape, kind-tagged like `CurveDefinition2D`, built on the
  shared `Geometry.Point2D`. Definitions are records so editors use `with` expressions; `WithKind` re-authors a
  definition as another kind fitted to the same bounds.
- Entity files are version 2: hit windows, the movement box, projectiles and guards store a `shape` definition instead
  of width/height pairs. `EntityAssetUpgrade` converts version 1 files in memory on load; the shipped entities were
  rewritten through the saver.
- `EntityRegion` carries a local shape plus a `Similarity2D` pose. A hit window's shape lives in its anchor frame
  (origin pushed along the anchor axis, +X along the axis, +Y toward the frame's across direction), so every kind,
  boxes included, rotates with its socket or prop. The movement shape sits on the feet with +X toward facing.
- Hurt layouts stay pose-driven (controls plus pad). Puppet parts keep width, height, taper and roundness as their
  authoring handles; a trapezoid is a four-vertex convex polygon in shape terms.

