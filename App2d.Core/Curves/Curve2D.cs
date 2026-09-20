using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>Sampling and local-frame operations shared by two-dimensional curves.</summary>
public static class Curve2D
{
    private const float DerivativeEpsilon = 0.000001f;
    private const float FallbackSampleDistance = 0.001f;

    /// <summary>Returns a unit tangent, or zero when the curve is locally degenerate.</summary>
    public static Vector2 Tangent(ICurve2D curve, float amount)
    {
        ArgumentNullException.ThrowIfNull(curve);
        amount = Math.Clamp(amount, 0f, 1f);
        var derivative = curve.EvaluateDerivative(amount);
        if (derivative.LengthSquared() <= DerivativeEpsilon * DerivativeEpsilon)
        {
            var lower = MathF.Max(0f, amount - FallbackSampleDistance);
            var upper = MathF.Min(1f, amount + FallbackSampleDistance);
            derivative = curve.Evaluate(upper) - curve.Evaluate(lower);
        }
        return derivative.LengthSquared() <= DerivativeEpsilon * DerivativeEpsilon
            ? Vector2.Zero
            : Vector2.Normalize(derivative);
    }

    /// <summary>Returns the tangent rotated 90 degrees counter-clockwise.</summary>
    public static Vector2 Normal(ICurve2D curve, float amount)
    {
        var tangent = Tangent(curve, amount);
        return new Vector2(-tangent.Y, tangent.X);
    }

    /// <summary>Samples both endpoints and the requested number of equal parameter-space segments.</summary>
    public static Vector2[] Sample(ICurve2D curve, int segmentCount)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentCount);
        var points = new Vector2[segmentCount + 1];
        for (var index = 0; index <= segmentCount; index++)
            points[index] = curve.Evaluate(index / (float)segmentCount);
        return points;
    }

    /// <summary>A unit-height Gaussian profile centered at <paramref name="center"/>.</summary>
    public static float Gaussian(float amount, float center, float sigma)
    {
        ArgGuard.ThrowIfNotFinite(amount);
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotPositive(sigma);
        var distance = amount - center;
        return MathF.Exp(-(distance * distance) / (2f * sigma * sigma));
    }
}
