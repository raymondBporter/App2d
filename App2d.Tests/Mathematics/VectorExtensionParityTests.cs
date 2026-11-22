using System.Reflection;

namespace App2d.Tests.Mathematics;

/// <summary>
/// VectorExtensions.Float.cs and VectorExtensions.Double.cs are the same file with the scalar swapped,
/// because C# cannot infer the scalar from a constraint. These tests fail when one gains or loses a
/// member the other does not have, and when a struct stops reaching its own faster override.
/// </summary>
public sealed class VectorExtensionParityTests
{
    [Theory]
    [InlineData(typeof(FloatVectorMembers), typeof(DoubleVectorMembers))]
    [InlineData(typeof(FloatVectorStatics), typeof(DoubleVectorStatics))]
    [InlineData(typeof(FloatVector3Members), typeof(DoubleVector3Members))]
    [InlineData(typeof(FloatVector3Statics), typeof(DoubleVector3Statics))]
    public void FloatAndDoubleBlocksOfferTheSameMembers(Type floatBlock, Type doubleBlock)
    {
        Assert.NotEmpty(Signatures(floatBlock));
        Assert.Equal(Signatures(floatBlock), Signatures(doubleBlock));
    }

    [Fact]
    public void EveryInheritedOperationIsReachableAsBothAnInstanceAndAStaticCall()
    {
        // Minimal struct: nothing but storage and operators, so every call below is inherited.
        var value = new TestVector2<double>(3, 4);
        var other = new TestVector2<double>(1, 0);

        Assert.Equal(5d, value.Length());
        Assert.Equal(5d, TestVector2<double>.Length(value));
        Assert.Equal(3d, value.Dot(other));
        Assert.Equal(3d, TestVector2<double>.Dot(value, other));
        Assert.Equal(-4d, value.Cross(other));
        Assert.Equal(new TestVector2<double>(1, 0), value.Min(other));
        Assert.Equal(new TestVector2<double>(2, 2), value.Midpoint(other));
        Assert.Equal(new TestVector2<double>(3, 0), value.Project(other));
        Assert.Equal(new TestVector2<double>(0, 4), TestVector2<double>.Reject(value, other));
        Assert.Equal(new TestVector2<double>(-4, 3), value.PerpCcw);
        Assert.Equal(new Vector2d(3, 4), value.ToVector2d());
        Assert.Equal(default, ZeroOf<TestVector2<double>, double>());
    }

    [Fact]
    public void ThreeAndFourCoordinateVectorsInheritEverythingButStorageAndOperators()
    {
        var x = new TestVector3<double>(1, 0, 0);
        var y = new TestVector3<double>(0, 1, 0);
        var value = new TestVector3<double>(2, -3, 6);
        Assert.Equal(new TestVector3<double>(0, 0, 1), x.Cross(y));
        Assert.Equal(new TestVector3<double>(0, 0, -1), TestVector3<double>.Cross(y, x));
        Assert.Equal(7d, value.Length());
        Assert.Equal(2d, value.Dot(x));
        Assert.Equal(new TestVector3<double>(2, 3, 6), value.Abs());
        Assert.Equal(new TestVector3<double>(0, -3, 0), value.Min(y));
        Assert.Equal(new TestVector3<double>(1, 0.5, 0), TestVector3<double>.Midpoint(new(2, 0, 0), y));
        Assert.False(new TestVector3<double>(0, 0, double.NaN).IsFinite());
        Assert.Equal(default, ZeroOf<TestVector3<double>, double>());

        var four = new TestVector4<float>(1, -2, 2, -4);
        Assert.Equal(5f, four.Length());
        Assert.Equal(25f, TestVector4<float>.Dot(four, four));
        Assert.Equal(new TestVector4<float>(1, 2, 2, 4), four.Abs());
        Assert.Equal(new TestVector4<float>(1, 0, 2, 0), four.Max(default));
        Assert.False(new TestVector4<float>(0, 0, 0, float.PositiveInfinity).IsFinite());
        Assert.Equal(default, ZeroOf<TestVector4<float>, float>());
    }

    [Theory]
    [InlineData(1f, 2f, 3f, 4f, -5f, 6f)]
    [InlineData(-3e10f, 0f, 2e-10f, 7f, 8f, -9f)]
    public void PackedFloatVersionsAgreeWithTheInheritedFormulas(float ax, float ay, float az, float bx, float by, float bz)
    {
        var a = new Vector3f(ax, ay, az);
        var b = new Vector3f(bx, by, bz);
        var plainA = new TestVector3<float>(ax, ay, az);
        var plainB = new TestVector3<float>(bx, by, bz);

        Assert.Equal(plainA.Dot(plainB), a.Dot(b), 0.0001f * MathF.Abs(a.Dot(b)) + 1e-6f);
        Assert.Equal(Plain(plainA.Cross(plainB)), Vector3f.Cross(a, b));
        Assert.Equal(Plain(plainA.Min(plainB)), Vector3f.Min(a, b));
        Assert.Equal(Plain(plainA.Max(plainB)), Vector3f.Max(a, b));
        Assert.Equal(Plain(plainA.Abs()), Vector3f.Abs(a));
        Assert.Equal(Plain(TestVector3<float>.Clamp(plainA, plainB.Min(plainA), plainB.Max(plainA))), Vector3f.Clamp(a, Vector3f.Min(a, b), Vector3f.Max(a, b)));
        Assert.Equal(plainA.IsFinite(), a.IsFinite());

        static Vector3f Plain(TestVector3<float> value) => new(value.X, value.Y, value.Z);
    }

    [Fact]
    public void GenericCodeReachesTheStructsOwnPackedVersion()
    {
        // Vector2f's System.Numerics override is what runs, generic call or not,
        // so all three spellings must agree bit for bit.
        var value = new Vector2f(3e18f, 4e18f);
        Assert.Equal(value.Length(), Vector2f.Length(value));
        Assert.Equal(value.Length(), LengthOf<Vector2f, float>(value));
        Assert.Equal(Vector2f.Normalize(value), NormalizeOf<Vector2f, float>(value));
    }

    private static TReal LengthOf<TVector, TReal>(TVector value)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, System.Numerics.IFloatingPointIeee754<TReal> => TVector.Length(value);

    private static TVector NormalizeOf<TVector, TReal>(TVector value)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, System.Numerics.IFloatingPointIeee754<TReal> => TVector.Normalize(value);

    private static TVector ZeroOf<TVector, TReal>()
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, System.Numerics.IFloatingPointIeee754<TReal> => TVector.Zero;

    private static string[] Signatures(Type block) => [.. block
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Select(method => $"{method.Name}/{method.GetParameters().Length}")
        .Order(StringComparer.Ordinal)];
}
