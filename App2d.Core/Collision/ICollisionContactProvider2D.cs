using App2d.Core.Collision.Contacts;
using App2d.Core;

namespace App2d.Core.Collision;

public interface ICollisionContactProvider2D
{
    bool TryGetContact(SpatialObject2D first, SpatialObject2D second, out CollisionContact2D contact);
}
