using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>Adapts finite convex shapes to the shared distance and contact query.</summary>
public static class ShapeConvexQuery2D
{
    /// <summary>Queries two shapes using exact cores and radii, or analytic support mappings such as an ellipse's.</summary>
    public static ConvexQueryResult2D Query(IConvexShape2D first, Similarity2D firstPose, IConvexShape2D second, Similarity2D secondPose)
        => Query(first, firstPose, second, secondPose, penetration: true);

    /// <summary>The gap or negative escape depth, without constructing boundary witnesses where possible.</summary>
    public static float SignedDistance(IConvexShape2D first, Similarity2D firstPose, IConvexShape2D second, Similarity2D secondPose)
        => Query(first, firstPose, second, secondPose, penetration: true, witnesses: false).SignedDistance;

    /// <summary>The nonnegative gap, skipping penetration calculations when the cores overlap.</summary>
    public static float Distance(IConvexShape2D first, Similarity2D firstPose, IConvexShape2D second, Similarity2D secondPose)
        => Math.Max(0f, Query(first, firstPose, second, secondPose, penetration: false).SignedDistance);

    /// <summary>True for touching or overlapping shapes, without calculating penetration depth.</summary>
    public static bool Intersects(IConvexShape2D first, Similarity2D firstPose, IConvexShape2D second, Similarity2D secondPose)
        => Query(first, firstPose, second, secondPose, penetration: false).SignedDistance <= 0f;

    private static ConvexQueryResult2D Query(IConvexShape2D first, Similarity2D firstPose, IConvexShape2D second, Similarity2D secondPose, bool penetration, bool witnesses = true)
    {
        // The point-to-ellipse solver is both cheaper and more accurate than EPA for this common pair.
        if (first is Circle2D circle && second is Ellipse2D ellipse) return CircleEllipse(circle, firstPose, ellipse, secondPose);
        if (first is Ellipse2D otherEllipse && second is Circle2D otherCircle)
        {
            var result = CircleEllipse(otherCircle, secondPose, otherEllipse, firstPose);
            return result with { Normal = -result.Normal, PointOnFirst = result.PointOnSecond, PointOnSecond = result.PointOnFirst };
        }

        // Polygon vertices already live in an immutable buffer; borrow it instead of copying large polygons.
        var firstCount = first is ConvexPolygon2D ? 0 : first.GetVertCount();
        var secondCount = second is ConvexPolygon2D ? 0 : second.GetVertCount();
        Span<Vector2> firstVertices = firstCount <= 64 ? stackalloc Vector2[firstCount] : new Vector2[firstCount];
        Span<Vector2> secondVertices = secondCount <= 64 ? stackalloc Vector2[secondCount] : new Vector2[secondCount];
        var firstRadius = 0f;
        var secondRadius = 0f;
        if (firstCount > 0) first.GetVerts(firstVertices, out firstRadius);
        if (secondCount > 0) second.GetVerts(secondVertices, out secondRadius);
        var firstProxy = first is ConvexPolygon2D firstPolygon ? new ConvexProxy2D(firstPolygon.Vertices, firstPose)
            : firstCount > 0 ? new ConvexProxy2D(firstVertices, firstPose, firstRadius) : new ConvexProxy2D(first, firstPose);
        var secondProxy = second is ConvexPolygon2D secondPolygon ? new ConvexProxy2D(secondPolygon.Vertices, secondPose)
            : secondCount > 0 ? new ConvexProxy2D(secondVertices, secondPose, secondRadius) : new ConvexProxy2D(second, secondPose);
        return penetration ? Gjk2D.Query(firstProxy, secondProxy, witnesses) : Gjk2D.QuerySeparation(firstProxy, secondProxy);
    }

    private static ConvexQueryResult2D CircleEllipse(Circle2D circle, Similarity2D circlePose, Ellipse2D ellipse, Similarity2D ellipsePose)
    {
        var (worldCenter, radius) = WorldShape2D.Circle(circle, circlePose);
        var center = ellipsePose.InverseTransformPoint(worldCenter);
        var closest = ClosestPoint2D.OnEllipsePerimeter(center, ellipse.Center, ellipse.Radii);
        var inside = ellipse.ContainsPoint(center);
        var distance = Vector2.Distance(center, closest);
        var localNormal = distance > float.Epsilon
            ? (inside ? closest - center : center - closest) / distance
            : Vector2.Normalize((closest - ellipse.Center) / (ellipse.Radii * ellipse.Radii));
        var normal = Vector2.Normalize(ellipsePose.TransformDirection(localNormal));
        return new((inside ? -distance : distance) * ellipsePose.Scale - radius, normal,
            worldCenter - normal * radius, ellipsePose.TransformPoint(closest), 0f);
    }
}
