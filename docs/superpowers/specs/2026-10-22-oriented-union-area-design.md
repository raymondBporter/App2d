# Oriented boundary area: rotated ellipses in the union integral, and a pairwise intersection area

Date: 2026-10-22. Follows `2026-10-22-oriented-shapes-design.md`, which gave `Ellipse2D` an axis.

## Intent

`ShapeArea2D.Union` already integrates conic arcs and polygon edges exactly, but only hands an ellipse to
that path when it is axis-aligned; a rotated ellipse falls back to a sampled outline. This work gives the
internal boundary part a frame so rotated ellipses are exact too, and exposes the intersection area of two
convex shapes from the same walk, selecting covered arcs instead of exposed ones.

## Why

- `Area2D.Union.cs` is the "boundary-arc engine" Raymond sketched: tan-half-angle quartic for crossings,
  arcs split at crossings, curved-midpoint containment, Green's theorem arc integral, Kahan sum, doubles
  throughout. The only thing it lacks is rotation, and the gap is a deliberate leftover from the oriented
  shapes work (`ShapeArea2D.cs` line 47).
- Intersection of two shapes is the complement selection of the same per-piece coverage. Summing covered
  arcs directly keeps precision for small overlaps, where `A + B - Union` loses most of its digits.

## Goals

- `ShapeArea2D.Union` is exact for rotated ellipses and no longer depends on `outlineSegments` for them.
- `CompositeShape2D` with `includeOverlap: false` is exact for rotated ellipse parts without change.
- `ShapeArea2D.Intersection(first, second)` returns the overlap area of any two `IConvexShape2D`, exact for
  every kind the union is exact for, through the same coverage code.
- Axis-aligned results are unchanged up to rounding; every existing area test stays green.

## Non-goals

- A public boundary-arc interface, `ParameterAt`, or Bezier parts. Bezier crossings exceed the quartic solver
  and need their own intersection method; that is a different piece of work.
- Intersection of more than two shapes, or of two groups beyond what one capsule decomposition needs.
- Analytic rounded rectangles in the union (a polygon plus corner circles, like the capsule). Natural follow-up.
- Exact farthest distance between rotated ellipses in `ShapeDiameter2D`.
- Any change to shape types, JSON, builders or the inspector.

## Anti-goals

- Reintroducing `A + B - Union` as the intersection implementation.
- Tolerant axis comparisons for coincidence. Canonical form makes exact comparison correct, as for radii today.
- Polyline approximations anywhere in production area code.

## Design

### The part carries a frame

`UnionPart` (renamed `BoundaryPart`, see renames) gains `Axis` and `Perp` as `Vector2d`, `Perp = Axis.PerpCcw`.
New constructor `(Vector2 center, Vector2 radii, Vector2 axis)`; the circle constructor passes `Vector2.UnitX`.
The constructor puts the part in canonical form, in this order:

0. Normalize the axis in double. `OrientedFrame2D.ValidateAxis` accepts an axis within 0.001 of unit length
   and the shapes project through the raw axis, so a slightly long axis already makes `ContainsPoint` and the
   outline disagree with each other; the part's ellipse has area exactly `pi * Rx * Ry`, which is what
   `Ellipse2D.Area` reports and what the inclusion-exclusion test compares against.
1. Circle (`radii.X == radii.Y`): axis becomes `(1, 0)`.
2. `radii.X < radii.Y`: swap the radii and rotate the axis by a quarter turn (`axis = axis.PerpCcw`).
3. `axis.X < 0`, or `axis.X == 0 && axis.Y < 0`: negate the axis.

All three are exact in floating point (swap, negation, `(-y, x)`), so two parts spelling the same ellipse
differently get bit-identical radii and axes, and coincidence stays an exact comparison.

Frame queries, all on center-relative doubles:

- `ToFrame(p) = (Axis.Dot(p), Perp.Dot(p))`; `FromFrame(q) = Axis * q.X + Perp * q.Y`.
- `NormalizeRelative(p) = ToFrame(p) / Radii` componentwise. It is linear, so it serves directions too:
  `CurveRoots` passes the segment direction through it instead of dividing by the radii inline.
- `PointAt(angle) = FromFrame((Radii.X * cos, Radii.Y * sin))` with today's exact endpoints at `0` and `Tau`.
- Bounds used by `Overlaps`: an axis-aligned part keeps `+-Radii` exactly; a rotated one uses half extents
  `(sqrt((Rx*Ax)^2 + (Ry*Px)^2), sqrt((Rx*Ay)^2 + (Ry*Py)^2))`, the `OrientedFrame2D.EllipseBounds` formula.
- `ContainsInterior` and `CoverCircle` keep their shape; circles have the identity frame.

### Ellipse against ellipse

In `CoverCurveWithCurve`, with `curve` = A and `other` = B:

```
o = B.NormalizeRelative(A.Center - B.Center)
u = B.NormalizeRelative(A.Axis * A.Radii.X)
v = B.NormalizeRelative(A.Perp * A.Radii.Y)
```

A's boundary in B's unit-disk coordinates is `p(theta) = o + u cos(theta) + v sin(theta)`. Substituting
`t = tan(theta / 2)` into `|p|^2 = 1` and multiplying by `(1 + t^2)^2` gives a quartic. With `k = o.o - 1`
and `s = +1` for chart 0, `s = -1` for chart 1 (chart 1 is `theta + pi`, which negates `u` and `v`):

```
c0 = k + u.u + 2 s (o.u)
c1 = 4 s (o.v) + 4 (u.v)
c2 = 2 k - 2 u.u + 4 v.v
c3 = 4 s (o.v) - 4 (u.v)
c4 = k + u.u - 2 s (o.u)
```

Roots map to angles as today, `chart * pi + 2 * atan(t)`, then `PositiveAngle`. With `u = (sx, 0)` and
`v = (0, sy)` these are exactly today's coefficients, so axis-aligned results do not change.

Coincidence: `Radii` equal and `Axis` equal (exact, canonical) and `|o|` within `ConicCoincidenceTolerance`,
with today's `preferOther` tie-break.

### Segments and polygons against an ellipse

`CoverCurveWithPolygon` already routes the vertex through `NormalizeRelative`. Its rounding-magnitude guard
becomes `1 + (|offset.X| + |offset.Y| + |vertex.X| + |vertex.Y|) / Radii.Y`; `Radii.Y` is the smaller radius
in canonical form, so for an axis-aligned part this is never below today's value and keeps every corner
contact today's tests rely on. `CurveRoots` changes only in how the direction is normalized.

### One walk, two selections

The per-piece loop becomes a private `Integrate(ReadOnlySpan<BoundaryPart> parts, int firstCount, bool intersect)`.
Parts before `firstCount` are group one, the rest group two. For each boundary piece (a whole conic, or one
polygon edge) the walk collects two interval lists with today's coverage functions and today's `j < i`
tie-break: `own` (covered by parts of the same group) and `other` (covered by parts of the other group).

- Union: integrate the gaps of `own` (today's `IntegrateExposed`). `Union(parts)` is
  `Integrate(parts, parts.Length, false)`; `other` stays empty.
- Intersection: integrate the gaps of `own` clipped to the merged intervals of `other`. The boundary of
  `A ∩ B` is the part of `∂A` inside `B` plus the part of `∂B` inside `A`, and `∂A` is what group one
  exposes within itself, which is why a capsule's internal rectangle edges drop out. One new helper walks
  both sorted lists and feeds the same per-piece integrand.
- `Intersection(first, second)` concatenates the two spans and calls `Integrate(all, first.Length, true)`.

Coincident boundaries count once under both selections: the tie-break puts the shared piece in exactly one
part's covered list, so union integrates it from the uncovered side and intersection from the covered side.
Opposite-side coincident edges (two rectangles touching along an edge) cancel under intersection because
both are traversed in opposite directions. The result is clamped at zero as today.

### Public surface

- `ShapeArea2D.Union`: drop the `IsAxisAligned` guard; ellipses pass center, radii and axis. Rotated
  `Rectangle2D` already enters as its four corners through the outline path, so nothing changes for it.
- New `ShapeArea2D.Intersection(IConvexShape2D first, IConvexShape2D second, int outlineSegments = DefaultOutlineSegments)`
  returning `float`. Same guards as `Union`. Both `IRect2D`: `Area2D.RectangleIntersection(firstMin, firstMax, secondMin, secondMax)`,
  a new raw op that is the overlap width times height `RectangleUnion` already computes. Otherwise each shape
  maps to its parts through the per-shape conversion `Union` uses today, extracted into a private
  `AddParts(List<BoundaryPart> parts, IConvexShape2D shape, int outlineSegments)`, then `Area2D.Intersection`.
- `CompositeShape2D` is untouched and inherits exact rotated ellipses.
- Renames, so the structure is visible without a public abstraction: `UnionPart` to `BoundaryPart`,
  `ArcArea` to `SignedArcArea`, file `Area2D.Union.cs` to `Area2D.Boundary.cs` via `git mv`.
- `App2d.Core/Geometry/README.md`, the composite area paragraph: ellipses stay analytic in any orientation,
  and `ShapeArea2D.Intersection` exists; the rounded rectangle sampling sentence stays true.

## Tests

Extend `ShapeArea2DTests.cs` for the union and add `ShapeIntersectionArea2DTests.cs` for the pairwise entry.
The independent oracle `CrossSectionArea` must learn rotated ellipses: at each `x`, the points `(x, y)` with
`q = ToFrame((x, y) - C)` satisfy `(q.X / Rx)^2 + (q.Y / Ry)^2 = 1`, a quadratic in `y`; its two roots are
the interval. Rotated rectangles may stay in engine-versus-engine tests or get an edge-crossing branch.

Union:

1. Rotating every part of a mixed union by one angle about one point leaves the area unchanged
   (`MixedEllipseUnionsAgreeWithIndependentCrossSectionIntegration` style, random angles, cross-section oracle).
2. A rotated ellipse union gives the same value with `outlineSegments` 3 and 128.
3. `EllipsePairsAgreeWithAnAffineTransformationOfTheCircleLens` rotated by a fixed angle: expected unchanged.
4. Four crossings: `(a, b)` against `(a, b)` with axis `UnitY` equals today's `(b, a)` expectation
   `4ab * atan2(a, b)`; the same pair at 45 and 135 degrees too.
5. Coincidence through canonical form: axis `U` against `-U`; `(Rx, Ry, U)` against `(Ry, Rx, Perp(U))`;
   a circle with any axis against a plain circle. Each union equals one area.
6. A rotated ellipse against a rotated `Rectangle2D` and against a `ConvexPolygon2D`, against the oracle.
7. Thin rotated ellipse (`50 x 0.01`) crossing a circle, against the oracle, to exercise the magnitude guard.

Intersection:

8. Inclusion-exclusion on random pairs drawn from rotated ellipses, circles, rotated rectangles and capsules:
   `Intersection(a, b) + Union([a, b]) == a.Area + b.Area` to five places, and `Intersection(a, b) == Intersection(b, a)`
   to five places (part order changes tie-breaks and summation order, as in the union reorder tests).
9. `0 <= Intersection(a, b) <= min(a.Area, b.Area)` on the same draws.
10. Identical rotated ellipses give `pi * a * b`; one inside another gives the smaller area; disjoint gives 0;
    external tangency gives 0; internal tangency gives the smaller area.
11. Unit circles at distance 1 equal `CircleLens(1, 1, 1)`; at distance `1.999` equal the lens within `1e-9`
    absolute (about `4.2e-5`), which the subtraction route cannot reach in float.
12. Two and four crossings between rotated ellipses, against the cross-section oracle; one case where a
    crossing straddles a part's parameter seam (ellipse B centered on A's `+X` vertex).
13. Capsule against rotated ellipse (group semantics) matches `a.Area + b.Area - Union` to five places.
14. Translating both shapes and rotating both about one point preserve the result.
15. Two `AxisAlignedRectangle2D` take the fast path and equal the polygon path's value.
16. `null` parts and `outlineSegments < 3` throw; `CompositeShape2D` with rotated ellipse parts and
    `includeOverlap: false` equals the engine's exact union regardless of `AreaOutlineSegments`.
17. An ellipse built raw (analyzer pragma) with an axis of length `1.0005`, inside the validation tolerance:
    `Intersection(e, e)` equals `e.Area` to five places, and its union with a far circle equals the two areas.

## Order of work

1. Renames, the frame in the part, canonical form, projections, bounds, coefficients, guard removal, oracle
   extension and union tests. Commit when the whole suite is green.
2. The two-selection walk, `Area2D.Intersection`, `Area2D.RectangleIntersection`, `ShapeArea2D.Intersection`,
   intersection tests and the README paragraph. Commit.

## Workflow constraints

- Work in the worktree `.claude/worktrees/rotatable-rect-design-5e94ff` on branch
  `claude/rotatable-rect-design-5e94ff`, fast-forwarded to main's `3e4125ee` on 2026-10-22. Raymond edits
  in the main checkout concurrently; at merge time check `git status` there, back up overlapping files,
  then `git checkout --` and `git merge --ff-only`, as on 2026-10-22.
- Single-line method signatures and parameter lists, however long.
- The Write tool and sed emit LF; normalize every written file to CRLF.
- No Bash heredocs containing code.
- Commit per task on the branch. Merge into `main` only when Raymond says. Never push.
- Visual Studio stays open; retry a locked write once.
- Gates: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName!~GameUsesConfiguredWidthForTheActualCollidableTerrain"`
  and `dotnet test App2d.Analyzers.Tests/App2d.Analyzers.Tests.csproj`.

## Left to the builder

- Whether `Integrate` takes a bool or a two-value enum, and the shape of the interval clipping helper.
- Whether the intersection tests share the random draw helper with the union tests or keep their own.
- Test values and tolerances beyond the ones pinned above.
- Whether rotated rectangles get an oracle branch.

## Calls I made (flip any of these)

- Renames: `BoundaryPart`, `SignedArcArea`, `Area2D.Boundary.cs`.
- Group semantics for intersection (two spans, `firstCount`) rather than a two-part-only walk, so a capsule
  works through its decomposition.
- A rectangle fast path for `Intersection` and the new `Area2D.RectangleIntersection` raw op.
- Canonical form in the constructor instead of a tolerant axis comparison.
- The part normalizes its axis in double, so its area is `pi * Rx * Ry` whatever the float axis length within
  tolerance. Making the shapes themselves normalize is out of scope and noted as a possible follow-up.
- Argument-order symmetry of `Intersection` is tested to five places, not bit for bit.
- The magnitude guard divides by the smaller radius.
- Axis-aligned parts keep exact `+-Radii` bounds so existing tangency behavior is bit-identical.
- No `ParameterAt`; angles come straight from the quartic roots.
