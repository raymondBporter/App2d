using App2d.Collision.Contacts;

namespace App2d.Collision;

public readonly record struct CollisionOverlap2D(
    Collider2D Collider,
    CollisionContact2D Contact);
