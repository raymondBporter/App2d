using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Component queries and coordinate-plane projections of a 3D vector.</summary>
public static class Vector3Extensions
{
    extension(Vector3 value)
    {
        /// <summary>Widens all coordinates before double-precision arithmetic, including subtraction.</summary>
        public Vector3d ToVector3d() => new(value);

        /// <summary>True when every component is finite, regardless of the vector's length.</summary>
        public bool IsFinite() => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        public Vector2 XY => new(value.X, value.Y);
        public Vector2 XZ => new(value.X, value.Z);
        public Vector2 YZ => new(value.Y, value.Z);
    }
}
