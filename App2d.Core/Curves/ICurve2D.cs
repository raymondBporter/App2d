using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>A two-dimensional parametric curve evaluated over normalized progress [0, 1].</summary>
public interface ICurve2D
{
    /// <summary>Returns the point at <paramref name="amount"/>. Implementations clamp to [0, 1].</summary>
    Vector2 Evaluate(float amount);

    /// <summary>Returns the first derivative with respect to normalized progress.</summary>
    Vector2 EvaluateDerivative(float amount);
}
