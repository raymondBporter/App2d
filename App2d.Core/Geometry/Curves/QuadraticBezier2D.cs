using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>A quadratic Bezier curve over normalized progress [0, 1].</summary>
public readonly partial record struct QuadraticBezier2D : ICurve2D
{
    public string Kind => CurveKinds2D.QuadraticBezier;

    public QuadraticBezier2D(Vector2 start, Vector2 control, Vector2 end)
    {
        Start = start;
        Control = control;
        End = end;
    }

    public Vector2 Start { get; init; }
    public Vector2 Control { get; init; }
    public Vector2 End { get; init; }

    public Vector2 Evaluate(float amount)
    {
        amount = Clamping.Clamp01(amount);
        var inverse = 1f - amount;
        return inverse * inverse * Start + 2f * inverse * amount * Control + amount * amount * End;
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        amount = Clamping.Clamp01(amount);
        return 2f * ((1f - amount) * (Control - Start) + amount * (End - Control));
    }

    public float Distance(Vector2 point) => CurveDistance2D.SampledDistance(point, this, 64);
}
