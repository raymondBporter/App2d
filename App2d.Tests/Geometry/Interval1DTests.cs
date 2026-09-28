using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Interval1DTests
{
    [Fact]
    public void ProjectsPolygonOntoAxis()
    {
        ReadOnlySpan<Vector2> square = [new(0f, 0f), new(2f, 0f), new(2f, 2f), new(0f, 2f)];
        var interval = Interval1D.ProjectPolygon(square, Vector2.UnitX);
        Assert.Equal(0f, interval.Min, 3);
        Assert.Equal(2f, interval.Max, 3);
    }

    [Fact]
    public void ProjectsCapsuleOntoAxisIncludingRadius()
    {
        var interval = Interval1D.ProjectCapsule(new Vector2(-1f, 0f), new Vector2(3f, 0f), 0.5f, Vector2.UnitX);
        Assert.Equal(-1.5f, interval.Min, 3);
        Assert.Equal(3.5f, interval.Max, 3);
    }
}
