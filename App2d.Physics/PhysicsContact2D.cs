using App2d.Collision.Contacts;

namespace App2d.Physics;

public readonly record struct PhysicsContact2D(PhysicsBody2D First, PhysicsBody2D Second, CollisionContact2D Geometry);
