using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Collision;

public sealed class CompositeCollisionIndexTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargeCompositeMatchesSmallCompositeIncludingPartOrderTiesAndTransforms(bool mirrored)
    {
        IConvexShape2D[] near = [Circle2D.Builder.Create(1f, new(1f, 0f)), Circle2D.Builder.Create(1f, new(-1f, 0f))];
        var small = CompositeShape2D.Builder.Create(near);
        var parts = near.Concat(Enumerable.Range(0, 64).Select(index => (IConvexShape2D)Circle2D.Builder.Create(1f, new(100f + index * 10f, 40f))));
        var large = CompositeShape2D.Builder.Create(parts);
        var pose = Similarity2D.FromAxis(new(20f, 30f), new(0.8f, 0.6f), mirror: mirrored, scale: 2f);
        var wheel = Circle2D.Builder.Create(0.75f);
        var wheelPose = Similarity2D.FromTranslation(pose.TransformPoint(Vector2.Zero));
        Assert.True(ShapeCollision2D.TryGetContact(wheel, wheelPose, small, pose, out var expected));
        Assert.True(ShapeCollision2D.TryGetContact(wheel, wheelPose, large, pose, out var actual));
        Assert.Equal(expected.PenetrationDepth, actual.PenetrationDepth, 5);
        Assert.InRange(Vector2.Distance(expected.Normal, actual.Normal), 0f, 0.00001f);
        Assert.InRange(Vector2.Distance(expected.Point, actual.Point), 0f, 0.00001f);
        Assert.False(ShapeCollision2D.TryGetContact(wheel, Similarity2D.FromTranslation(new(-1000f, -1000f)), large, pose, out _));
    }
}
