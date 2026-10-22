# Oriented rectangles, rounded rectangles and ellipses

Date: 2026-10-22. Branch: `claude/rotatable-rect-design-5e94ff`.

## Intent

`Rectangle2D`, `RoundedRectangle2D` and `Ellipse2D` gain an orientation in their own local
space. A composite can then hold a rotated box, authored JSON can say "this box at 30
degrees", and every shape except `AxisAlignedRectangle2D` closes under a similarity
(translate, rotate, uniform scale, mirror). The final step adds that operation:
`shape.Posed(Similarity2D)` returns a new shape of the same kind.

## Why

- The three shapes store axis-aligned extents (`Min`/`Max`, or `Center`/`Radii`). Their only
  rotation comes from the owner's pose. `CompositeShape2D` parts have no per-part pose, so a
  rotated box inside a composite is impossible today except as a `ConvexPolygon2D` (what
  Trials does with `Tube`).
- `Rectangle2D` and `AxisAlignedRectangle2D` have identical storage and differ in two switch
  cases. The names promise a difference the code does not deliver.
- The intended `pose x shape -> shape` operation cannot be written for these types.
- Nothing in the engine does `world = pose x shape` and keeps the result. Shapes are immutable
  local geometry and poses are applied at query time. So there is no drift problem to solve:
  the orientation is fixed at construction and the pose matrix already carries the frame
  chain. `Similarity2D.TryFromMatrix` rejects a pose whose axes drifted.

## Goals

- `Rectangle2D`, `RoundedRectangle2D`, `Ellipse2D` carry a unit `Axis` (the shape's local +X).
- Every existing builder call compiles unchanged and means what it meant (axis-aligned).
- `min`/`max` stays an accepted authoring form for rectangles and rounded rectangles. It is
  the axis-aligned spelling, so switching `kind` between the two rectangle kinds keeps working,
  and it is a feature rather than legacy support (Raymond's call). Angle-less ellipses read
  as axis-aligned. New JSON is
  written in the new form.
- Character Studio's shape inspector gets an angle field for the three kinds.
- `ShapeTransforms2D.Posed(shape, Similarity2D)` for every built-in shape, with
  `AxisAlignedRectangle2D` refusing poses that would rotate it.

## Non-goals

- `AxisAlignedRectangle2D` stays `Min`/`Max`. It is now the one shape whose contract is "the
  owner keeps the world axes aligned", and it enforces that in `Posed`.
- `HalfSpace2D` stays a shape. Demoting it to geometry, or a bounded-geometry interface, is a
  separate discussion Raymond is thinking about.
- No bulk rewrite of the twenty asset files holding the 34 `"kind": "rectangle"` entries. They
  read through the `min`/`max` form and are rewritten in the new form whenever an editor saves them.
  (My call; a one-shot rewrite tool is a small follow-up if Raymond wants the repo JSON current.)
- Non-uniform scale and shear are not representable and stay out of collision, as today.
- `HeightFieldShape2D` does not get an orientation.
- Exact analytic pair solvers for two rotated ellipses (union area, farthest distance). See
  the touchpoints: those fall back to the sampled and numerical paths when an ellipse is rotated.

## Anti-goals

- An angle stored as the orientation. Every query needs cos and sin; storing them is the point.
- Hidden renormalization at query time. The builder normalizes once; queries trust the axis.
- Changing the meaning of any existing builder overload or JSON that reads today.
- A stale two-argument raw constructor call that compiles and silently reads `(min, max)` as
  `(center, halfSize)`.

## Representation

| Shape | Stored | Derived |
| --- | --- | --- |
| `Rectangle2D` | `Center`, `HalfSize`, `Axis` | `Size`, `Width`, `Height`, `AngleRadians`, `Area`, `Diameter` |
| `RoundedRectangle2D` | `Center`, `HalfSize`, `Radius`, `Axis` | inner half size `HalfSize - Radius` (may collapse to a segment or point), plus the above |
| `Ellipse2D` | `Center`, `Radii`, `Axis` | `AngleRadians`, `Area`, `Diameter` |

- `Axis` is a unit `Vector2`, not an angle and not a `Direction2D`. `Direction2D` throws on a
  zero vector in its constructor, which would break raw JSON construction and `default`; a
  plain vector keeps construction raw and validation in `Validate`, as every other shape does.
- An unset axis reads as +X. The property getter returns `Vector2.UnitX` when the backing field
  is zero. `default(Rectangle2D)`, object initializers such as the one in `PersonWardrobe`, and
  `min`/`max` JSON therefore stay axis-aligned without special cases. `Validate` still rejects an
  explicit non-unit axis.
- The local +Y of the shape is `Axis.PerpCcw` (`(-Axis.Y, Axis.X)`). Mirroring never happens
  inside a shape; a mirrored pose is applied outside.
- `AngleRadians` is `Polar2D.AngleOf(Axis)`, computed on demand for editors and JSON.
- `Rectangle2D` stops implementing `IRect2D`. `Min` and `Max` mean nothing once rotated.
  `AxisAlignedRectangle2D` keeps `IRect2D` and the `Rect2DExtensions` members.

### Validation

- `HalfSize` and `Radii` finite and strictly positive (today's `min < max` rule, restated).
- `Radius` finite, in `[0, min(HalfSize)]`, same rule as today.
- `Axis` finite, length within `0.001` of one, the tolerance `Similarity2D.TryFromMatrix` uses.
- `Center` finite.

### Frame math

Every query is: map into the shape's frame, run the raw axis-aligned operation that exists
today, map back. The shape's frame is `Center` and `Axis`:

- local point to frame: `(dot(Axis, p - Center), dot(Perp, p - Center))`
- frame point to local: `Center + Axis * x + Perp * y`
- local direction to frame: `(dot(Axis, d), dot(Perp, d))` (the transpose, no subtraction)
- frame direction to local: `Axis * x + Perp * y`

This is `Similarity2D.FromAxis(Center, Axis)` with unit scale, but `FromAxis` renormalizes and
guards on every call, so the shapes use the four lines above directly. A small shared helper
holding `Center` and `Axis` with those four operations and a span map-in-place keeps the three
shapes from triplicating it. Its name and namespace are the builder's call; it belongs beside
the raw operations, not in `Mathematics.Transforms`.

Raw operations then take frame-space arguments with the shape at the origin:

| Query | Rectangle | Rounded rectangle | Ellipse |
| --- | --- | --- | --- |
| Contains | `Containment2D.Rectangle(local, -HalfSize, +HalfSize)` | `SignedDistanceToRectangle(local, -inner, +inner) <= Radius` | `Containment2D.Ellipse(local, 0, Radii)` |
| Support | `SupportPoint2D.Rectangle(frameDir, ...)` mapped back | `SupportPoint2D.Circle(frameDir, rect support, Radius)` mapped back | `SupportPoint2D.Ellipse(frameDir, 0, Radii)` mapped back |
| Signed distance | `SignedDistanceToRectangle(local, ...)` | same minus `Radius` | `SignedDistanceToEllipse(local, 0, Radii)` |
| Corners / core verts | `WriteRectangle(-HalfSize, +HalfSize)` then map | inner rectangle then map; collapse rules unchanged | no polygonal core, unchanged |
| Outline | as corners | `WriteRoundedRectangle` in frame then map | `WriteEllipse` in frame then map |
| Area, diameter | unchanged formulas on `HalfSize`/`Radii` | unchanged | unchanged |
| Mass | box about `Center`, `(w^2 + h^2) / 12` | `RoundedBox(a, b, r, center)` already frame-free | unchanged |
| Local bounds | half extent x = `hx * abs(Axis.X) + hy * abs(Perp.X)`, y likewise | same on the inner half size, plus `Radius` each side | half extent x = `sqrt((rx * Axis.X)^2 + (ry * Perp.X)^2)`, y likewise |
| Transformed bounds (ellipse) | n/a | n/a | compose the frame matrix with the transform, then the existing `TransformedEllipse` |
| Raycast, closest point, circle-vs-ellipse | project the ray or point into the frame, call the raw op, map the hit back | same | same (`Raycast2D.TryEllipse`, `ClosestPoint2D.OnEllipsePerimeter`, `ShapeConvexQuery2D.CircleEllipse` compose the pose) |

Scalar inertia is rotation-invariant, so the mass table only needs the center.

### Pair operations between two ellipses

Two solvers take two ellipses in the same space with no pose and are axis-aligned by
construction: the union-area integral (`ShapeArea2D.Union` feeding `Area2D.UnionPart(center,
radii)`) and the farthest-distance solver (`ShapeDiameter2D` calling
`FarthestDistance2D.BetweenEllipses` at two sites). Projecting into one frame cannot serve two
different axes. Decision: when either ellipse has an axis other than +X, the union uses the
outline-sampled part the polygon path already uses, and the diameter uses
`FarthestDistance2D.BetweenConvex`. When both are axis-aligned the exact paths stay. Every
ellipse in authored JSON today (79 entries) is axis-aligned, so no existing asset changes
result. This matches the rectangle decision below (the two-`IRect2D` union fast path no longer
catches `Rectangle2D`). Extending the analytic solvers is a follow-up if exactness for rotated
ellipses ever matters; `CompositeShape2D.Area` with `IncludeOverlap = false` and composite
`Diameter` are the only consumers.

## JSON

| Kind | Written | Also read (axis-aligned spelling) |
| --- | --- | --- |
| `rectangle` | `center`, `size`, `angleRadians` | `min` + `max` -> center `(min + max) / 2`, size `max - min`, angle 0 |
| `rounded-rectangle` | `center`, `size`, `radius`, `angleRadians` | same as rectangle, plus `radius` |
| `ellipse` | `center`, `radii`, `angleRadians` | missing `angleRadians` -> 0 (no other change) |

- `size` is the full width and height, matching every builder and the inspector. (My call over
  `halfSize`; people tweak width and height, not half of them.)
- `angleRadians` is optional on read, default 0, and always written. Radians follow the whole
  codebase and the `Arc2D` precedent (`startAngleRadians`, `sweepAngleRadians`). The name says
  the unit so a hand edit is never ambiguous.
- A rectangle object carries either `min` and `max` or `center` and `size`, never both; mixing
  them is a `JsonException`. The `min`/`max` members count as read for
  `JsonUnmappedMemberHandling.Disallow`. `angleRadians` is accepted with either spelling.
- The reader keeps raw construction; `Validate` runs afterwards as today. A `min`/`max` rectangle
  with `min > max` becomes a negative size and still fails validation (the existing
  "backwards" test in `ShapeSerialization2DTests` keeps passing with that expectation).

## Builders

- Every existing `Builder` member stays. The angle is added to the three canonical members
  only: `FromSize(Vector2 size, Vector2 center, float angleRadians = 0f)`,
  `FromSize(float width, float height, Vector2 center, float angleRadians = 0f)` and
  `Create(float width, float height, Vector2 center, float angleRadians = 0f)`, plus the rounded
  and ellipse equivalents. The `CreateFrom*` and `FromDimensions` aliases are left exactly as
  they are: Raymond is pruning those aliases in the main checkout right now (`Circle2D.Builder.
  CreateFromPoints` is already gone there), and that pass stays his. New code, the inspector
  and the new tests call the canonical three.
- `Create(min, max)` stays and yields +X.
- New: `Create(Vector2 center, Vector2 halfSize, Vector2 axis)` (any finite nonzero axis,
  normalized through `Direction2D`), and the rounded and ellipse equivalents.
- Raw constructors become `(center, halfSize, axis)`, `(center, halfSize, radius, axis)` and
  `(radii, center, axis)`, with `axis` a required parameter and no two-argument overload. A
  stale raw call then fails to compile instead of silently reading corners as center and half
  size. Known stale raw calls to update: `GeometryValidation2DTests`, `NativeRigTests`, and the
  C# source strings inside the analyzer tests.
- `AxisAlignedRectangle2D` stops delegating to a private `Rectangle2D` and calls the raw
  operations with its `Min`/`Max` directly. `Rectangle2D.ValidateBounds` moves with it or into
  the shared validation.
- `RoundedRectangle2D.WithSize` keeps `Center` and `Axis` (used by `ModelAuthoring`).

## Posed operation (last step)

`ShapeTransforms2D.Posed(IShape2D shape, Similarity2D pose)` returns a new shape of the same
kind, a per-type switch shaped like `Scaled`:

| Shape | Result |
| --- | --- |
| Circle | center transformed, radius times `pose.Scale` |
| Capsule | endpoints transformed, radius scaled |
| Triangle, ConvexPolygon, SimplePolygon | vertices transformed (either winding is accepted) |
| Rectangle, RoundedRectangle, Ellipse | center transformed, axis = normalized `pose.TransformDirection(Axis)`, extents and radius scaled |
| HalfSpace | the existing `ShapeTransforms2D.HalfSpace` math, repackaged |
| Composite | each part posed, same flags |
| AxisAlignedRectangle | allowed only when both pose axes are parallel to world axes within the `0.001` tolerance `Similarity2D.TryFromMatrix` uses (0, 90, 180, 270 degrees, mirrors included); the corners are transformed and `Rect2D.FromPoints` reorders them. Otherwise `NotSupportedException` naming `Rectangle2D` as the fix. No flag parameter: the type's contract decides (my call over Raymond's "take a bool" idea). |
| HeightField | `NotSupportedException` |

Mirror is harmless for every oriented shape: each is symmetric about its own axes.
`Scaled` remains as the uniform-scale-about-origin convenience.

## Touchpoints

Core tables and files, what changes:

- `Geometry/Shapes/Rectangle2D.cs`, `RoundedRectangle2D.cs`, `Ellipse2D.cs`: new storage and members.
- `Geometry/Builders/*` for the three: new overloads, validation.
- `Geometry/Shapes/AxisAlignedRectangle2D.cs`: own raw-op delegation.
- `Operations/Bounds/ShapeBounds2D.cs`: explicit `Rectangle2D` case (it no longer matches `IRect2D`), rounded case, ellipse `TryCalculateTransformed`. `CalculateBoundingCircle` already reads `Center` and `Diameter`.
- `Operations/Distance/ShapeDistance2D.cs`: explicit cases for the three.
- `Operations/Vertices/ShapeVertices2D.cs`: explicit `Rectangle2D` in the four tables, mapped writes for rounded and ellipse outlines.
- `Operations/Vertices/VertexGenerator2D.cs`: unchanged; the frame helper maps the written span.
- `Operations/Area/ShapeArea2D.cs` and `Area2D.Union.cs`: the two-`IRect2D` union fast path no longer catches `Rectangle2D` (falls to the polygon union, acceptable); the rounded outline sampling maps through the frame; rotated ellipses take the sampled part (see pair operations).
- `Operations/Diameter/ShapeDiameter2D.cs`: the two `FarthestDistance2D.BetweenEllipses` sites fall back to `BetweenConvex` when an ellipse is rotated.
- `Operations/MassProperties/ShapeMassProperties2D.cs`: rectangle case takes `Center`/`HalfSize`.
- `Operations/Transforms/ShapeTransforms2D.cs`: `Scaled` keeps the axis; `Posed` added.
- `Operations/Conversion/ShapeEditing2D.cs`: unchanged semantics (re-authoring from bounds yields +X).
- `Operations/Conversion/ShapeConversions2D.cs`, `Operations/Intersection/ShapeRaycast2D.cs`, `Operations/ClosestPoint/*`, `Operations/Convex/ShapeConvexQuery2D.cs`: project into the frame wherever a raw center/radii or min/max call remains. The builder greps `Ellipse2D`, `Rectangle2D`, `RoundedRectangle2D`, `OnEllipsePerimeter`, `TryEllipse` and `CircleEllipse` to find them all.
- `Geometry/Serialization/GeometryJsonConverterFactory.cs`: read and write per the JSON section.
- `Characters/Authored/ModelAuthoring.cs`: `WithSize` keeps working.
- `App2d.Noodle/Rigging/RigAuthoredBridge2D.cs`: `rounded.Max.X - rounded.Min.X` becomes `rounded.Size.X`, same for Y.
- `App2d.CharacterStudio/Editor/ShapeInspector.cs`: rectangle and rounded fields take center, size and angle; ellipse gets an angle; the widget shows degrees and stores radians (my call), drag speed and limits are the builder's.
- `App2d.Analyzers`: the type-name list already covers the three; no change.
- `README.md` Geometry section: one paragraph on oriented shapes and the JSON members.

Unchanged callers (builders only): `TileEditorView2D`, `MovingPlatformMigration2D`,
`PlatformStudy2D`, `PlayerMoves`, `StarterContent`, `RigShape2D`, `EditorSmoke`,
`EditorSurfaceSmoke`, `PersonWardrobe`, `EntityRegion`, `EntityAsset`.

Rendering, tessellation and collision contain no rectangle or ellipse cases; they go through
outlines, cores and support mappings and need no change.

## Tests

- Existing: `ShapeSerialization2DTests`, `GeometryJson2DTests`, `Rect2DTests` (lines 19 to 20
  compare `Rectangle2D...ToRect()`; compare `Center` and `Size` or `ShapeBounds2D.Calculate`
  instead), `RoundedRectangle2DTests`, `MassProperties2DTests`, `ShapeVertices2DTests`,
  `ShapeArea2DTests`, `Distance2DTests`, `ShapeBoundsPrecisionTests`, `DistanceSquaredTests`
  line 36 and `NativeRigTests` line 43 (both read `Rectangle2D` through `IRect2D`),
  `GeometryValidation2DTests` and the analyzer tests (raw constructor calls).
- New, per oriented shape, at a non-trivial angle (such as 30 degrees) and at 90 degrees:
  contains for a point inside only when rotated, support point, corners, local bounds against
  `Rect2D.FromPoints(corners)`, signed distance, mass center and inertia equal to the unrotated
  shape, diameter, JSON round trip, `min`/`max` JSON read with and without `angleRadians`,
  mixed `min`/`center` rejected, `Scaled` keeps the axis.
- Two rotated ellipses: union area and diameter agree with the axis-aligned exact result when
  both are rotated by the same angle about a common center (the pair is a rigid rotation of an
  axis-aligned pair, so the exact answer is known).
- `Posed`: identity, translation, rotation by 90 degrees matches an axis-aligned construction,
  uniform scale, mirror; `AxisAlignedRectangle2D` posed at 90 degrees succeeds and at 30
  degrees throws.
- A composite with a rotated `Rectangle2D` collides where a `ConvexPolygon2D` of the same
  corners collides (this is the Trials `Tube` equivalence).

Whole-suite gate: exclude `GameUsesConfiguredWidthForTheActualCollidableTerrain` by name; the
dark-cave tileset tests need the gitignored tileset copied from the main checkout.

## Order of work

1. Frame helper and `Rectangle2D`: type, builders, validation, every table, JSON, tests green.
2. `RoundedRectangle2D`, same shape of change.
3. `Ellipse2D`, same, plus the two pair-solver fallbacks.
4. Studio inspector angle fields; Noodle bridge size read.
5. `Posed` with the `AxisAlignedRectangle2D` rule and tests.
6. README paragraph.

Each step builds and passes the suite before the next. Steps 2 and 3 are copies of step 1's
pattern, which is why they come after it rather than in parallel.

## Workflow constraints

- Raymond has about forty uncommitted files in the main checkout (`C:\Users\eonei\source\App2d`),
  mostly using-order and formatting passes plus the alias pruning, including `ShapeBounds2D.cs`,
  `SimplePolygon2D.cs` and several builders. Copy the ones that overlap this change into the
  worktree before touching any shared file, normalize to CRLF, and adopt his shape of the code
  where they overlap.
- Single-line method signatures and parameter lists, however long.
- The Write tool and sed emit LF; normalize every written file to CRLF.
- No Bash heredocs containing code.
- Leave the work uncommitted. Raymond tests in the app, then says when to merge into `main`.
  Never push.
- Visual Studio stays open; retry a locked write once.

## Left to the builder

- Name and namespace of the frame helper, and whether the shapes also expose an
  `IsAxisAligned` fast path (the pair-solver fallbacks need the check either way).
- Analytic versus corner-based local bounds for the rectangle (analytic is preferred; both are exact).
- Inspector drag speeds and limits.
- Test values and tolerances.

## Calls I made (flip any of these)

- Axis stored as a unit vector with "unset reads as +X", not an angle or a `Direction2D`.
- JSON `size`, not `halfSize`; `angleRadians`, always written.
- No bulk rewrite of the asset JSON in this change.
- `Posed` ships in this change as the last step, not a later one.
- `AxisAlignedRectangle2D` throws on a rotating pose; no boolean parameter.
- The inspector shows degrees and stores radians.
- The angle parameter goes on the three canonical builder members only; the aliases you are
  pruning are untouched.
- Raw constructors require the axis argument so stale calls fail to compile.
- Two rotated ellipses fall back to sampled union area and numerical farthest distance; exact
  paths stay for axis-aligned pairs, which is every ellipse authored today.
