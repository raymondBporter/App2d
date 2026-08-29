using System.Numerics;
using App2d.Engine.Geometry;

namespace App2d.Engine.Collision.Contacts;

public static class HalfSpaceCollision2D
{
    public static bool TryGetContact(
        SpatialObject2D convexObject,
        SpatialObject2D halfSpaceObject,
        out HalfSpaceContact2D contact)
    {
        ArgGuard.ThrowIfNull(convexObject);
        ArgGuard.ThrowIfNull(halfSpaceObject);
        var convexShape = ArgGuard.RequireType<IConvexShape2D>(
            convexObject.Shape,
            "The constrained object must have a finite convex shape.",
            nameof(convexObject));
        var halfSpace = ArgGuard.RequireType<HalfSpace2D>(
            halfSpaceObject.Shape,
            "The constraint object must contain a HalfSpace2D.",
            nameof(halfSpaceObject));

        var (worldNormal, worldOffset) = CollisionMath2D.GetWorldPlane(halfSpace, halfSpaceObject.Transform);
        var objectToWorld = convexObject.Transform.LocalToWorldMatrix;

        // Convert the world projection direction into the object's local support direction.
        var localDirection = new Vector2(
            objectToWorld.M11 * worldNormal.X + objectToWorld.M12 * worldNormal.Y,
            objectToWorld.M21 * worldNormal.X + objectToWorld.M22 * worldNormal.Y);
        var localMinimum = convexShape.GetSupportPoint(-localDirection);
        var worldMinimum = Vector2.Transform(localMinimum, objectToWorld);
        var minimumProjection = Vector2.Dot(worldMinimum, worldNormal);
        var penetration = worldOffset - minimumProjection;

        if (penetration <= 0f)
        {
            contact = default;
            return false;
        }

        contact = new HalfSpaceContact2D(worldNormal, penetration);
        return true;
    }

    public static bool ConstrainOutside(
        SpatialObject2D convexObject,
        SpatialObject2D halfSpaceObject,
        out HalfSpaceContact2D contact)
    {
        if (!TryGetContact(convexObject, halfSpaceObject, out contact))
            return false;

        convexObject.Transform.Position += contact.MinimumTranslationVector;
        return true;
    }
}
