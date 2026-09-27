# Reusable math

Use these primitives without creating a shape, scene object, or cached bounds.

| API | Purpose |
| --- | --- |
| `Polar2D` | Radius/angle value and direct polar/Cartesian conversions |
| `Vector2Extensions` | `AngleRadians`, `ToPolar()`, cross product, and perpendicular vectors |
| `Rotation2D` | Rotate an existing vector in XY, including a Vector3 while preserving Z |
| `Interpolation` | Progress mappings, lerp, and inverse lerp |
| `Similarity2D` | Translation, rotation, uniform scale, and mirroring used by collision |

```csharp
using App2d.Core.Mathematics;

float heading = contact.Direction.AngleRadians;
Vector2 axis = Polar2D.Direction(heading);
Vector2 offset = Polar2D.ToCartesian(radius: 24, angleRadians: heading);

Polar2D polar = (point - center).ToPolar();
Vector2 restored = center + polar.ToCartesian();
var authored = new Polar2D(radius: 24, angleRadians: 3 * MathF.Tau + heading);
```

Angles are radians, increasing counter-clockwise from +X in Y-up coordinates.
In a screen coordinate system where Y points down, convert the Y component explicitly
when you want the same visual convention. Angles are not degrees.

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

For circles, ellipses, and arcs with multiple vertices, use `VertexGenerator2D` in
`App2d.Core.Geometry.Functions`. Its point calculations use the same polar helpers.
