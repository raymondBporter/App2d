using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Coordinate-plane projections of a 3D vector.</summary>
public static class Vector3Extensions
{
    extension(Vector3 value)
    {
        public Vector2 XY => new(value.X, value.Y);
        public Vector2 XZ => new(value.X, value.Z);
        public Vector2 YZ => new(value.Y, value.Z);
    }
}
