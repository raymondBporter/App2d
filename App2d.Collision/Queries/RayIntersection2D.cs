using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Queries;

public static class RayIntersection2D
{
    private const float Epsilon = 0.000001f;

    public static bool TryIntersect(
        Ray2D ray,
        SpatialObject2D worldObject,
        float maxDistance,
        out RayHit2D hit)
    {
        ArgGuard.ThrowIfNull(worldObject);
        ValidateMaxDistance(maxDistance);

        var worldBounds = worldObject.WorldBounds;
        if (worldBounds.IsFinite && !IntersectsBounds(ray, worldBounds, maxDistance))
        {
            hit = default;
            return false;
        }

        var pose = worldObject.CollisionPose;
        var localOrigin = pose.InverseTransformPoint(ray.Origin);
        var localDirection = pose.InverseTransformDirection(ray.Direction);
        if (!TryIntersectLocal(
                localOrigin,
                localDirection,
                worldObject.Shape,
                maxDistance,
                out var localHit))
        {
            hit = default;
            return false;
        }

        // Normals transform by (A⁻¹)ᵀ = A / Scale² for this family; normalize after.
        var worldNormal = pose.TransformDirection(localHit.Normal);
        if (worldNormal.LengthSquared() <= Epsilon)
        {
            hit = default;
            return false;
        }

        hit = new RayHit2D(
            ray.GetPoint(localHit.Distance),
            Vector2.Normalize(worldNormal),
            localHit.Distance);
        return true;
    }

    public static bool IntersectsBounds(
        Ray2D ray,
        Bounds2D bounds,
        float maxDistance)
    {
        ValidateMaxDistance(maxDistance);
        if (!bounds.IsFinite)
            return true;

        var minimum = 0f;
        var maximum = maxDistance;
        return ClipAxis(ray.Origin.X, ray.Direction.X, bounds.Min.X, bounds.Max.X, ref minimum, ref maximum) &&
               ClipAxis(ray.Origin.Y, ray.Direction.Y, bounds.Min.Y, bounds.Max.Y, ref minimum, ref maximum);
    }

    private static bool TryIntersectLocal(
        Vector2 origin,
        Vector2 direction,
        IShape2D shape,
        float maxDistance,
        out LocalRayHit hit)
    {
        switch (shape)
        {
            case Circle2D circle:
                return TryCircle(
                    origin,
                    direction,
                    circle.Center,
                    circle.Radius,
                    maxDistance,
                    out hit);

            case Rectangle2D rectangle:
                Span<Vector2> rectangleVertices = stackalloc Vector2[4];
                rectangle.WriteCorners(rectangleVertices);
                return TryConvexPolygon(origin, direction, rectangleVertices, maxDistance, out hit);

            case ConvexPolygon2D polygon:
                return TryConvexPolygon(origin, direction, polygon.Vertices, maxDistance, out hit);

            case Capsule2D capsule:
                return TryCapsule(origin, direction, capsule, maxDistance, out hit);

            case HalfSpace2D halfSpace:
                return TryHalfSpace(origin, direction, halfSpace, maxDistance, out hit);

            case CompositeShape2D composite:
            {
                hit = default;
                var found = false;
                foreach (var part in composite.Parts)
                {
                    if (TryIntersectLocal(origin, direction, part, maxDistance, out var partHit) && (!found || partHit.Distance < hit.Distance))
                    {
                        hit = partHit;
                        found = true;
                    }
                }

                return found;
            }

            default:
                hit = default;
                return false;
        }
    }

    private static bool TryCircle(
        Vector2 origin,
        Vector2 direction,
        Vector2 center,
        float radius,
        float maxDistance,
        out LocalRayHit hit)
    {
        var offset = origin - center;
        var a = Vector2.Dot(direction, direction);
        var b = 2f * Vector2.Dot(offset, direction);
        var c = Vector2.Dot(offset, offset) - radius * radius;
        var discriminant = b * b - 4f * a * c;
        if (a <= Epsilon || discriminant < 0f)
        {
            hit = default;
            return false;
        }

        var squareRoot = MathF.Sqrt(Math.Max(discriminant, 0f));
        var inverseDenominator = 0.5f / a;
        var first = (-b - squareRoot) * inverseDenominator;
        var second = (-b + squareRoot) * inverseDenominator;
        var distance = first >= 0f ? first : second;
        if (distance < 0f || distance > maxDistance)
        {
            hit = default;
            return false;
        }

        var point = origin + direction * distance;
        var normal = point - center;
        if (normal.LengthSquared() <= Epsilon)
        {
            hit = default;
            return false;
        }

        hit = new LocalRayHit(Vector2.Normalize(normal), distance);
        return true;
    }

    private static bool TryConvexPolygon(
        Vector2 origin,
        Vector2 direction,
        ReadOnlySpan<Vector2> vertices,
        float maxDistance,
        out LocalRayHit hit)
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
            var outward = signedAreaTwice >= 0f
                ? edge.PerpCw()
                : edge.PerpCcw();
            var originSide = Vector2.Dot(origin - start, outward);
            var directionProjection = Vector2.Dot(direction, outward);

            if (MathF.Abs(directionProjection) <= Epsilon)
            {
                if (originSide > 0f)
                {
                    hit = default;
                    return false;
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
                hit = default;
                return false;
            }
        }

        float hitDistance;
        Vector2 hitNormal;
        if (enteringDistance >= 0f)
        {
            hitDistance = enteringDistance;
            hitNormal = enteringNormal;
        }
        else
        {
            hitDistance = exitingDistance;
            hitNormal = exitingNormal;
        }

        if (!float.IsFinite(hitDistance) ||
            hitDistance < 0f ||
            hitDistance > maxDistance ||
            hitNormal.LengthSquared() <= Epsilon)
        {
            hit = default;
            return false;
        }

        hit = new LocalRayHit(Vector2.Normalize(hitNormal), hitDistance);
        return true;
    }

    private static bool TryCapsule(
        Vector2 origin,
        Vector2 direction,
        Capsule2D capsule,
        float maxDistance,
        out LocalRayHit hit)
    {
        var segment = capsule.End - capsule.Start;
        var segmentLength = segment.Length();
        if (segmentLength <= Epsilon)
        {
            return TryCircle(
                origin,
                direction,
                capsule.Start,
                capsule.Radius,
                maxDistance,
                out hit);
        }

        var tangent = segment / segmentLength;
        var perpendicular = tangent.PerpCcw();
        var relativeOrigin = origin - capsule.Start;
        var originAlong = Vector2.Dot(relativeOrigin, tangent);
        var originAcross = Vector2.Dot(relativeOrigin, perpendicular);
        var directionAlong = Vector2.Dot(direction, tangent);
        var directionAcross = Vector2.Dot(direction, perpendicular);
        var bestDistance = float.PositiveInfinity;
        var bestNormal = Vector2.Zero;

        void Consider(float distance, Vector2 normal)
        {
            if (distance < 0f || distance > maxDistance || distance >= bestDistance)
                return;

            bestDistance = distance;
            bestNormal = normal;
        }

        if (MathF.Abs(directionAcross) > Epsilon)
        {
            var firstSideDistance = (capsule.Radius - originAcross) / directionAcross;
            var firstSideAlong = originAlong + directionAlong * firstSideDistance;
            if (firstSideAlong >= 0f && firstSideAlong <= segmentLength)
                Consider(firstSideDistance, perpendicular);

            var secondSideDistance = (-capsule.Radius - originAcross) / directionAcross;
            var secondSideAlong = originAlong + directionAlong * secondSideDistance;
            if (secondSideAlong >= 0f && secondSideAlong <= segmentLength)
                Consider(secondSideDistance, -perpendicular);
        }

        ConsiderCap(0f, acceptStartCap: true);
        ConsiderCap(segmentLength, acceptStartCap: false);

        if (!float.IsFinite(bestDistance))
        {
            hit = default;
            return false;
        }

        hit = new LocalRayHit(Vector2.Normalize(bestNormal), bestDistance);
        return true;

        void ConsiderCap(float centerAlong, bool acceptStartCap)
        {
            var capOriginAlong = originAlong - centerAlong;
            var a = directionAlong * directionAlong + directionAcross * directionAcross;
            var b = 2f * (capOriginAlong * directionAlong + originAcross * directionAcross);
            var c = capOriginAlong * capOriginAlong + originAcross * originAcross - capsule.Radius * capsule.Radius;
            var discriminant = b * b - 4f * a * c;
            if (a <= Epsilon || discriminant < 0f)
                return;

            var squareRoot = MathF.Sqrt(Math.Max(discriminant, 0f));
            var inverseDenominator = 0.5f / a;
            ConsiderCapRoot((-b - squareRoot) * inverseDenominator);
            ConsiderCapRoot((-b + squareRoot) * inverseDenominator);

            void ConsiderCapRoot(float distance)
            {
                if (distance < 0f || distance > maxDistance)
                    return;

                var along = originAlong + directionAlong * distance;
                if (acceptStartCap ? along > Epsilon : along < segmentLength - Epsilon)
                    return;

                var across = originAcross + directionAcross * distance;
                var capNormal = tangent * (along - centerAlong) + perpendicular * across;
                if (capNormal.LengthSquared() > Epsilon)
                    Consider(distance, Vector2.Normalize(capNormal));
            }
        }
    }

    private static bool TryHalfSpace(
        Vector2 origin,
        Vector2 direction,
        HalfSpace2D halfSpace,
        float maxDistance,
        out LocalRayHit hit)
    {
        var denominator = Vector2.Dot(direction, halfSpace.Normal);
        if (MathF.Abs(denominator) <= Epsilon)
        {
            hit = default;
            return false;
        }

        var distance = (halfSpace.Offset - Vector2.Dot(origin, halfSpace.Normal)) / denominator;
        if (distance < 0f || distance > maxDistance)
        {
            hit = default;
            return false;
        }

        hit = new LocalRayHit(halfSpace.Normal, distance);
        return true;
    }

    private static bool ClipAxis(
        float origin,
        float direction,
        float minimumBound,
        float maximumBound,
        ref float minimumDistance,
        ref float maximumDistance)
    {
        if (MathF.Abs(direction) <= Epsilon)
            return origin >= minimumBound && origin <= maximumBound;

        var inverseDirection = 1f / direction;
        var first = (minimumBound - origin) * inverseDirection;
        var second = (maximumBound - origin) * inverseDirection;
        if (first > second)
            (first, second) = (second, first);

        minimumDistance = Math.Max(minimumDistance, first);
        maximumDistance = Math.Min(maximumDistance, second);
        return minimumDistance <= maximumDistance;
    }

    public static void ValidateMaxDistance(float maxDistance)
        => ArgGuard.ThrowIfNegativeOrNaN(maxDistance);

    private readonly record struct LocalRayHit(Vector2 Normal, float Distance);
}
