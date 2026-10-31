using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// Drawing order inside one character. Smaller values are closer to the viewer.
/// This is independent of the scene ZIndex that orders whole world objects.
/// Continuous values preserve the spacing between overlapping shapes and animated layer changes.
/// </summary>
public readonly record struct CharacterLayer2D(float Order)
{
    public static CharacterLayer2D Front => new(-.2f);
    public static CharacterLayer2D Middle => new(0);
    public static CharacterLayer2D Back => new(.2f);
}

/// <summary>A screen-plane point and its drawing layer within a character.</summary>
public readonly record struct LayeredPoint2D(Vector2 Position, CharacterLayer2D Layer)
{
    public static LayeredPoint2D From(Vector3 point) => new(new(point.X, point.Y), new(point.Z));
    public Vector3 ToVector3() => new(Position, Layer.Order);
}
