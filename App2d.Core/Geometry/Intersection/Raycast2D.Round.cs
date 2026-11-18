using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Shapes;

namespace App2d.Core.Geometry.Operations.Intersection;

public static partial class Raycast2D
{
    /// <summary>
    /// First hit on an arc, considering both intersections with its supporting circle.
    /// The normal is counterclockwise from the curve tangent. Zero radius/sweep has no surface and misses.
    /// </summary>
    public static bool TryArc(Vector2f origin, Vector2f direction, Arc2D arc, float maxDistance, out RayHit2D hit) =>
        TryArc(origin, direction, arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepAngleRadians, maxDistance, out hit);

    internal static bool TryArc(Vector2f origin, Vector2f direction, Vector2f center, float radius,
        double startAngle, double sweep, float maxDistance, out RayHit2D hit)
    {
        hit = default;
        if (radius <= 0 || sweep == 0) return false;
        var offset = origin.ToVector2d() - center.ToVector2d();
        var axis = direction.ToVector2d();
        var a = axis.LengthSquared();
        if (a == 0) return false;
        var b = 2 * offset.Dot(axis);
        var c = offset.LengthSquared() - (double)radius * radius;
        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0) return false;
        var q = -.5 * (b + Math.CopySign(Math.Sqrt(discriminant), b));
        var first = q == 0 ? -b / (2 * a) : q / a;
        var second = q == 0 ? first : c / q;
        if (first > second) (first, second) = (second, first);
        if (Consider(first, out hit)) return true;
        return second != first && Consider(second, out hit);

        bool Consider(double distance, out RayHit2D candidate)
        {
            candidate = default;
            if (!double.IsFinite(distance) || distance < 0 || distance > float.MaxValue || distance > maxDistance) return false;
            var point = origin.ToVector2d() + axis * distance;
            var radial = point - center.ToVector2d();
            var angle = radial.AngleRadians;
            var progress = sweep > 0 ? angle - startAngle : startAngle - angle;
            progress = (progress % Math.Tau + Math.Tau) % Math.Tau;
            // Arc channels are floats. Permit angular roundoff at either endpoint, including wraparound.
            const double angularTolerance = 2e-7;
            if (Math.Abs(sweep) < Math.Tau && progress > Math.Abs(sweep) + angularTolerance && progress < Math.Tau - angularTolerance)
                return false;
            var length = radial.Length();
            if (length == 0) return false;
            var normal = radial * (-Math.Sign(sweep) / length);
            candidate = new(point.ToVector2f(), normal.ToVector2f(), (float)distance);
            return true;
        }
    }

    /// <summary>Intersects the exact straight sides and circular corners, without tessellating a rounded rectangle.</summary>
    public static bool TryRoundedRectangle(Vector2f origin, Vector2f direction, RoundedRectangle2D rectangle,
        float maxDistance, out RayHit2D hit)
    {
        var frame = rectangle.Frame;
        var localOrigin = frame.ToFrame(origin);
        var localDirection = frame.ToFrameDirection(direction);
        if (rectangle.Radius == 0)
        {
            if (!TryRectangle(localOrigin, localDirection, -rectangle.HalfSize, rectangle.HalfSize, maxDistance, out var squareHit))
                return Miss(out hit);
            hit = new(frame.FromFrame(squareHit.Point), frame.FromFrameDirection(squareHit.Normal), squareHit.Distance);
            return true;
        }

        var inner = rectangle.InnerHalfSize;
        var outer = rectangle.HalfSize;
        var found = false;
        var best = default(RayHit2D);
        Side(new(-inner.X, -outer.Y), new(inner.X, -outer.Y), -Vector2f.UnitY);
        Side(new(outer.X, -inner.Y), new(outer.X, inner.Y), Vector2f.UnitX);
        Side(new(inner.X, outer.Y), new(-inner.X, outer.Y), Vector2f.UnitY);
        Side(new(-outer.X, inner.Y), new(-outer.X, -inner.Y), -Vector2f.UnitX);
        Corner(inner, 0);
        Corner(new(-inner.X, inner.Y), Math.PI / 2);
        Corner(new(-inner.X, -inner.Y), Math.PI);
        Corner(new(inner.X, -inner.Y), 3 * Math.PI / 2);
        hit = found ? new(frame.FromFrame(best.Point), frame.FromFrameDirection(best.Normal), best.Distance) : default;
        return found;

        void Side(Vector2f start, Vector2f end, Vector2f normal)
        {
            if (TrySegment(localOrigin, localDirection, start.ToVector2d(), end.ToVector2d(), maxDistance, out var candidate))
                Keep(candidate with { Normal = normal });
        }

        void Corner(Vector2f center, double start)
        {
            if (TryArc(localOrigin, localDirection, center, rectangle.Radius, start, Math.PI / 2, maxDistance, out var candidate))
                Keep(candidate with { Normal = -candidate.Normal });
        }

        void Keep(RayHit2D candidate)
        {
            if (!found || candidate.Distance < best.Distance)
            {
                found = true;
                best = candidate;
                maxDistance = candidate.Distance;
            }
        }
    }
}
