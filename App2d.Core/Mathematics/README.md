# Reusable math

Use these primitives without creating a shape, scene object, or cached bounds.

| API | Purpose |
| --- | --- |
| `Direction2D` | Finite unit vector with angle, perpendicular, reversal and signed scaling helpers |
| `Polar<TReal>` | Radius and angle in radians, with conversions to and from concrete 2D vectors |
| `Vector2i` | Signed integer coordinates, checked addition/subtraction/scaling, and explicit float conversion |
| `INumberTuple<TSelf, TReal>` | Numeric coordinates and construction, requiring `INumber<TReal>` |
| `INumberTuple2<TSelf, TReal>` | Two named coordinates, `X`/`Y`, and `Create(x, y)` for points and vectors |
| `INumberTuple3<TSelf, TReal>`, `INumberTuple4<TSelf, TReal>` | The same for three and four coordinates: `X`/`Y`/`Z`(/`W`) and `Create` |
| `IVector<TSelf, TReal>` | What a vector must supply (operators, `Dot`, `Min`, `Max`, `Abs`, `IsFinite`) and everything inherited from that: lengths, distances, normalization, interpolation, projection/rejection, reflection, clamping |
| `IVector2<TSelf, TReal>` | Supplies the `IVector` requirements from `X`, `Y` and `Create`, and adds cross product, orientation, perpendiculars, angles, polar conversion, rotation, transforms and refraction |
| `IVector3<TSelf, TReal>`, `IVector4<TSelf, TReal>` | Supply the `IVector` requirements for three and four coordinates; `IVector3` adds the cross product |
| `Vector2f`, `Vector3f`, `Vector4f` | Readonly float vectors backed by the corresponding `System.Numerics` vectors |
| `Vector2d`, `Vector3d`, `Vector4d` | Readonly double vectors for higher-precision geometry |
| `FloatVectorMembers` / `FloatVectorStatics` (and the `Double` twins) | Extension blocks that put the inherited operations on every vector struct as `v.Length()` and `Vector3d.Distance(a, b)` |
| `Real<TReal>` | Floating-point constants and `Clamp01`; aliases `Realf` / `Reald` |
| `Matrix2x2<TVector, TReal>` | A linear transform stored as concrete vector columns; `Matrix2x2f` / `Matrix2x2d` select the types |
| `Vector2f` | Two-dimensional float vector backed by `System.Numerics.Vector2` |
| `Vector4Extensions` | `IsFinite()` component query |
| `Matrix3x2Extensions` | `GetRows()`, `GetColumns()`, `IsFinite()` entry query and transpose direction transform |
| `Rotation2D` | Solve a pivot from endpoints and a turn, or interpolate the resulting arc |
| `Angles` | Convert between degrees and radians and wrap signed angles |
| `Interpolation` | Value blending, optional progress-map application, and inverse lerp |
| `Easing<TReal>` | Base curves in float or double precision; aliases `Easingf` / `Easingd` |
| `ProgressMap<TReal>` | Scalar functions, travel, direction, derivatives and optional inverses; aliases `ProgressMapf` / `ProgressMapd` |
| `EasingCurve`, `EasingDirection`, `ProgressTravel` | Independent authored selections with stable JSON names |
| `PolynomialRoots` | Sorted distinct real roots of linear through quartic polynomials on an inclusive finite interval |
| `Interval<T>` | A closed `[Min, Max]` range with all of its queries as members; `IInterval<T>` is only the two endpoints (details in the Geometry README) |
| `Interval` | Directed endpoint helpers `Lerp`, `InverseLerp`, `Remap` and clamped counterparts; unbounded interval factories |
| `Similarity2D` | Immutable translation, rotation, positive uniform scale, and mirroring used by collision |
| `RigidTransform2D` | Immutable translation and unwrapped rotation; default identity, composition and inverse |
| `Frame2D` | Immutable origin and two basis axes, forward mapping, inversion, and rotation from radians or degrees |
| `Affine2D` | Editable translation, rotation, independent axis scales and shear; shared by scene objects and authored rigs |

## Numeric tuples and 2D vectors

`IVector2<TSelf, TReal>` is the shared 2D interface. `Vector2f` and `Vector2d` are
its concrete float and double implementations. `Vec2<TReal>` has been removed;
there is one production float vector representation, backed by `System.Numerics.Vector2`.
The float implementation retains its packed arithmetic, dot product, length,
normalization, interpolation, and graphics transforms.

`INumberTuple<TSelf, TReal>` describes indexed coordinates and construction.
`INumberTuple2<TSelf, TReal>` adds `X`, `Y`, and `Create(x, y)`. Both use
`INumber<TReal>`, so a future rational coordinate type can implement them without
pretending to support IEEE infinities or trigonometry. `IVector` and `IVector2`
currently add the IEEE requirements needed by the floating-point algorithms.

Reusable algorithms receive the concrete value as `TSelf`:

```csharp
static TSelf Move<TSelf, TReal>(TSelf point, TSelf offset, TReal scale)
    where TSelf : unmanaged, IVector2<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
    => (point + offset) * scale;
```

A vector struct writes only what depends on its storage: fields, `Create`, an indexer and its
operators. `TestVector2<TReal>` in the tests is the whole of it. The rest is inherited in two steps.

1. **The formulas are `static virtual` members of the interfaces.** `IVector` requires `Dot`, `Min`,
   `Max`, `Abs`, `IsFinite` and `Zero` and builds `Length`, `Normalize`, `Distance`, `Lerp`, `Midpoint`,
   `Project`, `Reject`, `Reflect` and `Clamp` on them. `IVector2`, `IVector3` and `IVector4` each supply
   those requirements for their number of coordinates; `IVector2` adds the 2D operations and `IVector3`
   the cross product. Generic code calls them through the type parameter:

```csharp
static TReal PerpendicularDot<TSelf, TReal>(TSelf vector)
    where TSelf : unmanaged, IVector2<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
    => TSelf.Dot(vector, TSelf.PerpCcw(vector));
```

2. **Extension blocks put them on the concrete structs.** An interface static is not visible as
   `Vector3d.Length(v)` or `v.Length()` by itself, so `VectorExtensions.Float.cs` and
   `VectorExtensions.Double.cs` forward each one. C# cannot infer the scalar type solely from a
   constraint on `TSelf`, which is why there is one file per scalar; `VectorExtensionParityTests`
   keeps the two in step.

The double vectors (`Vector2d`, `Vector3d`, `Vector4d`) inherit everything and declare only storage, constants
and operators; `Vector2d` and `Vector4d` keep their packed divide. The float vectors wrap `System.Numerics`
for each interface operation it offers. Every interface default and every wrapper carries `AggressiveInlining`.
That attribute is required, not decoration: without it the JIT leaves a call to an interface default such as
`Dot`, which shows up in every `Length` and `Distance` of the double vectors. With it, optimized code for a sample of
operations (length, distance, dot, cross, lerp, clamp, reject, divide) disassembles with no calls for either
precision, whether reached as an instance, static or generic call.
To repeat that check on x64, run `tools/VectorJitProbe/Verify.ps1` in PowerShell; it fails and names the call if one returns.

To make one operation faster for one struct, declare a public static with the same signature on that
struct. `Vector2f.Length(Vector2f)` calls `System.Numerics`, and that version then runs for
`v.Length()`, `Vector2f.Length(v)` and `TSelf.Length(v)` alike. Calls through `TSelf` are resolved per
struct by the JIT, with no boxing and no virtual dispatch. Keep vectors in concrete or `TSelf`
variables: converting a struct to an interface value boxes it.
See Microsoft's [generic interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/generic-interfaces)
and [generic math](https://learn.microsoft.com/en-us/dotnet/standard/generics/math) guides.

`Matrix2x2<TVector, TReal>` stores the selected vector directly. The `Matrix2x2f`
and `Matrix2x2d` aliases select `Vector2f`/float and `Vector2d`/double. The same
coordinate contracts also support [generic rectangle bounds](../Geometry/README.md).

## Scalar helpers

`OrderedSearch.LowerBound(values, query)` finds the first index whose value is at least the query;
`UpperBound` finds the first strictly greater value. Both return `Count` when no value qualifies, accept
duplicates, and search an already ascending `IReadOnlyList<T>` without copying or sorting it. The overload
with a key selector searches records directly, for example `LowerBound(keys, time, static key => key.Time)`.
Frame timelines use the upper bound of frame end times; keyed values use the lower bound of key times.

`UniformSequence<TReal>(first, step, count)` represents `first + index * step` without storing an array.
It supports constant-time indexing and lazy enumeration in float or double precision. For example,
`new UniformSequence<float>(.25f, .25f, 4)` yields `.25, .5, .75, 1`. Nonnegative steps produce ascending
values suitable for `OrderedSearch`; negative steps are also supported as sequences. Floating-point rounding
can make neighboring values equal. Uniform frame clips use this representation instead of timing arrays.

`PolynomialRoots.FindRealRoots(coefficients, minimum, maximum, roots)` accepts coefficients in ascending order
(`c0 + c1*x + ...`), writes sorted distinct roots, and returns their count. The output span needs at least the
polynomial degree. Bounds are inclusive; repeated roots appear once. Nonzero constants have no roots, while
the identically zero polynomial is rejected. Coefficients and bounds must be finite. Linear and quadratic equations
use direct formulas; the quadratic path avoids cancellation when the roots have very different magnitudes.
Cubics through quintics use derivative isolation and bisection with double arithmetic. Roots that cannot be distinguished
within evaluation precision may merge.

For scalar angles, use .NET's `float.DegreesToRadians(value)` / `float.RadiansToDegrees(value)`
(or the `double` equivalents). `Vector3f.DegreesToRadians()` and `Vector3f.RadiansToDegrees()` convert each component with the same operations.

Progress mapping files live in `Progress/`; affine, similarity, frame, rotation, and matrix helpers live in
`Transforms/`. Progress mapping uses `App2d.Core.Mathematics.Progress`; transforms use
`App2d.Core.Mathematics.Transforms`.

Normalize time, shape the progress, then interpolate the value:

```csharp
var phase = Interpolation.InverseLerp(startTime, endTime, time);
var value = Interpolation.Interpolate(start, end, Easingf.Smooth, phase);
// The same call is available as start.Interpolate(end, Easingf.Smooth, phase).
```

`IInterpolatable<TSelf, TReal>` is an opt-in value contract with one operation:
`static abstract TSelf Lerp(TSelf start, TSelf end, TReal amount)`. It receives the already mapped amount;
`Interpolation.Interpolate(start, end, map, time)` samples the map once and calls that rule without clamping.
The generic call retains the concrete value type. It requires neither reflection over properties nor arithmetic
operators on the value. `IVector` supplies the component-wise default for every float/double vector dimension,
with existing packed implementations taking precedence. The map uses the vector's native scalar precision.
Built-in float and double use scalar overloads because they cannot implement an application interface.

`RigidTransform2D` opts in by interpolating its translation and unwrapped rotation angle, preserving full turns
and rigid axes. Matrices, affine transforms, and mirrored similarities need an explicitly chosen interpolation
policy before opting in. The contract makes no assumption that every property of an arbitrary type can be blended.
For an already mapped amount use `Interpolation.Lerp(start, end, amount)`. For clamped input use
`Interpolation.Lerp(start, end, map.SampleClamped(time))`; output overshoot remains available.

`Interpolator<TValue, TReal>` represents a reusable `(start, end, time) -> value` function. `IInterpolatable` supplies
one default rule for a type; strategies allow many choices without changing the type or adding interfaces. Use
`Interpolation.Default<TValue, TReal>()` for that default, `Linear<TReal>()` for scalars, `Hold<TValue, TReal>()` to
retain the first value, or construct a strategy from a function. Float and double use the same implementation.

```csharp
var vectorRule = Interpolation.Default<Vector3f, float>().WithProgress(Easingf.Smooth)
    .With(Vector3f.Channels.X, Easingf.Power(2))
    .With(Vector3f.Channels.Y, Easingf.Sine);
var value = vectorRule.Evaluate(start, end, time);
```

`InterpolationChannel<TValue, TChannel>` only describes how to read and replace a part. Any type can add any number
of channels by supplying those two functions; no registry, reflection, or interpolation interface is required.
Vectors expose X/Y/Z/W as appropriate, indexed components, and V for the whole value. `RigidTransform2D.Channels`
exposes Translation, Rotation, and V. Four-dimensional vectors also expose R/G/B/A and a grouped RGB channel whose
replacement retains the current alpha. Start with `Hold` and override RGB to animate only RGB; start with `Default`
to keep the normal interpolation on unmentioned components.

`With(channel, child)` evaluates the baseline, then replaces that channel. Each child reads the original endpoints
and original input time; later overrides win on overlaps. `WithProgress(map)` wraps the whole strategy at that point
in the composition, so putting it after channel overrides transforms time for all children with one map sample.
Putting it before an override changes the baseline's time while the new child retains its own curve. Build channel
strategies once and reuse them where the channel configuration is stable. Simple default strategies with a progress
map are value wrappers without a newly allocated capturing callback.

`InCoordinates(toCoordinates, fromCoordinates)` converts the endpoints, interpolates in that representation, and
converts back. `Polar<TReal>.Channels` exposes Radius, Angle, and V; its default keeps the angle unwrapped and requires
a nonnegative radius. `Interpolation.Polar<TVector, TReal>(radius, angle)` directly interpolates Cartesian 2D vectors
in polar coordinates: defaults are linear radius and shortest angle, with either endpoint at the origin using the
other heading. This Cartesian strategy also preserves signed radius overshoot. `ShortestAngle<TReal>()` is an explicit
alternative to unwrapped angle interpolation; half-turn ties follow IEEE remainder.

`Interpolation.Bezier` creates a scalar strategy with relative or absolute handles. Absolute handles can move between
equal endpoints. It shares the existing timing solver and endpoint extrapolation with `Easing.TimedBezier`.
`EvaluateClamped` restricts input to `InputBegin` / `InputEnd`, including a span adapted by a progress map, and never
saturates output. Channel and coordinate composition retain that span. Factories retain construction recipes, so
the core JSON converters can round-trip named rules, progress maps, and channel compositions. Loading builds their
functions once. See [interpolation JSON](Serialization/README.md) for the format and application registrations.

Use the numeric type's built-in `Clamp(value, minimum, maximum)` for arbitrary bounds. `Real<TReal>.Clamp01(value)`
uses the same floating-point clamp with bounds of zero and one.

The three progress files have separate jobs: `Easing<TReal>` defines curves, `ProgressMap<TReal>` represents and composes
functions, and `ProgressOptions` contains authored choices. `Interpolation` blends the actual values.
Both generic classes require `IFloatingPointIeee754<TReal>` and perform arithmetic, trigonometry, differentiation,
and inversion in that precision. Shared aliases in `Directory.Build.props` expose `Easingf` / `Easingd` and
`ProgressMapf` / `ProgressMapd`. Animation uses float maps; motion uses double maps to match playback time.
`Easingf.SmoothStepValue(value, order)` and its double counterpart expose the raw polynomial for direct scalar
calculations. Neither API clamps. Scalar interpolation infers float or double, and either progress precision can
interpolate scalars in either precision; conversion happens after sampling the map.
Use `map.SampleClamped(input)` to clamp only the input to `InputBegin` / `InputEnd`, preserving possible output overshoot.
Apply `Realf.Clamp01` or `Reald.Clamp01` explicitly when a particular consumer needs output saturation.

`Interval.Lerp(begin, end, amount)` and `Interval.InverseLerp(begin, end, value)` operate directly on endpoints;
there is no need to construct an `Interval<T>`. Begin and end retain their order, including descending spans.
`Interval.Remap(value, sourceBegin, sourceEnd, destinationBegin, destinationEnd)` composes those two operations.
Their clamped counterparts restrict the parameter without requiring ordered endpoints. Integer arithmetic
still truncates division and checks overflow. The ordered `Interval<T>` value delegates its parameterization to
the same helpers while retaining its containment, intersection, and validation rules.

Range mapping is also part of the function composition API:

```csharp
var valueAtTime = Easingf.Smooth.FromRange(2f, 6f).ToRange(10f, 20f);
float value = valueAtTime.Sample(3f);           // 11.5625
float velocity = valueAtTime.Derivative(3f);    // 2.8125 per second
float time = valueAtTime.Inverse!(value);      // 3
float held = valueAtTime.SampleClamped(20f);   // 20

var same = ProgressMapf.Normalize(2f, 6f).Then(Easingf.Smooth).ToRange(10f, 20f);
var linear = ProgressMapf.Affine(2f, 3f);      // 2*x + 3
var constant = ProgressMapf.Constant(7f);
```

Apply ease direction to the normalized curve before adapting its ranges. `FromRange` implements
`f((x-begin)/(end-begin))`; `ToRange` implements `begin + f(x)*(end-begin)`. They carry the chain-rule derivative,
inverse when available, and input endpoints used for clamped sampling. Either span may be reversed.
Range adapters require finite endpoints; unbounded ray intervals remain set descriptions. A collapsed input
samples the original curve at zero, and a collapsed output gives a constant; neither has a unique inverse.

`Easingf.SmoothStep(order)` supports orders 0 through 3: linear, cubic (`Smooth`), quintic (`Smoother`), and
septic (`Smoothest`). `Easingf.Power(degree)` accepts any positive integer degree. Choose
`WithDirection(EasingDirection.In/Out/InOut)` separately: Out reflects both axes, while InOut fits the base
curve and its reflection into two halves. Neither operation reverses travel.

`Easingf.Bezier(firstControlValue, secondControlValue)` defines a normalized cubic segment with linear time.
Its control times are the fixed constants one third and two thirds, so no time-coordinate solve is needed.
The four-value overload `Bezier(begin, firstControlValue, secondControlValue, end)` supports actual value units,
including excursions between equal endpoint values. Both use the shared scalar/vector `BezierMath` and straight
endpoint extensions. `Easingf.TimedBezier(firstControlTime, firstControlValue, secondControlTime, secondControlValue)`
supports custom control times in [0,1]. Its six-argument overload also takes actual beginning and ending values.
The factory solves time internally and returns an ordinary `ProgressMapf` with analytic value/time derivative.
The `Easingd` versions use double precision. Fixed one-third/two-thirds times reuse the simpler `Bezier` factory.

```csharp
var curve = Easingd.Power(6).WithDirection(EasingDirection.InOut);
double progress = curve.Sample(phase);
double velocityScale = curve.Derivative(phase);
double? recoveredPhase = curve.Inverse?.Invoke(progress);

ProgressMapf smoothRoundTrip = ProgressMapf.PingPong.Then(Easingf.Smooth);
```

The four travel maps are `ProgressMapf.Forward` (`t`), `Reverse` (`1-t`), `PingPong` (`1-|2t-1|`), and
`ReversePingPong` (`|2t-1|`). `Then(next)` applies functions left to right and carries derivatives through the
chain rule. Ping-pong takes the returning slope at its midpoint; composing smoothstep after it gives zero
turnaround velocity. Holding or repeating a cycle belongs to `PlaybackClock`, or `Repeat` on a motion definition.

Use `new ProgressMapf(sample, derivative, inverse: ..., isContinuous: ...)` for custom float functions, or explicitly
choose `ProgressMapf.FromFunction(sample)` for finite-difference derivatives. The double aliases have the same API.
`Real<TReal>.MachineEpsilon` is the gap above one, unlike `TReal.Epsilon`, which is the smallest subnormal.
Smoothstep inversion uses `4 * MachineEpsilon` as its normalized phase tolerance. Finite differences use its
cube root as their base step and scale it to the phase magnitude, retaining representable sample differences.
There is one generic function object;
no additional implementation interface or preset identity metadata is needed. `IsContinuous` describes the
function on the real line and remains null when unknown. `Inverse` is optional for the function's authored input span; Step
and the two round trips have no unique inverse. Step's derivative is NaN at its jump. Composition preserves
an inverse when both functions provide one, applying inverses in reverse order.

Animation keys store `EasingCurve` and `EasingDirection` separately and resolve them with `Easingf.FromCurve`.
Existing `"linear"`, `"smooth"`, and `"step"` key values retain their names and default to direction `"in"`.
Motion definitions also select `ProgressTravel`; for example, `"travel": "ping-pong", "ease": "smoother"`.
An in-out sine curve uses `"ease": "sine", "easeDirection": "in-out"`. JSON enum names are kebab-case;
unknown names and numeric enum values are rejected.

Smoothstep output can exceed [0,1] by roundoff, and custom functions may deliberately overshoot. Interpolation
preserves both. `CurveMotion2D` allows progress in [-0.5,1.5], rejecting only larger excursions or nonfinite
values. Beyond an endpoint it extends along that endpoint's tangent, with the corresponding velocity.
Motion definitions require continuous easing; animation keys also support Step holds.

## Transforms

`Vector2f`, `Vector3f`, `Vector4`, and `Matrix3x2` expose `IsFinite()` to check that every component or entry is finite.
Zero values and singular matrices are finite; a finite vector's magnitude can still overflow.
`Affine2D.IsFinite` checks its seven authored channels, while `affine.Matrix.IsFinite()` checks the evaluated map
(channel arithmetic can overflow). `Similarity2D.IsFinite` checks both axes, translation, and scale; it does not
require nonzero scale. Use `TryInverse` or `Similarity2D.TryFromMatrix` for those stricter requirements.

`RigidTransform2D` stores only translation and rotation, with default identity. `Similarity2D` adds uniform scale and
optional reflection for collision; `Affine2D` permits independent axis scales and shear.
`Affine2D` is the general affine transform shared by scene objects, rendering, and animation. Nonuniform scales and
composition can introduce shear. All use `Matrix3x2`'s row-vector convention: local * parent.

`Frame2D` stores an evaluated matrix as an immutable origin and basis. It is useful for
placing local offsets, projecting directions, and optical coordinate frames without editable channels:

```csharp
var frame = Frame2D.FromRotationDegrees(angle, source.Center);
Vector2 origin = frame.TransformPoint(new(.32f, offset)); // rotation + translation
Vector2 direction = frame.XAxis;
```

In this convention the transformed axes are the first two matrix rows, exposed as
`XAxis` and `YAxis`; `Origin` is the translation row. For orthonormal axes,
`frame.Matrix.TransposeTransformDirection(direction)` gives the two axis dot products,
with no translation. A scaled or sheared basis needs an actual inverse for local coordinates.

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
transform type. `RigidTransform2D` is the stricter translation-and-rotation value; use `Similarity2D` when reflection
or uniform scale is part of the operation. `RigidTransform2D.Then(parent)` composes local before parent and adds
their unwrapped rotations; `Inverse()` reverses it. `ToFrame()` carries it into a general affine frame.

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
var axis = Vector2f.Direction(heading);
Vector2f offset = Vector2f.FromPolar(radius: 24, angleRadians: heading);

Direction2D travel = new(target - origin);
Vector2 destination = origin + travel.ScaledBy(24);
Ray2D ray = Ray2D.FromDirection(origin, travel); // using App2d.Core.Geometry.Shapes

Polar<float> polar = (point - center).ToPolar();
Vector2f restored = center + polar.ToCartesian().ToVector2f();
var authored = new Polar<float>(radius: 24, angleRadians: 3 * MathF.Tau + heading);
```

Angles are radians unless a method explicitly names degrees, increasing counter-clockwise from +X in Y-up coordinates.
`Angles.WrapSigned(radians)` works with IEEE floating-point types, including float and double. Its IEEE remainder
range is **[-PI, PI]**, with both half-turn signs possible; it does not choose a unique side at the seam. Use it for
angle differences, not stored animation rotations whose full turns matter. A matrix's `matrix.X.AngleRadians` is the
heading of its transformed local +X axis, not a unique decomposition of a reflected or sheared transform.
In a screen coordinate system where Y points down, convert the Y component explicitly
when you want the same visual convention. `Vector2f.FromAngleDegrees(angle)` shares
the direction convention of `Vector2f.FromAngle(radians)`. The shared `AngleRadians` property uses
IEEE `Atan2(Y, X)`. Use `point.Rotate(radians)` or `point.Rotate(radians, pivot)`
for 2D rotation, and `point3D.RotateXY(radians, pivot)` to preserve Z. Convert with
`Angles.DegreesToRadians` and `Angles.RadiansToDegrees`, or use `0.5f * Angles.RadiansPerDegree`
for a half-degree constant.

`vector.Reject(unitNormal)` removes the component parallel to a normalized normal.
`Vector2f.TryRefract(direction, incidentNormal, fromIndex, toIndex, out transmitted)`
applies Snell's law to unit vectors. The normal points toward the incident medium;
indices must be finite and positive. Total internal reflection returns false and
sets the output to zero. These CPU helpers support ray tracing and lighting calculations;
a GPU shader can use the equivalent vector equations.

`Vector2i` is a plain `(int X, int Y)` value in this namespace, suitable for grid cells,
offsets, or integer dimensions without grid-specific rules. Addition, subtraction,
and integer scaling throw on overflow. Division is omitted so callers choose their
own rounding policy. `ToVector2()` explicitly converts to the existing float vector;
large integers may round. Existing graphics APIs continue to accept `System.Numerics.Vector2`.

The floating-point vector types implement `IVector<TSelf, TReal>`, with `TReal` constrained to
`IFloatingPointIeee754<TReal>`. Keep the concrete vector type in storage and generic arguments,
for example `Segment<Vector3d, double>`. Do not store an `IVector<...>` interface value: converting a struct
to an interface boxes it. The integer vector, unit directions, and validated sizes have different
arithmetic contracts and do not implement this interface.

The interfaces contain one implementation of each shared algorithm. Two small extension groups
fix the scalar to float or double, so C# can infer the vector type for ordinary calls:

```csharp
var offset = new Vector3d(3, 4, 12);
double length = offset.Length();
Vector3d direction = offset.Normalized();
double distance = Vector3d.Distance(Vector3d.Zero, offset);
Vector3d halfway = Vector3d.Lerp(Vector3d.Zero, offset, .5);
```

`Segment<TVector, TReal>` carries both types, so calling its shared math needs no extra type arguments:

```csharp
var segment = new Segment<Vector3d, double>(Vector3d.Zero, offset);
double length = segment.Length();
Vector3d midpoint = segment.Midpoint;
```

`Vector2f`, `Vector3f`, and `Vector4f` store one corresponding `System.Numerics` vector and forward
their basic arithmetic to it. `Vector2d` stores two doubles, and its division uses packed
intrinsics, as does `Vector4d` division. Use constructors and `ToVector2()` / `ToVector3()` /
`ToVector4()` at graphics boundaries. `Polar<TReal>.ToCartesian<TVector>()` selects
the concrete output; float and double conveniences return `Vector2f` and `Vector2d`.
Use `new Vector2d(value)` to widen a float vector and `value.ToVector2f()` to narrow it explicitly.

Lengths use `Sqrt(Dot(value, value))`, so finite coordinates can still produce an overflowing or
underflowing squared length. `Normalize` divides by that length; zero vectors produce NaN components.
These raw vectors perform no validation. `Direction2D` retains its separate validated construction.
`Lerp` extrapolates outside [0, 1]. `Project`, `Reject`, and `Reflect` require a unit normal.

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

`AngleRadians` follows `Atan2`, including its signed-zero behavior. `Polar<TReal>.AngleOf`
validates the vector and returns positive zero for any combination of zero coordinates.
Tiny nonzero vectors retain their heading.

`Polar<TReal>` is an immutable value type with a finite, nonnegative radius and a finite angle.
The constructor preserves full rotations for authored motion. Equality compares the stored radius
and angle. `FromCartesian` returns the principal angle and requires the vector's magnitude to fit in
`TReal`. `value.ToPolar()` returns the matching `Polar<TReal>`.

`Direction2D` is also an immutable value type. Its constructor normalizes any finite
nonzero `Vector2`; `FromAngle` uses the same +X/radian convention as `Polar<float>`.
`FromPoints(from, to)` normalizes the difference using double intermediates so even
widely separated finite float points produce a direction.
Use `.Vector` when a `Vector2` is needed. It does not implicitly accept a `Vector2`,
so APIs that request a direction keep the normalization guarantee. Its default value
is invalid; check `IsValid` or construct one before using it. `Ray2D` and `Line2D`
hold it internally, expose it as `UnitDirection`, and retain their `Vector2 Direction`
property for existing callers.

For circles, ellipses, and arcs with multiple vertices, use `VertexGenerator2D` in
`App2d.Core.Geometry`. Its point calculations use the same polar helpers.
