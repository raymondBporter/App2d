# Reusable math

Use these primitives without creating a shape, scene object, or cached bounds.

| API | Purpose |
| --- | --- |
| `Direction2D` | Finite unit vector with angle, perpendicular, reversal and signed scaling helpers |
| `Polar2D` | Radius/angle value and direct polar/Cartesian conversions |
| `Vector2Extensions` | `IsFinite()`, `AngleRadians`, `ToPolar()`, `Rotate()`, `RotateAround()`, cross products, three-point orientation, and perpendicular vectors |
| `Vector3Extensions` | `IsFinite()`, componentwise `DegreesToRadians()` / `RadiansToDegrees()`, and `XY`, `XZ`, and `YZ` coordinate-plane projections |
| `Vector4Extensions` | `IsFinite()` component query |
| `Matrix3x2Extensions` | `IsFinite()` entry query and transpose direction transform |
| `Rotation2D` | Rotate vectors or points about a pivot, solve a pivot from two endpoints and a turn, and interpolate an endpoint arc |
| `Interpolation` | Value blending, optional progress-map application, and inverse lerp |
| `Clamping` | Generic `Clamp(value, minimum, maximum)` and `Clamp01(value)` scalar helpers |
| `Progress` | Clamped smoothstep families, their inverses, and unclamped maps with analytic derivatives |
| `Easing` | Smoothstep, power and sine easing families with analytic derivatives |
| `ProgressMap` / `IProgressMap` | A sampled progress function and derivative for velocity |
| `ProgressMaps` | Forward, reverse, ping-pong, easing transformations, and map composition using the chain rule |
| `ProgressMapKind` | Stable JSON names for built-in maps; ordered lists describe composition |
| `PolynomialRoots` | Sorted distinct real roots of linear through quartic polynomials on an inclusive finite interval |
| `Similarity2D` | Immutable translation, rotation, positive uniform scale, and mirroring used by collision |
| `Affine2D` | Editable translation, rotation, independent axis scales and shear; shared by scene objects and authored rigs |

`PolynomialRoots.FindRealRoots(coefficients, minimum, maximum, roots)` accepts coefficients in ascending order
(`c0 + c1*x + ...`), writes sorted distinct roots, and returns their count. The output span needs at least the
polynomial degree. Bounds are inclusive; repeated roots appear once. Nonzero constants have no roots, while
the identically zero polynomial is rejected. Coefficients and bounds must be finite. Linear and quadratic equations
use direct formulas; the quadratic path avoids cancellation when the roots have very different magnitudes.
Cubics and quartics use derivative isolation and bisection with double arithmetic. Roots that cannot be distinguished
within evaluation precision may merge.

For scalar angles, use .NET's `float.DegreesToRadians(value)` / `float.RadiansToDegrees(value)`
(or the `double` equivalents). `Vector3` extensions convert every component with the same operations.

Progress mapping files live in `Progress/`; affine, similarity, frame, rotation, and matrix helpers live in
`Transforms/`. Progress mapping uses `App2d.Core.Mathematics.Progress`; transforms use
`App2d.Core.Mathematics.Transforms`.

Normalize time, shape the progress, then interpolate the value:

```csharp
var phase = Interpolation.InverseLerp(startTime, endTime, time);
var value = Interpolation.Lerp(start, end, phase, Easing.Smooth);
```

`Clamping.Clamp` and `Clamp01` work with numeric types such as int, float, and double, preserving the type's
standard clamping behavior.

`Progress.SmoothStep`, `SmootherStep`, and `SmoothestStep` clamp inputs to [0,1]. Easing maps do not clamp inputs
or outputs. The `*StepUnclamped<T>` polynomials share float/double arithmetic
without forcing float callers to change precision. `ClipEase` retains its saved names and held-step behavior.
`Progress.SmoothStepMap`, `SmootherStepMap`, and `SmoothestStepMap` package each polynomial with
its analytic derivative; `Easing.Smooth`, `Smoother`, and `Smoothest` are aliases for those same maps.
These maps guarantee output in [0,1] for normalized phase, clamping polynomial rounding at the producer.
They still extrapolate outside that domain; the `*StepUnclamped<T>` helpers always return the raw polynomial.
Every built-in easing has an analytic derivative. `Easing.EaseInPower(n)`, `EaseOutPower(n)`, and
`EaseInOutPower(n)` support any positive integer degree; 1 is linear, and the named Quad/Cubic/Quart/Quint
maps use degrees 2/3/4/5. Degree 0 is excluded because a constant cannot map both normalized endpoints.
`ProgressMaps.EaseOut(map)` reflects both axes; `EaseInOut(map)` fits the curve into each half. Both
carry the derivative through the chain rule, so custom maps can reuse the same transformations.
Use `new ProgressMap(sample, derivative)` for an exact derivative, or explicitly choose
`ProgressMap.FromFunction(sample)` for numerical differences. There are no implicit delegate conversions.

```csharp
var curve = Easing.EaseInOutPower(6);
double progress = curve.Sample(phase);
double velocityScale = curve.Derivative(phase);
```

Every retained built-in map has a stable `Kind`. `ProgressMaps.FromKind(kind)` resolves it;
JSON writes kebab-case names such as `"smooth"`, `"smoother"`, `"reverse"`, `"ping-pong"`,
and `"ease-in-out-sine"`. `ProgressMaps.Compose(kinds)` applies an ordered list left to right.
For example, `["ping-pong", "smoother"]` means `smoother(pingPong(phase))`, with the
analytic chain-rule derivative. Custom delegate maps have no built-in kind; author their
parameters separately. Arbitrary power degrees remain available in code.

The unused back, circular, exponential, elastic and bounce families were removed. The retained
maps have finite normalized slopes. Ping-pong uses its returning slope at the turnaround;
composing smoothstep after it gives zero turnaround velocity.

## Transforms

`Vector2`, `Vector3`, `Vector4`, and `Matrix3x2` share the `IsFinite()` extension name: every component or entry
must be finite. Zero values and singular matrices are finite; a finite vector's magnitude can still overflow.
`Affine2D.IsFinite` checks its seven authored channels, while `affine.Matrix.IsFinite()` checks the evaluated map
(channel arithmetic can overflow). `Similarity2D.IsFinite` checks both axes, translation, and scale; it does not
require nonzero scale. Use `TryInverse` or `Similarity2D.TryFromMatrix` for those stricter requirements.

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

`Affine2D.CreateMatrix(position, rotation, scale, shear)` evaluates absolute channels directly into a `Matrix3x2`,
without allocating an editable transform. Shear is the pair of additional axis angles, matching `ShearX`/`ShearY`.
The cached `Matrix` property uses this same function. For sampled changes relative to an existing transform, use
`transform.EvaluateMatrix(position, rotation, scale, shear)`: it does not edit channels, increment the version or notify
subscribers. An unchanged proposal retains the exact cached matrix, including an imported singular map; equal individual
channels retain their original float values as an ordinary property edit would. Bone sampling uses this path instead
of making temporary editor-transform copies. Matrix evaluation itself performs no range validation or clamping.

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

`u.Cross(v)` is the two-vector determinant (also called the perpendicular dot product).
`a.Orientation(b, c)` computes `cross(b - a, c - a)`: twice the signed triangle area,
positive for a counter-clockwise turn in Y-up coordinates. Divide by two for signed area.
Both use the vector's coordinate type: `Vector2` uses float arithmetic, and `Vector2d` uses double.
For geometric predicates, widen the points before subtracting so float rounding does not lose the turn:

```csharp
float cross = u.Cross(v);
float turn = a.Orientation(b, c);
double preciseCross = new Vector2d(u).Cross(new(v));
double preciseTurn = new Vector2d(a).Orientation(new(b), new(c));
```

Hulls, triangulation, clipping, and polygon winding retain double precision for near-collinear or large inputs.
Character drawing and editor calculations use float arithmetic where they already did so.

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
