using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

public readonly record struct QuadraticBezier<TVector, TReal>(TVector Start, TVector Control, TVector End) : ICurve<TVector, TReal>
    where TVector : unmanaged, IVector<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public TVector Evaluate(TReal amount) => BezierMath.Quadratic<TVector, TReal>(Start, Control, End, amount);
    public TVector EvaluateDerivative(TReal amount) => BezierMath.QuadraticDerivative<TVector, TReal>(Start, Control, End, amount);
}

public readonly record struct CubicBezier<TVector, TReal>(TVector Start, TVector Control1, TVector Control2, TVector End) : ICurve<TVector, TReal>
    where TVector : unmanaged, IVector<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public TVector Evaluate(TReal amount) => BezierMath.Cubic<TVector, TReal>(Start, Control1, Control2, End, amount);
    public TVector EvaluateDerivative(TReal amount) => BezierMath.CubicDerivative<TVector, TReal>(Start, Control1, Control2, End, amount);
}
