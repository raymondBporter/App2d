using System.Numerics;
using System.Runtime.Intrinsics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A vector of four doubles. Arithmetic follows IEEE 754 without validation.
/// Only storage, constants and operators are declared here; Dot, Length and the rest are inherited from IVector4 and IVector.
/// </summary>
public readonly record struct Vector4d(double X, double Y, double Z, double W) : IVector4<Vector4d, double>
{
    public Vector4d(Vector4 value) : this(value.X, value.Y, value.Z, value.W) { }
    public Vector4d(Vector3d xyz, double w) : this(xyz.X, xyz.Y, xyz.Z, w) { }

    /// <summary>Cartesian components and their RGBA interpretation. RGB replacement preserves the existing alpha.</summary>
    public static class Channels
    {
        public static InterpolationChannel<Vector4d, Vector4d> V => InterpolationChannel<Vector4d, Vector4d>.Whole;
        public static InterpolationChannel<Vector4d, double> X => VectorChannels<Vector4d, double>.Instance.X;
        public static InterpolationChannel<Vector4d, double> Y => VectorChannels<Vector4d, double>.Instance.Y;
        public static InterpolationChannel<Vector4d, double> Z => VectorChannels<Vector4d, double>.Instance.Z;
        public static InterpolationChannel<Vector4d, double> W => VectorChannels<Vector4d, double>.Instance.W;
        public static InterpolationChannel<Vector4d, double> R => X;
        public static InterpolationChannel<Vector4d, double> G => Y;
        public static InterpolationChannel<Vector4d, double> B => Z;
        public static InterpolationChannel<Vector4d, double> A => W;
        public static InterpolationChannel<Vector4d, Vector3d> RGB { get; } = new(static value => value.XYZ,
            static (value, rgb) => new(rgb, value.W));
    }

    public static Vector4d Create(double x, double y, double z, double w) => new(x, y, z, w);
    public static Vector4d Zero => default;
    public static Vector4d One => new(1, 1, 1, 1);
    public static Vector4d UnitX => new(1, 0, 0, 0);
    public static Vector4d UnitY => new(0, 1, 0, 0);
    public static Vector4d UnitZ => new(0, 0, 1, 0);
    public static Vector4d UnitW => new(0, 0, 0, 1);
    public double this[int index] => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        3 => W,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static Vector4d operator +(Vector4d left, Vector4d right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);
    public static Vector4d operator -(Vector4d left, Vector4d right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);
    public static Vector4d operator -(Vector4d value) => new(-value.X, -value.Y, -value.Z, -value.W);
    public static Vector4d operator *(Vector4d value, double scale) => new(value.X * scale, value.Y * scale, value.Z * scale, value.W * scale);
    public static Vector4d operator *(double scale, Vector4d value) => value * scale;
    public static Vector4d operator /(Vector4d value, double scale)
    {
        var quotient = Vector256.Create(value.X, value.Y, value.Z, value.W) / Vector256.Create(scale);
        return new(quotient.GetElement(0), quotient.GetElement(1), quotient.GetElement(2), quotient.GetElement(3));
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public Vector3d XYZ => new(X, Y, Z);
    public Vector4 ToVector4() => new((float)X, (float)Y, (float)Z, (float)W);
    public override string ToString() => $"<{X}, {Y}, {Z}, {W}>";
}
