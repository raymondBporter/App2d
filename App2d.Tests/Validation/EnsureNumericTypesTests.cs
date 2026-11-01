using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Tests.Validation;

public sealed class EnsureNumericTypesTests
{
    [Fact]
    public void ScalarChecksPreserveIntegerAndDecimalPrecision()
    {
        Assert.Equal(ulong.MaxValue - 1, Ensure.LessThan(ulong.MaxValue - 1, ulong.MaxValue));
        Assert.Equal(Int128.MaxValue, Ensure.GreaterThan(Int128.MaxValue, Int128.MaxValue - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThan(decimal.MaxValue, decimal.MaxValue));
        var large = BigInteger.One << 256;
        Assert.Equal(large, Ensure.GreaterThan(large, large - 1));
        Assert.Equal((Half).5, Ensure.Positive((Half).5));
        Assert.Equal((byte)0, Ensure.NonPositive((byte)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonPositive((byte)1));
        Assert.Equal(0m, Ensure.Equal(-0m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NotEqual(large, large));
    }

    [Fact]
    public void VectorComparisonsAndSignsApplyToEveryComponent()
    {
        var value = new Vector3(-3, -2, -1);
        Assert.Equal(value, Ensure.Negative(value));
        Assert.Equal(value, Ensure.LessThanOrEqual(value, new Vector3(-1)));
        Assert.Equal(value, Ensure.GreaterThanOrEqual(value, new Vector3(-3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThan(value, new Vector3(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThan(value, new Vector3(-3)));
        Assert.Equal(Vector4.Zero, Ensure.NonPositive(Vector4.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonPositive(new Vector4(0, 0, 0, float.Epsilon)));
        Assert.False(NumericValidation.IsNegative(new Vector2(-1, 0)));
        Assert.False(NumericValidation.IsNonPositive(new Vector4(-1, -1, -1, float.NaN)));
    }

    [Fact]
    public void FloatAndDoubleVectorChecksKeepTheirOriginalPrecision()
    {
        var tiny = new Vector4d(0, 0, 0, double.Epsilon);
        Assert.Equal(tiny, Ensure.NonZero(tiny));
        Assert.Equal(new Vector3d(double.MaxValue, 0, 0), Ensure.Finite(new Vector3d(double.MaxValue, 0, 0)));
        Assert.Equal(Vector3f.UnitZ, Ensure.NonZero(Vector3f.UnitZ));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Finite(new Vector3d(0, double.NaN, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Finite(new Vector4f(0, 0, 0, float.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(Vector2d.Zero));
    }

    [Fact]
    public void MatrixChecksDistinguishFiniteEntriesFromAFiniteInverse()
    {
        Ensure.Finite(default(Matrix3x2));
        Ensure.Finite(default(Matrix4x4));
        Assert.False(NumericValidation.IsInvertible(default(Matrix3x2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Invertible(default(Matrix4x4)));
        var affine = Matrix3x2.CreateScale(2, 4) * Matrix3x2.CreateTranslation(3, 5);
        Ensure.Invertible(affine, out var inverse);
        Assert.Equal(Matrix3x2.Identity, affine * inverse);
        var spatial = Matrix4x4.CreateScale(2, 4, 8) * Matrix4x4.CreateTranslation(3, 5, 7);
        Ensure.Invertible(spatial, out var spatialInverse);
        Assert.Equal(Matrix4x4.Identity, spatial * spatialInverse);
        var bad = Matrix4x4.Identity;
        bad.M34 = float.NaN;
        Assert.False(NumericValidation.IsNumber(bad));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Finite(bad));
    }

    [Fact]
    public void InfiniteOrderingRequiresOptInAndAlwaysRejectsNaN()
    {
        Ensure.LessThanOrEqual(double.NegativeInfinity, double.PositiveInfinity, allowInfinity: true);
        Ensure.GreaterThan(Vector2.One, new Vector2(float.NegativeInfinity), allowInfinity: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThan(1d, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThanOrEqual(double.NaN, double.NegativeInfinity, allowInfinity: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqual(Vector3.One, new Vector3(1, float.NaN, 1), allowInfinity: true));
    }
}
