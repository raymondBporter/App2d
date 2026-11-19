using System.Numerics;

namespace App2d.Tests.Mathematics;

/// <summary>
/// The least a 2D vector has to write: storage, Create, an indexer and its operators.
/// Tests use it to prove that Dot, Length, Cross, Rotate and the rest really are inherited.
/// </summary>
internal readonly record struct TestVector2<TReal>(TReal X, TReal Y) : IVector2<TestVector2<TReal>, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public static TestVector2<TReal> Create(TReal x, TReal y) => new(x, y);
    public TReal this[int index] => index switch { 0 => X, 1 => Y, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public static TestVector2<TReal> operator +(TestVector2<TReal> a, TestVector2<TReal> b) => new(a.X + b.X, a.Y + b.Y);
    public static TestVector2<TReal> operator -(TestVector2<TReal> a, TestVector2<TReal> b) => new(a.X - b.X, a.Y - b.Y);
    public static TestVector2<TReal> operator -(TestVector2<TReal> value) => new(-value.X, -value.Y);
    public static TestVector2<TReal> operator *(TestVector2<TReal> value, TReal scale) => new(value.X * scale, value.Y * scale);
    public static TestVector2<TReal> operator *(TReal scale, TestVector2<TReal> value) => value * scale;
    public static TestVector2<TReal> operator /(TestVector2<TReal> value, TReal scale) => new(value.X / scale, value.Y / scale);
    public override string ToString() => $"<{X}, {Y}>";
}
