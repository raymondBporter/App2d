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
        Assert.Equal(new Vector2(2.5f, 4), segment.Midpoint);
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
        Assert.Equal(segment.Midpoint, reversed.Midpoint);

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
        Assert.Equal(point, segment.Midpoint);
    }

    [Fact]
    public void PlainSegmentsGetMidpointWithoutASeparateVectorInterface()
    {
        var segment = new Segment<Vector2>(new(-3, 7), new(8, -2));
        Assert.Equal(new Vector2(2.5f, 2.5f), segment.Midpoint);
        var large = new Segment<Vector2>(new(float.MaxValue), new(float.MaxValue));
        Assert.Equal(new Vector2(float.MaxValue), large.Midpoint);
        var opposite = new Segment<Vector2>(new(float.MaxValue), new(-float.MaxValue));
        Assert.Equal(Vector2.Zero, opposite.Midpoint);
        var tiny = new Segment<Vector2>(new(float.Epsilon, 2 * float.Epsilon), new(float.Epsilon, 4 * float.Epsilon));
        Assert.Equal(new Vector2(float.Epsilon, 3 * float.Epsilon), tiny.Midpoint);
    }

    private readonly record struct FactorySegment(Vector2 Start, Vector2 End, bool CreatedByFactory = false)
        : ISegment<FactorySegment, Vector2>
    {
        public static FactorySegment Create(Vector2 start, Vector2 end) => new(start, end, true);
    }
}
