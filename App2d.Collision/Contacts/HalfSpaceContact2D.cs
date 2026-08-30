using System.Numerics;

namespace App2d.Engine.Collision.Contacts;

public readonly record struct HalfSpaceContact2D(Vector2 Normal, float PenetrationDepth)
{
    public Vector2 MinimumTranslationVector => Normal * PenetrationDepth;
}
