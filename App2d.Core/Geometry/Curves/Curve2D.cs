using App2d.Core.Geometry.Distance;
using App2d.Core.Validation;
using System.Runtime.InteropServices;

namespace App2d.Core.Geometry.Curves;

/// <summary>Sampling and local-frame operations shared by two-dimensional curves.</summary>
public static class Curve2D
{
    private const float DerivativeEpsilon = 0.000001f;
    private const float FallbackSampleDistance = 0.001f;

    /// <summary>Restricts any curve to an ordered interval of its parameter, preserving its evaluation exactly.</summary>
    public static CurveSlice2D<TCurve> Slice<TCurve>(this TCurve curve, float from, float to) where TCurve : ICurve2D =>
        new(curve, from, to);

    /// <summary>Returns the left and right parameter intervals. Concrete curves may provide their own typed subdivision.</summary>
    public static (CurveSlice2D<TCurve> Left, CurveSlice2D<TCurve> Right) Split<TCurve>(this TCurve curve, float amount) where TCurve : ICurve2D
    {
        Ensure.InRange(amount, 0f, 1f);
        return (new(curve, 0f, amount), new(curve, amount, 1f));
    }

    /// <summary>Returns a unit tangent, or zero when the curve is locally degenerate.</summary>
    public static Vector2f Tangent(ICurve2D curve, float amount)
    {
        Ensure.ThrowIfNull(curve);
        amount = Realf.Clamp01(amount);
        var derivative = curve.Derivative(amount);
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

    /// <summary>Samples both endpoints and the requested number of equal parameter-space segments.</summary>
    public static Vector2f[] Sample(ICurve2D curve, int segmentCount)
    {
        Ensure.ThrowIfNull(curve);
        Ensure.Positive(segmentCount);
        var points = new Vector2f[segmentCount + 1];
        Sample(curve, points);
        return points;
    }

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
            _ => SamplePolyline(new InterfaceCurveSampler(curve), ValidateSampling(16, maximumSegmentLength, maximumDeviation),
                maximumSegmentLength, maximumDeviation)
        };
    }

    /// <summary>Adaptively samples a B-spline, starting with each knot span.</summary>
    public static PolylineCurve2D ToPolyline(BSpline2D curve, float maximumSegmentLength = 12f, float maximumDeviation = 0.05f)
    {
        var segments = ValidateSampling(curve.ControlPoints.Length - curve.Degree, maximumSegmentLength, maximumDeviation);
        return SamplePolyline(curve.PreparePositionSampler(), segments, maximumSegmentLength, maximumDeviation);
    }

    /// <summary>Subdivides a polyline while retaining every original vertex.</summary>
    public static PolylineCurve2D ToPolyline(PolylineCurve2D curve, float maximumSegmentLength = 12f, float maximumDeviation = 0.05f)
    {
        var segments = ValidateSampling(curve.Points.Length - 1, maximumSegmentLength, maximumDeviation);
        return SamplePolyline(new PolylineSampler(curve), segments, maximumSegmentLength, maximumDeviation);
    }

    private static int ValidateSampling(int segments, float maximumSegmentLength, float maximumDeviation)
    {
        Ensure.Positive(maximumSegmentLength);
        Ensure.Positive(maximumDeviation);
        return Ensure.Positive(segments);
    }

    internal interface IPolylineSampler<TSelf> where TSelf : struct, IPolylineSampler<TSelf>
    {
        static abstract Vector2f Evaluate(in TSelf sampler, float amount);
        static abstract Vector2f EvaluateBoundary(in TSelf sampler, int index, float amount);
    }

    private readonly struct PolylineSampler(PolylineCurve2D curve) : IPolylineSampler<PolylineSampler>
    {
        private readonly PolylineCurve2D _curve = curve;

        public static Vector2f Evaluate(in PolylineSampler sampler, float amount) => sampler._curve.Evaluate(amount);
        public static Vector2f EvaluateBoundary(in PolylineSampler sampler, int index, float amount) => sampler._curve.Points[index];
    }

    private readonly struct InterfaceCurveSampler(ICurve2D curve) : IPolylineSampler<InterfaceCurveSampler>
    {
        private readonly ICurve2D _curve = curve;

        public static Vector2f Evaluate(in InterfaceCurveSampler sampler, float amount) => sampler._curve.Evaluate(amount);
        public static Vector2f EvaluateBoundary(in InterfaceCurveSampler sampler, int index, float amount) => Evaluate(in sampler, amount);
    }

    private static PolylineCurve2D SamplePolyline<TSampler>(TSampler sampler, int segments, float maximumSegmentLength, float maximumDeviation)
        where TSampler : struct, IPolylineSampler<TSampler>
    {
        var points = new List<Vector2f> { TSampler.EvaluateBoundary(in sampler, 0, 0f) };
        for (var segment = 0; segment < segments; segment++)
        {
            var from = segment / (float)segments;
            var to = (segment + 1) / (float)segments;
            Append(from, to, points[^1], TSampler.EvaluateBoundary(in sampler, segment + 1, to), 0);
        }
        return PolylineCurve2D.Builder.CreateFromSpan(CollectionsMarshal.AsSpan(points));

        void Append(float from, float to, Vector2f start, Vector2f end, int depth)
        {
            Ensure.Finite(start);
            Ensure.Finite(end);
            var middle = (from + to) * 0.5f;
            var midpoint = TSampler.Evaluate(in sampler, middle);
            var needsSplit = Vector2f.Distance(start, end) > maximumSegmentLength;
            for (var sample = 1; sample <= 3; sample++)
            {
                var point = sample == 2 ? midpoint : TSampler.Evaluate(in sampler, float.Lerp(from, to, sample / 4f));
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

    /// <summary>Samples both endpoints into caller-owned storage without allocating.</summary>
    public static void Sample<TCurve>(TCurve curve, Span<Vector2f> points) where TCurve : ICurve2D
    {
        if (curve is null)
        {
            throw new ArgumentNullException(nameof(curve));
        }

        Ensure.ThrowIfTooShort(points, 2);
        var segmentCount = points.Length - 1;
        for (var index = 0; index < points.Length; index++)
        {
            points[index] = curve.Evaluate(index / (float)segmentCount);
        }
    }

    /// <summary>A unit-height Gaussian profile centered at <paramref name="center"/>.</summary>
    public static float Gaussian(float amount, float center, float sigma)
    {
        Ensure.Finite(amount);
        Ensure.Finite(center);
        Ensure.Positive(sigma);
        var distance = amount - center;
        return MathF.Exp(-(distance * distance) / (2f * sigma * sigma));
    }
}
