using App2d.Core.Geometry.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;

namespace App2d.Core.Collision.Contacts;

public static class HalfSpaceCollision2D
{
    public static bool TryGetContact(SpatialObject2D convexObject, SpatialObject2D halfSpaceObject, out CollisionContact2D contact)
    {
        Ensure.ThrowIfNull(convexObject);
        Ensure.ThrowIfNull(halfSpaceObject);
        Ensure.RequireType<IConvexShape2D>(convexObject.Shape, "The constrained object must have a finite convex shape.", nameof(convexObject));
        Ensure.RequireType<HalfSpace2D>(halfSpaceObject.Shape, "The constraint object must contain a HalfSpace2D.", nameof(halfSpaceObject));

        return ShapeCollision2D.TryGetContact(convexObject, halfSpaceObject, out contact);
    }
}
