using System.Numerics;
using App2d.Core.Mathematics;

namespace App2d.Tests.Mathematics;

public sealed class Similarity2DTests
{
    private static Matrix3x2 Trs(Vector2 scale, float rotation, Vector2 translation) =>
        Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(translation);

    [Fact]
    public void RoundTripsRotationScaleTranslation()
    {
        var matrix = Trs(new Vector2(2f, 2f), 0.7f, new Vector2(3f, -4f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));

        var point = new Vector2(1.5f, -2.5f);
        AssertClose(Vector2.Transform(point, matrix), pose.TransformPoint(point));
        AssertClose(point, pose.InverseTransformPoint(pose.TransformPoint(point)));
        Assert.Equal(2f, pose.Scale, 3);
    }

    [Fact]
    public void SupportsMirroring()
    {
        var matrix = Trs(new Vector2(-1f, 1f), 0.3f, new Vector2(5f, 0f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));

        var point = new Vector2(2f, 1f);
        AssertClose(Vector2.Transform(point, matrix), pose.TransformPoint(point));
        AssertClose(point, pose.InverseTransformPoint(pose.TransformPoint(point)));
    }

    [Fact]
    public void DirectionsIgnoreTranslation()
    {
        var matrix = Trs(new Vector2(3f, 3f), 1.1f, new Vector2(100f, 100f));
        Assert.True(Similarity2D.TryFromMatrix(matrix, out var pose));
        AssertClose(Vector2.TransformNormal(Vector2.UnitX, matrix), pose.TransformDirection(Vector2.UnitX));
    }

    [Fact]
    public void RejectsNonUniformScale() =>
        Assert.False(Similarity2D.TryFromMatrix(Trs(new Vector2(2f, 1f), 0f, Vector2.Zero), out _));

    [Fact]
    public void RejectsDegenerateScale() =>
        Assert.False(Similarity2D.TryFromMatrix(Trs(Vector2.Zero, 0f, Vector2.Zero), out _));

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }
}
