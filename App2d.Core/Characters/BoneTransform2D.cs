using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>Parent-local affine bone channels. Angles are radians; shear turns the two axes independently.</summary>
public sealed record BoneTransform2D
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
    public float ScaleX { get; set; } = 1;
    public float ScaleY { get; set; } = 1;
    public float ShearX { get; set; }
    public float ShearY { get; set; }

    // Row vectors: local * parent. Unlike a similarity transform, this can reflect, shear, or collapse.
    [JsonIgnore] public Matrix3x2 Matrix => new(
        MathF.Cos(Rotation + ShearX) * ScaleX, MathF.Sin(Rotation + ShearX) * ScaleX,
        -MathF.Sin(Rotation + ShearY) * ScaleY, MathF.Cos(Rotation + ShearY) * ScaleY, X, Y);

    public void Validate(string field)
    {
        foreach (var value in new[] { X, Y, Rotation, ScaleX, ScaleY, ShearX, ShearY })
            if (!float.IsFinite(value)) throw new InvalidDataException($"{field}: transform values must be finite.");
    }

    /// <summary>A decomposition that reproduces even a singular matrix. It fixes shearX at zero.</summary>
    public static BoneTransform2D FromMatrix(Matrix3x2 matrix)
    {
        var rotation = MathF.Atan2(matrix.M12, matrix.M11);
        return new()
        {
            X = matrix.M31, Y = matrix.M32, Rotation = rotation,
            ScaleX = new Vector2(matrix.M11, matrix.M12).Length(),
            ScaleY = new Vector2(matrix.M21, matrix.M22).Length(),
            ShearY = MathF.Atan2(-matrix.M21, matrix.M22) - rotation
        };
    }
}
