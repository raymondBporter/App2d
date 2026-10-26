using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Vector2dTests
{
    [Fact]
    public void TransformUsesRowVectorsAndIncludesTranslation()
    {
        var matrix = new Matrix3x2(2, 3, 5, 7, 11, 13);
        Assert.Equal(new Vector2d(140, 197), Vector2d.Transform(new(17, 19), matrix));
    }

    [Fact]
    public void TransformPreservesCancellationAndSmallOffsetsAtLargeCoordinates()
    {
        var point = new Vector2d(16_777_216, 16_777_215);
        var matrix = new Matrix3x2(16_777_214, 1, -16_777_215, 0, 0, 1);
        Assert.Equal(new Vector2d(-1, 16_777_217), Vector2d.Transform(point, matrix));
    }

    [Fact]
    public void WideningBeforeArithmeticPreservesDifferencesThatFloatsLose()
    {
        var first = new Vector2(16777216, 3);
        var second = new Vector2(-1, 4);
        var difference = first.ToVector2d() - second.ToVector2d();
        Assert.Equal(new Vector2d(16777217, -1), difference);
        Assert.NotEqual((first - second).X, difference.X);
        Assert.Equal(first.ToVector2d(), difference + second.ToVector2d());
        Assert.Equal(new Vector2(16777216, -1), difference.ToVector2());
    }

    [Fact]
    public void OrientationPreservesDifferencesWhenPointsAreWidenedBeforeSubtracting()
    {
        var a = new Vector2(16_777_216, 0);
        var b = new Vector2(-1, 1);
        var c = new Vector2(0, 1);

        Assert.Equal(0f, a.Orientation(b, c));
        Assert.Equal(-1d, new Vector2d(a).Orientation(new(b), new(c)));
    }

    [Fact]
    public void DotCrossAndPerpendicularsUseTheSameOrientationAsVector2()
    {
        var vector = new Vector2d(3, 4);
        Assert.Equal(5, vector.Length());
        Assert.Equal(0, vector.Dot(vector.PerpCcw));
        Assert.Equal(25, vector.Cross(vector.PerpCcw));
        Assert.Equal(-25, vector.Cross(vector.PerpCw));
        Assert.Equal(-vector, vector.PerpCcw.PerpCcw);
        Assert.Equal(vector, (2 * vector) / 2);
        Assert.Equal(1, (vector / vector.Length()).Length());
        Assert.Equal(Vector2d.Zero, vector + -vector);
    }

    [Fact]
    public void DivisionPreservesScalarRoundingSignedZeroAndIeeeSpecialValues()
    {
        var minimumNormal = BitConverter.Int64BitsToDouble(0x0010_0000_0000_0000);
        double[] values = [0d, -0d, 1d, -1d, 3d, 1d / 3, double.Epsilon, -double.Epsilon,
            minimumNormal, -minimumNormal, double.MaxValue, double.MinValue,
            double.PositiveInfinity, double.NegativeInfinity, double.NaN];
        foreach (var value in values)
        {
            foreach (var scale in values)
            {
                var quotient = new Vector2d(value, -value) / scale;
                AssertSameBits(value / scale, quotient.X);
                AssertSameBits(-value / scale, quotient.Y);
            }
        }
    }

    [Fact]
    public void DivisionMatchesScalarArithmeticForRandomDoubleBitPatterns()
    {
        var random = new Random(1467);
        for (var i = 0; i < 1024; i++)
        {
            var x = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            var y = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            var scale = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            var quotient = new Vector2d(x, y) / scale;
            AssertSameBits(x / scale, quotient.X);
            AssertSameBits(y / scale, quotient.Y);
        }
    }

    private static void AssertSameBits(double expected, double actual)
    {
        if (double.IsNaN(expected))
        {
            Assert.True(double.IsNaN(actual));
        }
        else
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
    }

    [Fact]
    public void FloatExtremesHaveFiniteDoubleDistancesAndProducts()
    {
        var a = new Vector2(float.MaxValue, float.MaxValue).ToVector2d();
        var b = -a;
        var distance = Vector2d.Distance(a, b);
        Assert.True(double.IsFinite(distance));
        Assert.Equal(2 * Math.Sqrt(2) * float.MaxValue, distance, 1e24);
        Assert.True(double.IsFinite(Vector2d.DistanceSquared(a, b)));
        Assert.Equal(0, a.Dot(new(a.X, -a.Y)));
        Assert.Equal(0, a.Cross(a));

        var tiny = new Vector2(float.Epsilon, 0).ToVector2d();
        Assert.Equal(float.Epsilon, Vector2d.Distance(Vector2d.Zero, tiny));
        Assert.True(tiny.LengthSquared() > 0);
    }

    [Fact]
    public void NarrowingIsExplicitAndFollowsFloatRounding()
    {
        var value = new Vector2d(1d / 3, -(double)float.MaxValue * 2);
        Assert.True(value.IsFinite());
        Assert.Equal(new Vector2(1f / 3, float.NegativeInfinity), value.ToVector2());
        Assert.False(new Vector2d(double.NaN, 0).IsFinite());
        Assert.False(new Vector2d(0, double.PositiveInfinity).IsFinite());
    }
}
