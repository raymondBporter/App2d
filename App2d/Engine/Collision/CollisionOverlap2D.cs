using App2d.Engine.Collision.Contacts;

namespace App2d.Engine.Collision;

public readonly record struct CollisionOverlap2D(
    Collider2D Collider,
    CollisionContact2D Contact);
