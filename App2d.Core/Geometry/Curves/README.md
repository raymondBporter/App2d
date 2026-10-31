# Generic parameterized curves

`ICurve<TPoint, TReal>` supplies position and first derivative over normalized progress. `ICurve2D` also supplies
2D geometry identity and distance queries. Generic vector Béziers use `IVector<TVector, TReal>`, so the same evaluator
supports the six float/double vectors without putting vector structs into interface-valued storage.

```csharp
var curve = new CubicBezier<Vector3f, float>(
    new(0, 0, 0), new(1, 2, 1), new(2, 2, -1), new(3, 0, 0));
Vector3f position = curve.Evaluate(.5f);
Vector3f velocity = curve.EvaluateDerivative(.5f);
var placed = curve.TransformedBy(frame); // Frame3D; translation does not affect the derivative
```

`TransformedCurve3D<TCurve>` handles float vectors; `TransformedCurve3d<TCurve>` retains double coordinates while
widening the existing frame's coefficients. Transforming a curve preserves its parameter, including under scale,
reflection and shear. It does not normalize its derivative or reparameterize by arc length.

`BezierMath` also supports scalar attributes with the same Bernstein weights. Existing quadratic/cubic 2D curve
evaluation delegates to it. Axis-specific root finding, 2D normals, polygon operations and distance queries stay in
their respective geometry APIs. `CurveControlPoints2D` edits supported planar curves independently of characters.
