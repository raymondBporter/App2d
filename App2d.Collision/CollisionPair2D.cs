using App2d.Engine.Collision.Contacts;

namespace App2d.Engine.Collision;

public readonly record struct CollisionPair2D(
    Collider2D First,
    Collider2D Second,
    CollisionContact2D Contact);
