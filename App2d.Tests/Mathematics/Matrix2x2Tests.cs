using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Matrix2x2Tests
{
    [Fact]
    public void FloatRowsColumnsAndTransformsUseColumnVectors()
    {
        var matrix = new Matrix2x2f(1, 2, 3, 4);
        var point = new Vector2f(5, 7);
        var expected = new Vector2f(19, 43);

        Assert.Equal((new Vector2f(1, 2), new Vector2f(3, 4)), matrix.GetRows());
        Assert.Equal((new Vector2f(1, 3), new Vector2f(2, 4)), matrix.GetColumns());
        Assert.Equal(expected, matrix.TransformPoint(point));
        Assert.Equal(expected, matrix.TransformNormal(point));
        Assert.Equal(matrix.X, matrix * Vector2f.UnitX);
        Assert.Equal(matrix.Y, matrix * Vector2f.UnitY);
    }

    [Fact]
    public void FloatProductAppliesRightThenLeft()
    {
        var left = new Matrix2x2f(1, 2, 3, 4);
        var right = new Matrix2x2f(2, 0, 1, 3);
        var point = new Vector2f(5, 7);

        Assert.Equal(new Matrix2x2f(4, 6, 10, 12), left * right);
        Assert.Equal(left * (right * point), TransformComposed(left, right, point));
        Assert.Equal(left, left * Matrix2x2f.Identity);
        Assert.Equal(left, (2 * left) / 2);
        Assert.Equal(Matrix2x2f.Zero, left + -left);
        Assert.Equal(Matrix2x2f.Zero, left - left);
    }

    [Fact]
    public void DoubleTransformsAndProductsRetainDoublePrecision()
    {
        const double beyondFloatPrecision = 16_777_217;
        var left = new Matrix2x2d(beyondFloatPrecision, 2, 3, 4);
        var right = new Matrix2x2d(2, 0, 1, 3);
        var point = new Vector2d(1, 2);
        var transformed = new Vector2d(beyondFloatPrecision + 4, 11);

        Assert.Equal((new Vector2d(beyondFloatPrecision, 2), new Vector2d(3, 4)), left.GetRows());
        Assert.Equal((new Vector2d(beyondFloatPrecision, 3), new Vector2d(2, 4)), left.GetColumns());
        Assert.Equal(transformed, left.TransformPoint(point));
        Assert.Equal(transformed, left.TransformNormal(point));
        Assert.Equal(new Matrix2x2d(beyondFloatPrecision * 2 + 2, 6, 10, 12), left * right);
        Assert.Equal(left * (right * point), TransformComposed(left, right, point));
        Assert.Equal(left, left * Matrix2x2d.Identity);
        Assert.Equal(left, (2 * left) / 2);
        Assert.Equal(Matrix2x2d.Zero, left + -left);
        Assert.Equal(Matrix2x2d.Zero, left - left);
    }

    private static TVector TransformComposed<TVector, TReal>(
        Matrix2x2<TVector, TReal> left, Matrix2x2<TVector, TReal> right, TVector point)
        where TVector : unmanaged, IVector2<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => left * right * point;
}
