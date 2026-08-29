using App2d.Engine.Collision.Contacts;

namespace App2d.Engine.Collision;

public interface ICollisionContactProvider2D
{
    bool TryGetContact(
        SpatialObject2D first,
        SpatialObject2D second,
        out CollisionContact2D contact);
}
