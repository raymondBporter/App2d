using App2d.Core.Mathematics.Transforms;

namespace App2d.Tests.Mathematics;

public sealed class Similarity2DPoseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AxisPosesRoundTripThroughAffineTransformsAndMatrices(bool mirror)
    {
        var pose = Similarity2D.FromAxis(new(3, -4), new(1, 2), mirror, scale: 1.5f);
        Assert.Equal(1.5f, pose.Scale, 5);
        Assert.Equal(mirror, pose.IsMirrored);
        Assert.Equal(0f, Vector2f.Dot(pose.XAxis, pose.YAxis), 5);
        Assert.Equal(1.5f, pose.YAxis.Length(), 5);

        var transform = pose.ToAffine();
        Assert.True(Similarity2D.TryFromMatrix(transform.Matrix, out var rebuilt));
        var point = new Vector2f(.7f, -1.3f);
        Assert.True(Vector2f.Distance(pose.TransformPoint(point), rebuilt.TransformPoint(point)) < 1e-4f);
        Assert.True(Vector2f.Distance(pose.TransformPoint(point), Vector2f.Transform(point, pose.ToMatrix())) < 1e-5f);
        Assert.True(Vector2f.Distance(point, pose.InverseTransformPoint(pose.TransformPoint(point))) < 1e-5f);
    }

    [Fact]
    public void MirroredAxisKeepsUpPointingUpWhileARotatedAxisFlipsIt()
    {
        var facingLeft = Similarity2D.FromAxis(Vector2f.Zero, new(-1, 0), mirror: true);
        Assert.Equal(Vector2f.UnitY, facingLeft.YAxis);
        Assert.Equal(new Vector2f(-2, 3), facingLeft.TransformPoint(new(2, 3)));
        var turnedAround = Similarity2D.FromAxis(Vector2f.Zero, new(-1, 0));
        Assert.Equal(-Vector2f.UnitY, turnedAround.YAxis);
        Assert.False(turnedAround.IsMirrored);
    }

    [Fact]
    public void TranslationAndScalingComposeAboutTheOrigin()
    {
        var pose = Similarity2D.FromAxis(new(1, 1), Vector2f.UnitX).Translated(new(2, 0)).ScaledBy(3);
        Assert.Equal(new Vector2f(9, 3), pose.Translation);
        Assert.Equal(3f, pose.Scale);
        Assert.Equal(new Vector2f(12, 6), pose.TransformPoint(new(1, 1)));
        Assert.Equal(Similarity2D.Identity, Similarity2D.FromTranslation(Vector2f.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => pose.ScaledBy(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Similarity2D.FromAxis(Vector2f.Zero, Vector2f.Zero));
    }
}
