using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult HalfSpaceAgainst(HalfSpace2D halfSpace, Transform2D halfSpaceTransform, IShape2D other, Transform2D otherTransform) =>
        other switch
        {
            Circle2D circle => CircleVsHalfSpace(circle, otherTransform, halfSpace, halfSpaceTransform).Flipped(),
            IConvexShape2D convex => ConvexVsHalfSpace(convex, otherTransform, halfSpace, halfSpaceTransform).Flipped(),
            _ => CollisionResult.None
        };

    private static CollisionResult ConvexVsHalfSpace(IConvexShape2D convex, Transform2D convexTransform, HalfSpace2D halfSpace, Transform2D halfSpaceTransform)
    {
        if (!TryGetConvexHalfSpacePenetration(convex, convexTransform, halfSpace, halfSpaceTransform, out var normal, out var penetration, out var deepestPoint))
            return CollisionResult.None;

        // Report the deepest point projected onto the boundary, matching the circle row.
        return CollisionResult.From(new CollisionContact2D(deepestPoint + normal * penetration, normal, penetration));
    }

    internal static bool TryGetConvexHalfSpacePenetration(IConvexShape2D convex, Transform2D convexTransform, HalfSpace2D halfSpace, Transform2D halfSpaceTransform, out Vector2 worldNormal, out float penetration, out Vector2 deepestPoint)
    {
        (worldNormal, var worldOffset) = CollisionMath2D.GetWorldPlane(halfSpace, halfSpaceTransform);
        var objectToWorld = convexTransform.LocalToWorldMatrix;

        // The world projection direction becomes the object's local support direction.
        var localDirection = objectToWorld.TransposeTransformDirection(worldNormal);
        deepestPoint = Vector2.Transform(convex.GetSupportPoint(-localDirection), objectToWorld);
        penetration = worldOffset - Vector2.Dot(deepestPoint, worldNormal);
        return penetration > 0f;
    }
}
