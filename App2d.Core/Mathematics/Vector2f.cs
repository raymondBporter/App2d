using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>
/// A two-dimensional float vector backed by <see cref="Vector2"/>. Members not declared here
/// (Cross, Rotate, Midpoint, Project, Reject, ...) are inherited from IVector2 and IVector.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Vector2f : IVector2<Vector2f, float>
{
    private readonly Vector2 _value;

    [JsonConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2f(float X, float Y) : this(new Vector2(X, Y)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2f(float value) : this(new Vector2(value)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2f(Vector2 value) => _value = value;

    public float X { get => _value.X; init => _value.X = value; }
    public float Y { get => _value.Y; init => _value.Y = value; }
    public float this[int index] => _value[index];

    public void Deconstruct(out float x, out float y) => (x, y) = (X, Y);

    public static Vector2f Create(float x, float y) => new(x, y);
    public static Vector2f Zero => default;
    public static Vector2f One => new(Vector2.One);
    public static Vector2f UnitX => new(Vector2.UnitX);
    public static Vector2f UnitY => new(Vector2.UnitY);
    public static Vector2f NegativeInfinity => new(float.NegativeInfinity);
    public static Vector2f PositiveInfinity => new(float.PositiveInfinity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator +(Vector2f left, Vector2f right) => new(left._value + right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator -(Vector2f left, Vector2f right) => new(left._value - right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator -(Vector2f value) => new(-value._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator *(Vector2f value, float scale) => new(value._value * scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator *(float scale, Vector2f value) => value * scale;

    /// <summary>Multiplies the corresponding coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator *(Vector2f left, Vector2f right) => new(left._value * right._value);

    /// <summary>Divides the corresponding coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator /(Vector2f left, Vector2f right) => new(left._value / right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator /(Vector2f value, float scale) => new(value._value / scale);

    // ---- Packed System.Numerics versions. Each replaces the inherited one of the same signature. ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector2f left, Vector2f right) => Vector2.Dot(left._value, right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Min(Vector2f left, Vector2f right) => new(Vector2.Min(left._value, right._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Max(Vector2f left, Vector2f right) => new(Vector2.Max(left._value, right._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Abs(Vector2f value) => new(Vector2.Abs(value._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Clamp(Vector2f value, Vector2f minimum, Vector2f maximum) => new(Vector2.Clamp(value._value, minimum._value, maximum._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LengthSquared(Vector2f value) => value._value.LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Length(Vector2f value) => value._value.Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Normalize(Vector2f value) => new(Vector2.Normalize(value._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector2f first, Vector2f second) => Vector2.DistanceSquared(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector2f first, Vector2f second) => Vector2.Distance(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Lerp(Vector2f first, Vector2f second, float amount) => new(Vector2.Lerp(first._value, second._value, amount));

    /// <summary>Reflects a vector across a plane with a unit normal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Reflect(Vector2f value, Vector2f unitNormal) => new(Vector2.Reflect(value._value, unitNormal._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Transform(Vector2f point, Matrix3x2 matrix) => new(Vector2.Transform(point._value, matrix));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f TransformNormal(Vector2f direction, Matrix3x2 matrix) => new(Vector2.TransformNormal(direction._value, matrix));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToVector2(Vector2f value) => value._value;

    // ---- Direct instance forms of the hottest calls, so they stay a single call in Debug builds too. ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dot(Vector2f other) => Vector2.Dot(_value, other._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => _value.LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => _value.Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ToVector2() => _value;

    // ---- Float-only extras that are not part of the vector interfaces. ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f SquareRoot(Vector2f value) => new(Vector2.SquareRoot(value._value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Transform(Vector2f point, Matrix4x4 matrix) => new(Vector2.Transform(point._value, matrix));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f TransformNormal(Vector2f direction, Matrix4x4 matrix) => new(Vector2.TransformNormal(direction._value, matrix));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Transform(Vector2f value, Quaternion rotation) => new(Vector2.Transform(value._value, rotation));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<float> destination) => _value.CopyTo(destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCopyTo(Span<float> destination) => _value.TryCopyTo(destination);

    public override string ToString() => _value.ToString();
}
