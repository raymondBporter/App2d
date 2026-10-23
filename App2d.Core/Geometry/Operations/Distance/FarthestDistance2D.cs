using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Distance;

/// <summary>Farthest-point distances used by diameters of unions, without sampling drawable outlines.</summary>
public static class FarthestDistance2D
{
    /// <summary>The farthest distance between two filled, axis-aligned ellipses. Zero radii also allow points and circles.</summary>
    public static float BetweenEllipses(Vector2 firstCenter, Vector2 firstRadii, Vector2 secondCenter, Vector2 secondRadii)
    {
        var delta = firstCenter.ToVector2d() - secondCenter.ToVector2d();
        var ax = (double)firstRadii.X * firstRadii.X;
        var ay = (double)firstRadii.Y * firstRadii.Y;
        var bx = (double)secondRadii.X * secondRadii.X;
        var by = (double)secondRadii.Y * secondRadii.Y;
        double At(double t) => Math.Abs(delta.X) * Math.Sqrt(t) + Math.Abs(delta.Y) * Math.Sqrt(1 - t)
            + Math.Sqrt(ax * t + ay * (1 - t)) + Math.Sqrt(bx * t + by * (1 - t));

        // Maximize the support of their Minkowski difference. With t = cos²(angle), each term is concave,
        // so this brackets the global maximum, including elongated ellipses with different major axes.
        var low = 0d;
        var high = 1d;
        for (var i = 0; i < 80; i++)
        {
            var left = low + (high - low) / 3;
            var right = high - (high - low) / 3;
            if (At(left) < At(right))
            {
                low = left;
            }
            else
            {
                high = right;
            }
        }
        return (float)Math.Max(Math.Max(At(0), At(1)), At((low + high) / 2));
    }

    /// <summary>
    /// The farthest distance for arbitrary finite convex support mappings. Angular intervals are refined until
    /// the distance upper bound is within two parts per million of the best point pair, apart from support-map rounding.
    /// Built-in polygonal and elliptical shapes use their more direct formulas instead.
    /// </summary>
    public static float BetweenConvex(IConvexSupport2D first, IConvexSupport2D second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        var best = 0d;
        Support Sample(double angle)
        {
            var direction = Vector2d.FromAngle(angle);
            var a = first.GetSupportPoint(direction.ToVector2());
            var b = second.GetSupportPoint((-direction).ToVector2());
            var delta = a.ToVector2d() - b.ToVector2d();
            best = Math.Max(best, delta.Length());
            return new(angle, delta.Dot(direction));
        }

        var intervals = new PriorityQueue<(Support Start, Support End), double>();
        void Enqueue(Support start, Support end)
        {
            // A farthest vector pointing into this interval is within half its angle of an endpoint normal.
            // Its projection there cannot exceed that endpoint's support value.
            var upper = Math.Max(0, Math.Max(start.Projection, end.Projection)) / Math.Cos((end.Angle - start.Angle) / 2);
            if (upper > best * (1 + 2e-6))
            {
                intervals.Enqueue((start, end), -upper);
            }
        }

        var previous = Sample(0);
        for (var i = 1; i <= 4; i++)
        {
            var next = Sample(i * Math.PI / 2);
            Enqueue(previous, next);
            previous = next;
        }
        while (intervals.TryDequeue(out var interval, out var priority))
        {
            if (-priority <= best * (1 + 2e-6))
            {
                break;
            }

            var middle = Sample((interval.Start.Angle + interval.End.Angle) / 2);
            Enqueue(interval.Start, middle);
            Enqueue(middle, interval.End);
        }
        return (float)best;
    }

    private readonly record struct Support(double Angle, double Projection);
}
