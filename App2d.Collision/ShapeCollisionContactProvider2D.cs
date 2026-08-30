using App2d.Collision.Contacts;
using App2d.Core;

namespace App2d.Collision;

public sealed class ShapeCollisionContactProvider2D : ICollisionContactProvider2D
{
    public bool TryGetContact(SpatialObject2D first, SpatialObject2D second, out CollisionContact2D contact) =>
        ShapeCollision2D.TryGetContact(first, second, out contact);
}
