using App2d.Engine.Collision.Contacts;
using App2d.Engine.Geometry;

namespace App2d.Engine.Collision;

public sealed class ShapeCollisionContactProvider2D : ICollisionContactProvider2D
{
    public bool TryGetContact(
        SpatialObject2D first,
        SpatialObject2D second,
        out CollisionContact2D contact)
    {
        if (ShapeCollision2D.TryGetContact(first, second, out contact))
            return true;

        if (second.Shape is HalfSpace2D && first.Shape is IConvexShape2D &&
            HalfSpaceCollision2D.TryGetContact(first, second, out var halfSpaceContact))
        {
            contact = new CollisionContact2D(
                first.Transform.Position,
                halfSpaceContact.Normal,
                halfSpaceContact.PenetrationDepth);
            return true;
        }

        if (first.Shape is HalfSpace2D && second.Shape is IConvexShape2D &&
            HalfSpaceCollision2D.TryGetContact(second, first, out halfSpaceContact))
        {
            contact = new CollisionContact2D(
                second.Transform.Position,
                -halfSpaceContact.Normal,
                halfSpaceContact.PenetrationDepth);
            return true;
        }

        contact = default;
        return false;
    }
}
