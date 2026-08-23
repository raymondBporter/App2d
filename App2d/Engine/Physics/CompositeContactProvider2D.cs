using App2d.Engine.Collision.Contacts;

namespace App2d.Engine.Physics;

public sealed class CompositeContactProvider2D(params IEnumerable<IPhysicsContactProvider2D> providers) : IPhysicsContactProvider2D
{
    public bool TryGetContact(PhysicsBody2D first, PhysicsBody2D second, out CollisionContact2D contact)
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
