using System.Numerics;

namespace App2d.Engine.Mathematics;

public static class Vector2Extensions
{
    public static float Cross(this Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;
}