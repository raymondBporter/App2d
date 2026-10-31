using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>A parameterized path. Evaluation clamps progress to [0, 1]; derivatives use normalized progress.</summary>
public interface ICurve<TPoint, TReal> where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    TPoint Evaluate(TReal amount);
    TPoint EvaluateDerivative(TReal amount);
}
