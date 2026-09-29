# Reusable math

Use these primitives without creating a shape, scene object, or cached bounds.

| API | Purpose |
| --- | --- |
| `Direction2D` | Finite unit vector with angle, perpendicular, reversal and signed scaling helpers |
| `Polar2D` | Radius/angle value and direct polar/Cartesian conversions |
| `Vector2Extensions` | `AngleRadians`, `ToPolar()`, `Rotate()`, `RotateAround()`, cross product, and perpendicular vectors |
| `Vector3Extensions` | `XY`, `XZ`, and `YZ` coordinate-plane projections |
| `CrossProduct2D` | Double-precision perpendicular dot products and three-point orientation |
| `Rotation2D` | Rotate vectors or points about a pivot, solve a pivot from two endpoints and a turn, and interpolate an endpoint arc |
| `Interpolation` | Progress mappings, lerp, and inverse lerp |
| `Similarity2D` | Translation, rotation, uniform scale, and mirroring used by collision |

```csharp
using App2d.Core.Mathematics;

float heading = contact.Direction.AngleRadians;
Vector2 axis = Polar2D.Direction(heading);
Vector2 offset = Polar2D.ToCartesian(radius: 24, angleRadians: heading);

Direction2D travel = new(target - origin);
Vector2 destination = origin + travel.ScaledBy(24);
Ray2D ray = Ray2D.FromDirection(origin, travel); // using App2d.Core.Geometry.Shapes

Polar2D polar = (point - center).ToPolar();
Vector2 restored = center + polar.ToCartesian();
var authored = new Polar2D(radius: 24, angleRadians: 3 * MathF.Tau + heading);
```

Angles are radians, increasing counter-clockwise from +X in Y-up coordinates.
In a screen coordinate system where Y points down, convert the Y component explicitly
when you want the same visual convention. Angles are not degrees.

`Vector2.Cross(right)` returns a float for existing callers. It uses the same determinant as
`CrossProduct2D.Of`, which returns a double. `CrossProduct2D.Orientation(a, b, c)` computes
`cross(b - a, c - a)` after widening the coordinates to double, so geometric predicates
do not lose precision in the subtraction.

`AngleOf` / `AngleRadians` return an angle in [-PI, PI]. A zero vector has no
direction; the shared convention returns zero, including for signed-zero components.
Tiny nonzero vectors retain their heading. A caller such as a constraint solver can
still choose its own near-zero tolerance and fallback before calling this helper.

`Polar2D` is an immutable value type with no per-coordinate heap allocation. Its
radius must be finite and nonnegative; its angle must be finite. The constructor
preserves full rotations for authored motion. Equality compares the stored radius
and angle, so coordinates separated by a full turn need not compare equal even when
their Cartesian positions coincide. `FromCartesian` returns the principal angle
and rejects vectors whose magnitude cannot fit in a float.

`Direction2D` is also an immutable value type. Its constructor normalizes any finite
nonzero `Vector2`; `FromAngle` uses the same +X/radian convention as `Polar2D`.
`FromPoints(from, to)` normalizes the difference using double intermediates so even
widely separated finite float points produce a direction.
Use `.Vector` when a `Vector2` is needed. It does not implicitly accept a `Vector2`,
so APIs that request a direction keep the normalization guarantee. Its default value
is invalid; check `IsValid` or construct one before using it. `Ray2D` and `Line2D`
hold it internally, expose it as `UnitDirection`, and retain their `Vector2 Direction`
property for existing callers.

For circles, ellipses, and arcs with multiple vertices, use `VertexGenerator2D` in
`App2d.Core.Geometry.Functions`. Its point calculations use the same polar helpers.
