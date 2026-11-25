using App2d.Core.Geometry.Distance;
using App2d.Core.Validation;
using System.Runtime.InteropServices;

namespace App2d.Core.Geometry.Curves;

/// <summary>Sampling and local-frame operations shared by two-dimensional curves.</summary>
public static class Curve2D
{
    internal const float DerivativeEpsilon = 0.000001f;
    private const float FallbackSampleDistance = 0.001f;

    /// <summary>Returns a unit tangent, or zero when the curve is locally degenerate.</summary>
    public static Vector2f Tangent(ICurve2D curve, float amount)
    {
        Ensure.ThrowIfNull(curve);
        amount = Realf.Clamp01(amount);
        var derivative = curve.Derivative(amount);
        // Where the curve is at rest, or nearly, its direction is the second derivative's: with it when leaving the
        // rest point and against it when arriving. A remaining trace of velocity says which side this is.
        if (derivative.LengthSquared() <= DerivativeEpsilon * DerivativeEpsilon && curve is ITwiceDifferentiableCurve<Vector2f, float> twice)
        {
            var second = twice.SecondDerivative(amount);
            var along = Vector2f.Dot(derivative, second);
            derivative = along < 0f || along == 0f && amount >= 1f ? -second : second;
        }
        if (derivative.LengthSquared() <= DerivativeEpsilon * DerivativeEpsilon)
        {
            var lower = MathF.Max(0f, amount - FallbackSampleDistance);
            var upper = MathF.Min(1f, amount + FallbackSampleDistance);
            derivative = curve.Evaluate(upper) - curve.Evaluate(lower);
        }
        return derivative.LengthSquared() <= DerivativeEpsilon * DerivativeEpsilon
            ? Vector2f.Zero
            : Vector2f.Normalize(derivative);
    }

    /// <summary>Returns the tangent rotated 90 degrees counter-clockwise.</summary>
    public static Vector2f Normal(ICurve2D curve, float amount) => Tangent(curve, amount).PerpCcw;

    /// <summary>
    /// Adaptively samples a curve into a polyline. Limits chord length and sampled deviation at
    /// quarter/mid/three-quarter points. B-spline knot spans and polyline corners are sampled separately.
    /// The deviation check is an approximation, not a certified bound for arbitrary user-defined curves.
    /// </summary>
    public static PolylineCurve2D ToPolyline(ICurve2D curve, float maximumSegmentLength = 12f, float maximumDeviation = 0.05f)
    {
        Ensure.ThrowIfNull(curve);
        return curve switch
        {
            BSpline2D spline => ToPolyline(spline, maximumSegmentLength, maximumDeviation),
            PolylineCurve2D polyline => ToPolyline(polyline, maximumSegmentLength, maximumDeviation),
            _ => ToPolyline(curve, 16, maximumSegmentLength, maximumDeviation)
        };
    }

    /// <summary>Adaptively samples a B-spline, starting with each knot span.</summary>
    public static PolylineCurve2D ToPolyline(BSpline2D curve, float maximumSegmentLength = 12f, float maximumDeviation = 0.05f) =>
        ToPolyline(curve.PreparePositionSampler(), curve.ControlPoints.Length - curve.Degree, maximumSegmentLength, maximumDeviation);

    /// <summary>Subdivides a polyline while retaining every original vertex.</summary>
    public static PolylineCurve2D ToPolyline(PolylineCurve2D curve, float maximumSegmentLength = 12f, float maximumDeviation = 0.05f) =>
        SamplePolyline(curve, ValidateSampling(curve.Points.Length - 1, maximumSegmentLength, maximumDeviation), maximumSegmentLength, maximumDeviation, curve.Points);

    /// <summary>
    /// Adaptively samples any curve with 2D values, including a chain of curve operations, starting from the given
    /// number of equal parameter segments.
    /// </summary>
    public static PolylineCurve2D ToPolyline<TCurve>(TCurve curve, int segments, float maximumSegmentLength, float maximumDeviation) where TCurve : ICurve<Vector2f, float>
    {
        if (!typeof(TCurve).IsValueType) Ensure.ThrowIfNull(curve);
        return SamplePolyline(curve, ValidateSampling(segments, maximumSegmentLength, maximumDeviation), maximumSegmentLength, maximumDeviation, default);
    }

    private static int ValidateSampling(int segments, float maximumSegmentLength, float maximumDeviation)
    {
        Ensure.Positive(maximumSegmentLength);
        Ensure.Positive(maximumDeviation);
        return Ensure.Positive(segments);
    }

    // Exact boundaries, when supplied, are the values at each initial segment's ends: a polyline passes its own
    // vertices so that none is lost to rounding.
    private static PolylineCurve2D SamplePolyline<TCurve>(TCurve curve, int segments, float maximumSegmentLength, float maximumDeviation, ReadOnlySpan<Vector2f> boundaries)
        where TCurve : ICurve<Vector2f, float>
    {
        // Segment fractions are over the curve's domain: [0, 1] for geometry, so there the fraction is the parameter.
        var domain = curve.Domain;
        var points = new List<Vector2f> { boundaries.IsEmpty ? curve.Evaluate(At(0f)) : boundaries[0] };
        for (var segment = 0; segment < segments; segment++)
        {
            var from = segment / (float)segments;
            var to = (segment + 1) / (float)segments;
            Append(from, to, points[^1], boundaries.IsEmpty ? curve.Evaluate(At(to)) : boundaries[segment + 1], 0);
        }
        return PolylineCurve2D.Builder.CreateFromSpan(CollectionsMarshal.AsSpan(points));

        float At(float fraction) => fraction == 1f ? domain.Max : domain.Min + (domain.Max - domain.Min) * fraction;

        void Append(float from, float to, Vector2f start, Vector2f end, int depth)
        {
            Ensure.Finite(start);
            Ensure.Finite(end);
            var middle = (from + to) * 0.5f;
            var midpoint = curve.Evaluate(At(middle));
            var needsSplit = Vector2f.Distance(start, end) > maximumSegmentLength;
            for (var sample = 1; sample <= 3; sample++)
            {
                var point = sample == 2 ? midpoint : curve.Evaluate(At(float.Lerp(from, to, sample / 4f)));
                Ensure.Finite(point);
                needsSplit |= Distance2D.DistanceSquaredToSegment(point, start, end) > maximumDeviation * maximumDeviation;
            }
            if (needsSplit)
            {
                Ensure.LessThan(depth, 20);
                Ensure.NotEqual(middle, from);
                Ensure.NotEqual(middle, to);
                Append(from, middle, start, midpoint, depth + 1);
                Append(middle, to, midpoint, end, depth + 1);
            }
            else
            {
                points.Add(end);
            }
        }
    }
}
