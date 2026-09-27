using App2d.Core;
using App2d.Core.Collision.Contacts;

namespace App2d.Core.Collision;

public sealed class ShapeCollisionContactProvider2D : ICollisionContactProvider2D
{
    public bool TryGetContact(SpatialObject2D first, SpatialObject2D second, out CollisionContact2D contact) =>
        ShapeCollision2D.TryGetContact(first, second, out contact);
}
