using App2d.Core.Collision.Contacts;

namespace App2d.Core.Physics;

public readonly record struct PhysicsContact2D(PhysicsBody2D First, PhysicsBody2D Second, CollisionContact2D Geometry);
