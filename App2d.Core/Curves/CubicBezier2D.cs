using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>A cubic Bezier curve with normalized parameter range [0, 1].</summary>
public readonly record struct CubicBezier2D : ICurve2D
{
    public CubicBezier2D(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(control1);
        ArgGuard.ThrowIfNotFinite(control2);
        ArgGuard.ThrowIfNotFinite(end);
        Start = start;
        Control1 = control1;
        Control2 = control2;
        End = end;
    }

    public Vector2 Start { get; }
    public Vector2 Control1 { get; }
    public Vector2 Control2 { get; }
    public Vector2 End { get; }

    public Vector2 Evaluate(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var inverse = 1f - amount;
        return inverse * inverse * inverse * Start +
            3f * inverse * inverse * amount * Control1 +
            3f * inverse * amount * amount * Control2 +
            amount * amount * amount * End;
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var inverse = 1f - amount;
        return 3f * inverse * inverse * (Control1 - Start) +
            6f * inverse * amount * (Control2 - Control1) +
            3f * amount * amount * (End - Control2);
    }
}
