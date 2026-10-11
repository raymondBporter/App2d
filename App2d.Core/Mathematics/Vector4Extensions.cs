using System.Numerics;

namespace App2d.Core.Mathematics;

public static class Vector4Extensions
{
    extension(Vector4 value)
    {
        /// <summary>True when every component is finite, regardless of the vector's length.</summary>
        public bool IsFinite() => float.IsFinite(value.X) && float.IsFinite(value.Y) &&
            float.IsFinite(value.Z) && float.IsFinite(value.W);
    }
}
