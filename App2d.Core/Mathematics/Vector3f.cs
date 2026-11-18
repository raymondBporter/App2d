using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>A float vector backed by System.Numerics.Vector3 for packed arithmetic.</summary>
public readonly record struct Vector3f : IVector<Vector3f, float>
{
    private readonly Vector3 _value;

    [JsonConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3f(float x, float y, float z = 0) : this(new Vector3(x, y, z)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3f(float value) : this(new Vector3(value)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3f(Vector2f xy, float z) : this(new Vector3(xy.ToVector2(), z)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3f(Vector3 value) => _value = value;
    public float X { get => _value.X; init => _value.X = value; }
    public float Y { get => _value.Y; init => _value.Y = value; }
    public float Z { get => _value.Z; init => _value.Z = value; }
    public float this[int index] => _value[index];

    public static int Dimension => 3;
    public static Vector3f FromCoordinates(ReadOnlySpan<float> coordinates) => new(coordinates[0], coordinates[1], coordinates[2]);
    public static Vector3f Zero => default;
    public static Vector3f One => new(Vector3.One);
    public static Vector3f UnitX => new(Vector3.UnitX);
    public static Vector3f UnitY => new(Vector3.UnitY);
    public static Vector3f UnitZ => new(Vector3.UnitZ);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator +(Vector3f left, Vector3f right) => new(left._value + right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator -(Vector3f left, Vector3f right) => new(left._value - right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator -(Vector3f value) => new(-value._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Vector3f value, float scale) => new(value._value * scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(float scale, Vector3f value) => value * scale;

    /// <summary>Multiplies the corresponding coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Vector3f left, Vector3f right) => new(left._value * right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator /(Vector3f value, float scale) => new(value._value / scale);

    /// <summary>Divides the corresponding coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator /(Vector3f left, Vector3f right) => new(left._value / right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector3f left, Vector3f right) => Vector3.Dot(left._value, right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dot(Vector3f other) => Vector3.Dot(_value, other._value);

    public static Vector3f Min(Vector3f left, Vector3f right) => new(Vector3.Min(left._value, right._value));
    public static Vector3f Max(Vector3f left, Vector3f right) => new(Vector3.Max(left._value, right._value));
    public static Vector3f Abs(Vector3f value) => new(Vector3.Abs(value._value));
    public static bool IsFinite(Vector3f value) => value.IsFinite();

    /// <summary>True when every component is finite, regardless of the vector's length.</summary>
    public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

    public Vector3f Min(Vector3f other) => Min(this, other);
    public Vector3f Max(Vector3f other) => Max(this, other);
    public Vector3f Abs() => Abs(this);
    public float DistanceSquared(Vector3f other) => DistanceSquared(this, other);
    public float Distance(Vector3f other) => Distance(this, other);
    public Vector3f Lerp(Vector3f other, float amount) => Lerp(this, other, amount);
    public Vector3f Midpoint(Vector3f other) => IVectorExtensions.Midpoint<Vector3f, float>(this, other);
    public Vector3f Project(Vector3f unitNormal) => IVectorExtensions.Project<Vector3f, float>(this, unitNormal);
    public Vector3f Reject(Vector3f unitNormal) => IVectorExtensions.Reject<Vector3f, float>(this, unitNormal);
    public Vector3f Reflect(Vector3f unitNormal) => new(Vector3.Reflect(_value, unitNormal._value));
    public Vector3f Clamp(Vector3f minimum, Vector3f maximum) => IVectorExtensions.Clamp<Vector3f, float>(this, minimum, maximum);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => _value.Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => _value.LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector3f first, Vector3f second) => Vector3.Distance(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector3f first, Vector3f second) => Vector3.DistanceSquared(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Normalize(Vector3f value) => new(Vector3.Normalize(value._value));

    public Vector3f Normalized() => Normalize(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Lerp(Vector3f first, Vector3f second, float amount) =>
        new(Vector3.Lerp(first._value, second._value, amount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Cross(Vector3f left, Vector3f right) => new(Vector3.Cross(left._value, right._value));

    public Vector3f Cross(Vector3f other) => Cross(this, other);

    public static Vector3f Transform(Vector3f point, Matrix4x4 matrix) => new(Vector3.Transform(point._value, matrix));
    public static Vector3f TransformNormal(Vector3f direction, Matrix4x4 matrix) => new(Vector3.TransformNormal(direction._value, matrix));
    public static Vector3f Transform(Vector3f value, Quaternion rotation) => new(Vector3.Transform(value._value, rotation));

    /// <summary>Converts each component from degrees to radians.</summary>
    public Vector3f DegreesToRadians() => new(Angles.DegreesToRadians(X), Angles.DegreesToRadians(Y), Angles.DegreesToRadians(Z));

    /// <summary>Converts each component from radians to degrees.</summary>
    public Vector3f RadiansToDegrees() => new(Angles.RadiansToDegrees(X), Angles.RadiansToDegrees(Y), Angles.RadiansToDegrees(Z));

    /// <summary>Rotates XY about the origin in radians, preserving Z.</summary>
    public Vector3f RotateXY(float radians) => new(XY.Rotate(radians), Z);

    /// <summary>Rotates XY about a pivot in radians, preserving Z.</summary>
    public Vector3f RotateXY(float radians, Vector2f pivot) => new(XY.Rotate(radians, pivot), Z);

    [JsonIgnore]
    public Vector2f XY => new(X, Y);
    [JsonIgnore]
    public Vector2f XZ => new(X, Z);
    [JsonIgnore]
    public Vector2f YZ => new(Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 ToVector3() => _value;

    /// <summary>Widens all coordinates before double-precision arithmetic, including subtraction.</summary>
    public Vector3d ToVector3d() => new(X, Y, Z);

    public override string ToString() => _value.ToString();
}
