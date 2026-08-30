# Collision Cleanup & Similarity2D Design

**Date:** 2026-08-29
**Status:** Approved in chat (phases 1 and 2)

## Goal

Less code, cleaner code in the 2D collision layer: extract shared primitives, unify the
narrow-phase dispatch, fix the real allocation offenders, move tile-map sources out of the
`App2d.Collision` project, and replace raw `Matrix3x2` consumption in collision with a
validated `Similarity2D` transform (rotation + uniform scale + mirror + translation).

## Context findings (verified 2026-08-29)

- Source of truth for all code is the `App2d` folder. Sibling projects (`App2d.Core`,
  `.Collision`, `.Physics`, `.Rendering`, `.Audio`, `.Gameplay`) compile subsets via
  `<Compile Include="..\App2d\...">` links with `EnableDefaultCompileItems=false`.
  `App2d.csproj` `Compile Remove`s those files and references the projects.
- `App2d.Collision.csproj` links `Engine\Geometry`, `Engine\Tiles`, and `SpatialObject2D`
  under a `Shapes\` link prefix — that is the "tile map stuff in Collision" complaint.
  No collision *source* references tile types.
- Narrow phase is span/stackalloc-based; real allocation offenders are
  `SideScrollerChunkStreamer2D.Update`'s per-frame `Keys.ToArray()`,
  `PhysicsWorld2D.IsTouching`'s LINQ closure, and the collision grid retaining empty
  cell buckets forever.
- Non-uniform-scale handling is inconsistent: circle-vs-circle falls back to a 40-vertex
  ellipse polygon; circle-vs-capsule and capsule-vs-capsule silently report no contact.
- The player mirrors via `Transform.Scale = (±1, 1)` on its collidable — mirror must work.
  Rope link visuals use genuinely non-uniform scale but are render-only
  (`WorldObject2D : SpatialObject2D`); collision never sees them.
- `HalfSpaceCollision2D` is dispatched outside `ShapeCollision2D` by the contact provider
  and fabricates the contact point as `Transform.Position`.

## Phase 1 — mechanical cleanups (behavior-preserving except where noted)

1. **Vector helpers:** `PerpCcw`/`PerpCw` on `Vector2Extensions`; replace all hand-rolled
   `new Vector2(-d.Y, d.X)` / `new Vector2(d.Y, -d.X)`; delete the private `Cross` in
   `RayIntersection2D`.
2. **Rect corners:** `Rectangle2D.WriteCorners(Span<Vector2>)`; delete
   `WriteLocalRectangleVertices` and the inline copy in `RayIntersection2D`.
3. **HalfSpace dispatch fold-in:** convex-vs-halfspace becomes a `ShapeCollision2D`
   dispatch row using the support-point math. Contact point becomes the deepest support
   point projected onto the plane (matches the circle-vs-halfspace convention) —
   *deliberate behavior improvement* over `Transform.Position`.
   `ShapeCollisionContactProvider2D` becomes a one-line delegation.
   `HalfSpaceCollision2D.TryGetContact`/`ConstrainOutside` keep their signatures,
   delegating to the shared math.
4. **Shared polygon/segment math:** new `Engine\Geometry\PolygonGeometry2D`
   (SignedAreaTwice, ContainsPoint, GetSupportPoint, ClosestPointOnPerimeter,
   GetOutwardEdgeNormal). `ConvexPolygon2D` and `ShapeCollision2D` delegate to it.
   `ClosestPoint2D` moves from `Engine\Collision` to `Engine\Geometry` (namespace
   follows); `Capsule2D.ContainsPoint` uses `ClosestPoint2D.OnSegment`.
   `CapsuleVsCapsule` uses `TryUpdateMtv` instead of its inline copy.
5. **Tile mesher dedupe:** `Engine\Tiles\TileRectangleMesher2D` holds the greedy
   rectangle-merge (horizontal run of equal kind; vertical growth only for solid kinds).
   `TileMap2D` and `ProceduralTileMap2D` keep their public APIs and delegate.
6. **Allocation fixes:** reusable unload buffer in `SideScrollerChunkStreamer2D`;
   foreach in `PhysicsWorld2D.IsTouching`; prune empty grid buckets past a retention cap
   in `CollisionSystem2D`.
7. **Project re-wiring:** `Engine\Geometry` + `SpatialObject2D` links move to
   `App2d.Core`; new `App2d.Tiles` project links `Engine\Tiles` and references Core;
   `App2d.Collision` keeps only `Engine\Collision`; `App2d`, `App2d.Gameplay`,
   `App2d.Tests` reference `App2d.Tiles`; drop `App2d.Rendering`'s Collision reference
   if the build allows; fix the `Raycasts` link prefix to `Queries`. Namespaces stay
   `App2d.Engine.*`.

## Phase 2 — Similarity2D

New `Engine\Mathematics\Similarity2D` readonly struct storing the images of the local
axes plus translation and scale:

- Fields: `Vector2 XAxis`, `Vector2 YAxis` (orthogonal, equal length = `Scale`),
  `Vector2 Translation`, `float Scale`.
- `TryFromMatrix(Matrix3x2, out Similarity2D)` — accepts rotation + uniform scale +
  mirror; rejects non-uniform or degenerate scale (relative tolerance 0.001, matching
  the current `TryGetUniformScale`).
- `TransformPoint`, `TransformDirection`, `TransposeTransformDirection` (support-
  direction mapping), `InverseTransformPoint`, `InverseTransformDirection`.
- Row-vector convention matches `Matrix3x2`: `XAxis = (M11, M12)`, `YAxis = (M21, M22)`.
  For this family `(A⁻¹)ᵀ = A / Scale²`, so world normals are `TransformDirection`
  + normalize.

`SpatialObject2D.CollisionPose` caches the extraction by `Transform.Version` and throws
(`StateGuard`) on non-uniform/degenerate scale. Narrow phase (`ShapeCollision2D`,
`CollisionMath2D`, halfspace math) and ray queries (`RayIntersection2D`) consume
`Similarity2D` instead of `Transform2D`/`Matrix3x2`. The ellipse fallback and
`TryGetUniformScale` are deleted; a non-uniformly scaled collidable now throws instead of
silently missing or approximating. `SpatialObject2D.ContainsWorldPoint` stays
matrix-based (valid for render objects too).

## Testing

xUnit (`App2d.Tests`). Characterization tests pin normals/depths at the seams before
refactoring (halfspace contacts, capsule MTV, tile meshing equivalence). Phase 2 adds
pose round-trip tests (rotation, scale, mirror), mirrored-collidable contact tests, and
a throws-on-non-uniform test. Every task ends with `dotnet build App2d.slnx` +
`dotnet test App2d.Tests` green (baseline: 14/14).

## Out of scope

Broad-phase rebuild frequency (CPU, not allocation), moving physics sources, renaming
namespaces to match assemblies, static-abstract transform hierarchy.
