using App2d.Core.Collision.Contacts;

namespace App2d.Core.Collision;

public readonly record struct CollisionPair2D(
    Collider2D First,
    Collider2D Second,
    CollisionContact2D Contact);
