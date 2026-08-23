using App2d.Engine.Collision.Contacts;

namespace App2d.Engine.Physics;

public interface IPhysicsContactProvider2D
{
    bool TryGetContact(PhysicsBody2D first, PhysicsBody2D second, out CollisionContact2D contact);
}
