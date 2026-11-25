using App2d.Core.Geometry.Distance;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

/// <summary>
/// Offsets another curve along its local normal by a differentiable profile of the parameter, such as a progress map.
/// Clamps its parameter to [0, 1].
/// </summary>
public sealed class NormalOffsetCurve2D : ICurve2D
{
    private const float JerkStep = 0.001f;
    private readonly ICurve2D _source;
    private readonly IDifferentiableCurve<float, float> _offset;

    public NormalOffsetCurve2D(ICurve2D source, IDifferentiableCurve<float, float> offset)
    {
        Ensure.ThrowIfNull(source);
        Ensure.ThrowIfNull(offset);
        _source = source;
        _offset = offset;
    }

    public GeometryKind Kind => GeometryKind.NormalOffsetCurve;
    public Interval<float> Domain => Interval.UnitInterval<float>();

    public Vector2f Evaluate(float amount)
    {
        amount = Realf.Clamp01(amount);
        return _source.Evaluate(amount) + Curve2D.Normal(_source, amount) * _offset.Evaluate(amount);
    }

    public float Distance(Vector2f point) => CurveDistance2D.SampledDistance(point, this, 128);

    /// <summary>
    /// The derivative P' + N' * o + N * o'. The normal turns with the source's curvature, so the source must
    /// have a second derivative; one without it cannot be differentiated here. Exact wherever the source is moving.
    /// Where it is at rest the turn needs the third derivative, taken as the slope of the second; that is exact for
    /// curves up to cubic, whose second derivative is a straight line.
    /// </summary>
    public Vector2f Derivative(float amount)
    {
        if (_source is not ITwiceDifferentiableCurve<Vector2f, float> source)
            throw new NotSupportedException("Differentiating a normal offset needs a source curve with a second derivative.");

        amount = Realf.Clamp01(amount);
        var velocity = source.Derivative(amount);
        var derivative = velocity + Curve2D.Normal(_source, amount) * _offset.Derivative(amount);
        var speed = velocity.Length();
        if (speed <= Curve2D.DerivativeEpsilon) return derivative + TangentRateAtRest(source, amount).PerpCcw * _offset.Evaluate(amount);

        var tangent = velocity / speed;
        var acceleration = source.SecondDerivative(amount);
        var tangentRate = (acceleration - tangent * Vector2f.Dot(tangent, acceleration)) / speed;
        return derivative + tangentRate.PerpCcw * _offset.Evaluate(amount);
    }

    // Leaving a rest point the velocity is h P'' + h^2/2 P''', so the unit tangent starts along P'' and turns at the
    // part of P''' across it, over twice the length of P''. Arriving at the end, both signs reverse.
    private static Vector2f TangentRateAtRest(ITwiceDifferentiableCurve<Vector2f, float> source, float amount)
    {
        var acceleration = source.SecondDerivative(amount);
        var length = acceleration.Length();
        if (length <= Curve2D.DerivativeEpsilon) return Vector2f.Zero;

        var lower = MathF.Max(0f, amount - JerkStep);
        var upper = MathF.Min(1f, amount + JerkStep);
        var jerk = (source.SecondDerivative(upper) - source.SecondDerivative(lower)) / (upper - lower);
        var direction = acceleration / length;
        var rate = (jerk - direction * Vector2f.Dot(direction, jerk)) / (2f * length);
        return amount < 1f ? rate : -rate;
    }
}
