using System.Numerics;

namespace App2d.Core.Mathematics;

public static class Vector4Extensions
{
    extension(Vector4 value)
    {
        public Vector4f ToVector4f() => new(value);
        public Vector4d ToVector4d() => new(value);

        /// <summary>True when every component is finite, regardless of the vector's length.</summary>
        public bool IsFinite() => Vector4f.IsFinite(new(value));
    }
}
