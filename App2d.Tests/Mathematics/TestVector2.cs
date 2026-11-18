using System.Numerics;

namespace App2d.Tests.Mathematics;

/// <summary>A minimal implementation used to verify that vector algorithms come from the contract.</summary>
internal readonly record struct TestVector2<TReal>(TReal X, TReal Y) : IVector2<TestVector2<TReal>, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public static int Dimension => 2;
    public static TestVector2<TReal> Create(TReal x, TReal y) => new(x, y);
    public static TestVector2<TReal> FromCoordinates(ReadOnlySpan<TReal> coordinates) => new(coordinates[0], coordinates[1]);
    public static TestVector2<TReal> Zero => default;
    public TReal this[int index] => index switch { 0 => X, 1 => Y, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public static TestVector2<TReal> operator +(TestVector2<TReal> a, TestVector2<TReal> b) => new(a.X + b.X, a.Y + b.Y);
    public static TestVector2<TReal> operator -(TestVector2<TReal> a, TestVector2<TReal> b) => new(a.X - b.X, a.Y - b.Y);
    public static TestVector2<TReal> operator -(TestVector2<TReal> value) => new(-value.X, -value.Y);
    public static TestVector2<TReal> operator *(TestVector2<TReal> value, TReal scale) => new(value.X * scale, value.Y * scale);
    public static TestVector2<TReal> operator /(TestVector2<TReal> value, TReal scale) => new(value.X / scale, value.Y / scale);
    public static TReal Dot(TestVector2<TReal> a, TestVector2<TReal> b) => a.X * b.X + a.Y * b.Y;
    public static TestVector2<TReal> Min(TestVector2<TReal> a, TestVector2<TReal> b) => new(TReal.Min(a.X, b.X), TReal.Min(a.Y, b.Y));
    public static TestVector2<TReal> Max(TestVector2<TReal> a, TestVector2<TReal> b) => new(TReal.Max(a.X, b.X), TReal.Max(a.Y, b.Y));
    public static TestVector2<TReal> Abs(TestVector2<TReal> value) => new(TReal.Abs(value.X), TReal.Abs(value.Y));
    public static bool IsFinite(TestVector2<TReal> value) => TReal.IsFinite(value.X) && TReal.IsFinite(value.Y);

    public TReal Dot(TestVector2<TReal> other) => Dot(this, other);
    public TestVector2<TReal> Min(TestVector2<TReal> other) => Min(this, other);
    public TestVector2<TReal> Max(TestVector2<TReal> other) => Max(this, other);
    public TestVector2<TReal> Abs() => Abs(this);
    public bool IsFinite() => IsFinite(this);
    public TReal LengthSquared() => IVectorExtensions.LengthSquared<TestVector2<TReal>, TReal>(this);
    public TReal Length() => IVectorExtensions.Length<TestVector2<TReal>, TReal>(this);
    public TestVector2<TReal> Normalized() => IVectorExtensions.Normalize<TestVector2<TReal>, TReal>(this);
    public TReal DistanceSquared(TestVector2<TReal> other) => IVectorExtensions.DistanceSquared<TestVector2<TReal>, TReal>(this, other);
    public TReal Distance(TestVector2<TReal> other) => IVectorExtensions.Distance<TestVector2<TReal>, TReal>(this, other);
    public TestVector2<TReal> Lerp(TestVector2<TReal> other, TReal amount) => IVectorExtensions.Lerp<TestVector2<TReal>, TReal>(this, other, amount);
    public TestVector2<TReal> Midpoint(TestVector2<TReal> other) => IVectorExtensions.Midpoint<TestVector2<TReal>, TReal>(this, other);
    public TestVector2<TReal> Project(TestVector2<TReal> unitNormal) => IVectorExtensions.Project<TestVector2<TReal>, TReal>(this, unitNormal);
    public TestVector2<TReal> Reject(TestVector2<TReal> unitNormal) => IVectorExtensions.Reject<TestVector2<TReal>, TReal>(this, unitNormal);
    public TestVector2<TReal> Reflect(TestVector2<TReal> unitNormal) => IVectorExtensions.Reflect<TestVector2<TReal>, TReal>(this, unitNormal);
    public TestVector2<TReal> Clamp(TestVector2<TReal> minimum, TestVector2<TReal> maximum) => IVectorExtensions.Clamp<TestVector2<TReal>, TReal>(this, minimum, maximum);

    public TReal Cross(TestVector2<TReal> other) => Vector2Math<TestVector2<TReal>, TReal>.Cross(this, other);
    public TReal Orientation(TestVector2<TReal> second, TestVector2<TReal> third) => Vector2Math<TestVector2<TReal>, TReal>.Orientation(this, second, third);
    public TReal AngleRadians => Vector2Math<TestVector2<TReal>, TReal>.AngleRadians(this);
    public TestVector2<TReal> PerpCcw => Vector2Math<TestVector2<TReal>, TReal>.PerpCcw(this);
    public TestVector2<TReal> PerpCw => Vector2Math<TestVector2<TReal>, TReal>.PerpCw(this);
    public TestVector2<TReal> Rotate(TReal radians) => Vector2Math<TestVector2<TReal>, TReal>.Rotate(this, radians);
    public TestVector2<TReal> Rotate(TReal radians, TestVector2<TReal> pivot) => Vector2Math<TestVector2<TReal>, TReal>.Rotate(this, radians, pivot);
    public Polar<TReal> ToPolar() => Vector2Math<TestVector2<TReal>, TReal>.ToPolar(this);
    public Vector2 ToVector2() => Vector2Math<TestVector2<TReal>, TReal>.ToVector2(this);
    public Vector2f ToVector2f() => Vector2Math<TestVector2<TReal>, TReal>.ToVector2f(this);
    public Vector2d ToVector2d() => Vector2Math<TestVector2<TReal>, TReal>.ToVector2d(this);
    public override string ToString() => $"<{X}, {Y}>";
}
