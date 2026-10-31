using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>A float vector backed by System.Numerics.Vector2 for packed arithmetic.</summary>
public readonly record struct Vector2f : IVector<Vector2f, float>
{
    private readonly Vector2 _value;

    public Vector2f(float x, float y) : this(new Vector2(x, y)) { }
    public Vector2f(Vector2 value) => _value = value;
    public float X => _value.X;
    public float Y => _value.Y;
    public float this[int index] => _value[index];

    public static int Dimension => 2;
    public static Vector2f Zero => default;
    public static Vector2f One => new(Vector2.One);
    public static Vector2f UnitX => new(Vector2.UnitX);
    public static Vector2f UnitY => new(Vector2.UnitY);

    public static Vector2f operator +(Vector2f left, Vector2f right) => new(left._value + right._value);
    public static Vector2f operator -(Vector2f left, Vector2f right) => new(left._value - right._value);
    public static Vector2f operator -(Vector2f value) => new(-value._value);
    public static Vector2f operator *(Vector2f value, float scale) => new(value._value * scale);
    public static Vector2f operator *(float scale, Vector2f value) => value * scale;
    public static Vector2f operator /(Vector2f value, float scale) => new(value._value / scale);

    public static float Dot(Vector2f left, Vector2f right) => Vector2.Dot(left._value, right._value);
    public static Vector2f Min(Vector2f left, Vector2f right) => new(Vector2.Min(left._value, right._value));
    public static Vector2f Max(Vector2f left, Vector2f right) => new(Vector2.Max(left._value, right._value));
    public static Vector2f Abs(Vector2f value) => new(Vector2.Abs(value._value));
    public static bool IsFinite(Vector2f value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    public float Cross(Vector2f other) => X * other.Y - Y * other.X;
    public float Orientation(Vector2f b, Vector2f c) => (b - this).Cross(c - this);
    public Vector2f PerpCcw => new(-Y, X);
    public Vector2f PerpCw => new(Y, -X);

    public static Vector2f FromAngle(float angleRadians)
    {
        var (sin, cos) = float.SinCos(angleRadians);
        return new(cos, sin);
    }

    public Vector2 ToVector2() => _value;
    public Vector2d ToVector2d() => new(X, Y);
    public override string ToString() => _value.ToString();
}
