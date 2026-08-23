using System.Numerics;

namespace App2d.Engine.Collision.Contacts;

// Normal points in the direction that moves the first object out of the second.
public readonly record struct CollisionContact2D(Vector2 Point, Vector2 Normal, float PenetrationDepth)
{
    public Vector2 MinimumTranslationVector => Normal * PenetrationDepth;

    public CollisionContact2D Flipped() => this with { Normal = -Normal };
}
