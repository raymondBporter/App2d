using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>A two-dimensional float vector backed by <see cref="Vector2"/>.</summary>
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

    public static int Dimension => 2;
    public static Vector2f Zero => default;
    public static Vector2f One => new(Vector2.One);
    public static Vector2f UnitX => new(Vector2.UnitX);
    public static Vector2f UnitY => new(Vector2.UnitY);
    public static Vector2f NegativeInfinity => new(float.NegativeInfinity);
    public static Vector2f PositiveInfinity => new(float.PositiveInfinity);
    public static Vector2f FromCoordinates(ReadOnlySpan<float> coordinates) => new(coordinates[0], coordinates[1]);
    public static Vector2f Create(float x, float y) => new(x, y);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector2f left, Vector2f right) => Vector2.Dot(left._value, right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dot(Vector2f other) => Vector2.Dot(_value, other._value);

    public static Vector2f Min(Vector2f left, Vector2f right) => new(Vector2.Min(left._value, right._value));
    public static Vector2f Max(Vector2f left, Vector2f right) => new(Vector2.Max(left._value, right._value));
    public static Vector2f Abs(Vector2f value) => new(Vector2.Abs(value._value));
    public static bool IsFinite(Vector2f value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    public Vector2f Min(Vector2f other) => Min(this, other);
    public Vector2f Max(Vector2f other) => Max(this, other);
    public Vector2f Abs() => Abs(this);
    public bool IsFinite() => IsFinite(this);
    public float DistanceSquared(Vector2f other) => DistanceSquared(this, other);
    public float Distance(Vector2f other) => Distance(this, other);
    public Vector2f Lerp(Vector2f other, float amount) => Lerp(this, other, amount);
    public Vector2f Midpoint(Vector2f other) => IVectorExtensions.Midpoint<Vector2f, float>(this, other);
    public Vector2f Project(Vector2f unitNormal) => IVectorExtensions.Project<Vector2f, float>(this, unitNormal);
    public Vector2f Reject(Vector2f unitNormal) => Reject(this, unitNormal);
    public Vector2f Reflect(Vector2f unitNormal) => Reflect(this, unitNormal);
    public Vector2f Clamp(Vector2f minimum, Vector2f maximum) => Clamp(this, minimum, maximum);

    public float Cross(Vector2f other) => Vector2Math<Vector2f, float>.Cross(this, other);
    public float Orientation(Vector2f second, Vector2f third) => Vector2Math<Vector2f, float>.Orientation(this, second, third);
    [JsonIgnore]
    public float AngleRadians => Vector2Math<Vector2f, float>.AngleRadians(this);
    [JsonIgnore]
    public Vector2f PerpCcw => Vector2Math<Vector2f, float>.PerpCcw(this);
    [JsonIgnore]
    public Vector2f PerpCw => Vector2Math<Vector2f, float>.PerpCw(this);
    public Vector2f Rotate(float radians) => Vector2Math<Vector2f, float>.Rotate(this, radians);
    public Vector2f Rotate(float radians, Vector2f pivot) => Vector2Math<Vector2f, float>.Rotate(this, radians, pivot);
    public Polar<float> ToPolar() => Vector2Math<Vector2f, float>.ToPolar(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => _value.LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => _value.Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector2f first, Vector2f second) => Vector2.Distance(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector2f first, Vector2f second) => Vector2.DistanceSquared(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Normalize(Vector2f value) => new(Vector2.Normalize(value._value));

    public Vector2f Normalized() => Normalize(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Lerp(Vector2f first, Vector2f second, float amount) => new(Vector2.Lerp(first._value, second._value, amount));

    public static Vector2f Clamp(Vector2f value, Vector2f minimum, Vector2f maximum) => Min(Max(value, minimum), maximum);

    /// <summary>Reflects a vector across a plane with a unit normal.</summary>
    public static Vector2f Reflect(Vector2f value, Vector2f unitNormal) => new(Vector2.Reflect(value._value, unitNormal._value));

    public static Vector2f SquareRoot(Vector2f value) => new(Vector2.SquareRoot(value._value));

    /// <summary>Removes the component parallel to a unit normal.</summary>
    public static Vector2f Reject(Vector2f value, Vector2f unitNormal) => value - unitNormal * Dot(value, unitNormal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Transform(Vector2f point, Matrix3x2 matrix) => new(Vector2.Transform(point._value, matrix));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f TransformNormal(Vector2f direction, Matrix3x2 matrix) => new(Vector2.TransformNormal(direction._value, matrix));

    public static Vector2f Transform(Vector2f point, Matrix4x4 matrix) => new(Vector2.Transform(point._value, matrix));
    public static Vector2f TransformNormal(Vector2f direction, Matrix4x4 matrix) => new(Vector2.TransformNormal(direction._value, matrix));
    public static Vector2f Transform(Vector2f value, Quaternion rotation) => new(Vector2.Transform(value._value, rotation));

    public void CopyTo(Span<float> destination) => _value.CopyTo(destination);
    public bool TryCopyTo(Span<float> destination) => _value.TryCopyTo(destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ToVector2() => _value;

    public Vector2f ToVector2f() => this;
    public Vector2d ToVector2d() => new(this);
    public override string ToString() => _value.ToString();
}
