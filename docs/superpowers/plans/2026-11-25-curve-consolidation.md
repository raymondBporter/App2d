# Curve Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One implementation per curve capability, real derivatives where they are well defined, and the duplicate helpers deleted.

**Architecture:** Capabilities are interfaces on the curve (`ITwiceDifferentiableCurve`, `IControlPointCurve`) or operations on the `Curve<,,>` wrapper. The 2D geometry types keep `Kind`, `Distance` and their builders and delegate the math.

**Tech Stack:** .NET 10, C# 14, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-25-curve-consolidation-design.md`

## Global Constraints

- Constructors only assign. `Validate()` checks. Builders and `Ops` construct then validate.
- Do not edit the `Derivative` bodies of `PolylineCurve2D`, `LineSegmentCurve2D` or `BSpline2D`.
- Single-line signatures. CRLF. No commits.
- Each task: failing test first, then code, then `dotnet test App2d.Tests --filter "FullyQualifiedName~Curves|FullyQualifiedName~Mathematics|FullyQualifiedName~Geometry|FullyQualifiedName~Animation"`.
- Full suite excludes `GameUsesConfiguredWidthForTheActualCollidableTerrain`. Known before this work: the `Capsule2D` analyzer test fails; exact-zero allocation tests can flake in a full run.

## Review Focus

- A source curve with zero speed under `NormalOffsetCurve2D.Derivative` returns a finite value. (Task 1)
- `WithControlPoints` with the wrong count throws and never builds a half-updated curve. (Task 2)
- `Split` at exactly 0 and 1 returns a point piece and the whole curve. (Task 3)
- A reversed or empty interval in the public interval distance and raycast queries. (Task 4)
- `Track.Sample` exactly on a key, before the first and after the last key returns the key's own value. (Task 7)

---

### Task 1: Second derivatives

**Files:** create `Mathematics/Curves/ITwiceDifferentiableCurve.cs`; modify `BezierMath.cs`, `BezierCurves.cs`, the eight 2D curve files, `Curve2D.cs`, `NormalOffsetCurve2D.cs`, `Easing.cs`; test `App2d.Tests/Curves/SecondDerivativeTests.cs` and the three test files that build a `NormalOffsetCurve2D`.

**Produces:** `ITwiceDifferentiableCurve<TValue, TReal>.SecondDerivative(TReal t)`; `BezierMath.QuadraticSecondDerivative`, `BezierMath.CubicSecondDerivative`; `NormalOffsetCurve2D(ICurve2D source, IDifferentiableCurve<float, float> offset)`; `Easing<TReal>.Gaussian(TReal center, TReal sigma)`.

- [ ] Failing tests: `t^3` gives `6t`; `t^2` gives 2; a 2D cubic matches the generic one; a line and a polyline give zero; an arc of radius 2 and sweep pi/2 gives length `2 * (pi/2)^2` pointing at the centre; a degree-2 B-spline of three points matches its quadratic Bézier; a cubic with `Start == Control1` has tangent `(1, 1) / sqrt(2)` at 0 to six places; a line offset by `ProgressMapf.Forward` has derivative `(10, 1)`; an arc offset by a constant 1 has the arc's derivative times 0.5; a zero-speed source gives a finite offset derivative; `Easingd.Gaussian(.5, .1)` is 1 at its centre with zero slope and matches a central difference elsewhere.
- [ ] Run: fail to compile.
- [ ] Implement. Run: pass.

### Task 2: Control points

**Files:** create `Mathematics/Curves/IControlPointCurve.cs`; modify `BezierCurves.cs`, the 2D Béziers, line, polyline, B-spline, rounded polyline, `Characters/Attachments/Face.cs`; delete `CurveControlPoints2D.cs`, `SpatialCurveControlPoints.cs`, `Geometry/Serialization/SpatialCurveJsonConverter.cs`; test `App2d.Tests/Curves/ControlPointCurveTests.cs`.

- [ ] Failing tests: read and replace round-trips for each implementer; a 3D generic cubic; wrong count throws `InvalidDataException`; an erased `ICurve2D` works through `IControlPointCurve<Vector2f, ICurve2D>`.
- [ ] Run: fail. Implement. Run: pass.

### Task 3: Bézier math on the generic types

**Files:** modify `BezierCurves.cs`, `CubicBezier2D.cs`; test `App2d.Tests/Curves/GenericBezierOperationsTests.cs`.

**Produces:** `CubicBezier<,>.Split(TReal t)`, `.Slice(TReal from, TReal to)`, `static FromHermite(start, end, startTangent, endTangent)`; `QuadraticBezier<,>.Split`, `.Slice`.

- [ ] Failing tests: both halves reproduce the source for a scalar and a 3D cubic and a quadratic; `Slice(.2, .8)` matches the source at five parameters; `Split(0)` and `Split(1)`; Hermite endpoints and tangents.
- [ ] Run: fail. Implement; `CubicBezier2D` delegates. Run: pass with `CubicBezierOperationsTests` unchanged, or report what moved.

### Task 4: One slice

**Files:** delete `CurveSlice2D.cs`; modify `Curve2D.cs`, `Curve.cs`, `CurveDistance2D.cs`, `CurveRaycast2D.cs`, `GeometryKind.cs`, the geometry JSON factory if it names the kind; rewrite the slice tests in `Curve2DTests`, `CurveSubdivisionTests`, `CubicBezierOperationsTests`, `CurveRaycast2DTests`.

**Produces:** `Curve<,,>.Split(TReal t)`; `CurveDistance2D.Distance(Vector2f point, ICurve2D curve, float from, float to)`; a public interval overload of `CurveRaycast2D.TryIntersect`.

- [ ] Rewrite the tests to the new calls first. Run: fail. Implement and delete. Run: pass.

### Task 5: Sampling on `Ops`

**Files:** modify `Curve.cs`, `Curve2D.cs`, `BSpline2D.cs`, `Face.cs`; tests in `CurveOperationsTests`, `CurvePolylineSamplingTests`, `Curve2DTests`.

- [ ] Failing tests: `Ops.Sample` writes both endpoints for a 3D curve; `Ops.Sample(4)` returns five points. Run: fail. Implement; remove `IPolylineSampler`, `PolylineSampler`, `InterfaceCurveSampler`, `Curve2D.Sample`. Run: pass with `CurvePolylineSamplingTests` unchanged.

### Task 6: Easing on the curve layer

**Files:** modify `Easing.cs`.

- [ ] `Easing.Bezier` uses `CubicBezier<TReal, TReal>.Ops.Extended()`; `InverseSmoothStep` uses `BracketedNewton.TrySolve`. Run `EasingTests`, `ProgressMapTests`, `InterpolationTests`, `InterpolatorTests`, JSON tests: pass unchanged, or report what moved.

### Task 7: One segment search

**Files:** modify `Mathematics/OrderedSearch.cs`, `Animation/Track.cs`, `Animation/KeyframeSampling.cs`; test `OrderedSearchTests`.

- [ ] Failing test for a `ReadOnlySpan` overload of `LowerBound` with a key selector. Implement; `Track.Sample` and `TrySegment` use it. Run `TrackTests`, `KeyframeSamplerTests`, `OrderedSearchTests`: pass unchanged.

### Task 8: Full suite, review, hand-over

- [ ] Build `App2d.slnx`, run the full suite, dispatch one fresh reviewer on the whole diff, fix Critical and Important findings test-first, copy to main and run the suite there.
