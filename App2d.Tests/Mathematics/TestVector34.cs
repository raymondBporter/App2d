using System.Numerics;

namespace App2d.Tests.Mathematics;

/// <summary>The least a 3D vector has to write: storage, Create, an indexer and its operators.</summary>
internal readonly record struct TestVector3<TReal>(TReal X, TReal Y, TReal Z) : IVector3<TestVector3<TReal>, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public static TestVector3<TReal> Create(TReal x, TReal y, TReal z) => new(x, y, z);
    public TReal this[int index] => index switch { 0 => X, 1 => Y, 2 => Z, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public static TestVector3<TReal> operator +(TestVector3<TReal> a, TestVector3<TReal> b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static TestVector3<TReal> operator -(TestVector3<TReal> a, TestVector3<TReal> b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static TestVector3<TReal> operator -(TestVector3<TReal> value) => new(-value.X, -value.Y, -value.Z);
    public static TestVector3<TReal> operator *(TestVector3<TReal> value, TReal scale) => new(value.X * scale, value.Y * scale, value.Z * scale);
    public static TestVector3<TReal> operator *(TReal scale, TestVector3<TReal> value) => value * scale;
    public static TestVector3<TReal> operator /(TestVector3<TReal> value, TReal scale) => new(value.X / scale, value.Y / scale, value.Z / scale);
}

/// <summary>The least a 4D vector has to write: storage, Create, an indexer and its operators.</summary>
internal readonly record struct TestVector4<TReal>(TReal X, TReal Y, TReal Z, TReal W) : IVector4<TestVector4<TReal>, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public static TestVector4<TReal> Create(TReal x, TReal y, TReal z, TReal w) => new(x, y, z, w);
    public TReal this[int index] => index switch { 0 => X, 1 => Y, 2 => Z, 3 => W, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public static TestVector4<TReal> operator +(TestVector4<TReal> a, TestVector4<TReal> b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    public static TestVector4<TReal> operator -(TestVector4<TReal> a, TestVector4<TReal> b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
    public static TestVector4<TReal> operator -(TestVector4<TReal> value) => new(-value.X, -value.Y, -value.Z, -value.W);
    public static TestVector4<TReal> operator *(TestVector4<TReal> value, TReal scale) => new(value.X * scale, value.Y * scale, value.Z * scale, value.W * scale);
    public static TestVector4<TReal> operator *(TReal scale, TestVector4<TReal> value) => value * scale;
    public static TestVector4<TReal> operator /(TestVector4<TReal> value, TReal scale) => new(value.X / scale, value.Y / scale, value.Z / scale, value.W / scale);
}
