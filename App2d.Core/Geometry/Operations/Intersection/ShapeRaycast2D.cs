using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Intersection;

/// <summary>Local-space ray queries over shape parameters, independent of spatial objects and collision systems.</summary>
public static class ShapeRaycast2D
{
    /// <summary>Finds the first hit; composites choose the nearest part and unsupported shapes return false.</summary>
    /// <param name="origin">The local ray origin.</param>
    /// <param name="direction">A finite nonzero direction, which may be unnormalized.</param>
    /// <param name="shape">The shape in the same coordinate space.</param>
    /// <param name="maxDistance">The largest ray parameter accepted; parameters are distances only for unit directions.</param>
    /// <param name="hit">The local hit, normal and ray parameter.</param>
    public static bool TryIntersect(Vector2 origin, Vector2 direction, IShape2D shape, float maxDistance, out RayHit2D hit)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.NonNegative(maxDistance, allowInfinity: true);
        switch (shape)
        {
            case Circle2D circle:
                return Raycast2D.TryCircle(origin, direction, circle.Center, circle.Radius, maxDistance, out hit);
            case Ellipse2D ellipse:
                return Raycast2D.TryEllipse(origin, direction, ellipse.Center, ellipse.Radii, maxDistance, out hit);
            case Capsule2D capsule:
                return Raycast2D.TryCapsule(origin, direction, capsule.Start, capsule.End, capsule.Radius, maxDistance, out hit);
            case HalfSpace2D halfSpace:
                return Raycast2D.TryHalfSpace(origin, direction, halfSpace.Normal, halfSpace.Offset, maxDistance, out hit);
            case SimplePolygon2D polygon:
                return Raycast2D.TrySimplePolygon(origin, direction, polygon.Vertices, maxDistance, out hit);
            case CompositeShape2D composite:
                hit = default;
                var found = false;
                foreach (var part in composite.Parts)
                {
                    if (TryIntersect(origin, direction, part, maxDistance, out var partHit) && (!found || partHit.Distance < hit.Distance))
                    {
                        hit = partHit;
                        found = true;
                    }
                }
                return found;
            default:
                var count = shape.GetOutlineVertCount();
                if (count == 0)
                {
                    hit = default;
                    return false;
                }
                Span<Vector2> vertices = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
                shape.GetOutlineVerts(vertices);
                return Raycast2D.TryConvexPolygon(origin, direction, vertices, maxDistance, out hit);
        }
    }

}
