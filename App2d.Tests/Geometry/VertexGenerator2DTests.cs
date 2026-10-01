using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class VertexGenerator2DTests
{
    [Fact]
    public void EllipseSamplesCardinalPointsInCounterClockwiseOrder()
    {
        Span<Vector2> points = stackalloc Vector2[4];
        Assert.Equal(4, VertexGenerator2D.WriteEllipse(points, new(3, -2), new(4, 2)));
        Near(new(7, -2), points[0]);
        Near(new(3, 0), points[1]);
        Near(new(-1, -2), points[2]);
        Near(new(3, -4), points[3]);
        Assert.True(PolygonGeometry2D.SignedAreaTwice(points) > 0);
    }

    [Fact]
    public void ClockwiseArcIncludesBothEndpoints()
    {
        Span<Vector2> points = stackalloc Vector2[3];
        VertexGenerator2D.WriteArc(points, new(2, 3), new(4, 2), MathF.PI / 2, -MathF.PI / 2);
        Near(new(2, 5), points[0]);
        Near(new(2 + MathF.Sqrt(8), 3 + MathF.Sqrt(2)), points[1]);
        Near(new(6, 3), points[2]);
    }

    [Fact]
    public void RectangleWritesOnlyItsFourCorners()
    {
        Span<Vector2> points = stackalloc Vector2[5];
        points[4] = new(99);
        Assert.Equal(4, VertexGenerator2D.WriteRectangle(points, new(-2, -1), new(3, 4)));
        Assert.Equal(new Vector2(-2, -1), points[0]);
        Assert.Equal(new Vector2(3, -1), points[1]);
        Assert.Equal(new Vector2(3, 4), points[2]);
        Assert.Equal(new Vector2(-2, 4), points[3]);
        Assert.Equal(new Vector2(99), points[4]);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(.5f)]
    [InlineData(100f)]
    public void RoundedRectangleKeepsBoundsWindingAndSampleCount(float radius)
    {
        Span<Vector2> points = stackalloc Vector2[37];
        points[36] = new(99);
        var count = VertexGenerator2D.WriteRoundedRectangle(points, new(-2, -1), new(2, 1), radius);
        Assert.Equal(36, count);
        Assert.Equal(new Vector2(99), points[36]);
        Near(new(2, 1 - Math.Min(radius, 1)), points[0]);
        Assert.True(PolygonGeometry2D.SignedAreaTwice(points[..count]) > 0);
        foreach (var point in points[..count])
        {
            Assert.InRange(point.X, -2.00001f, 2.00001f);
            Assert.InRange(point.Y, -1.00001f, 1.00001f);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapsulePerimeterStaysOneRadiusFromItsSegment(bool collapsed)
    {
        var start = new Vector2(1, 2);
        var end = collapsed ? start : new Vector2(4, 6);
        Span<Vector2> points = stackalloc Vector2[19];
        points[18] = new(99);
        var count = VertexGenerator2D.WriteCapsule(points, start, end, 2, 8);
        Assert.Equal(18, count);
        Assert.Equal(new Vector2(99), points[18]);
        Assert.True(PolygonGeometry2D.SignedAreaTwice(points[..count]) > 0);
        foreach (var point in points[..count])
            Assert.Equal(2, Distance2D.DistanceToSegment(point, start, end), 5);
    }

    [Fact]
    public void GeneratorsRejectInvalidDimensionsAndUndersizedBuffers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VertexGenerator2D.WriteEllipse(new Vector2[2], default, Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => VertexGenerator2D.WriteCapsule(new Vector2[17], default, Vector2.One, 1, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => VertexGenerator2D.WriteCircle(new Vector2[8], default, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => VertexGenerator2D.WriteRectangle(new Vector2[4], Vector2.One, Vector2.Zero));
    }

    [Fact]
    public void ReusingVertexBuffersDoesNotAllocate()
    {
        Span<Vector2> points = stackalloc Vector2[64];
        Generate(points);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) Generate(points);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        static void Generate(Span<Vector2> buffer)
        {
            VertexGenerator2D.WriteCircle(buffer, default, 2);
            VertexGenerator2D.WriteRoundedRectangle(buffer, new(-2), new(2), 1);
            VertexGenerator2D.WriteCapsule(buffer, default, Vector2.UnitY, 1, 8);
        }
    }

    private static void Near(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < .00001f, $"Expected {expected}, got {actual}.");
}
