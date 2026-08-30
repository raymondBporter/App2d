using App2d.Engine.Collision.Contacts;

namespace App2d.Engine.Collision;

public sealed class CompositeCollisionContactProvider2D(params IEnumerable<ICollisionContactProvider2D> providers) :
    ICollisionContactProvider2D
{
    public bool TryGetContact(SpatialObject2D first, SpatialObject2D second, out CollisionContact2D contact)
    {
        foreach (var provider in providers)
        {
            if (provider.TryGetContact(first, second, out contact))
                return true;
        }

        contact = default;
        return false;
    }
}
