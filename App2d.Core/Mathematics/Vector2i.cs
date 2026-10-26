using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>A pair of signed integer coordinates. Arithmetic is checked; division is not defined.</summary>
public readonly record struct Vector2i(int X, int Y)
{
    public static Vector2i Zero => default;

    /// <summary>Converts to float coordinates explicitly; large integers may round.</summary>
    public Vector2 ToVector2() => new(X, Y);

    public static Vector2i operator +(Vector2i a, Vector2i b) => new(checked(a.X + b.X), checked(a.Y + b.Y));
    public static Vector2i operator -(Vector2i a, Vector2i b) => new(checked(a.X - b.X), checked(a.Y - b.Y));
    public static Vector2i operator *(Vector2i value, int scale) => new(checked(value.X * scale), checked(value.Y * scale));
    public static Vector2i operator *(int scale, Vector2i value) => value * scale;
}
