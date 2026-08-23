using App2d.Engine.Collision.Contacts;
using App2d.Engine.Geometry;

namespace App2d.Engine.Physics;

public sealed class ShapeContactProvider2D : IPhysicsContactProvider2D
{
    public bool TryGetContact(
        PhysicsBody2D first,
        PhysicsBody2D second,
        out CollisionContact2D contact)
    {
        if (ShapeCollision2D.TryGetContact(first.WorldObject, second.WorldObject, out contact))
            return true;

        if (second.WorldObject.Shape is HalfSpace2D && first.WorldObject.Shape is IConvexShape2D && HalfSpaceCollision2D.TryGetContact(first.WorldObject, second.WorldObject, out var halfSpaceContact))
        {
            contact = new CollisionContact2D(first.WorldObject.Transform.Position, halfSpaceContact.Normal, halfSpaceContact.PenetrationDepth);
            return true;
        }

        if (first.WorldObject.Shape is HalfSpace2D && second.WorldObject.Shape is IConvexShape2D && HalfSpaceCollision2D.TryGetContact(second.WorldObject, first.WorldObject, out halfSpaceContact))
        {
            contact = new CollisionContact2D(second.WorldObject.Transform.Position, -halfSpaceContact.Normal, halfSpaceContact.PenetrationDepth);
            return true;
        }

        contact = default;
        return false;
    }
}
