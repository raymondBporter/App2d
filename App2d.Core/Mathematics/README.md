# Reusable math

Use these primitives without creating a shape, scene object, or cached bounds.

| API | Purpose |
| --- | --- |
| `Direction2D` | Finite unit vector with angle, perpendicular, reversal and signed scaling helpers |
| `Polar2D` | Radius/angle value and direct polar/Cartesian conversions |
| `Vector2Extensions` | `AngleRadians`, `ToPolar()`, `Rotate()`, `RotateAround()`, cross products, three-point orientation, and perpendicular vectors |
| `Vector3Extensions` | `XY`, `XZ`, and `YZ` coordinate-plane projections |
| `Rotation2D` | Rotate vectors or points about a pivot, solve a pivot from two endpoints and a turn, and interpolate an endpoint arc |
| `Interpolation` | Progress mappings, lerp, and inverse lerp |
| `Similarity2D` | Immutable translation, rotation, positive uniform scale, and mirroring used by collision |
| `Affine2D` | Editable translation, rotation, independent axis scales and shear; shared by scene objects and authored rigs |

## Transforms

There are two transform families. `Similarity2D` is the uniform-scale, orthogonal subset used by collision.
`Affine2D` is the general affine transform shared by scene objects, rendering, and animation. Nonuniform scales and
composition can introduce shear. Both use `Matrix3x2`'s row-vector convention: local * parent.

`Affine2D` permits reflections and singular transforms, including zero scale. "Affine" here means an affine map;
it does not promise an inverse. Use `TryInverse` when invertibility matters. Collision rejects an affine transform
unless `Similarity2D.TryFromMatrix` accepts its finite, equal-length, orthogonal axes.

Use `affine.Matrix`, `TransformPoint`, and `TransformDirection` for evaluation. `first.Then(second)` applies the first
transform and then the second. `FromMatrix` / `TryFromMatrix` decompose a raw matrix, including a singular one, with
`ShearX = 0`; this preserves the supplied matrix exactly until a channel is edited, but may choose different channel
values from the original authored transform. Editing channels rebuilds the matrix with ordinary float roundoff.
`similarity.ToAffine()` creates an editable copy of a similarity pose.

Scene and animation callers use the same `Affine2D`. Its `Position` and `Scale` vector properties edit the same channels
as `X`/`Y` and `ScaleX`/`ScaleY`; the scalar channels retain the existing authored JSON layout. `Matrix`, `Version`, and
the vector conveniences are not serialized. Setters increment `Version` and raise `Changed` only when values change,
so scene bounds and collision caches stay current. `CopyFrom(other)` updates an existing owner once.

For a snapshot, use `var saved = transform with { };` and restore with `transform.CopyFrom(saved)`. A copy owns its
cache and does not copy event subscribers. Record equality compares the seven channels, never cache/version state.
`Affine2D` replaces the former `BoneTransform2D`, `Transform2D`, and `TransformState2D`; there is no additional snapshot
transform type. Rigid motions and isometries can use unit-scale `Similarity2D` until an API needs a stricter type.

```csharp
var local = new Affine2D { Position = new(2, 3), Rotation = .4f, Scale = new(2, 1), ShearY = .2f };
var parent = Similarity2D.FromTranslation(new(10, 0)).ToAffine();
var world = local.Then(parent);
Vector2 point = world.TransformPoint(new(1, 0));
if (world.TryInverse(out var inverse))
{
    Vector2 restoredPoint = inverse.TransformPoint(point);
}
```

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

`Vector2.Cross(right)` returns a float for existing callers. `Vector2.CrossDouble(right)` uses
the same determinant and returns a double. `a.Orientation(b, c)` computes
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
`App2d.Core.Geometry`. Its point calculations use the same polar helpers.
