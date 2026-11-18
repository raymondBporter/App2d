using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;

namespace App2d.Core.Collision.Contacts;

public static class HalfSpaceCollision2D
{
    public static bool TryGetContact(SpatialObject2D convexObject, SpatialObject2D halfSpaceObject, out HalfSpaceContact2D contact)
    {
        Ensure.ThrowIfNull(convexObject);
        Ensure.ThrowIfNull(halfSpaceObject);
        var convexShape = Ensure.RequireType<IConvexShape2D>(convexObject.Shape, "The constrained object must have a finite convex shape.", nameof(convexObject));
        var halfSpace = Ensure.RequireType<HalfSpace2D>(halfSpaceObject.Shape, "The constraint object must contain a HalfSpace2D.", nameof(halfSpaceObject));

        var query = ShapeHalfSpaceQuery2D.Query(convexShape, convexObject.CollisionPose, halfSpace, halfSpaceObject.CollisionPose);
        if (!(query.SignedDistance < 0f))
        {
            contact = default;
            return false;
        }

        contact = new HalfSpaceContact2D(query.Normal, -query.SignedDistance);
        return true;
    }
}
