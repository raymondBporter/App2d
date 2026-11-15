using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Mathematics;

namespace App2d.Tests.Geometry;

public sealed class ContainingCapsuleTests
{
    [Fact]
    public void RectangleFitImprovesOnTheMinimumWidthRadius()
    {
        Vector2f[] points = [new(-4, -1), new(4, -1), new(4, 1), new(-4, 1)];

        var capsule = ShapeBounds2D.GetContainingCapsule(points);

        Assert.All(points, point => Assert.True(capsule.ContainsPoint(point)));
        Assert.InRange(capsule.Area, 16f, 18.9f);
        Assert.InRange(capsule.Radius, 1.001f, 1.1f);
    }

    [Fact]
    public void ThinRotatedRectangleFitsAlongItsEdges()
    {
        var offset = new Vector2f(11, -7);
        Vector2f[] corners = [new(-6, -.2f), new(6, -.2f), new(6, .2f), new(-6, .2f)];
        var points = corners.Select(point => point.Rotate(.37f) + offset).ToArray();

        var capsule = ShapeBounds2D.GetContainingCapsule(points);
        var withInterior = ShapeBounds2D.GetContainingCapsule([points[2], offset, points[0], points[3], points[1], points[2]]);

        Assert.All(points, point => Assert.True(capsule.ContainsPoint(point)));
        Assert.Equal(capsule, withInterior);
        Assert.InRange(capsule.Area, 4.8f, 4.95f);
    }

    [Fact]
    public void PointAndCollinearFitsUseTheRadiusFloor()
    {
        var point = new Vector2f(2, 3);
        var circle = ShapeBounds2D.GetContainingCapsule([point, point], minimumRadius: 5f);
        Assert.Equal(point, circle.Start);
        Assert.Equal(point, circle.End);
        Assert.Equal(5f, circle.Radius);

        Vector2f[] collinear = [new(4, 2), new(-4, 2), new(0, 2)];
        var capsule = ShapeBounds2D.GetContainingCapsule(collinear, minimumRadius: .5f);
        Assert.Equal(new Vector2f(-3.5f, 2), capsule.Start);
        Assert.Equal(new Vector2f(3.5f, 2), capsule.End);
        Assert.Equal(.5f, capsule.Radius);
        Assert.All(collinear, point => Assert.True(capsule.ContainsPoint(point)));
    }

    [Fact]
    public void ContainmentWorksWhenFloatSquaredDistancesUnderflowOrOverflow()
    {
        Vector2f[] tiny = [new(-4e-25f, -1e-25f), new(4e-25f, -1e-25f), new(4e-25f, 1e-25f), new(-4e-25f, 1e-25f)];
        var tinyCapsule = ShapeBounds2D.GetContainingCapsule(tiny);
        Assert.All(tiny, point => Assert.True(tinyCapsule.ContainsPoint(point)));
        Assert.InRange(tinyCapsule.Radius, 1e-25f, 1.1e-25f);
        Assert.False(tinyCapsule.ContainsPoint(new(0, 2e-25f)));

        const float large = float.MaxValue;
        Vector2f[] huge = [new(-large, -large), new(large, -large), new(large, large), new(-large, large)];
        var hugeCapsule = ShapeBounds2D.GetContainingCapsule(huge);
        Assert.Equal(large, hugeCapsule.Radius);
        Assert.All(huge, point => Assert.True(hugeCapsule.ContainsPoint(point)));
    }

    [Fact]
    public void RejectsEmptyNonfiniteInputAndNonpositiveRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShapeBounds2D.GetContainingCapsule([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShapeBounds2D.GetContainingCapsule([new(float.NaN, 0)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShapeBounds2D.GetContainingCapsule([Vector2f.Zero], minimumRadius: 0f));
    }
}
