using App2d.Core;
using App2d.Core.Geometry;

namespace App2d.Collision.Contacts;

public static class HalfSpaceCollision2D
{
    public static bool TryGetContact(SpatialObject2D convexObject, SpatialObject2D halfSpaceObject, out HalfSpaceContact2D contact)
    {
        ArgGuard.ThrowIfNull(convexObject);
        ArgGuard.ThrowIfNull(halfSpaceObject);
        var convexShape = ArgGuard.RequireType<IConvexShape2D>(convexObject.Shape, "The constrained object must have a finite convex shape.", nameof(convexObject));
        var halfSpace = ArgGuard.RequireType<HalfSpace2D>(halfSpaceObject.Shape, "The constraint object must contain a HalfSpace2D.", nameof(halfSpaceObject));

        if (!ShapeCollision2D.TryGetConvexHalfSpacePenetration(convexShape, convexObject.CollisionPose, halfSpace, halfSpaceObject.CollisionPose, out var worldNormal, out var penetration, out _))
        {
            contact = default;
            return false;
        }

        contact = new HalfSpaceContact2D(worldNormal, penetration);
        return true;
    }

    public static bool ConstrainOutside(SpatialObject2D convexObject, SpatialObject2D halfSpaceObject, out HalfSpaceContact2D contact)
    {
        if (!TryGetContact(convexObject, halfSpaceObject, out contact))
            return false;

        convexObject.Transform.Position += contact.MinimumTranslationVector;
        return true;
    }
}
