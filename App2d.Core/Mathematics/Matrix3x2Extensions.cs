using System.Numerics;

namespace App2d.Engine.Mathematics;

public static class Matrix3x2Extensions
{
    /// <summary>
    /// Multiplies a direction by the transpose of the linear part (row-vector
    /// convention). Maps world support/normal directions into the space the
    /// matrix transforms from.
    /// </summary>
    public static Vector2 TransposeTransformDirection(this Matrix3x2 matrix, Vector2 direction) => new(
        matrix.M11 * direction.X + matrix.M12 * direction.Y,
        matrix.M21 * direction.X + matrix.M22 * direction.Y);
}
