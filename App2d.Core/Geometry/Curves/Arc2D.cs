using App2d.Core.Mathematics.Curves;

namespace App2d.Core.Geometry.Curves;

/// <summary>A circular arc with a signed sweep; positive angles turn counter-clockwise in Y-up coordinates.</summary>
public readonly partial record struct Arc2D : ICurve2D, ITwiceDifferentiableCurve<Vector2f, float>
{
    public GeometryKind Kind => GeometryKind.Arc;
    public Interval<float> Domain => Interval.UnitInterval<float>();

    public Arc2D(Vector2f center, float radius, float startAngleRadians, float sweepAngleRadians)
    {
        Center = center;
        Radius = radius;
        StartAngleRadians = startAngleRadians;
        SweepAngleRadians = sweepAngleRadians;
    }

    public Vector2f Center { get; init; }
    public float Radius { get; init; }
    public float StartAngleRadians { get; init; }
    public float SweepAngleRadians { get; init; }
    public double Length => (double)Radius * Math.Abs(SweepAngleRadians);
    public Vector2f Start => Evaluate(0f);
    public Vector2f End => Evaluate(1f);

    public Vector2f Evaluate(float amount)
    {
        var angle = (double)StartAngleRadians + (double)SweepAngleRadians * amount;
        return (Center.ToVector2d() + Vector2d.FromAngle(angle) * Radius).ToVector2f();
    }

    public Vector2f Derivative(float amount)
    {
        var angle = (double)StartAngleRadians + (double)SweepAngleRadians * amount;
        var speed = (double)Radius * SweepAngleRadians;
        return (Vector2d.FromAngle(angle).PerpCcw * speed).ToVector2f();
    }

    /// <summary>Points at the centre, with length radius times sweep squared.</summary>
    public Vector2f SecondDerivative(float amount)
    {
        var angle = (double)StartAngleRadians + (double)SweepAngleRadians * amount;
        return (Vector2d.FromAngle(angle) * (-(double)Radius * SweepAngleRadians * SweepAngleRadians)).ToVector2f();
    }

    public float Distance(Vector2f point) => Distance(point, 0, 1);

    internal float Distance(Vector2f point, double from, double to)
    {
        var delta = point.ToVector2d() - Center.ToVector2d();
        var startAngle = StartAngleRadians + (double)SweepAngleRadians * from;
        var sweep = SweepAngleRadians * (to - from);
        if (Radius == 0 || sweep == 0)
            return (float)(delta - Vector2d.FromAngle(startAngle) * Radius).Length();

        var radial = delta.Length();
        if (radial == 0 || Math.Abs(sweep) >= Math.Tau)
        {
            return (float)Math.Abs(radial - Radius);
        }

        var angle = delta.AngleRadians;
        var progress = sweep > 0
            ? Angles.WrapPositive(angle - startAngle)
            : Angles.WrapPositive(startAngle - angle);
        if (progress <= Math.Abs(sweep))
        {
            return (float)Math.Abs(radial - Radius);
        }

        var first = delta - Vector2d.FromAngle(startAngle) * Radius;
        var last = delta - Vector2d.FromAngle(StartAngleRadians + (double)SweepAngleRadians * to) * Radius;
        return (float)Math.Sqrt(Math.Min(first.LengthSquared(), last.LengthSquared()));
    }
}
