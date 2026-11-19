using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics;

/// <summary>
/// A float vector backed by System.Numerics.Vector4 for packed arithmetic. Members not declared
/// here (IsFinite, Midpoint, Project, Reject, Reflect, ...) are inherited from IVector4 and IVector.
/// </summary>
public readonly record struct Vector4f : IVector4<Vector4f, float>
{
    private readonly Vector4 _value;

    [System.Text.Json.Serialization.JsonConstructor]
    public Vector4f(float x, float y, float z, float w) : this(new Vector4(x, y, z, w)) { }
    public Vector4f(Vector4 value) => _value = value;
    public float X => _value.X;
    public float Y => _value.Y;
    public float Z => _value.Z;
    public float W => _value.W;
    public float this[int index] => _value[index];

    public static Vector4f Create(float x, float y, float z, float w) => new(x, y, z, w);
    public static Vector4f Zero => default;
    public static Vector4f One => new(Vector4.One);
    public static Vector4f UnitX => new(Vector4.UnitX);
    public static Vector4f UnitY => new(Vector4.UnitY);
    public static Vector4f UnitZ => new(Vector4.UnitZ);
    public static Vector4f UnitW => new(Vector4.UnitW);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator +(Vector4f left, Vector4f right) => new(left._value + right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator -(Vector4f left, Vector4f right) => new(left._value - right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator -(Vector4f value) => new(-value._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(Vector4f value, float scale) => new(value._value * scale);

    public static Vector4f operator *(float scale, Vector4f value) => value * scale;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator /(Vector4f value, float scale) => new(value._value / scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector4f left, Vector4f right) => Vector4.Dot(left._value, right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f Min(Vector4f left, Vector4f right) => new(Vector4.Min(left._value, right._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f Max(Vector4f left, Vector4f right) => new(Vector4.Max(left._value, right._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f Abs(Vector4f value) => new(Vector4.Abs(value._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f Clamp(Vector4f value, Vector4f minimum, Vector4f maximum) => new(Vector4.Clamp(value._value, minimum._value, maximum._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LengthSquared(Vector4f value) => value._value.LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Length(Vector4f value) => value._value.Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f Normalize(Vector4f value) => new(Vector4.Normalize(value._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector4f first, Vector4f second) => Vector4.DistanceSquared(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector4f first, Vector4f second) => Vector4.Distance(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f Lerp(Vector4f first, Vector4f second, float amount) => new(Vector4.Lerp(first._value, second._value, amount));

    [System.Text.Json.Serialization.JsonIgnore]
    public Vector3f XYZ => new(X, Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 ToVector4() => _value;

    public Vector4d ToVector4d() => new(X, Y, Z, W);
    public override string ToString() => _value.ToString();
}
