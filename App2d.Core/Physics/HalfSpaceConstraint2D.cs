using App2d.Core.Collision.Contacts;

namespace App2d.Core.Physics;

/// <summary>Moves a convex object out of a half-space using its minimum translation.</summary>
public static class HalfSpaceConstraint2D
{
    public static bool ConstrainOutside(SpatialObject2D convexObject, SpatialObject2D halfSpaceObject, out HalfSpaceContact2D contact)
    {
        if (!HalfSpaceCollision2D.TryGetContact(convexObject, halfSpaceObject, out contact))
            return false;

        convexObject.Transform.Position += contact.MinimumTranslationVector;
        return true;
    }
}
