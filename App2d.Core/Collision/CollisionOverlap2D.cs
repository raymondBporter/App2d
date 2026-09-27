using App2d.Core.Collision.Contacts;

namespace App2d.Core.Collision;

public readonly record struct CollisionOverlap2D(
    Collider2D Collider,
    CollisionContact2D Contact);
