using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Intersection;

/// <summary>
/// First-hit ray casts against raw primitives in one coordinate space. Directions need not be unit length:
/// the reported distance is the ray parameter, which equals a distance in input units for a unit direction.
/// A ray starting inside a solid reports the exit with its outward normal. Inputs are finite.
/// </summary>
public static partial class Raycast2D
{
    private const float Epsilon = 0.000001f;

    /// <summary>Casts against a finite, two-sided segment. Collinear rays miss; the normal is counterclockwise from Start to End.</summary>
    /// <param name="origin">The finite ray origin.</param>
    /// <param name="direction">A finite, nonzero direction; it need not be normalized.</param>
    /// <param name="segment">The finite segment endpoints.</param>
    /// <param name="maxDistance">The largest ray parameter accepted.</param>
    /// <param name="hit">The crossing, normal and ray parameter.</param>
    public static bool TrySegment(Vector2 origin, Vector2 direction, Segment<Vector2> segment, float maxDistance, out RayHit2D hit)
    {
        // Double intermediates avoid cancellation when a ray has just left a nearby optical surface.
        var edge = segment.End.ToVector2d() - segment.Start.ToVector2d();
        var axis = direction.ToVector2d();
        var denominator = axis.Cross(edge);
        if (Math.Abs(denominator) < 1e-12 || edge.LengthSquared() == 0)
        {
            return Miss(out hit);
        }
        var offset = segment.Start.ToVector2d() - origin.ToVector2d();
        var distance = offset.Cross(edge) / denominator;
        var fraction = offset.Cross(axis) / denominator;
        if (distance < 0 || distance > maxDistance || fraction < 0 || fraction > 1)
        {
            return Miss(out hit);
        }
        var normal = Vector2.Normalize(new Vector2((float)-edge.Y, (float)edge.X));
        hit = new(origin + direction * (float)distance, normal, (float)distance);
        return true;
    }

    /// <summary>Casts a ray against a circle.</summary>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">The ray direction; nonzero, any length.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The circle radius.</param>
    /// <param name="maxDistance">The largest ray parameter accepted.</param>
    /// <param name="hit">The nearest hit at or before <paramref name="maxDistance"/>.</param>
    /// <returns>True when the ray reaches the circle.</returns>
    public static bool TryCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius, float maxDistance, out RayHit2D hit)
    {
        var offset = origin - center;
        var a = Vector2.Dot(direction, direction);
        var b = 2f * Vector2.Dot(offset, direction);
        var c = Vector2.Dot(offset, offset) - radius * radius;
        var discriminant = b * b - 4f * a * c;
        if (a <= Epsilon || discriminant < 0f)
        {
            return Miss(out hit);
        }

        var squareRoot = MathF.Sqrt(Math.Max(discriminant, 0f));
        var inverseDenominator = 0.5f / a;
        var first = (-b - squareRoot) * inverseDenominator;
        var second = (-b + squareRoot) * inverseDenominator;
        var distance = first >= 0f ? first : second;
        if (distance < 0f || distance > maxDistance)
        {
            return Miss(out hit);
        }

        var point = origin + direction * distance;
        var normal = point - center;
        if (normal.LengthSquared() <= Epsilon)
        {
            return Miss(out hit);
        }

        hit = new(point, Vector2.Normalize(normal), distance);
        return true;
    }

    /// <summary>Casts a ray against an axis-aligned ellipse, solving in normalized double precision.</summary>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">The ray direction; nonzero, any length.</param>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The positive half-extents along X and Y.</param>
    /// <param name="maxDistance">The largest ray parameter accepted.</param>
    /// <param name="hit">The nearest hit, with the exact gradient normal.</param>
    /// <returns>True when the ray reaches the ellipse.</returns>
    public static bool TryEllipse(Vector2 origin, Vector2 direction, Vector2 center, Vector2 radii, float maxDistance, out RayHit2D hit)
    {
        var relative = origin.ToVector2d() - center.ToVector2d();
        var offset = new Vector2d(relative.X / radii.X, relative.Y / radii.Y);
        var axis = new Vector2d((double)direction.X / radii.X, (double)direction.Y / radii.Y);
        var a = axis.LengthSquared();
        var b = 2d * offset.Dot(axis);
        var c = offset.LengthSquared() - 1d;
        var discriminant = b * b - 4d * a * c;
        if (a == 0d || discriminant < 0d)
        {
            return Miss(out hit);
        }

        var root = Math.Sqrt(discriminant);
        var first = (-b - root) / (2d * a);
        var second = (-b + root) / (2d * a);
        var distance = first >= 0d ? first : second;
        if (distance < 0d || distance > maxDistance)
        {
            return Miss(out hit);
        }

        var boundary = offset + axis * distance;
        var normal = new Vector2d(boundary.X / radii.X, boundary.Y / radii.Y);
        var normalLength = normal.Length();
        if (normalLength == 0d)
        {
            return Miss(out hit);
        }

        var point = (origin.ToVector2d() + direction.ToVector2d() * distance).ToVector2();
        hit = new(point, (normal / normalLength).ToVector2(), (float)distance);
        return true;
    }

    /// <summary>Casts a ray against a convex polygon given in perimeter order, in either winding.</summary>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">The ray direction; nonzero, any length.</param>
    /// <param name="vertices">The convex perimeter with nonzero area.</param>
    /// <param name="maxDistance">The largest ray parameter accepted.</param>
    /// <param name="hit">The nearest hit, with the outward edge normal.</param>
    /// <returns>True when the ray reaches the polygon.</returns>
    public static bool TryConvexPolygon(Vector2 origin, Vector2 direction, ReadOnlySpan<Vector2> vertices, float maxDistance, out RayHit2D hit)
    {
        var signedAreaTwice = PolygonGeometry2D.SignedAreaTwice(vertices);
        var enteringDistance = float.NegativeInfinity;
        var exitingDistance = float.PositiveInfinity;
        var enteringNormal = Vector2.Zero;
        var exitingNormal = Vector2.Zero;
        for (var i = 0; i < vertices.Length; i++)
        {
            var start = vertices[i];
            var edge = vertices[(i + 1) % vertices.Length] - start;
            var outward = signedAreaTwice >= 0f ? edge.PerpCw : edge.PerpCcw;
            var originSide = Vector2.Dot(origin - start, outward);
            var directionProjection = Vector2.Dot(direction, outward);
            if (MathF.Abs(directionProjection) <= Epsilon)
            {
                if (originSide > 0f)
                {
                    return Miss(out hit);
                }

                continue;
            }
            var distance = -originSide / directionProjection;
            if (directionProjection < 0f)
            {
                if (distance > enteringDistance)
                {
                    enteringDistance = distance;
                    enteringNormal = outward;
                }
            }
            else if (distance < exitingDistance)
            {
                exitingDistance = distance;
                exitingNormal = outward;
            }
            if (enteringDistance > exitingDistance)
            {
                return Miss(out hit);
            }
        }
        var (hitDistance, hitNormal) = enteringDistance >= 0f ? (enteringDistance, enteringNormal) : (exitingDistance, exitingNormal);
        if (!float.IsFinite(hitDistance) || hitDistance < 0f || hitDistance > maxDistance || hitNormal.LengthSquared() <= Epsilon)
        {
            return Miss(out hit);
        }

        hit = new(origin + direction * hitDistance, Vector2.Normalize(hitNormal), hitDistance);
        return true;
    }

    /// <summary>Casts a ray against an ordered axis-aligned rectangle, reporting the entry face normal.</summary>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">The ray direction; nonzero, any length.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner; both corners must be finite.</param>
    /// <param name="maxDistance">The largest ray parameter accepted.</param>
    /// <param name="hit">The nearest hit.</param>
    /// <returns>True when the ray reaches the rectangle.</returns>
    public static bool TryRectangle(Vector2 origin, Vector2 direction, Vector2 min, Vector2 max, float maxDistance, out RayHit2D hit)
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        VertexGenerator2D.WriteRectangle(corners, min, max);
        return TryConvexPolygon(origin, direction, corners, maxDistance, out hit);
    }

    /// <summary>Tests whether a ray passes through an ordered rectangle within a distance, without computing the hit. Infinite rectangles always pass.</summary>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">The ray direction; nonzero, any length.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <param name="maxDistance">The largest ray parameter considered.</param>
    /// <returns>True when some parameter in [0, maxDistance] lies inside the rectangle.</returns>
    public static bool IntersectsRectangle(Vector2 origin, Vector2 direction, Vector2 min, Vector2 max, float maxDistance)
    {
        if (!min.IsFinite() || !max.IsFinite())
        {
            return true;
        }

        var minimum = 0f;
        var maximum = maxDistance;
        return ClipAxis(origin.X, direction.X, min.X, max.X, ref minimum, ref maximum) && ClipAxis(origin.Y, direction.Y, min.Y, max.Y, ref minimum, ref maximum);
    }

    /// <summary>Casts a ray against a capsule: two side walls and two end caps.</summary>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">The ray direction; nonzero, any length.</param>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <param name="maxDistance">The largest ray parameter accepted.</param>
    /// <param name="hit">The nearest hit.</param>
    /// <returns>True when the ray reaches the capsule.</returns>
    public static bool TryCapsule(Vector2 origin, Vector2 direction, Vector2 start, Vector2 end, float radius, float maxDistance, out RayHit2D hit)
    {
        var segment = end - start;
        var segmentLength = segment.Length();
        if (segmentLength <= Epsilon)
        {
            return TryCircle(origin, direction, start, radius, maxDistance, out hit);
        }

        var tangent = segment / segmentLength;
        var perpendicular = tangent.PerpCcw;
        var relativeOrigin = origin - start;
        var originAlong = Vector2.Dot(relativeOrigin, tangent);
        var originAcross = Vector2.Dot(relativeOrigin, perpendicular);
        var directionAlong = Vector2.Dot(direction, tangent);
        var directionAcross = Vector2.Dot(direction, perpendicular);
        var bestDistance = float.PositiveInfinity;
        var bestNormal = Vector2.Zero;

        void Consider(float distance, Vector2 normal)
        {
            if (distance < 0f || distance > maxDistance || distance >= bestDistance)
            {
                return;
            }

            bestDistance = distance;
            bestNormal = normal;
        }

        if (MathF.Abs(directionAcross) > Epsilon)
        {
            var firstSideDistance = (radius - originAcross) / directionAcross;
            var firstSideAlong = originAlong + directionAlong * firstSideDistance;
            if (firstSideAlong >= 0f && firstSideAlong <= segmentLength)
            {
                Consider(firstSideDistance, perpendicular);
            }

            var secondSideDistance = (-radius - originAcross) / directionAcross;
            var secondSideAlong = originAlong + directionAlong * secondSideDistance;
            if (secondSideAlong >= 0f && secondSideAlong <= segmentLength)
            {
                Consider(secondSideDistance, -perpendicular);
            }
        }

        ConsiderCap(0f, acceptStartCap: true);
        ConsiderCap(segmentLength, acceptStartCap: false);
        if (!float.IsFinite(bestDistance))
        {
            return Miss(out hit);
        }

        hit = new(origin + direction * bestDistance, Vector2.Normalize(bestNormal), bestDistance);
        return true;

        void ConsiderCap(float centerAlong, bool acceptStartCap)
        {
            var capOriginAlong = originAlong - centerAlong;
            var a = directionAlong * directionAlong + directionAcross * directionAcross;
            var b = 2f * (capOriginAlong * directionAlong + originAcross * directionAcross);
            var c = capOriginAlong * capOriginAlong + originAcross * originAcross - radius * radius;
            var discriminant = b * b - 4f * a * c;
            if (a <= Epsilon || discriminant < 0f)
            {
                return;
            }

            var squareRoot = MathF.Sqrt(Math.Max(discriminant, 0f));
            var inverseDenominator = 0.5f / a;
            ConsiderCapRoot((-b - squareRoot) * inverseDenominator);
            ConsiderCapRoot((-b + squareRoot) * inverseDenominator);

            void ConsiderCapRoot(float distance)
            {
                if (distance < 0f || distance > maxDistance)
                {
                    return;
                }

                var along = originAlong + directionAlong * distance;
                if (acceptStartCap ? along > Epsilon : along < segmentLength - Epsilon)
                {
                    return;
                }

                var across = originAcross + directionAcross * distance;
                var capNormal = tangent * (along - centerAlong) + perpendicular * across;
                if (capNormal.LengthSquared() > Epsilon)
                {
                    Consider(distance, Vector2.Normalize(capNormal));
                }
            }
        }
    }

    /// <summary>Casts a ray against the boundary of a half-space, from either side.</summary>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">The ray direction; nonzero, any length.</param>
    /// <param name="unitNormal">The unit normal pointing toward free space.</param>
    /// <param name="offset">The signed boundary offset along the normal.</param>
    /// <param name="maxDistance">The largest ray parameter accepted.</param>
    /// <param name="hit">The boundary hit with the half-space normal.</param>
    /// <returns>True when the ray reaches the boundary; parallel rays miss.</returns>
    public static bool TryHalfSpace(Vector2 origin, Vector2 direction, Vector2 unitNormal, float offset, float maxDistance, out RayHit2D hit)
    {
        var denominator = Vector2.Dot(direction, unitNormal);
        if (MathF.Abs(denominator) <= Epsilon)
        {
            return Miss(out hit);
        }

        var distance = (offset - Vector2.Dot(origin, unitNormal)) / denominator;
        if (distance < 0f || distance > maxDistance)
        {
            return Miss(out hit);
        }

        hit = new(origin + direction * distance, unitNormal, distance);
        return true;
    }

    private static bool ClipAxis(float origin, float direction, float minimumBound, float maximumBound, ref float minimumDistance, ref float maximumDistance)
    {
        if (MathF.Abs(direction) <= Epsilon)
        {
            return origin >= minimumBound && origin <= maximumBound;
        }

        var inverseDirection = 1f / direction;
        var first = (minimumBound - origin) * inverseDirection;
        var second = (maximumBound - origin) * inverseDirection;
        if (first > second)
        {
            (first, second) = (second, first);
        }

        minimumDistance = Math.Max(minimumDistance, first);
        maximumDistance = Math.Min(maximumDistance, second);
        return minimumDistance <= maximumDistance;
    }

    private static bool Miss(out RayHit2D hit)
    {
        hit = default;
        return false;
    }
    /// <summary>Finds the first boundary crossing of a simple, possibly concave polygon in either winding.</summary>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">A finite nonzero direction; it need not be normalized.</param>
    /// <param name="vertices">The simple perimeter without a repeated closing vertex.</param>
    /// <param name="maxDistance">The largest ray parameter accepted.</param>
    /// <param name="hit">The crossing, outward normal and ray parameter.</param>
    public static bool TrySimplePolygon(Vector2 origin, Vector2 direction, ReadOnlySpan<Vector2> vertices, float maxDistance, out RayHit2D hit)
    {
        var winding = Math.Sign(PolygonGeometry2D.SignedAreaTwiceDouble(vertices));
        hit = default;
        var found = false;
        for (var i = 0; i < vertices.Length; i++)
        {
            var start = vertices[i];
            var edge = vertices[(i + 1) % vertices.Length] - start;
            var denominator = direction.Cross(edge);
            if (MathF.Abs(denominator) < Epsilon)
            {
                continue;
            }

            var offset = start - origin;
            var distance = offset.Cross(edge) / denominator;
            var fraction = offset.Cross(direction) / denominator;
            if (distance < 0 || distance > maxDistance || fraction < 0 || fraction > 1 || found && distance >= hit.Distance)
            {
                continue;
            }

            var normal = Vector2.Normalize(new Vector2(edge.Y, -edge.X) * winding);
            hit = new RayHit2D(origin + direction * distance, normal, distance);
            found = true;
        }
        return found;
    }
}
