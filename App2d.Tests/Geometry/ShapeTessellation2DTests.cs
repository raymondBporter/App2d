using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeTessellation2DTests
{
    public static TheoryData<IShape2D> FiniteShapes =>
    [
        Circle2D.Builder.Create(2), Ellipse2D.Builder.Create(new(2, 1)),
        Capsule2D.Builder.Create(new(-1, 0), new(1, 0), 1),
        Rectangle2D.Builder.Create(Vector2.Zero, new(4, 3)), AxisAlignedRectangle2D.Builder.Create(Vector2.Zero, new(4, 3)),
        RoundedRectangle2D.Builder.Create(Vector2.Zero, new(4, 3), .5f),
        Triangle2D.Builder.Create(Vector2.Zero, new(4, 0), new(0, 3)),
        ConvexPolygon2D.Builder.Create([Vector2.Zero, new(2, 0), new(3, 1), new(0, 2)]),
        SimplePolygon2D.Builder.Create([Vector2.Zero, new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)])
    ];

    [Theory]
    [MemberData(nameof(FiniteShapes))]
    public void ShapeTriangleAndSegmentViewsAgreeWithItsAreaAndOutline(IShape2D shape)
    {
        Span<Vector2> buffer = stackalloc Vector2[260];
        var area = 0d;
        foreach (var triangle in shape.Triangles(buffer, 64)) area += triangle.GetArea();
        Assert.InRange(area, shape.Area * .98, shape.Area * 1.001);
        var count = 0;
        foreach (var segment in shape.OutlineSegments(buffer, 64))
        {
            Assert.True(float.IsFinite(segment.Start.X));
            count++;
        }
        Assert.Equal(shape.GetOutlineVertCount(64), count);
    }

    [Fact]
    public void ConcaveTrianglesReuseTopologyAndLeaveTheNotchEmpty()
    {
        IShape2D shape = SimplePolygon2D.Builder.Create([Vector2.Zero, new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)]);
        foreach (var triangle in shape.Triangles([])) Assert.False(triangle.Contains(new(2, 2)));
        Assert.Equal(4, shape.Triangles([]).Count);
        Assert.Equal(6, shape.OutlineSegments([]).Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(16)]
    public void RoundedCornerSamplingIsConfigurableAndRespectsCallerStorage(int segments)
    {
        IShape2D shape = RoundedRectangle2D.Builder.Create(Vector2.Zero, new(4, 3), .5f);
        var count = shape.GetOutlineVertCount(cornerSegments: segments);
        Assert.Equal(4 * (segments + 1), count);
        var buffer = Enumerable.Repeat(new Vector2(99), count + 1).ToArray();
        var outline = shape.GetOutlineVerts(buffer, cornerSegments: segments);
        Assert.Equal(count, outline.Length);
        Assert.Equal(new Vector2(99), buffer[^1]);
        Assert.Equal(count - 2, shape.Triangles(buffer, cornerSegments: segments).Count);
        Assert.Equal(count, shape.OutlineSegments(buffer, cornerSegments: segments).Count);
    }

    [Theory]
    [InlineData(-10, 0, 0d)]
    [InlineData(0, 1, 12d)]
    [InlineData(10, 0, 24d)]
    public void ClippedHalfSpaceFillAndTrueBoundaryStaySeparate(float offset, int boundaryCount, double expectedArea)
    {
        IShape2D shape = HalfSpace2D.Builder.Create(Vector2.UnitY, offset);
        var bounds = new Rect2D(new(-3, -2), new(3, 2));
        Span<Vector2> buffer = stackalloc Vector2[5];
        var area = 0d;
        foreach (var triangle in shape.Triangles(buffer, clipBounds: bounds)) area += triangle.GetArea();
        Assert.Equal(expectedArea, area, 6);
        var segments = shape.OutlineSegments(buffer, clipBounds: bounds);
        Assert.Equal(boundaryCount, segments.Count);
        foreach (var segment in segments)
        {
            Assert.Equal(offset, segment.Start.Y);
            Assert.Equal(offset, segment.End.Y);
            Assert.Equal(6f, segment.Length());
        }
        Assert.Throws<ArgumentException>(() => shape.Triangles(new Vector2[5]));
        Assert.Throws<ArgumentException>(() => shape.OutlineSegments(new Vector2[5]));
    }

    [Fact]
    public void CurveQualityRespondsToScaleAndRemainsBoundedAtExtremeSizes()
    {
        IShape2D circle = Circle2D.Builder.Create(10);
        Assert.True(circle.RoundSegmentsFor(Matrix3x2.CreateScale(10)) > circle.RoundSegmentsFor(Matrix3x2.Identity));
        Assert.Equal(128, ShapeTessellation2D.CurveSegments(float.MaxValue, Matrix3x2.CreateScale(float.MaxValue)));
        Assert.Equal(16, ShapeTessellation2D.CurveSegments(0, Matrix3x2.Identity));
        Assert.Equal(16, ShapeTessellation2D.CurveSegments(0, Matrix3x2.CreateScale(float.MaxValue)));
    }

    [Fact]
    public void RectangleInterfaceShapesUseTheSharedOutlineAndTrianglePath()
    {
        IShape2D rectangle = new CustomRectangle();
        Span<Vector2> scratch = stackalloc Vector2[4];
        Assert.Equal(4, rectangle.GetVertCount());
        Assert.Equal(4, rectangle.OutlineSegments(scratch).Count);
        var area = 0d;
        foreach (var triangle in rectangle.Triangles(scratch)) area += triangle.GetArea();
        Assert.Equal(rectangle.Area, area);
    }

    [Theory]
    [InlineData(LineCap2D.Butt, 20d)]
    [InlineData(LineCap2D.Square, 24d)]
    [InlineData(LineCap2D.Round, 23.09d)]
    public void ThickSegmentsProduceReusablePackedTriangles(LineCap2D cap, double expectedArea)
    {
        Span<Vector2> buffer = stackalloc Vector2[StrokeTessellation2D.MaximumSegmentVertices + 1];
        buffer.Fill(new(99));
        var count = StrokeTessellation2D.WriteSegment(buffer, Vector2.Zero, new(10, 0), 2, cap, cap);
        var area = 0d;
        foreach (var triangle in MeshTopology.TriangleList<Vector2>(buffer[..count])) area += triangle.GetArea();
        Assert.InRange(area, expectedArea - .01, expectedArea + .01);
        Assert.Equal(new Vector2(99), buffer[count]);
    }

    [Fact]
    public void TaperedSegmentsProduceTrapezoidPackedTriangles()
    {
        Span<Vector2> buffer = stackalloc Vector2[StrokeTessellation2D.MaximumSegmentVertices];
        var count = StrokeTessellation2D.WriteSegment(buffer, Vector2.Zero, new(10, 0), startWidth: 2, endWidth: 4, LineCap2D.Butt, LineCap2D.Butt);
        Assert.Equal(6, count);
        var area = 0d;
        foreach (var triangle in MeshTopology.TriangleList<Vector2>(buffer[..count])) area += triangle.GetArea();
        Assert.InRange(area, 29.99, 30.01);
    }

    [Theory]
    [InlineData(LineCap2D.Butt, 0)]
    [InlineData(LineCap2D.Square, 6)]
    [InlineData(LineCap2D.Round, 60)]
    public void CollapsedSegmentsFollowCapPolicy(LineCap2D cap, int expectedVertices)
    {
        Span<Vector2> buffer = stackalloc Vector2[StrokeTessellation2D.MaximumSegmentVertices];
        Assert.Equal(expectedVertices, StrokeTessellation2D.WriteSegment(buffer, Vector2.One, Vector2.One, 2, cap, cap));
    }

    [Fact]
    public void WarmSamplingAndStrokeIterationDoesNotAllocate()
    {
        IShape2D shape = Circle2D.Builder.Create(2);
        _ = Sum(shape);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0d;
        for (var i = 0; i < 100; i++) sum += Sum(shape);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0);
        Assert.Equal(0, allocated);

        static double Sum(IShape2D shape)
        {
            Span<Vector2> scratch = stackalloc Vector2[128];
            var sum = 0d;
            foreach (var triangle in shape.Triangles(scratch)) sum += triangle.GetArea();
            foreach (var segment in shape.OutlineSegments(scratch)) sum += segment.Length();
            var count = StrokeTessellation2D.WriteSegment(scratch, Vector2.Zero, Vector2.One, 1, LineCap2D.Round, LineCap2D.Round);
            foreach (var triangle in MeshTopology.TriangleList<Vector2>(scratch[..count])) sum += triangle.GetArea();
            return sum;
        }
    }

    [Fact]
    public void CachedTriangulationTracksEditedListsAndToleranceWithoutReturningStaleGeometry()
    {
        List<Vector2> points = [Vector2.Zero, new(2, 0), new(2, 2), new(0, 2)];
        var original = PolygonTriangulationCache2D.Get(points);
        Assert.Same(original, PolygonTriangulationCache2D.Get(points));
        points[1] = new(3, 0);
        var edited = PolygonTriangulationCache2D.Get(points);
        Assert.NotSame(original, edited);
        Assert.NotEqual(original.Area, edited.Area);
        points.RemoveAt(3);
        var resized = PolygonTriangulationCache2D.Get(points);
        Assert.Equal(1, resized.TriangleCount);
        Assert.NotSame(resized, PolygonTriangulationCache2D.Get(points, 1e-8));
        points[1] = new(float.NaN, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => PolygonTriangulationCache2D.Get(points));
    }

    [Fact]
    public void RestTopologyCacheIgnoresDepthChangesButObservesXYChanges()
    {
        List<Vector3> points = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
        var original = PolygonTriangulationCache2D.GetXY(points);
        points[1] += new Vector3(0, 0, 3);
        Assert.Same(original, PolygonTriangulationCache2D.GetXY(points));
        points[1] += new Vector3(2, 0, 0);
        Assert.NotSame(original, PolygonTriangulationCache2D.GetXY(points));
    }

    private readonly record struct CustomRectangle : IShape2D, IRect2D
    {
        public string Kind => "test-rectangle";
        public Vector2 Min => Vector2.Zero;
        public Vector2 Max => new(3, 2);
        public float Area => 6;
        public bool ContainsPoint(Vector2 point) => this.Contains(point);
    }
}
