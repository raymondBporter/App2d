using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Tests.Mathematics;

public sealed class IVectorTests
{
    [Fact]
    public void SharedGeometryWorksWithEveryDimensionAndPrecision()
    {
        CheckVector<Vector2f, float>(new(3, 4), Vector2f.UnitX, 5, 2);
        CheckVector<Vector3f, float>(new(3, 4, 12), Vector3f.UnitX, 13, 3);
        CheckVector<Vector4f, float>(new(1, 2, 2, 4), Vector4f.UnitX, 5, 4);
        CheckVector<Vector2d, double>(new(3, 4), Vector2d.UnitX, 5, 2);
        CheckVector<Vector3d, double>(new(3, 4, 12), Vector3d.UnitX, 13, 3);
        CheckVector<Vector4d, double>(new(1, 2, 2, 4), Vector4d.UnitX, 5, 4);
    }

    [Fact]
    public void ExtensionsInferBothPrecisionAndDimensionForInstanceAndStaticCalls()
    {
        Assert.Equal(5f, new Vector2f(3, 4).Length());
        Assert.Equal(5f, new Vector3f(0, 3, 4).Length());
        Assert.Equal(5f, new Vector4f(0, 0, 3, 4).Length());
        Assert.Equal(5d, new Vector2d(3, 4).Length());
        Assert.Equal(5d, new Vector3d(0, 3, 4).Length());
        Assert.Equal(5d, new Vector4d(0, 0, 3, 4).Length());

        Assert.Equal(5f, Vector2f.Distance(default, new(3, 4)));
        Assert.Equal(5f, Vector3f.Distance(default, new(0, 3, 4)));
        Assert.Equal(5f, Vector4f.Distance(default, new(0, 0, 3, 4)));
        Assert.Equal(5d, Vector2d.Distance(default, new(3, 4)));
        Assert.Equal(5d, Vector3d.Distance(default, new(0, 3, 4)));
        Assert.Equal(5d, Vector4d.Distance(default, new(0, 0, 3, 4)));

        Assert.Equal(Vector2f.UnitX, Vector2f.Normalize(new(7, 0)));
        Assert.Equal(Vector3f.UnitY, new Vector3f(0, 7, 0).Normalized());
        Assert.Equal(Vector4f.UnitW, new Vector4f(0, 0, 0, 7).Normalized());
        Assert.Equal(Vector2d.UnitX, Vector2d.Normalize(new(7, 0)));
        Assert.Equal(Vector3d.UnitY, new Vector3d(0, 7, 0).Normalized());
        Assert.Equal(Vector4d.UnitW, new Vector4d(0, 0, 0, 7).Normalized());

        Assert.Equal(25f, new Vector2f(3, 4).Dot(new(3, 4)));
        Assert.Equal(25d, new Vector4d(0, 0, 3, 4).Dot(new(0, 0, 3, 4)));
        Assert.Equal(new Vector3f(0, 2, 3), Vector3f.Reject(new(1, 2, 3), Vector3f.UnitX));
        Assert.Equal(new Vector4d(1, 1, 1, 1), Vector4d.Clamp(new(-1, 2, 3, 4), Vector4d.One, Vector4d.One));
        Assert.Equal(new Vector2d(2, 3), Vector2d.Lerp(new(1, 2), new(3, 4), .5));
    }

    [Fact]
    public void PackedFloatOperationsMatchSystemNumerics()
    {
        var random = new Random(7821);
        for (var i = 0; i < 128; i++)
        {
            var a = new Vector4(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()) * 100 - Vector4.One * 50;
            var b = new Vector4(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()) * 100 - Vector4.One * 50;
            var scale = random.NextSingle() * 20 - 10;
            var a2 = new Vector2(a.X, a.Y);
            var b2 = new Vector2(b.X, b.Y);
            var a3 = new Vector3(a.X, a.Y, a.Z);
            var b3 = new Vector3(b.X, b.Y, b.Z);

            Assert.Equal(a2 + b2, (new Vector2f(a2) + new Vector2f(b2)).ToVector2());
            Assert.Equal(a3 - b3, (new Vector3f(a3) - new Vector3f(b3)).ToVector3());
            Assert.Equal(a * scale, (new Vector4f(a) * scale).ToVector4());
            Assert.Equal(a2 / scale, (new Vector2f(a2) / scale).ToVector2());
            Assert.Equal(a3 / scale, (new Vector3f(a3) / scale).ToVector3());
            Assert.Equal(a / scale, (new Vector4f(a) / scale).ToVector4());
            Assert.Equal(Vector2.Dot(a2, b2), Vector2f.Dot(new(a2), new(b2)));
            Assert.Equal(Vector3.Dot(a3, b3), Vector3f.Dot(new(a3), new(b3)));
            Assert.Equal(Vector4.Dot(a, b), Vector4f.Dot(new(a), new(b)));
            Assert.Equal(Vector3.Cross(a3, b3), new Vector3f(a3).Cross(new(b3)).ToVector3());
            Assert.Equal(Vector2.Normalize(a2), new Vector2f(a2).Normalized().ToVector2());
            Assert.Equal(Vector3.Normalize(a3), new Vector3f(a3).Normalized().ToVector3());
            Assert.Equal(Vector4.Normalize(a), new Vector4f(a).Normalized().ToVector4());
        }
    }

    [Fact]
    public void FormattingPrintsCoordinatesWithoutFollowingVectorValuedProperties()
    {
        Assert.Equal("<1, 2>", new Vector2f(1, 2).ToString());
        Assert.Equal("<1, 2, 3>", new Vector3f(1, 2, 3).ToString());
        Assert.Equal("<1, 2, 3, 4>", new Vector4f(1, 2, 3, 4).ToString());
        Assert.Equal("<1, 2>", new Vector2d(1, 2).ToString());
        Assert.Equal("<1, 2, 3>", new Vector3d(1, 2, 3).ToString());
        Assert.Equal("<1, 2, 3, 4>", new Vector4d(1, 2, 3, 4).ToString());
    }

    [Fact]
    public void PrecisionConversionsAndStorageAreExplicit()
    {
        Assert.Equal(8, Unsafe.SizeOf<Vector2f>());
        Assert.Equal(12, Unsafe.SizeOf<Vector3f>());
        Assert.Equal(16, Unsafe.SizeOf<Vector4f>());
        Assert.Equal(16, Unsafe.SizeOf<Vector2d>());
        Assert.Equal(24, Unsafe.SizeOf<Vector3d>());
        Assert.Equal(32, Unsafe.SizeOf<Vector4d>());

        var value = new Vector4(16_777_216, 3, -16_777_216, 1);
        var wide = value.ToVector4d() - new Vector4d(-1, 0, 1, 0);
        Assert.Equal(new Vector4d(16_777_217, 3, -16_777_217, 1), wide);
        Assert.Equal(value, wide.ToVector4());
        Assert.Equal(value, value.ToVector4f().ToVector4());
        Assert.Equal(new Vector2(1, 2), new Vector2(1, 2).ToVector2f().ToVector2());
        Assert.Equal(new Vector3(1, 2, 3), new Vector3(1, 2, 3).ToVector3f().ToVector3());
    }

    [Fact]
    public void DoubleDivisionPreservesRoundingSignedZeroAndSpecialValues()
    {
        double[] values = [0d, -0d, 1d, -1d, 3d, double.Epsilon, -double.Epsilon,
            double.MaxValue, double.MinValue, double.PositiveInfinity, double.NegativeInfinity, double.NaN];
        foreach (var value in values)
        {
            foreach (var scale in values)
            {
                var v3 = new Vector3d(value, -value, value) / scale;
                var v4 = new Vector4d(value, -value, value, -value) / scale;
                for (var axis = 0; axis < 4; axis++)
                {
                    var expected = (axis % 2 == 0 ? value : -value) / scale;
                    SameBits(expected, v4[axis]);
                    if (axis < 3) SameBits(expected, v3[axis]);
                }
            }
        }
    }

    private static void CheckVector<TVector, TReal>(TVector value, TVector axis, TReal expectedLength, int dimension)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        Assert.Equal(dimension, TVector.Dimension);
        Assert.Equal(TVector.Zero, AdditiveZero<TVector>());
        Assert.Equal(expectedLength, IVectorExtensions.Length<TVector, TReal>(value));
        Assert.Equal(expectedLength * expectedLength, IVectorExtensions.LengthSquared<TVector, TReal>(value));

        var segment = new Segment<TVector>(TVector.Zero, value);
        Assert.Equal(expectedLength, IVectorExtensions.Distance<TVector, TReal>(segment.Start, segment.End));
        Assert.Equal(TVector.Zero, IVectorExtensions.Lerp<TVector, TReal>(segment.Start, segment.End, TReal.Zero));
        Assert.Equal(value, IVectorExtensions.Lerp<TVector, TReal>(segment.Start, segment.End, TReal.One));
        Assert.Equal(value * TReal.CreateChecked(2), IVectorExtensions.Lerp<TVector, TReal>(segment.Start, segment.End, TReal.CreateChecked(2)));

        var normalized = IVectorExtensions.Normalize<TVector, TReal>(value);
        var error = TReal.Abs(IVectorExtensions.Length<TVector, TReal>(normalized) - TReal.One);
        Assert.True(error < TReal.CreateChecked(1e-6));
        Assert.Equal(value / expectedLength, normalized);

        var parallel = IVectorExtensions.Project<TVector, TReal>(value, axis);
        var perpendicular = IVectorExtensions.Reject<TVector, TReal>(value, axis);
        Assert.Equal(TReal.Zero, TVector.Dot(perpendicular, axis));
        Assert.Equal(value, parallel + perpendicular);
        Assert.Equal(value, IVectorExtensions.Reflect<TVector, TReal>(IVectorExtensions.Reflect<TVector, TReal>(value, axis), axis));
        Assert.Equal(value, TVector.Abs(-value));
        Assert.Equal(TVector.Zero, IVectorExtensions.Clamp<TVector, TReal>(-value, TVector.Zero, value));
        Assert.Equal(value, IVectorExtensions.Clamp<TVector, TReal>(value * TReal.CreateChecked(2), TVector.Zero, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => value[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => value[dimension]);

        var zeroNormalized = IVectorExtensions.Normalize<TVector, TReal>(TVector.Zero);
        for (var i = 0; i < dimension; i++) Assert.True(TReal.IsNaN(zeroNormalized[i]));
        Assert.False(TVector.IsFinite(zeroNormalized));
        Assert.False(TVector.IsFinite(value * TReal.PositiveInfinity));
        var maximum = TReal.BitDecrement(TReal.PositiveInfinity);
        Assert.True(TVector.IsFinite(axis * maximum));
        Assert.True(TReal.IsPositiveInfinity(IVectorExtensions.Length<TVector, TReal>(axis * maximum)));
    }

    private static TVector AdditiveZero<TVector>() where TVector : IAdditiveIdentity<TVector, TVector> => TVector.AdditiveIdentity;

    private static void SameBits(double expected, double actual)
    {
        if (double.IsNaN(expected)) Assert.True(double.IsNaN(actual));
        else Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }
}
