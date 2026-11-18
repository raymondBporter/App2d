using System.Numerics;

namespace App2d.Core.Mathematics.Transforms;

public static class Matrix3x2Extensions
{
    /// <summary>Applies this transform first, then <paramref name="next"/>.</summary>
    public static Matrix3x2 Then(this Matrix3x2 first, Matrix3x2 next) => first * next;

    /// <summary>The three rows: transformed local X and Y axes, followed by translation (row-vector convention).</summary>
    public static (Vector2f X, Vector2f Y, Vector2f Translation) GetRows(this Matrix3x2 matrix) =>
        (new(matrix.X), new(matrix.Y), new(matrix.Z));

    /// <summary>The two full columns, including translation in each vector's Z component.</summary>
    public static (Vector3f X, Vector3f Y) GetColumns(this Matrix3x2 matrix) =>
        (new(matrix.M11, matrix.M21, matrix.M31), new(matrix.M12, matrix.M22, matrix.M32));

    /// <summary>True when every matrix entry is finite. Singular matrices are allowed.</summary>
    public static bool IsFinite(this Matrix3x2 matrix) =>
        float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) && float.IsFinite(matrix.M21) &&
        float.IsFinite(matrix.M22) && float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32);

    /// <summary>
    /// Multiplies a direction by the transpose of the linear part (row-vector
    /// convention). Maps world support/normal directions into the space the
    /// matrix transforms from.
    /// </summary>
    public static Vector2f TransposeTransformDirection(this Matrix3x2 matrix, Vector2f direction) => new(
        matrix.M11 * direction.X + matrix.M12 * direction.Y,
        matrix.M21 * direction.X + matrix.M22 * direction.Y);
}
