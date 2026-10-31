using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>A float vector backed by System.Numerics.Vector4 for packed arithmetic.</summary>
public readonly record struct Vector4f : IVector<Vector4f, float>
{
    private readonly Vector4 _value;

    public Vector4f(float x, float y, float z, float w) : this(new Vector4(x, y, z, w)) { }
    public Vector4f(Vector4 value) => _value = value;
    public float X => _value.X;
    public float Y => _value.Y;
    public float Z => _value.Z;
    public float W => _value.W;
    public float this[int index] => _value[index];

    public static int Dimension => 4;
    public static Vector4f Zero => default;
    public static Vector4f One => new(Vector4.One);
    public static Vector4f UnitX => new(Vector4.UnitX);
    public static Vector4f UnitY => new(Vector4.UnitY);
    public static Vector4f UnitZ => new(Vector4.UnitZ);
    public static Vector4f UnitW => new(Vector4.UnitW);

    public static Vector4f operator +(Vector4f left, Vector4f right) => new(left._value + right._value);
    public static Vector4f operator -(Vector4f left, Vector4f right) => new(left._value - right._value);
    public static Vector4f operator -(Vector4f value) => new(-value._value);
    public static Vector4f operator *(Vector4f value, float scale) => new(value._value * scale);
    public static Vector4f operator *(float scale, Vector4f value) => value * scale;
    public static Vector4f operator /(Vector4f value, float scale) => new(value._value / scale);

    public static float Dot(Vector4f left, Vector4f right) => Vector4.Dot(left._value, right._value);
    public static Vector4f Min(Vector4f left, Vector4f right) => new(Vector4.Min(left._value, right._value));
    public static Vector4f Max(Vector4f left, Vector4f right) => new(Vector4.Max(left._value, right._value));
    public static Vector4f Abs(Vector4f value) => new(Vector4.Abs(value._value));
    public static bool IsFinite(Vector4f value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);

    public Vector3f XYZ => new(X, Y, Z);
    public Vector4 ToVector4() => _value;
    public Vector4d ToVector4d() => new(X, Y, Z, W);
    public override string ToString() => _value.ToString();
}
