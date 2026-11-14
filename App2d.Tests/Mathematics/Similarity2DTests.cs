using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Similarity2DTests
{
    private static Matrix3x2 Trs(Vector2f scale, float rotation, Vector2f translation) =>
        Matrix3x2.CreateScale(scale.ToVector2()) * Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(translation.ToVector2());

    [Fact]
    public void FinitenessDetectsArithmeticOverflowAndDoesNotPromiseNonzeroScale()
    {
        Assert.True(Similarity2D.Identity.IsFinite);
        Assert.True(default(Similarity2D).IsFinite);
        Assert.False(Similarity2D.TryFromMatrix(default(Similarity2D).ToMatrix(), out _));

        var large = Similarity2D.FromTranslation(new(float.MaxValue, 0));
        Assert.True(large.IsFinite);
        Assert.False(large.Translated(new(float.MaxValue, 0)).IsFinite);
        Assert.False(large.ScaledBy(2).IsFinite);
        Assert.False(Similarity2D.FromAxis(Vector2f.Zero, Vector2f.UnitX, scale: float.MaxValue).ScaledBy(2).IsFinite);
    }

    [Fact]
    public void TranslationFactoryMatchesMatrixAndRejectsNonFinitePositions()
    {
        var position = new Vector2f(3f, -4f);
        var pose = Similarity2D.FromTranslation(position);

        Assert.Equal(Vector2f.UnitX, pose.XAxis);
        Assert.Equal(Vector2f.UnitY, pose.YAxis);
        Assert.Equal(1f, pose.Scale);
        AssertClose(Vector2f.Transform(new Vector2f(2f, 5f), Matrix3x2.CreateTranslation(position.ToVector2())),
            pose.TransformPoint(new Vector2f(2f, 5f)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Similarity2D.FromTranslation(new(float.NaN, 0f)));
    }

    [Fact]
    public void RoundTripsRotationScaleTranslation()
    {
        var matrix = Trs(new Vector2f(2f, 2f), 0.7f, new Vector2f(3f, -4f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));

        var point = new Vector2f(1.5f, -2.5f);
        AssertClose(Vector2f.Transform(point, matrix), pose.TransformPoint(point));
        AssertClose(point, pose.InverseTransformPoint(pose.TransformPoint(point)));
        Assert.Equal(2f, pose.Scale, 3);
    }

    [Fact]
    public void SupportsMirroring()
    {
        var matrix = Trs(new Vector2f(-1f, 1f), 0.3f, new Vector2f(5f, 0f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));

        var point = new Vector2f(2f, 1f);
        AssertClose(Vector2f.Transform(point, matrix), pose.TransformPoint(point));
        AssertClose(point, pose.InverseTransformPoint(pose.TransformPoint(point)));
    }

    [Fact]
    public void DirectionsIgnoreTranslation()
    {
        var matrix = Trs(new Vector2f(3f, 3f), 1.1f, new Vector2f(100f, 100f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));
        AssertClose(Vector2f.TransformNormal(Vector2f.UnitX, matrix), pose.TransformDirection(Vector2f.UnitX));
    }

    [Fact]
    public void RejectsNonUniformScale() =>
        Assert.False(Similarity2D.TryFromMatrix(Trs(new Vector2f(2f, 1f), 0f, Vector2f.Zero), out _));

    [Fact]
    public void RejectsDegenerateScale() =>
        Assert.False(Similarity2D.TryFromMatrix(Trs(Vector2f.Zero, 0f, Vector2f.Zero), out _));

    [Fact]
    public void RejectsEqualLengthShearedAxesAndNonFiniteMatrices()
    {
        Assert.False(Similarity2D.TryFromMatrix(new(1, 0, .6f, .8f, 0, 0), out _));
        Assert.False(Similarity2D.TryFromMatrix(new(float.NaN, 0, 0, 1, 0, 0), out _));
        Assert.False(Similarity2D.TryFromMatrix(new(1, 0, 0, 1, float.PositiveInfinity, 0), out _));
    }

    private static void AssertClose(Vector2f expected, Vector2f actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }
}
