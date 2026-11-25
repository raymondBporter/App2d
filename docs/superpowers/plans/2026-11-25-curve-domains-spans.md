# Curve Domains, Spans and Linear Blend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every curve a domain that `Ops` honours, write distance and raycast once over exact polynomial spans with exact cubic distance, and provide one linear interpolation for scalars and vectors.

**Architecture:** `Domain` is a required property on `ICurve`. `IPolynomialCurve2D` hands out one Bézier span at a time into caller storage. The linear blend is constrained on the operator interfaces.

**Tech Stack:** .NET 10, C# 14, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-25-curve-domains-spans-design.md`

## Global Constraints

- Constructors only assign; `Validate()` checks; `Ops` constructs then validates.
- No allocation per raycast or distance query against a single-span curve.
- Single-line signatures. CRLF. No commits.
- Each task: failing test first, then code, then the curve, mathematics, geometry and animation tests.
- Full suite excludes `GameUsesConfiguredWidthForTheActualCollidableTerrain`. Known before this work: the `Capsule2D` analyzer test fails; exact-zero allocation tests can flake.

## Review Focus

- A `ProgressMap` built with `FromRange(6, 2)` (reversed span) under `Ops.Clamped()` clamps at 2 and 6. (Task 1)
- `Concatenate` exactly at the join returns the first curve's end value. (Task 1)
- A polyline raycast that hits exactly on a shared vertex reports one hit. (Task 3)
- Cubic distance where the nearest point is an endpoint, and where the cubic is a straight line. (Task 4)
- A cubic with coordinates near 1e20 returns a finite distance. (Task 4)

---

### Task 1: Domains

**Files:** modify `Mathematics/Curves/{ICurve,Curve,CurveOperations,Extrapolation,BezierCurves}.cs`, every `ICurve2D` implementer, `ProgressMap.cs`, `Easing.cs`, `BSpline2D.cs`, `Curve2D.cs`; test `App2d.Tests/Curves/CurveDomainTests.cs` and the test-local curve structs.

**Produces:** `ICurve.Domain`; `Concatenated<TFirst, TSecond, TValue, TReal>(TFirst First, TSecond Second)`; `Curve<,,>.Concatenate<TNext>(TNext next)`, `.Normalized()`.

- [ ] Failing tests: unit domain for a Bézier and a 2D curve; `Easingf.Smooth.FromRange(6f, 2f).Domain` is [2, 6]; `map.Ops.EvaluateClamped(20f)` equals `map.EvaluateClamped(20f)` and `map.Ops.Clamped()` holds outside [2, 6] with a zero derivative; `Extended()` on a ranged map runs straight from the span's ends; `Normalized()` maps [0, 1] onto the span; `Reverse`, `Split` and `Sample` over a ranged map; `Concatenate` of two curves: domain length 2, value at the join, value and derivative in the second part, NaN; a chain allocates nothing.
- [ ] Run: fail to compile. Implement. Run: pass with every existing test unchanged.

### Task 2: Polynomial spans

**Files:** create `Geometry/Curves/IPolynomialCurve2D.cs`; modify the six implementers; test `App2d.Tests/Curves/PolynomialSpanTests.cs`.

- [ ] Failing tests: each implementer's span count and maximum degree; evaluating each span's controls with `BezierMath.EvaluateControls` reproduces the curve at five parameters per span to 1e-5; spans are contiguous from 0 to 1; an arc is not polynomial.
- [ ] Run: fail. Implement. Run: pass.

### Task 3: Raycast and distance over spans

**Files:** modify `CurveRaycast2D.cs`, `CurveDistance2D.cs`, `RoundedPolylineCurve2D.cs`, `QuadraticBezier2D.cs`.

- [ ] Add a failing test for a polyline hit exactly on a shared vertex and for interval distance on a B-spline. Replace the per-type cases with the span loop. Run `CurveRaycast2DTests`, `Curve2DTests`, `CurveSubdivisionTests`, `RoundedPolylineCurve2DTests`: pass unchanged, or report what moved.

### Task 4: Exact cubic distance

**Files:** modify `CurveDistance2D.cs`, `CubicBezier2D.cs`, `BSpline2D.cs`; test `App2d.Tests/Curves/CubicDistanceTests.cs`.

- [ ] Failing tests: distance from 200 fixed pseudo-random points to three cubics matches a 20,000-sample brute force to 1e-4 and is never larger; a cubic that is a straight line matches the segment distance to 1e-5; nearest point at an endpoint; a point on the curve gives less than 1e-5; coordinates near 1e20 give a finite result; a cubic B-spline matches brute force to 1e-4.
- [ ] Run: fail on the sampled values' accuracy. Implement. Run: pass.

### Task 5: One linear blend

**Files:** modify `Mathematics/Interpolation/Interpolation.cs`, `Interpolation.Strategies.cs`, `Animation/Track.cs`, the interpolation JSON types if they name the scalar rule; tests in `InterpolationTests`, `TrackTests`.

- [ ] Failing tests: `Interpolation.Linear<Vector2f, float>()` and `Track.Linear<Vector2f>` interpolate a vector; the scalar forms give the same values as before; a scalar and a vector both call the one `Interpolate` extension.
- [ ] Run: fail. Implement. Run: pass, with the clip files compiling unchanged.

### Task 6: Full suite, review, hand-over

- [ ] Build, run the full suite, dispatch one fresh reviewer on the whole diff, fix Critical and Important findings test-first, copy to main and run the suite there.
