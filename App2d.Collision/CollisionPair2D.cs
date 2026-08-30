using App2d.Collision.Contacts;

namespace App2d.Collision;

public readonly record struct CollisionPair2D(
    Collider2D First,
    Collider2D Second,
    CollisionContact2D Contact);
