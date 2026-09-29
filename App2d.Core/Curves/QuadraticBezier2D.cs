using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>A quadratic Bezier curve over normalized progress [0, 1].</summary>
public readonly record struct QuadraticBezier2D : ICurve2D
{
    public QuadraticBezier2D(Vector2 start, Vector2 control, Vector2 end)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(control);
        ArgGuard.ThrowIfNotFinite(end);
        Start = start;
        Control = control;
        End = end;
    }

    public Vector2 Start { get; }
    public Vector2 Control { get; }
    public Vector2 End { get; }

    public Vector2 Evaluate(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var inverse = 1f - amount;
        return inverse * inverse * Start + 2f * inverse * amount * Control + amount * amount * End;
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return 2f * ((1f - amount) * (Control - Start) + amount * (End - Control));
    }
}
