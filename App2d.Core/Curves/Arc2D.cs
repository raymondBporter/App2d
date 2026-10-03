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

    public float Distance(Vector2 point)
    {
        if (Radius == 0 || SweepAngleRadians == 0) return Vector2.Distance(point, Start);
        var delta = point - Center;
        var radial = delta.Length();
        if (radial == 0 || Math.Abs((double)SweepAngleRadians) >= Math.Tau)
            return MathF.Abs(radial - Radius);

        var angle = Math.Atan2(delta.Y, delta.X);
        var sweep = (double)SweepAngleRadians;
        var progress = sweep > 0
            ? PositiveAngle(angle - StartAngleRadians)
            : PositiveAngle(StartAngleRadians - angle);
        if (progress <= Math.Abs(sweep)) return MathF.Abs(radial - Radius);
        return MathF.Min(Vector2.Distance(point, Start), Vector2.Distance(point, End));
    }

    private static double PositiveAngle(double angle)
    {
        var result = angle % Math.Tau;
        return result < 0 ? result + Math.Tau : result;
    }
}
