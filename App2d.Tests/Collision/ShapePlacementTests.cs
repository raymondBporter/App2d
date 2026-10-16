using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Collision;

public sealed class ShapePlacementTests
{
    [Theory]
    [InlineData(0f, true, true)]
    [InlineData(1.5f, true, true)]
    [InlineData(2f, true, false)]
    [InlineData(2.01f, false, false)]
    public void OverlapIncludesTouchingWhileContactRequiresPenetration(float separation, bool overlaps, bool penetrates)
    {
        var shape = Circle2D.Builder.Create(1);
        var first = Similarity2D.FromTranslation(new(7, -3));
        var second = Similarity2D.FromTranslation(new(7 + separation, -3));
        Assert.Equal(overlaps, ShapeCollision2D.Overlaps(shape, first, shape, second));
        Assert.Equal(overlaps, ShapeCollision2D.Overlaps(shape, second, shape, first));
        Assert.Equal(penetrates, ShapeCollision2D.TryGetContact(shape, first, shape, second, out _));
    }

    [Fact]
    public void DirectQueriesAgreeWithPlacedObjectsIncludingMirroringScalingAndCompositeParts()
    {
        var shape = Capsule2D.Builder.Create(new(-1, -2), new(-1, 2), 1);
        var pose = Similarity2D.FromAxis(new(4, 3), Vector2.UnitY, mirror: true, scale: 2);
        var first = new SpatialObject2D(shape);
        first.Transform.CopyFrom(pose.ToAffine());
        var second = new SpatialObject2D(CompositeShape2D.Builder.Create([Circle2D.Builder.Create(2, new(4, 4)), Circle2D.Builder.Create(1, new(50, 50))]));
        Assert.True(ShapeCollision2D.Overlaps(shape, pose, second.Shape, second.CollisionPose));
        Assert.True(ShapeCollision2D.TryGetContact(first, second, out var expected));
        Assert.True(ShapeCollision2D.TryGetContact(shape, pose, second.Shape, second.CollisionPose, out var actual));
        Assert.Equal(expected, actual);
        Assert.True(ShapeCollision2D.TryGetContact(second.Shape, second.CollisionPose, shape, pose, out var reversed));
        Assert.Equal(-actual.Normal, reversed.Normal);
        Assert.Equal(actual.PenetrationDepth, reversed.PenetrationDepth);
    }
}
