using App2d.Core.Mathematics;

namespace App2d.Core.Geometry.Curves;

/// <summary>An exact polynomial span in Bernstein form, retaining its source parameter interval.</summary>
internal sealed record PolynomialCurveSpan2D(double From, double To, Vector2d[] Controls);
