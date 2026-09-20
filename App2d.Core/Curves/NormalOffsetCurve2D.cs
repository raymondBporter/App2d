using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>Offsets another curve along its local normal by a caller-provided profile.</summary>
public sealed class NormalOffsetCurve2D : ICurve2D
{
    private const float DerivativeSampleDistance = 0.001f;
    private readonly ICurve2D _source;
    private readonly Func<float, float> _offset;

    public NormalOffsetCurve2D(ICurve2D source, Func<float, float> offset)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(offset);
        _source = source;
        _offset = offset;
    }

    public Vector2 Evaluate(float amount) => EvaluateCore(Math.Clamp(amount, 0f, 1f));

    public Vector2 EvaluateDerivative(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var lower = MathF.Max(0f, amount - DerivativeSampleDistance);
        var upper = MathF.Min(1f, amount + DerivativeSampleDistance);
        return upper == lower
            ? Vector2.Zero
            : (EvaluateCore(upper) - EvaluateCore(lower)) / (upper - lower);
    }

    private Vector2 EvaluateCore(float amount) =>
        _source.Evaluate(amount) + Curve2D.Normal(_source, amount) * _offset(amount);
}
