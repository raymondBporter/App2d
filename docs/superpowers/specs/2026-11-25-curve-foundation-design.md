# Curve foundation design

Date: 2026-11-25. Status: approved by Raymond on 2026-11-25, with the scope additions below. First of five curve consolidation projects.

## Approval notes

Raymond approved the design and added:

- The project is solo and nothing is deployed. There is no backward compatibility to keep, and the authored JSON is not a concern. Prefer the cleaner unification over a cautious one.
- His uncommitted experiments (`CurveExtensions.cs`, `ProgressMap.IsConstant`, four tests) are disposable. This design supersedes them.
- `BracketedNewton.IFunction` is one more interface for "a differentiable scalar function". It should be the curve interface. This moves into this project; see Root finding.

He did not ask for the endpoint tangent, so a clamped curve has a zero derivative past its end.

## Intent

The repo has three families that are all "a function of one real parameter": geometry curves (`ICurve`, `ICurve2D`), scalar maps (`ProgressMap`, `Easing`) and value interpolation (`Interpolator`, `Track`). Each family re-implements composition, slicing, out-of-range behaviour and numeric derivatives.

This project gives them one vocabulary and writes the shared operations once, as generic structs with no boxing and no virtual calls. It also settles who decides whether a curve clamps. Later projects build on it to delete duplicates and add features.

## Goals

- One interface ladder that geometry curves and `ProgressMap` both implement.
- Slice, reverse, reparameterize, transform, clamp and extend written once, for every value type and dimension.
- The user of a curve chooses clamp, extend or neither. The curve does not choose.
- No boxing and no interface dispatch when the curve type is known at compile time.
- No change in results for any parameter inside [0, 1].

## Non-goals (later projects)

- Domains other than [0, 1], concatenation, a shared piecewise type, `Track` and `KeyframeSampling`.
- The rest of root finding and inversion beyond `BracketedNewton` (see Root finding).
- Arc length, curvature and bounds.
- Collapsing `CubicBezier2D` and `QuadraticBezier2D` onto the generic Béziers; a control point interface.
- Turning `ProgressMap` into structs. It stays a class because it is serialized and stored in clips.
- Loop and ping-pong as extrapolation modes. `ProgressTravel` and `PlaybackClock` own those today.
- JSON for the new wrapper types.
- Using a wrapper result as an `ICurve2D`. Wrappers implement only `IDifferentiableCurve`, so they do not go to distance, raycast, `ToPolyline` or `CurveMotion2D`.
- Moving `CurveMotion2D`'s progress map onto `Reparameterize`. Its map is `double` and its curve is `float`.
- `SpatialCurveControlPoints` and `SpatialCurveJsonConverter`. They have no callers outside themselves and stay untouched here.

## Anti-goals

- A lattice of capability interfaces (`IContinuous`, `IMonotonic`, ...). Continuity and invertibility depend on runtime values, as `ProgressMap.WithDirection` shows, so they stay data.
- An operation whose signature takes `ICurve<TValue, TReal>` by interface. That boxes every struct curve.
- A second clamped copy of any operation.

## Constraints

- .NET 10, C# 14. Extension blocks are already in use.
- C# cannot infer `TValue` and `TReal` when they appear only in a constraint such as `where TCurve : ICurve<TValue, TReal>`. Both a generic extension method and an extension block fail with CS0411. Checked with a scratch compile on 2026-11-25.
- Single-line signatures. CRLF line endings.
- Raymond has uncommitted work in the main checkout that this project replaces: `App2d.Core/Geometry/Curves/CurveExtensions.cs`, an `internal IsConstant` on `ProgressMap`, and four new tests in `App2d.Tests/Curves/GenericCurveTests.cs`.

## Approaches

### Who owns clamping

1. **The curve clamps (today).** Every leaf clamps its parameter: 11 explicit clamps in `BezierMath`, `Arc2D`, `BSpline2D`, `PolylineCurve2D` and `RoundedPolylineCurve2D`, plus two implicit ones (`LineSegmentCurve2D` through `Segment.PointAt`, and the endpoint guards in `RoundedPolylineCurve2D.LocateSample`). The curve cannot know the context. `CurveMotion2D` already works around it by hand to extend along the end tangent. Clamped leaves are also inconsistent: outside [0, 1] the value holds but `EvaluateDerivative` still returns the endpoint tangent.
2. **The caller clamps the parameter by hand.** Fine for clamp. It cannot express "extend along the tangent" without each caller writing that again.
3. **Curves evaluate their plain formula; the user picks a policy that the library writes once.** Picked. It is the rule `ProgressMap` (`Sample` and `SampleClamped`) and `Interpolator` (`Evaluate` and `EvaluateClamped`) already follow, so geometry stops being the exception.

Raymond said he wants both clamped and unclamped and left "who handles it" open. Option 3 is Claude's pick.

### How to template without boxing

1. **Extension on the interface type** (`this ICurve<TValue, TReal> curve`). Infers, but boxes and dispatches through the interface. This is what the uncommitted `Reparameterize` does.
2. **One extension class per closed type.** No boxing, but every operation is repeated per type. This is why `TransformedCurve3D` and `TransformedCurve3d` both exist.
3. **A wrapper struct that carries the types once, with one entry point per value type.** Picked. Operations are instance methods on the wrapper, written once.

## Design

### Interfaces

```csharp
public interface ICurve<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    TValue Evaluate(TReal t);
}

public interface IDifferentiableCurve<TValue, TReal> : ICurve<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    TValue Derivative(TReal t);
}
```

- `EvaluateDerivative` is renamed `Derivative` everywhere. `ProgressMap` already uses that name.
- `ICurve2D` becomes `IGeometryValue2D, IDifferentiableCurve<Vector2f, float>` and keeps `Distance`.
- `ProgressMap<TReal>` implements `IDifferentiableCurve<TReal, TReal>`. `Sample` is renamed `Evaluate` and `SampleClamped` is renamed `EvaluateClamped`, so every curve-like type uses the same two names. `Interpolator` already does.
- The `unmanaged` constraint is dropped from `ICurve` so `ProgressMap` can implement it (Claude's call).

### Evaluation rule

`Evaluate(t)` and `Derivative(t)` compute the curve's formula at `t` and never clamp.

- Inside [0, 1] nothing changes.
- Outside [0, 1] a Bézier continues its polynomial, a line continues, an arc continues round its circle, and polyline and B-spline continue their first or last piece.
- NaN in gives NaN out.

Three `ICurve2D` types are exempt and keep the clamp they have now (Claude's call). `CurveSlice2D` and `NormalOffsetCurve2D` are wrappers around another curve. `RoundedPolylineCurve2D` is parameterized by arc length through a distance table, so it has no formula to continue. All three are reworked in later projects.

`LineSegmentCurve2D.Evaluate` stops calling `Segment.PointAt` and lerps directly. `Segment.PointAt` keeps its clamp; `IVectorTests` pins it.

No production caller relies on a leaf clamping today. `Attachment.cs` and `CurveDistance2D` stay in range, and `CurveMotion2D`, `Curve2D.Tangent`, `NormalOffsetCurve2D` and `CurveSlice2D` clamp for themselves. Only tests pin it.

### The wrapper and its entry point

```csharp
public readonly record struct Curve<TCurve, TValue, TReal>(TCurve Source) : IDifferentiableCurve<TValue, TReal> where TCurve : IDifferentiableCurve<TValue, TReal> ...
```

One entry per value type, each in its own static class so overload resolution drops the ones whose constraint fails:

```csharp
public static class Vector2fCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<Vector2f, float>
    {
        public Curve<TCurve, Vector2f, float> Ops => new(curve);
    }
}
```

Entries for `float`, `double`, `Vector2f`, `Vector2d`, `Vector3f`, `Vector3d`, `Vector4f`, `Vector4d`. That covers every current and future curve of those types. It also works when the curve is held as `ICurve2D`; then calls go through the interface, which is the intended erased boundary.

Scratch test: `bez.Ops.Slice(.25, .75).Reparameterize(map)` evaluated a million times allocated 24 bytes in total.

`Ops` is a placeholder name (Raymond's call).

### Operations on the wrapper

Each returns a `Curve<...>` around a new struct, so calls chain.

| Call | Meaning |
|---|---|
| `Slice(from, to)` | Affine remap of [0, 1] onto [from, to]. Keeps `From` and `To`. `from > to` is allowed and reverses. |
| `Reverse()` | `Slice(1, 0)`. |
| `Reparameterize(map)` | `curve(map(t))` with the chain rule. `map` is any `IDifferentiableCurve<TReal, TReal>`, including a `ProgressMap`. No clamping. |
| `Transform(transform)` | Points through `TransformPoint`, derivatives through `TransformDirection`. |
| `Clamped()` | Holds the endpoint value outside [0, 1]. |
| `Extended()` | Continues straight along the endpoint tangent outside [0, 1]. |
| `Extrapolated(before, after)` | Chooses a mode for each side. |
| `EvaluateClamped(t)` | One-off clamp at a call site. |

Slice and reverse stay separate from `Reparameterize` because they are affine: a Bézier slice is still a Bézier, and distance and raycast can map the interval and stay exact.

### Extrapolation

```csharp
public enum Extrapolation { None, Clamp, Extend }
public readonly record struct Extrapolated<TCurve, TValue, TReal>(TCurve Curve, Extrapolation Before, Extrapolation After) ...
```

| Mode | Value outside | Derivative outside |
|---|---|---|
| `None` | The curve's formula | The curve's formula |
| `Clamp` | Endpoint value | Zero. At exactly 0 or 1 it is the curve's own derivative. |
| `Extend` | Endpoint + tangent × distance past the end | Endpoint derivative |

Where the wrapper sits says what gets clamped:

- `curve.Ops.Clamped().Reparameterize(map)` clamps what the map produces. An overshooting ease holds at the ends of the path.
- `curve.Ops.Reparameterize(map).Clamped()` clamps the incoming `t`.
- `curve.Ops.Extended().Reparameterize(map)` lets an overshooting ease run past the ends in a straight line. `CurveMotion2D` hand-writes this behaviour today.

**Settled: the clamped derivative past the end is zero**, because a held point is not moving. Raymond's `ReversingAVectorCurveReversesItsTangent` expected the endpoint tangent for an incoming `t` of -0.25 and 1.25; those two rows now expect zero.

### Reparameterize and a zero map derivative

`Reparameterized.Derivative` returns zero when the map's derivative is exactly zero and does not ask the source curve. A constant map then gives a zero tangent even where the source has a corner, and `ProgressMap` no longer needs `IsConstant` exposed (Claude's call).

### Root finding

`BracketedNewton.TrySolve` takes any `IDifferentiableCurve<TReal, TReal>`:

```csharp
internal static bool TrySolve<TFunction, TReal>(TFunction function, TReal target, int iterationLimit, out TReal parameter) where TFunction : IDifferentiableCurve<TReal, TReal> where TReal : IFloatingPointIeee754<TReal>
```

- `BracketedNewton.IFunction` is deleted, with its `Midpoint` and `HasConverged` hooks. The solver uses one midpoint, `lower + (upper - lower) / 2`, and one stop rule: the next estimate equals the current one or either bracket end.
- `Easing.BezierTime` and `CubicBezier2D.NormalizedX` are deleted. Both call sites pass `new CubicBezier<TReal, TReal>(0, firstTime, secondTime, 1)`, the normalized time curve itself.
- `CubicBezier2D.ParameterAtX` keeps its 12-iteration limit and its fallback to the full polynomial solver.

The wider root-finding project (inverse for any monotone map, `ParameterAtX` for any curve) is still later.

### Transforms

```csharp
public interface ITransform<TPoint>
{
    TPoint TransformPoint(TPoint local);
    TPoint TransformDirection(TPoint local);
}
```

- `Frame2D`, `RigidTransform2D`, `Similarity2D` and `Affine2D` implement `ITransform<Vector2f>`. They already have both methods.
- `Frame3D` implements `ITransform<Vector3f>` and `ITransform<Vector3d>`. The double one widens the frame's coefficients, as `TransformedCurve3d` does now.
- `Frame3D` itself is not templated in this project.

### What this project replaces

| Removed | Replaced by |
|---|---|
| `TransformedCurve3D`, `TransformedCurve3d` and their two extension classes | `Transformed<TCurve, TTransform, TPoint, TReal>`; 2D transformed curves come for free |
| The uncommitted `ReparameterizedCurve` class and `ProgressMap.IsConstant` | `Reparameterized` and `Extrapolated` |
| `BracketedNewton.IFunction`, `Easing.BezierTime`, `CubicBezier2D.NormalizedX` | `IDifferentiableCurve<TReal, TReal>` and `CubicBezier<TReal, TReal>` |
| 9 parameter clamps and 1 implicit one in `BezierMath`, `Arc2D`, `BSpline2D`, `PolylineCurve2D` and `LineSegmentCurve2D` | Nothing; leaves are plain |
| `CurveMotion2D.EvaluateProgress` | `Extended()` over the stored `ICurve2D`. Phase, period scaling, `CheckedProgress` and the `double` progress map stay as they are. |

`CurveSlice2D` stays. It is the `ICurve2D` slice that distance and raycast recognise. It clamps explicitly, as now.

### Tests

- Tests that pin leaf clamping move to the clamped form. They fail at run time, not at build: `Curve2DTests` lines 130 and 131, `SegmentMath2DTests` lines 16 and 17, and the out-of-range parameters in `GenericCurveTests.BeziersSupportScalarsAndAllSixVectorTypesWithNormalizedDerivatives`. `CurveSubdivisionTests` is unaffected because `CurveSlice2D` keeps its clamp.
- Raymond's four reparameterize tests are kept and rewritten onto the wrapper. The overshoot and constant-map tests become `Clamped().Reparameterize(map)`. Two rows of the reversing test now expect a zero tangent.
- New tests: each extrapolation mode's value and derivative on both sides; that a chained struct curve does not allocate; a transformed 2D curve; `ProgressMap` used as the map and as a curve.

## Left to the builder

- Namespace and file layout. `ICurve`, `BezierMath` and the generic Béziers probably belong under `App2d.Core.Mathematics`, because `Easing` already depends on them.
- Exact generic constraints on each wrapper struct.
- How each piecewise leaf continues its end piece once the clamp is removed.
- Whether `Evaluate` on the wrapper needs `AggressiveInlining`.

## Hand-over

Work happens in the session worktree. Changed files are copied into `C:\Users\eonei\source\App2d` uncommitted for Raymond to test, and merged by hand where he has pending edits.

## Later projects, in order

1. Root finding and inversion on `IDifferentiableCurve<TReal, TReal>`: one solver, an inverse for any monotone map, `ParameterAtX` for any curve.
2. Domains, concatenation and one piecewise type under `Track`, `PolylineCurve2D` and the polynomial span arrays.
3. Generic geometry over `IVector`: tangent, arc length, constant-speed reparameterization, curvature, bounds, one numeric derivative.
4. Collapse the leaf types: 2D Béziers onto the generic ones, a control point interface, `IPolylineSampler` onto `ICurve`.
