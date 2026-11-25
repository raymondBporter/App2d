namespace App2d.Core.Geometry.Curves;

/// <summary>A circular arc with a signed sweep; positive angles turn counter-clockwise in Y-up coordinates.</summary>
public readonly partial record struct Arc2D : ICurve2D
{
    public const string Kind = "arc";

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
        amount = Realf.Clamp01(amount);
        var angle = (double)StartAngleRadians + (double)SweepAngleRadians * amount;
        return (Center.ToVector2d() + Vector2d.FromAngle(angle) * Radius).ToVector2f();
    }

    public Vector2f EvaluateDerivative(float amount)
    {
        amount = Realf.Clamp01(amount);
        var angle = (double)StartAngleRadians + (double)SweepAngleRadians * amount;
        var speed = (double)Radius * SweepAngleRadians;
        return (Vector2d.FromAngle(angle).PerpCcw * speed).ToVector2f();
    }

    public float Distance(Vector2f point)
    {
        if (Radius == 0 || SweepAngleRadians == 0)
        {
            return Vector2f.Distance(point, Start);
        }

        var delta = point - Center;
        var radial = delta.Length();
        if (radial == 0 || Math.Abs((double)SweepAngleRadians) >= Math.Tau)
        {
            return MathF.Abs(radial - Radius);
        }

        var angle = delta.ToVector2d().AngleRadians;
        var sweep = (double)SweepAngleRadians;
        var progress = sweep > 0
            ? Angles.WrapPositive(angle - StartAngleRadians)
            : Angles.WrapPositive(StartAngleRadians - angle);
        if (progress <= Math.Abs(sweep))
        {
            return MathF.Abs(radial - Radius);
        }

        return MathF.Min(Vector2f.Distance(point, Start), Vector2f.Distance(point, End));
    }
}
