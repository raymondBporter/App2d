using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>A float vector backed by System.Numerics.Vector3 for packed arithmetic.</summary>
public readonly record struct Vector3f : IVector<Vector3f, float>
{
    private readonly Vector3 _value;

    public Vector3f(float x, float y, float z) : this(new Vector3(x, y, z)) { }
    public Vector3f(Vector3 value) => _value = value;
    public float X => _value.X;
    public float Y => _value.Y;
    public float Z => _value.Z;
    public float this[int index] => _value[index];

    public static int Dimension => 3;
    public static Vector3f Zero => default;
    public static Vector3f One => new(Vector3.One);
    public static Vector3f UnitX => new(Vector3.UnitX);
    public static Vector3f UnitY => new(Vector3.UnitY);
    public static Vector3f UnitZ => new(Vector3.UnitZ);

    public static Vector3f operator +(Vector3f left, Vector3f right) => new(left._value + right._value);
    public static Vector3f operator -(Vector3f left, Vector3f right) => new(left._value - right._value);
    public static Vector3f operator -(Vector3f value) => new(-value._value);
    public static Vector3f operator *(Vector3f value, float scale) => new(value._value * scale);
    public static Vector3f operator *(float scale, Vector3f value) => value * scale;
    public static Vector3f operator /(Vector3f value, float scale) => new(value._value / scale);

    public static float Dot(Vector3f left, Vector3f right) => Vector3.Dot(left._value, right._value);
    public static Vector3f Min(Vector3f left, Vector3f right) => new(Vector3.Min(left._value, right._value));
    public static Vector3f Max(Vector3f left, Vector3f right) => new(Vector3.Max(left._value, right._value));
    public static Vector3f Abs(Vector3f value) => new(Vector3.Abs(value._value));
    public static bool IsFinite(Vector3f value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    public Vector3f Cross(Vector3f other) => new(Vector3.Cross(_value, other._value));
    public Vector2f XY => new(X, Y);
    public Vector2f XZ => new(X, Z);
    public Vector2f YZ => new(Y, Z);

    public Vector3 ToVector3() => _value;
    public Vector3d ToVector3d() => new(X, Y, Z);
    public override string ToString() => _value.ToString();
}
