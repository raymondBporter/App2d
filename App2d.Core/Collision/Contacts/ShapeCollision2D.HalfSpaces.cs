using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult ConvexVsHalfSpace(IConvexShape2D convex, Similarity2D convexPose, HalfSpace2D halfSpace, Similarity2D halfSpacePose)
    {
        var query = ShapeHalfSpaceQuery2D.Query(convex, convexPose, halfSpace, halfSpacePose);
        return query.SignedDistance < 0f
            ? CollisionResult.From(new CollisionContact2D(query.PointOnSecond, query.Normal, -query.SignedDistance))
            : CollisionResult.None;
    }
}
