namespace App2d.Core.Collision.Contacts;

// Normal points in the direction that moves the first object out of the second.
public readonly record struct CollisionContact2D(Vector2f Point, Vector2f Normal, float PenetrationDepth)
{
    public Vector2f MinimumTranslationVector => Normal * PenetrationDepth;

    public CollisionContact2D Flipped() => this with { Normal = -Normal };
}
