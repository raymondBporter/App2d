# Curve domains, polynomial spans and one linear blend

Date: 2026-11-25. Status: approved by Raymond on 2026-11-25 (all three parts). Parts 1 and 2 are built. Part 3 was dropped by Raymond on 2026-11-25 after the code showed it would not pay: the scalar lerp chooses between two formulas by the signs of its endpoints to avoid overflow, which a vector cannot do; no type would gain a capability; and the two `Interpolate` extensions cannot share a name. Third curve project.

## Intent

Three gaps are left after the foundation and the consolidation:

- A curve cannot say where it lives. Geometry assumes [0, 1]; `ProgressMap` carries its own input span, which `Ops` ignores.
- Distance and raycast learn each curve's shape through type switches, and cubic Béziers and B-splines measure distance by sampling.
- Scalars cannot implement `IInterpolatable`, so linear interpolation is written once for scalars and once for everything else.

## Goals

- Every curve has a domain, and every `Ops` operation honours it.
- Distance and raycast are written once over exact polynomial pieces.
- Cubic Bézier and cubic B-spline distance is exact.
- One linear interpolation for anything with `+` and `* real`.
- No result changes inside [0, 1] for existing callers, beyond rounding in the last place.

## Non-goals

- A shared piecewise type under `Track` and `PolylineCurve2D`. `Track` holds values such as `int` and `string` and is the hottest animation path.
- Generalising `BracketedNewton` beyond a normalized function.
- Arc length and curvature.
- Second derivatives through `Ops`.

## Approaches

Raymond approved the three parts as proposed. Within them:

- **Where the domain lives.** A default interface member would box every struct curve that does not override it. An optional side interface cannot be reached through a generic constraint. Picked: a required `Domain` property on `ICurve`, one line per curve.
- **How spans are exposed.** Returning an array would allocate on every raycast against a line. Picked: the caller supplies storage and asks for one piece at a time.
- **Linear blend.** `IInterpolatable` cannot be replaced: five types have no `+` or `*` of their own. Picked: the operator blend is the linear rule for scalars and vectors; `IInterpolatable` stays for types with their own rule.

## Design

### 1. Domains

```csharp
public interface ICurve<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    Interval<TReal> Domain { get; }
    TValue Evaluate(TReal t);
}
```

- Geometry curves and the generic Béziers return [0, 1].
- `ProgressMap.Domain` is its input span, ordered: `[min(InputBegin, InputEnd), max(...)]`.
- `Sliced` returns [0, 1]. `Reparameterized` returns its map's domain. `Transformed`, `Extrapolated` and `Curve<,,>` return their source's.
- `Extrapolated` and `EvaluateClamped` act at the source's domain ends, not at 0 and 1. This fixes the mismatch with `ProgressMap.EvaluateClamped`.
- `Reverse`, `Split` and `Sample` work over the domain. `Normalized()` remaps the domain to [0, 1].
- New: `Ops.Concatenate(next)`. The result runs the first curve over its domain, then the second, shifted to start where the first ends. Its domain is the two lengths added. Values are not required to meet.
- The generic `Curve2D.ToPolyline<TCurve>` samples over the curve's domain.

### 2. Polynomial spans

```csharp
public interface IPolynomialCurve2D
{
    PolynomialSpans2D GetPolynomialSpans();
}

public readonly record struct PolynomialSpanRange(double From, double To, int ControlCount);
```

As built: one interface call per query returns a `PolynomialSpans2D` struct with `Count`, `MaximumDegree` and `Get(index, controls)`. It carries a single piece's controls inline, or refers to a polyline's points or a prepared span array, so a B-spline or rounded polyline is not looked up once per piece. Its constructors are internal, so only App2d.Core implements the interface for now. `RoundedPolylineCurve2D` keeps its own interval distance entry, which seeds the search with its evaluated ends before using the shared loop.

- Implemented by `LineSegmentCurve2D`, `QuadraticBezier2D`, `CubicBezier2D`, `PolylineCurve2D`, `BSpline2D` and `RoundedPolylineCurve2D`. Spans are in parameter order and each is a Bézier in double precision.
- `CurveRaycast2D`: an arc is the one special case; any `IPolynomialCurve2D` is one loop over its spans. The six per-type cases are deleted.
- `CurveDistance2D`: an arc is the one special case; any `IPolynomialCurve2D` is one loop over its spans, with the existing control-hull pruning. Per span: two controls use the segment solver, three the quadratic solver, four a new exact cubic solver, more fall back to sampling that span.
- Exact cubic distance: the stationary points of `|B(t) - p|^2` are the roots of a degree-5 polynomial, which `PolynomialRoots.FindRealRoots` already solves. A span whose coefficients are not finite falls back to sampling.
- `CubicBezier2D.Distance` and `BSpline2D.Distance` use the span path, so they stop sampling.
- Curves that are neither arcs nor polynomial (`NormalOffsetCurve2D`) keep sampled distance and unsupported raycast.

### 3. One linear blend

- `Interpolation.Linear<TValue, TReal>()` works for any `TValue` with `+` and `* TReal`: scalars and every vector.
- `Track.Linear` becomes generic over the same constraint.
- The separate `float` and `double` overloads of `Interpolate` are replaced by the operator-constrained one.
- `IInterpolatable`, `Interpolation.Default` and `Track.Of` stay for types with their own rule.

## Left to the builder

- Exact names, file placement, and how the two `Interpolate` extensions avoid ambiguity for vectors.
- Report any test whose expectation moves; do not loosen one silently.

## Hand-over

As before: worktree, copy to main uncommitted, merge by hand where Raymond has pending edits.
