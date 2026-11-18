namespace App2d.Core.Collision.Contacts;

public readonly record struct HalfSpaceContact2D(Vector2f Normal, float PenetrationDepth)
{
    public Vector2f MinimumTranslationVector => Normal * PenetrationDepth;
}
