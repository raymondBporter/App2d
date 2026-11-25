# Curve Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give geometry curves and `ProgressMap` one interface ladder, write slice, reparameterize, transform, clamp and extend once as generic structs, and put root finding on the same interface.

**Architecture:** `ICurve` and `IDifferentiableCurve` live in `App2d.Core.Mathematics`. Leaves evaluate their plain formula. A `Curve<TCurve, TValue, TReal>` wrapper struct carries the type arguments once and exposes every operation as an instance method; one `Ops` extension per value type reaches it.

**Tech Stack:** .NET 10, C# 14 extension blocks, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-25-curve-foundation-design.md`

## Global Constraints

- No operation takes `ICurve<TValue, TReal>` by interface in its signature.
- No result changes for a parameter inside [0, 1].
- Single-line signatures. CRLF line endings; check with `git ls-files --eol`.
- No commits. Work stays uncommitted in the worktree, then is copied to `C:\Users\eonei\source\App2d` for Raymond to test.
- New types use namespace `App2d.Core.Mathematics`, which is a global using, in folder `App2d.Core/Mathematics/Curves/`.
- Exempt from "never clamps": `CurveSlice2D`, `NormalOffsetCurve2D`, `RoundedPolylineCurve2D`. `Segment.PointAt` keeps its clamp.
- Curve tests: `dotnet test App2d.Tests --filter "FullyQualifiedName~Curves|FullyQualifiedName~Mathematics|FullyQualifiedName~Geometry"`.
- Full suite: copy `Assets/Runtime/environments/tilesets/dark-cave` from the main checkout first, and exclude `GameUsesConfiguredWidthForTheActualCollidableTerrain`.

## Review Focus

- NaN parameter through every wrapper returns NaN and does not throw. (Task 3)
- A parameter of exactly 0 or 1 under `Clamped()` returns the curve's own derivative, not zero. (Task 3)
- A zero-width slice, `Slice(.3, .3)`, has a zero derivative and a constant value. (Task 3)
- A map derivative of NaN (`Easing.Step` at its jump) through `Reparameterize` stays NaN; only an exact zero short-circuits. (Task 3)
- A polyline asked far outside [0, 1] continues its end segment and never indexes out of range. (Task 2)

---

### Task 1: Interface ladder and rename

**Files:**
- Move: `App2d.Core/Geometry/Curves/{ICurve,BezierMath,BezierCurves}.cs` to `App2d.Core/Mathematics/Curves/`
- Modify: every file that mentions `EvaluateDerivative` (24 files), `App2d.Core/Geometry/Curves/ICurve2D.cs`

**Interfaces:**
- Produces: `ICurve<TValue, TReal> { TValue Evaluate(TReal t); }` and `IDifferentiableCurve<TValue, TReal> : ICurve<TValue, TReal> { TValue Derivative(TReal t); }`, both `where TReal : IFloatingPointIeee754<TReal>` with no `unmanaged`. `ICurve2D : IGeometryValue2D, IDifferentiableCurve<Vector2f, float>`. `QuadraticBezier<,>` and `CubicBezier<,>` implement `IDifferentiableCurve` and drop `unmanaged` on `TReal`.

- [ ] **Step 1:** Move the three files with `git mv`, change their namespace to `App2d.Core.Mathematics`, split the interface.
- [ ] **Step 2:** Rename the identifier `EvaluateDerivative` to `Derivative` repo-wide, preserving CRLF.
- [ ] **Step 3:** Build `App2d.slnx`. Expected: no errors.
- [ ] **Step 4:** Run the curve tests. Expected: same results as before the task.

### Task 2: Leaves evaluate their plain formula

**Files:**
- Modify: `Mathematics/Curves/BezierMath.cs`, `Geometry/Curves/{Arc2D,BSpline2D,PolylineCurve2D,LineSegmentCurve2D}.cs`
- Test: `App2d.Tests/Curves/Curve2DTests.cs`, `App2d.Tests/Geometry/SegmentMath2DTests.cs`, `App2d.Tests/Curves/GenericCurveTests.cs`, new `App2d.Tests/Curves/CurveExtrapolationTests.cs`

- [ ] **Step 1: Failing tests** in `CurveExtrapolationTests`:
  - `BezierContinuesItsPolynomial`: `new QuadraticBezier<double, double>(0, 0, 1)` gives `Evaluate(2) == 4` and `Derivative(2) == 4`.
  - `LineContinues`: `LineSegmentCurve2D.Create(new(1, 2), new(4, 6))` gives `Evaluate(2) == (7, 10)` and `Evaluate(-1) == (-2, -2)`.
  - `ArcContinuesRoundItsCircle`: a unit quarter arc from angle 0 gives `Evaluate(2)` at angle π, within 1e-6.
  - `PolylineContinuesItsEndSegments`: points (0,0), (1,0), (1,1) give `Evaluate(1.5f) == (1, 2)`, `Evaluate(-.5f) == (-1, 0)`, and `Evaluate(-100f)` does not throw.
  - `NaNStaysNaN` for the Bézier and the line.
- [ ] **Step 2:** Run them. Expected: fail on the clamped values.
- [ ] **Step 3:** Remove the four clamps in `BezierMath`, two in `Arc2D`, two in `BSpline2D`. `PolylineCurve2D.Segment` drops `Clamp01` and clamps the index to `[0, Points.Length - 2]`. `LineSegmentCurve2D.Evaluate` becomes `Vector2f.Lerp(Start, End, amount)`.
- [ ] **Step 4:** Change the tests that pinned leaf clamping to call `EvaluateClamped` through `Ops` once Task 3 exists; until then assert the continued values: `Curve2DTests` lines 130-131, `SegmentMath2DTests` lines 16-17, and the out-of-range rows of `BeziersSupportScalarsAndAllSixVectorTypesWithNormalizedDerivatives`.
- [ ] **Step 5:** Run the curve tests. Expected: pass.

### Task 3: The wrapper, its operations and extrapolation

**Files:**
- Create: `Mathematics/Curves/Curve.cs`, `CurveOperations.cs`, `Extrapolation.cs`, `CurveEntryPoints.cs`
- Test: `App2d.Tests/Curves/CurveOperationsTests.cs`

**Interfaces:**
- Produces:
  - `enum Extrapolation { None, Clamp, Extend }`
  - `Sliced<TCurve, TValue, TReal>(TCurve Curve, TReal From, TReal To)`
  - `Reparameterized<TCurve, TMap, TValue, TReal>(TCurve Curve, TMap Map)` with `TMap : IDifferentiableCurve<TReal, TReal>`
  - `Extrapolated<TCurve, TValue, TReal>(TCurve Curve, Extrapolation Before, Extrapolation After)`
  - `Curve<TCurve, TValue, TReal>(TCurve Source)` with `Slice(TReal from, TReal to)`, `Reverse()`, `Reparameterize<TMap>(TMap map)`, `Clamped()`, `Extended()`, `Extrapolated(Extrapolation before, Extrapolation after)`, `EvaluateClamped(TReal t)`. Each operation returns `Curve<NewStruct, TValue, TReal>`.
  - All are `readonly record struct`, implement `IDifferentiableCurve<TValue, TReal>`, and constrain `TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>, IAdditiveIdentity<TValue, TValue>`.
  - `Ops` extension property in eight static classes: `FloatCurves`, `DoubleCurves`, `Vector2fCurves`, `Vector2dCurves`, `Vector3fCurves`, `Vector3dCurves`, `Vector4fCurves`, `Vector4dCurves`.

- [ ] **Step 1: Failing tests** in `CurveOperationsTests`:
  - Raymond's four reparameterize tests, rewritten: smooth chain rule (`0.0244140625`, `0.3515625`, zero at both ends) on `curve.Ops.Reparameterize(Easingd.Smooth)`; reverse with rows `(.25, .75)` expecting `-curve.Derivative(.75)` and rows `(-.25)`, `(1.25)` expecting the held endpoint and a zero tangent on `curve.Ops.Reparameterize(ProgressMapf.Reverse).Clamped()`; overshoot rows `(.125, 0, 0)`, `(.25, 0, 4)`, `(.5, 1, 4)`, `(.75, 2, 4)`, `(.875, 2, 0)` on `curve.Ops.Clamped().Reparameterize(ProgressMapf.Affine(2f, -.5f))`; constant map over a corner curve gives value 0 and tangent 0.
  - `ExtendRunsStraightPastBothEnds`: quadratic `(0,0) (1,0) (2,0)` extended gives `Evaluate(1.5f) == (3, 0)` and `Derivative(1.5f) == (2, 0)`.
  - `SliceMatchesTheSourceAndScalesTheTangent`, `ReverseIsSliceOneZero`, `ZeroWidthSliceIsStationary`.
  - `ClampedKeepsTheTangentAtExactEndpoints`, `NaNPassesThroughEveryWrapper`, `StepJumpKeepsItsNaNDerivative`.
  - `ChainedStructCurveDoesNotAllocate`: after one warm-up call, 10,000 evaluations of `bez.Ops.Slice(.25, .75).Reparameterize(new SmoothStruct()).Clamped()` allocate 0 bytes.
  - `OpsWorksOnAnErasedCurve2D`: `((ICurve2D)line).Ops.Extended().Evaluate(2f)`.
- [ ] **Step 2:** Run. Expected: compile errors for the missing types.
- [ ] **Step 3:** Implement. `Reparameterized.Derivative` reads the map derivative first and returns `TValue.AdditiveIdentity` when it is exactly zero. `Extrapolated` treats `t < 0` as before and `t > 1` as after; anything else, including NaN, goes to the source.
- [ ] **Step 4:** Run the curve tests. Expected: pass.

### Task 4: `ProgressMap` is a curve

**Files:**
- Modify: `Mathematics/Progress/ProgressMap.cs` and every caller of `Sample` or `SampleClamped`

- [ ] **Step 1:** `ProgressMap<TReal> : IDifferentiableCurve<TReal, TReal>`; rename `Sample` to `Evaluate` and `SampleClamped` to `EvaluateClamped`.
- [ ] **Step 2:** Build, then fix each CS1061 at the reported position. Do not text-replace `.Sample(`; `Track`, `Curve2D`, `KeyframeSampling` and `ReactionTimeCurve2D` have their own.
- [ ] **Step 3:** Run the Mathematics tests. Expected: pass.

### Task 5: Transforms

**Files:**
- Create: `Mathematics/Transforms/ITransform.cs`
- Modify: `Frame2D.cs`, `Frame3D.cs`, `RigidTransform2D.cs`, `Similarity2D.cs`, `Affine2D.cs`, `Mathematics/Curves/Curve.cs`, `CurveOperations.cs`
- Delete: `Geometry/Curves/TransformedCurve3D.cs`
- Test: `App2d.Tests/Curves/GenericCurveTests.cs`

**Interfaces:**
- Produces: `ITransform<TPoint> { TPoint TransformPoint(TPoint local); TPoint TransformDirection(TPoint local); }`, `Transformed<TCurve, TTransform, TValue, TReal>(TCurve Curve, TTransform Transform)`, and `Curve<,,>.Transform<TTransform>(TTransform transform)`.
- `Frame3D` implements `ITransform<Vector3f>` and, explicitly, `ITransform<Vector3d>` with widened coefficients.

- [ ] **Step 1:** Rewrite the two existing transform tests to `curve.Ops.Transform(frame)` and add `Transformed2DCurveMovesPointsButNotTangentsByTranslation` using `Frame2D.FromRotation(MathF.PI / 2, new(10, 0))`.
- [ ] **Step 2:** Run. Expected: compile errors.
- [ ] **Step 3:** Implement and delete `TransformedCurve3D.cs`.
- [ ] **Step 4:** Run the curve tests. Expected: pass, including the double-precision test at `16_777_217`.

### Task 6: Root finding takes a curve

**Files:**
- Modify: `Mathematics/RootFinding/BracketedNewton.cs`, `Mathematics/Progress/Easing.cs`, `Geometry/Curves/CubicBezier2D.cs`

- [ ] **Step 1:** `BracketedNewton.TrySolve<TFunction, TReal>(TFunction function, TReal target, int iterationLimit, out TReal parameter) where TFunction : IDifferentiableCurve<TReal, TReal>`. Delete `IFunction`. Midpoint is `lower + (upper - lower) / 2`; stop when `next == parameter || next == lower || next == upper`.
- [ ] **Step 2:** Delete `Easing.BezierTime` and `CubicBezier2D.NormalizedX`; both pass `new CubicBezier<TReal, TReal>(0, firstTime, secondTime, 1)`.
- [ ] **Step 3:** Run `EasingTests`, `CubicBezierOperationsTests`, `InterpolationTests`, `InterpolatorTests`. Expected: pass with no expectation changed. If a `ParameterAtX` value moves, report it; do not loosen the test.

### Task 7: `CurveMotion2D` uses `Extended()`

**Files:**
- Modify: `Physics/Motion/CurveMotion2D.cs`

- [ ] **Step 1:** Delete `EvaluateProgress`. Position and velocity read `Curve.Ops.Extended()`. `CheckedProgress`, phase, period scaling and the `double` progress map stay.
- [ ] **Step 2:** Run `KinematicEntity2DTests` and `ObjectPlatforms`. Expected: pass unchanged.

### Task 8: Full suite and hand-over

- [ ] **Step 1:** Build `App2d.slnx` with warnings checked for the new files.
- [ ] **Step 2:** Run the full suite with the named exclusion. Expected: no failures beyond the known flaky zero-allocation tests.
- [ ] **Step 3:** Back up Raymond's uncommitted curve experiments from the main checkout to a patch, copy the changed files in, and run the full suite there.
