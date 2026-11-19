using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Bounds;

public static partial class ShapeBounds2D
{
    private const int CapsuleAngleSamples = 16;
    private const int CapsuleAngleRefinements = 8;
    private const int CapsuleRadiusSplits = 24;
    private const int CapsuleHeightIterations = 18;

    /// <summary>
    /// Fits an enclosing capsule to finite points. The angle and radius search has a fixed budget
    /// and approximates the minimum area. The stored endpoints and radius are checked against the input.
    /// </summary>
    /// <param name="points">At least one point, in any order.</param>
    /// <param name="minimumRadius">A positive radius floor, including for point and collinear inputs.</param>
    public static Capsule2D GetContainingCapsule(ReadOnlySpan<Vector2f> points, float minimumRadius = float.Epsilon)
    {
        Ensure.ThrowIfTooShort(points, 1);
        Ensure.Positive(minimumRadius);
        foreach (var point in points)
        {
            Ensure.Finite(point, nameof(points));
        }

        var hull = PolygonGeometry2D.ConvexHullOfFinitePoints(points.ToArray());
        if (hull.Length == 1)
        {
            return Capsule2D.Builder.Create(hull[0], hull[0], minimumRadius);
        }

        var bounds = Rect2f.FromPoints(points);
        var min = bounds.Min.ToVector2d();
        var max = bounds.Max.ToVector2d();
        var center = (min + max) / 2d;

        // A horizontal spine at Y = 0 always has a representable radius for finite float points.
        var best = Capsule2D.Builder.Create(new(bounds.Min.X, 0f), new(bounds.Max.X, 0f),
            Math.Max(minimumRadius, Math.Max(Math.Abs(bounds.Min.Y), Math.Abs(bounds.Max.Y))));
        best = KeepSmallerCapsule(points, new(min.X, center.Y), new(max.X, center.Y), minimumRadius, best);
        best = KeepSmallerCapsule(points, new(center.X, min.Y), new(center.X, max.Y), minimumRadius, best);

        if (hull.Length == 2)
        {
            var start = hull[0].ToVector2d();
            var end = hull[^1].ToVector2d();
            var segment = end - start;
            var length = segment.Length();
            if (length <= 2d * minimumRadius)
            {
                return KeepSmallerCapsule(points, center, center, minimumRadius, best);
            }

            // The two caps can cover one radius of the original segment at each end.
            var inset = segment * (minimumRadius / length);
            best = KeepSmallerCapsule(points, start, end, minimumRadius, best);
            return KeepSmallerCapsule(points, start + inset, end - inset, minimumRadius, best);
        }

        var centered = new Vector2d[hull.Length];
        var circleRadiusSquared = 0d;
        for (var i = 0; i < centered.Length; i++)
        {
            centered[i] = hull[i].ToVector2d() - center;
            circleRadiusSquared = Math.Max(circleRadiusSquared, centered[i].LengthSquared());
        }

        best = KeepSmallerCapsule(points, center, center, minimumRadius, best);
        var circleRadius = Math.Max(minimumRadius, Math.Sqrt(circleRadiusSquared));
        var fit = SearchCapsuleAngles(centered, minimumRadius, circleRadius);
        return KeepSmallerCapsule(points, fit.Start + center, fit.End + center, minimumRadius, best);
    }

    private static CapsuleFit SearchCapsuleAngles(Vector2d[] points, double minimumRadius, double circleRadius)
    {
        var projected = new Vector2d[points.Length];
        var radii = new PriorityQueue<RadiusInterval, double>();
        var circle = new CapsuleFit(Vector2d.Zero, Vector2d.Zero, CapsuleArea(0d, circleRadius));
        var best = circle;
        var step = Math.PI / CapsuleAngleSamples;
        var areas = new double[CapsuleAngleSamples];
        for (var i = 0; i < areas.Length; i++)
        {
            areas[i] = EvaluateAngle(i * step);
        }

        // Refine several coarse candidates. A long hull edge also seeds thin rotated inputs.
        for (var i = 0; i < 3; i++)
        {
            var index = Array.IndexOf(areas, areas.Min());
            RefineAngle(index * step, areas[index]);
            areas[index] = double.PositiveInfinity;
        }

        var longest = Vector2d.Zero;
        var previous = points[^1];
        foreach (var point in points)
        {
            var edge = point - previous;
            if (edge.LengthSquared() > longest.LengthSquared())
            {
                longest = edge;
            }
            previous = point;
        }

        var edgeAngle = longest.AngleRadians;
        RefineAngle(edgeAngle, EvaluateAngle(edgeAngle));
        return best;

        double EvaluateAngle(double angle)
        {
            var axis = Vector2d.FromAngle(angle);
            var normal = axis.PerpCcw;
            var minY = double.PositiveInfinity;
            var maxY = double.NegativeInfinity;
            for (var i = 0; i < points.Length; i++)
            {
                var y = points[i].Dot(normal);
                projected[i] = new(points[i].Dot(axis), y);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            var lowRadius = Math.Max(minimumRadius, (maxY - minY) / 2d);
            var highRadius = Math.Min(circleRadius, float.MaxValue);
            if (lowRadius > highRadius)
            {
                return double.PositiveInfinity;
            }

            var fit = SearchCapsuleRadii(projected, minY, maxY, axis, lowRadius, highRadius, circle, radii);
            if (fit.Area < best.Area)
            {
                best = fit;
            }
            return fit.Area;
        }

        void RefineAngle(double centerAngle, double centerArea)
        {
            var offset = step / 2d;
            for (var i = 0; i < CapsuleAngleRefinements; i++, offset /= 2d)
            {
                var left = centerAngle - offset;
                var right = centerAngle + offset;
                var leftArea = EvaluateAngle(left);
                var rightArea = EvaluateAngle(right);
                if (leftArea < centerArea && leftArea <= rightArea)
                {
                    centerAngle = left;
                    centerArea = leftArea;
                }
                else if (rightArea < centerArea)
                {
                    centerAngle = right;
                    centerArea = rightArea;
                }
            }
        }
    }

    private static CapsuleFit SearchCapsuleRadii(Vector2d[] points, double minY, double maxY,
        Vector2d axis, double lowRadius, double highRadius, CapsuleFit circle, PriorityQueue<RadiusInterval, double> radii)
    {
        var best = circle;
        radii.Clear();
        EvaluateRadius(lowRadius);
        if (lowRadius == highRadius)
        {
            return best;
        }

        Enqueue(lowRadius, highRadius, EvaluateRadius(highRadius));
        for (var i = 0; i < CapsuleRadiusSplits && radii.TryDequeue(out var interval, out var bound); i++)
        {
            if (bound >= best.Area)
            {
                break;
            }

            var middle = (interval.Low + interval.High) / 2d;
            if (middle > interval.Low && middle < interval.High)
            {
                Enqueue(interval.Low, middle, EvaluateRadius(middle));
                Enqueue(middle, interval.High, interval.LengthLowerBoundAtHigh);
            }
        }
        return best;

        double EvaluateRadius(double radius)
        {
            var spine = SearchSpineHeight(points, minY, maxY, radius, out var lengthLowerBound);
            var area = CapsuleArea(spine.Length, radius);
            if (area < best.Area)
            {
                var offset = axis.PerpCcw * spine.Height;
                best = new(axis * spine.Start + offset, axis * spine.End + offset, area);
            }
            return lengthLowerBound;
        }

        void Enqueue(double low, double high, double lengthLowerBoundAtHigh)
        {
            // The larger radius needs the shortest spine. Pair its length bound with the cheaper radius.
            var areaLowerBound = CapsuleArea(lengthLowerBoundAtHigh, low);
            if (areaLowerBound < best.Area)
            {
                radii.Enqueue(new(low, high, lengthLowerBoundAtHigh), areaLowerBound);
            }
        }
    }

    private static CapsuleSpine SearchSpineHeight(ReadOnlySpan<Vector2d> points, double minY, double maxY,
        double radius, out double lengthLowerBound)
    {
        var low = maxY - radius;
        var high = minY + radius;
        if (low >= high)
        {
            var spine = SpineAtHeight(points, radius, (low + high) / 2d);
            lengthLowerBound = spine.Length;
            return spine;
        }

        var best = ShorterSpine(SpineAtHeight(points, radius, low), SpineAtHeight(points, radius, high));
        best = ShorterSpine(best, SpineAtHeight(points, radius, (low + high) / 2d));
        for (var i = 0; i < CapsuleHeightIterations; i++)
        {
            var third = (high - low) / 3d;
            var left = SpineAtHeight(points, radius, low + third);
            var right = SpineAtHeight(points, radius, high - third);
            best = ShorterSpine(best, ShorterSpine(left, right));

            // Spine length is convex in height. The shorter trial tells us which side can be discarded.
            if (left.Length <= right.Length)
            {
                high = right.Height;
            }
            if (right.Length <= left.Length)
            {
                low = left.Height;
            }
        }

        // Give each point its best disk slice anywhere in the surviving bracket. Allowing separate
        // heights can only shorten the required spine, making this a lower bound for the radius search.
        var (start, end) = SpineExtents(points, radius, low, high);
        lengthLowerBound = Math.Max(0d, end - start);
        return best;
    }

    private static CapsuleSpine SpineAtHeight(ReadOnlySpan<Vector2d> points, double radius, double height)
    {
        var (start, end) = SpineExtents(points, radius, height, height);
        if (start > end)
        {
            // All disk slices share an interval, so a single point in it suffices.
            start = end = (start + end) / 2d;
        }
        return new(start, end, height);
    }

    private static (double Start, double End) SpineExtents(ReadOnlySpan<Vector2d> points, double radius,
        double lowHeight, double highHeight)
    {
        var start = double.PositiveInfinity;
        var end = double.NegativeInfinity;
        foreach (var point in points)
        {
            var offset = Math.Abs(point.Y - Math.Clamp(point.Y, lowHeight, highHeight));
            var reach = Math.Sqrt(Math.Max(0d, (radius - offset) * (radius + offset)));
            start = Math.Min(start, point.X + reach);
            end = Math.Max(end, point.X - reach);
        }
        return (start, end);
    }

    private static CapsuleSpine ShorterSpine(CapsuleSpine first, CapsuleSpine second) =>
        first.Length <= second.Length ? first : second;

    private static Capsule2D KeepSmallerCapsule(ReadOnlySpan<Vector2f> points, Vector2d start, Vector2d end,
        float minimumRadius, Capsule2D best)
    {
        var storedStart = Vector2d.Clamp(start, new(-float.MaxValue), new(float.MaxValue)).ToVector2f();
        var storedEnd = Vector2d.Clamp(end, new(-float.MaxValue), new(float.MaxValue)).ToVector2f();
        var wideStart = storedStart.ToVector2d();
        var wideEnd = storedEnd.ToVector2d();
        var radiusSquared = (double)minimumRadius * minimumRadius;
        foreach (var point in points)
        {
            radiusSquared = Math.Max(radiusSquared,
                LinearGeometry2D.DistanceSquaredToSegment(point.ToVector2d(), wideStart, wideEnd));
        }

        var radius = Upper(Math.Sqrt(radiusSquared));
        if ((double)radius * radius < radiusSquared)
        {
            radius = MathF.BitIncrement(radius);
        }

        var area = CapsuleArea(Vector2d.Distance(wideStart, wideEnd), radius);
        var bestArea = CapsuleArea(Vector2d.Distance(best.Start.ToVector2d(), best.End.ToVector2d()), best.Radius);
        return area < bestArea ? Capsule2D.Builder.Create(storedStart, storedEnd, radius) : best;
    }

    private static double CapsuleArea(double length, double radius) => radius * (2d * length + Math.PI * radius);

    private readonly record struct CapsuleFit(Vector2d Start, Vector2d End, double Area);
    private readonly record struct RadiusInterval(double Low, double High, double LengthLowerBoundAtHigh);
    private readonly record struct CapsuleSpine(double Start, double End, double Height)
    {
        public double Length => End - Start;
    }
}
