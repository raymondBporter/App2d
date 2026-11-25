# Curve consolidation design

Date: 2026-11-25. Status: approved by Raymond on 2026-11-25. Second curve project; builds on `2026-11-25-curve-foundation-design.md`.

## Intent

Most leftover curve helpers have no callers in the game, only in tests, and several exist twice. Keep each capability exactly once, on the generic layer, and delete the copies. Where a derivative is estimated although a real one is well defined, write the real one.

## Goals

- Every capability has one implementation.
- No finite-difference derivative where an analytic one exists.
- No result changes for existing callers in the game.

## Non-goals

- Domains, concatenation and a shared piecewise type.
- Arc length, curvature, bounds.
- Second derivatives through the `Ops` wrappers, and for `ProgressMap`.
- Raymond's NaN decisions: the `Derivative` bodies of `PolylineCurve2D`, `LineSegmentCurve2D` and `BSpline2D` are not edited.

## Approaches

Raymond approved the seven-step design below as proposed. He then replaced step 5:

- Proposed: one shared numeric derivative used by `NormalOffsetCurve2D`, `Curve2D.Tangent` and `ProgressMap.FromFunction`.
- Picked (Raymond): "just fix the derivatives for the ones that are well defined; they are just missing for some". The two geometry sites estimate because curves have no second derivative. Add it and use it.

## Design, in build order

### 1. Second derivatives (replaces "one numeric derivative")

```csharp
public interface ITwiceDifferentiableCurve<TValue, TReal> : IDifferentiableCurve<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    TValue SecondDerivative(TReal t);
}
```

- Implemented by `QuadraticBezier<,>`, `CubicBezier<,>`, `QuadraticBezier2D`, `CubicBezier2D`, `LineSegmentCurve2D`, `Arc2D`, `PolylineCurve2D`, `BSpline2D` and `RoundedPolylineCurve2D`.
- `Curve2D.Tangent`: at a stationary point the tangent is the direction of the second derivative. The chord estimate stays only for a curve that is not twice differentiable.
- `NormalOffsetCurve2D`: the offset profile becomes an `IDifferentiableCurve<float, float>` (a `ProgressMap` fits) in place of `Func<float, float>`. Its derivative is exact: `P' + N' * o + N * o'`, with `N'` from the source's second derivative. A source without a second derivative throws `NotSupportedException` from `Derivative`. The finite difference is deleted.
- `Easing<TReal>.Gaussian(center, sigma)` gives the bump profile with its derivative.
- `ProgressMap.FromFunction` stays the one explicit estimate. It wraps arbitrary code and has no callers in the game.

### 2. Control points as an interface

```csharp
public interface IControlPointCurve<TPoint, TCurve>
{
    int ControlPointCount { get; }
    TPoint GetControlPoint(int index);
    TCurve WithControlPoints(ReadOnlySpan<TPoint> points);
}
```

- The generic Béziers return their own type. The 2D geometry curves return `ICurve2D`, because `Face` holds them erased.
- Lines, polylines and B-splines gain it.
- Deleted: `CurveControlPoints2D`, `SpatialCurveControlPoints`, `SpatialCurveJsonConverter`.
- A wrong point count throws `InvalidDataException`, as now.

### 3. Bézier math on the generic types

`Split`, `Slice` and `FromHermite` move to `CubicBezier<TValue, TReal>`; `Split` and `Slice` are added to `QuadraticBezier<TValue, TReal>`. `CubicBezier2D` keeps its methods and delegates.

### 4. One slice

- Deleted: `CurveSlice2D`, `ICurveSlice2D`, `Curve2D.Slice`, `Curve2D.Split`, `GeometryKind.CurveSlice`.
- `curve.Ops.Slice` is the slice; `curve.Ops.Split(t)` returns both halves.
- Distance and raycast on part of a curve become public: `CurveDistance2D.Distance(point, curve, from, to)` and `CurveRaycast2D.TryIntersect(..., from, to, ...)`.
- A slice is no longer an `ICurve2D`.

### 5. Sampling on `Ops`

- `curve.Ops.Sample(Span<TValue>)` and `curve.Ops.Sample(int segmentCount)` for any value type; `Curve2D.Sample` is deleted.
- `Curve2D.IPolylineSampler` and two of its three sampler structs are deleted. `ToPolyline` takes any `ICurve<Vector2f, float>` plus optional exact boundary points; the B-spline sampler becomes an `ICurve`.

### 6. Easing on the curve layer

- `Easing.Bezier`: `new CubicBezier<TReal, TReal>(...).Ops.Extended()` replaces the hand-written straight extension.
- `InverseSmoothStep`: `BracketedNewton.TrySolve` replaces the hand bisection.

### 7. One segment search

`Track.Sample` and `KeyframeSampling.TrySegment` both use `OrderedSearch`, through a span overload so `Track` does not box its key array.

## Left to the builder

- Exact constraints, file placement, and whether `Curve2D.Gaussian` survives.
- Steps 6 and 7 may shift pinned values by rounding. Report any test whose expectation has to change; do not loosen one silently.

## Hand-over

As before: work in the session worktree, copy to `C:\Users\eonei\source\App2d` uncommitted, merge by hand where Raymond has pending edits.
