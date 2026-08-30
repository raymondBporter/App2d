using App2d.Engine.Collision.Contacts;

namespace App2d.Engine.Physics;

public readonly record struct PhysicsContact2D(PhysicsBody2D First, PhysicsBody2D Second, CollisionContact2D Geometry);
