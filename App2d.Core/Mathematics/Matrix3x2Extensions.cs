using System.Numerics;

namespace App2d.Core.Mathematics;

public static class Matrix3x2Extensions
{
    public static bool IsFinite(this Matrix3x2 matrix) =>
        float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) && float.IsFinite(matrix.M21) &&
        float.IsFinite(matrix.M22) && float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32);

    /// <summary>
    /// Multiplies a direction by the transpose of the linear part (row-vector
    /// convention). Maps world support/normal directions into the space the
    /// matrix transforms from.
    /// </summary>
    public static Vector2 TransposeTransformDirection(this Matrix3x2 matrix, Vector2 direction) => new(
        matrix.M11 * direction.X + matrix.M12 * direction.Y,
        matrix.M21 * direction.X + matrix.M22 * direction.Y);
}
