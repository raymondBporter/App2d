using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult ConvexVsHalfSpace(IConvexShape2D convex, Similarity2D convexPose, HalfSpace2D halfSpace, Similarity2D halfSpacePose)
    {
        if (!TryGetConvexHalfSpacePenetration(convex, convexPose, halfSpace, halfSpacePose, out var normal, out var penetration, out var deepestPoint)) return CollisionResult.None;

        // Report the deepest point projected onto the boundary, matching the circle row.
        return CollisionResult.From(new CollisionContact2D(deepestPoint + normal * penetration, normal, penetration));
    }

    /// <summary>Measures how far the deepest support point of a posed convex shape sits inside a posed half-space.</summary>
    /// <param name="convex">The convex shape.</param>
    /// <param name="convexPose">The convex shape's local-to-world similarity.</param>
    /// <param name="halfSpace">The half-space.</param>
    /// <param name="halfSpacePose">The half-space's local-to-world similarity.</param>
    /// <param name="worldNormal">The world unit normal of the half-space.</param>
    /// <param name="penetration">How far the deepest point lies inside the solid side; nonpositive when separated.</param>
    /// <param name="deepestPoint">The world support point of the shape against the normal.</param>
    /// <returns>True when the shape penetrates the half-space.</returns>
    internal static bool TryGetConvexHalfSpacePenetration(IConvexShape2D convex, Similarity2D convexPose, HalfSpace2D halfSpace, Similarity2D halfSpacePose, out Vector2 worldNormal, out float penetration, out Vector2 deepestPoint)
    {
        (worldNormal, var worldOffset) = WorldShape2D.HalfSpace(halfSpace, halfSpacePose);
        // The world projection direction becomes the object's local support direction.
        var localDirection = convexPose.TransposeTransformDirection(worldNormal);
        deepestPoint = convexPose.TransformPoint(convex.GetSupportPoint(-localDirection));
        penetration = -Distance2D.SignedDistanceToHalfSpace(deepestPoint, worldNormal, worldOffset);
        return penetration > 0f;
    }
}
