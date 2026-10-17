using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Vertices;

/// <summary>Transforms borrowed vertex buffers, including in-place and overlapping slices.</summary>
public static class VertexTransforms2D
{
    public static void Transform(ReadOnlySpan<Vector2> source, Span<Vector2> destination, Matrix3x2 transform)
    {
        ArgGuard.ThrowIfTooShort(destination, source.Length);
        ArgGuard.Finite(transform);
        if (source.Overlaps(destination, out var offset) && offset > 0)
        {
            for (var i = source.Length - 1; i >= 0; i--) destination[i] = Vector2.Transform(source[i], transform);
        }
        else
        {
            for (var i = 0; i < source.Length; i++) destination[i] = Vector2.Transform(source[i], transform);
        }
    }
}
