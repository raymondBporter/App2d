using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult HalfSpaceAgainst(HalfSpace2D halfSpace, Similarity2D halfSpacePose, IShape2D other, Similarity2D otherPose) =>
        other switch
        {
            Circle2D circle => CircleVsHalfSpace(circle, otherPose, halfSpace, halfSpacePose).Flipped(),
            IConvexShape2D convex => ConvexVsHalfSpace(convex, otherPose, halfSpace, halfSpacePose).Flipped(),
            _ => CollisionResult.None
        };

    private static CollisionResult ConvexVsHalfSpace(IConvexShape2D convex, Similarity2D convexPose, HalfSpace2D halfSpace, Similarity2D halfSpacePose)
    {
        if (!TryGetConvexHalfSpacePenetration(convex, convexPose, halfSpace, halfSpacePose, out var normal, out var penetration, out var deepestPoint))
            return CollisionResult.None;

        // Report the deepest point projected onto the boundary, matching the circle row.
        return CollisionResult.From(new CollisionContact2D(deepestPoint + normal * penetration, normal, penetration));
    }

    internal static bool TryGetConvexHalfSpacePenetration(IConvexShape2D convex, Similarity2D convexPose, HalfSpace2D halfSpace, Similarity2D halfSpacePose, out Vector2 worldNormal, out float penetration, out Vector2 deepestPoint)
    {
        (worldNormal, var worldOffset) = CollisionMath2D.GetWorldPlane(halfSpace, halfSpacePose);

        // The world projection direction becomes the object's local support direction.
        var localDirection = convexPose.TransposeTransformDirection(worldNormal);
        deepestPoint = convexPose.TransformPoint(convex.GetSupportPoint(-localDirection));
        penetration = worldOffset - Vector2.Dot(deepestPoint, worldNormal);
        return penetration > 0f;
    }
}
