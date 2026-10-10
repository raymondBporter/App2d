using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeVertices2DTests
{
    public static TheoryData<IConvexShape2D> ExactCoreShapes =>
    [
        new Circle2D(1.5f, new(1, 2)),
        new Capsule2D(new(-2, 1), new(3, 2), .3f),
        new Rectangle2D(new(-1, -2), new(3, 1)),
        new AxisAlignedRectangle2D(new(-1, -2), new(3, 1)),
        new RoundedRectangle2D(new(-2, -1), new(2, 1), .5f),
        RoundedRectangle2D.FromSize(new(2, 4), 1, new(1, 2)),
        RoundedRectangle2D.FromSize(new(4, 2), 1, new(1, 2)),
        RoundedRectangle2D.FromSize(new(2, 2), 1, new(1, 2)),
        new Triangle2D(new(0, 0), new(2, 0), new(1, 3)),
        new ConvexPolygon2D([new(0, 0), new(2, 0), new(3, 1), new(1, 2)])
    ];

    [Theory]
    [MemberData(nameof(ExactCoreShapes))]
    public void ExpandingCoreByRadiusMatchesTheExactShapeSupportInEveryDirection(IConvexShape2D shape)
    {
        Span<Vector2> buffer = stackalloc Vector2[shape.GetVertCount()];
        var core = shape.GetVerts(buffer, out var radius);
        for (var i = 0; i < 32; i++)
        {
            var angle = i * MathF.Tau / 32;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 3;
            var projection = float.NegativeInfinity;
            foreach (var vertex in core) projection = MathF.Max(projection, Vector2.Dot(vertex, direction));
            projection += radius * direction.Length();
            Assert.InRange(MathF.Abs(projection - Vector2.Dot(shape.GetSupportPoint(direction), direction)), 0, 1e-4f);
        }
    }

    [Theory]
    [MemberData(nameof(ExactCoreShapes))]
    public void WorldAdapterTransformsTheSameCoreAndScalesItsRadius(IConvexShape2D shape)
    {
        Span<Vector2> localBuffer = stackalloc Vector2[shape.GetVertCount()];
        var local = shape.GetVerts(localBuffer, out var localRadius);
        var pose = Similarity2D.FromAxis(new(7, -3), new(1, 2), mirror: true, scale: 2.5f);
        Span<Vector2> world = stackalloc Vector2[local.Length];
        Assert.Equal(local.Length, WorldShape2D.WriteWorldConvexCore(shape, pose, world, out var worldRadius));
        Assert.Equal(localRadius * pose.Scale, worldRadius);
        for (var i = 0; i < local.Length; i++) Assert.Equal(pose.TransformPoint(local[i]), world[i]);
    }

    [Fact]
    public void RoundedRectanglesReturnInsetCornersOrACollapsedSegmentOrPoint()
    {
        Span<Vector2> buffer = stackalloc Vector2[4];
        var rounded = new RoundedRectangle2D(new(-2, -1), new(2, 1), .5f);
        var core = rounded.GetVerts(buffer, out var radius);
        Assert.Equal(.5f, radius);
        Assert.Equal([new(-1.5f, -.5f), new(1.5f, -.5f), new(1.5f, .5f), new(-1.5f, .5f)], core.ToArray());

        var capsule = RoundedRectangle2D.FromSize(new(4, 2), 1);
        Assert.Equal([new(-1, 0), new(1, 0)], capsule.GetVerts(buffer, out radius).ToArray());
        Assert.Equal(1f, radius);
        var circle = RoundedRectangle2D.FromSize(new(2, 2), 1, new(3, 4));
        Assert.Equal([new(3, 4)], circle.GetVerts(buffer, out radius).ToArray());
        Assert.Equal(1f, radius);
    }

    [Fact]
    public void ReturnedSpansUseTheCallerBufferAndLeaveUnusedEntriesAlone()
    {
        Span<Vector2> buffer = stackalloc Vector2[40];
        var sentinel = new Vector2(123, 456);
        buffer.Fill(sentinel);
        var circle = new Circle2D(2, new(1, 3));
        var core = circle.GetVerts(buffer, out var radius);
        Assert.Equal(1, core.Length);
        Assert.Equal(2f, radius);
        Assert.Equal(circle.Center, core[0]);
        Assert.Equal(sentinel, buffer[1]);
        buffer[0] = sentinel;
        Assert.Equal(sentinel, core[0]);

        var outline = circle.GetOutlineVerts(buffer, 24);
        Assert.Equal(24, outline.Length);
        Assert.Equal(sentinel, buffer[24]);
        foreach (var point in outline) Assert.Equal(circle.Radius, Vector2.Distance(circle.Center, point), 5);
    }

    [Fact]
    public void EllipsesAndConcavePolygonsHaveOutlinesButNoExactPolygonalConvexCore()
    {
        IShape2D[] shapes =
        [
            new Ellipse2D(new(2, 1)),
            new SimplePolygon2D([new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)]),
            new CompositeShape2D([new Circle2D(1)]),
            new HalfSpace2D(Vector2.UnitY, 0)
        ];
        foreach (var shape in shapes)
        {
            Assert.Equal(0, shape.GetVertCount());
            Assert.Throws<NotSupportedException>(() => shape.GetVerts(new Vector2[64], out _));
        }
        var ellipse = shapes[0];
        Span<Vector2> buffer = stackalloc Vector2[64];
        Assert.Equal(24, ellipse.GetOutlineVerts(buffer, 24).Length);
        // Existing distance queries retain their explicit ellipse approximation.
        Assert.Equal(Ellipse2D.CollisionSegments, WorldShape2D.ConvexCoreVertexCount(ellipse));
        Assert.Equal(Ellipse2D.CollisionSegments, WorldShape2D.WriteWorldConvexCore(ellipse, Similarity2D.Identity, buffer, out var radius));
        Assert.Equal(0f, radius);
        Assert.Equal(6, shapes[1].GetOutlineVerts(buffer).Length);
        Assert.Throws<NotSupportedException>(() => shapes[2].GetOutlineVerts(new Vector2[64]));
        Assert.Throws<NotSupportedException>(() => shapes[3].GetOutlineVerts(new Vector2[64]));
    }

    [Fact]
    public void ShortBuffersAndInvalidSamplingAreRejectedBeforeWriting()
    {
        var rectangle = Rectangle2D.FromSize(new Vector2(2, 2));
        var shortBuffer = Enumerable.Repeat(new Vector2(123), 3).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => rectangle.GetVerts(shortBuffer, out _));
        Assert.All(shortBuffer, point => Assert.Equal(new Vector2(123), point));
        Assert.Throws<ArgumentOutOfRangeException>(() => rectangle.GetOutlineVerts(shortBuffer));
        Assert.Throws<ArgumentException>(() => rectangle.GetOutlineVertCount(2));
    }
}
