using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>A circular arc with a signed sweep; positive angles turn counter-clockwise in Y-up coordinates.</summary>
public readonly record struct Arc2D : ICurve2D
{
    public Arc2D(Vector2 center, float radius, float startAngleRadians, float sweepAngleRadians)
    {
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        ArgGuard.ThrowIfNotFinite(startAngleRadians);
        ArgGuard.ThrowIfNotFinite(sweepAngleRadians);
        Center = center;
        Radius = radius;
        StartAngleRadians = startAngleRadians;
        SweepAngleRadians = sweepAngleRadians;
    }

    public Vector2 Center { get; }
    public float Radius { get; }
    public float StartAngleRadians { get; }
    public float SweepAngleRadians { get; }
    public double Length => (double)Radius * Math.Abs(SweepAngleRadians);
    public Vector2 Start => Evaluate(0f);
    public Vector2 End => Evaluate(1f);

    public Vector2 Evaluate(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var angle = (double)StartAngleRadians + (double)SweepAngleRadians * amount;
        return new((float)(Center.X + Radius * Math.Cos(angle)),
            (float)(Center.Y + Radius * Math.Sin(angle)));
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var angle = (double)StartAngleRadians + (double)SweepAngleRadians * amount;
        var speed = (double)Radius * SweepAngleRadians;
        return new((float)(-speed * Math.Sin(angle)), (float)(speed * Math.Cos(angle)));
    }
}
