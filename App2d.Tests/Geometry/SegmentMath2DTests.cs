using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class SegmentMath2DTests
{
    [Fact]
    public void GeometrySegmentUsesSharedLengthAndClampedSampling()
    {
        var segment = LineSegmentCurve2D.Create(new(1, 2), new(4, 6));

        Assert.Equal(5f, segment.Length());
        Assert.Equal(new Vector2(2.5f, 4), segment.PointAt(.5f));
        Assert.Equal(segment.Start, segment.Evaluate(-1));
        Assert.Equal(segment.End, segment.Evaluate(2));
    }

    [Fact]
    public void ReversingUsesTheConcreteSegmentsStaticFactory()
    {
        var segment = new FactorySegment(new(1, 2), new(4, 6));
        FactorySegment reversed = segment.Reversed();

        Assert.True(reversed.CreatedByFactory);
        Assert.Equal(segment.End, reversed.Start);
        Assert.Equal(segment.Start, reversed.End);
        Assert.Equal(5f, reversed.Length());
        Assert.Equal(new Vector2(2.5f, 4), reversed.PointAt(.5f));

        var line = LineSegmentCurve2D.Builder.Create(segment.Start, segment.End);
        LineSegmentCurve2D reversedLine = line.Reversed();
        Assert.Equal(LineSegmentCurve2D.Builder.Create(line.End, line.Start), reversedLine);
    }

    [Fact]
    public void CollapsedSegmentsHaveZeroLengthAndSampleTheirEndpoint()
    {
        var point = new Vector2(2, 3);
        var segment = LineSegmentCurve2D.Builder.Create(point, point);

        Assert.Equal(0f, segment.Length());
        Assert.Equal(point, segment.PointAt(.5f));
        Assert.Equal(segment, segment.Reversed());
    }

    private readonly record struct FactorySegment(Vector2 Start, Vector2 End, bool CreatedByFactory = false)
        : ISegment<FactorySegment, Vector2>
    {
        public static FactorySegment Create(Vector2 start, Vector2 end) => new(start, end, true);
    }
}
