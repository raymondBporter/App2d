using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.HeightFields;
using App2d.Core.Geometry.Operations.Curves;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.HeightFields;

/// <summary>Queries on the source function, and explicit sampling for existing polygon collision/rendering.</summary>
public static class HeightField2D
{
    /// <summary>Creates an attachable shape. Polylines are exact; splines/functions use a sampled polyline snapshot.</summary>
    public static HeightFieldShape2D ToShape(this IHeightField2D field, float maximumSegmentLength = 12f, float maximumDeviation = 0.05f) =>
        HeightFieldShape2D.Builder.FromHeightField(field, maximumSegmentLength, maximumDeviation);

    /// <summary>Creates a finite shape above an explicit bottom cutoff, preserving the original source.</summary>
    public static HeightFieldShape2D ToClippedShape(this IHeightField2D field, float bottom,
        float maximumSegmentLength = 12f, float maximumDeviation = 0.05f)
    {
        ArgGuard.ThrowIfNull(field);
        ArgGuard.Finite(bottom);
        ArgGuard.ThrowIf(bottom < field.YMin, "Clipping cannot extend a heightfield below its supplied YMin.", nameof(bottom));
        ArgGuard.Positive(maximumSegmentLength);
        ArgGuard.Positive(maximumDeviation);
        var surface = field is PolylineHeightField2D polyline
            ? polyline.Curve : field.ToPolyline(maximumSegmentLength, maximumDeviation);
        return HeightFieldShape2D.Builder.Create(surface.Points.ToArray(), bottom);
    }

    /// <summary>
    /// Linear slope scan for a polyline heightfield. Nonincreasing slopes form a convex solid below the top;
    /// each increasing turn starts the next piece at the shared vertex. No ear clipping or binary search.
    /// </summary>
    public static CompositeShape2D ToConvexPieces(this PolylineHeightField2D field)
    {
        ArgGuard.ThrowIfNull(field);
        ArgGuard.Finite(field.YMin);
        var points = field.Curve.Points;
        foreach (var point in points)
            ArgGuard.ThrowIf(point.Y <= field.YMin, "The bottom must be strictly below the surface.");
        var pieces = new List<IConvexShape2D>();
        var start = 0;
        for (var index = 1; index < points.Length - 1; index++)
        {
            if (Turn(points[index - 1], points[index], points[index + 1]) <= 0d) continue;
            Append(start, index, points);
            start = index;
        }
        Append(start, points.Length - 1, points);
        return CompositeShape2D.Builder.Create(pieces);

        void Append(int from, int to, ReadOnlySpan<Vector2> surface)
        {
            var vertices = new List<Vector2>(to - from + 3);
            for (var index = from; index <= to; index++)
            {
                // Exact collinearity only: removing these points does not change the boundary.
                while (vertices.Count >= 2 && Turn(vertices[^2], vertices[^1], surface[index]) == 0d)
                    vertices.RemoveAt(vertices.Count - 1);
                vertices.Add(surface[index]);
            }
            vertices.Add(new(surface[to].X, field.YMin));
            vertices.Add(new(surface[from].X, field.YMin));
            pieces.Add(ConvexPolygon2D.Builder.Create(vertices));
        }
    }

    private static double Turn(Vector2 a, Vector2 b, Vector2 c) =>
        ((double)b.X - a.X) * ((double)c.Y - b.Y) - ((double)b.Y - a.Y) * ((double)c.X - b.X);

    /// <summary>Conservative bounds; infinite when the bottom or the supplied upper bound is infinite.</summary>
    public static Rect2D Bounds(this IHeightField2D field)
    {
        ArgGuard.ThrowIfNull(field);
        ValidateDomain(field.XMin, field.XMax, field.YMin, field.YMax);
        return new(new(field.XMin, field.YMin), new(field.XMax, field.YMax));
    }

    /// <summary>Includes the top, flat bottom and vertical end caps. Points beyond XMin/XMax are outside.</summary>
    public static bool ContainsPoint(this IHeightField2D field, Vector2 point)
    {
        ArgGuard.ThrowIfNull(field);
        return float.IsFinite(point.X) && float.IsFinite(point.Y)
            && point.X >= field.XMin && point.X <= field.XMax
            && point.Y >= field.YMin && point.Y <= field.HeightAt(point.X);
    }

    /// <summary>The outward unit normal of the top: normalize((-dy/dx, 1)). End-cap normals are separate.</summary>
    public static Vector2 NormalAt(this IHeightField2D field, float x)
    {
        ArgGuard.ThrowIfNull(field);
        ValidateX(field, x);
        var slope = ArgGuard.Finite(field.SlopeAt(x));
        var inverseLength = (float)(1d / Math.Sqrt(1d + (double)slope * slope));
        return new(-slope * inverseLength, inverseLength);
    }

    /// <summary>Finite differences in world X, using one-sided differences at the domain endpoints.</summary>
    public static float EstimateSlope(IHeightField2D field, float x)
    {
        ArgGuard.ThrowIfNull(field);
        ValidateX(field, x);
        var step = (field.XMax - field.XMin) * 0.0001f;
        var lower = MathF.Max(field.XMin, MathF.Min(x - step, float.BitDecrement(x)));
        var upper = MathF.Min(field.XMax, MathF.Max(x + step, float.BitIncrement(x)));
        return ArgGuard.Finite((float)(((double)field.HeightAt(upper) - field.HeightAt(lower)) / ((double)upper - lower)));
    }

    /// <summary>
    /// Samples the top only, preserving polyline corners and B-spline knot spans. Maximum deviation is checked
    /// at sample points, not mathematically certified for arbitrary functions; resolve their shortest features
    /// with maximumSegmentLength. Source height/normal queries do not need this allocation.
    /// </summary>
    public static PolylineCurve2D ToPolyline(this IHeightField2D field, float maximumSegmentLength = 12f, float maximumDeviation = 0.05f)
    {
        ArgGuard.ThrowIfNull(field);
        ValidateDomain(field.XMin, field.XMax, field.YMin, field.YMax);
        ICurve2D curve = field switch
        {
            HeightFieldShape2D shape => shape.Profile.Curve,
            PolylineHeightField2D polyline => polyline.Curve,
            BSplineHeightField2D spline => spline.Curve,
            _ => new FunctionCurve(field)
        };
        var surface = Curve2D.ToPolyline(curve, maximumSegmentLength, maximumDeviation);
        foreach (var point in surface.Points) ValidateHeight(field, point.Y);
        return surface;
    }

    /// <summary>Bakes a finite solid for the existing shape collision system; YMin must be strictly below the sampled top.</summary>
    public static SimplePolygon2D ToPolygon(this IHeightField2D field, float maximumSegmentLength = 12f, float maximumDeviation = 0.05f)
    {
        ArgGuard.ThrowIfNull(field);
        if (!float.IsFinite(field.YMin))
            throw new InvalidOperationException("An infinite heightfield cannot become a finite polygon. Use ToClippedPolygon with an explicit finite bottom.");
        return SimplePolygon2D.Builder.BelowPolyline(field.ToPolyline(maximumSegmentLength, maximumDeviation).Points, field.YMin);
    }

    /// <summary>
    /// Bakes the part of a heightfield above a finite bottom cutoff, without changing its source domain.
    /// The cutoff must be at least YMin and strictly below the entire sampled top.
    /// </summary>
    public static SimplePolygon2D ToClippedPolygon(this IHeightField2D field, float bottom,
        float maximumSegmentLength = 12f, float maximumDeviation = 0.05f)
    {
        ArgGuard.ThrowIfNull(field);
        ArgGuard.Finite(bottom);
        ArgGuard.ThrowIf(bottom < field.YMin, "Clipping cannot extend a heightfield below its supplied YMin.", nameof(bottom));
        return SimplePolygon2D.Builder.BelowPolyline(field.ToPolyline(maximumSegmentLength, maximumDeviation).Points, bottom);
    }

    internal static void ValidateDomain(float xMin, float xMax, float yMin, float yMax)
    {
        ArgGuard.Finite(xMin);
        ArgGuard.Finite(xMax);
        ArgGuard.ThrowIf(xMax <= xMin || !float.IsFinite(xMax - xMin), "Heightfields need a positive finite X interval.", nameof(xMax));
        ArgGuard.ThrowIf(float.IsNaN(yMin) || yMin == float.PositiveInfinity, "YMin must be finite or negative infinity.", nameof(yMin));
        ArgGuard.ThrowIf(float.IsNaN(yMax) || yMax == float.NegativeInfinity || yMax < yMin,
            "YMax must be at least YMin, and finite or positive infinity.", nameof(yMax));
    }

    internal static void ValidateX(IHeightField2D field, float x)
    {
        ArgGuard.Finite(x);
        ArgGuard.InRange(x, field.XMin, field.XMax);
    }

    internal static float ValidateHeight(IHeightField2D field, float height)
    {
        ArgGuard.Finite(height);
        ArgGuard.ThrowIf(height < field.YMin || height > field.YMax, "Surface height must remain within the supplied YMin/YMax bounds.", nameof(height));
        return height;
    }

    internal static void ValidateIncreasingX(ReadOnlySpan<Vector2> points)
    {
        for (var index = 1; index < points.Length; index++)
            ArgGuard.ThrowIf(points[index].X <= points[index - 1].X, "Heightfields must run strictly left to right.", nameof(points));
    }

    private sealed class FunctionCurve(IHeightField2D field) : ICurve2D
    {
        public Vector2 Evaluate(float amount)
        {
            var x = float.Lerp(field.XMin, field.XMax, Math.Clamp(amount, 0f, 1f));
            return new(x, field.HeightAt(x));
        }

        public Vector2 EvaluateDerivative(float amount)
        {
            var x = float.Lerp(field.XMin, field.XMax, Math.Clamp(amount, 0f, 1f));
            var width = field.XMax - field.XMin;
            return new(width, width * field.SlopeAt(x));
        }

        public float Distance(Vector2 point) => CurveDistance2D.SampledDistance(point, this, 256);
    }
}
