using App2d.Engine.Collision.Contacts;

namespace App2d.Engine.Collision;

public sealed class ShapeCollisionContactProvider2D : ICollisionContactProvider2D
{
    public bool TryGetContact(SpatialObject2D first, SpatialObject2D second, out CollisionContact2D contact) =>
        ShapeCollision2D.TryGetContact(first, second, out contact);
}
