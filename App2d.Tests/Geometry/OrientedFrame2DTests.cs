using App2d.Core.Geometry.Primitives;
using static App2d.Tests.Geometry.VectorComparers;

namespace App2d.Tests.Geometry;

public sealed class OrientedFrame2DTests
{
    private static readonly OrientedFrame2D Tilted = new(new(2, 1), OrientedFrame2D.AxisFromAngle(MathF.PI / 6));

    [Fact]
    public void RoundTripsPointsAndDirections()
    {
        var p = new Vector2f(-3, 4);
        Assert.Equal(p, Tilted.FromFrame(Tilted.ToFrame(p)), Near);
        Assert.Equal(p, Tilted.FromFrameDirection(Tilted.ToFrameDirection(p)), Near);
        Assert.Equal(Vector2f.Transform(new(1, 0), Tilted.Matrix), Tilted.FromFrame(new(1, 0)), Near);
    }

    [Fact]
    public void AxisPointsAlongLocalX() => Assert.Equal(Tilted.Center + Tilted.Axis, Tilted.FromFrame(Vector2f.UnitX), Near);

    [Fact]
    public void BoxBoundsMatchCorners()
    {
        Span<Vector2f> corners = stackalloc Vector2f[4];
        VertexGenerator2D.WriteRectangle(corners, new(-3, -1), new(3, 1));
        Tilted.MapFromFrame(corners);
        var expected = Rect2f.FromPoints(corners);
        var actual = Tilted.BoxBounds(new(3, 1));
        Assert.Equal(expected.Min, actual.Min, Near);
        Assert.Equal(expected.Max, actual.Max, Near);
    }

    [Fact]
    public void NinetyDegreesSwapsBoxExtentsExactly()
    {
        var frame = new OrientedFrame2D(Vector2f.Zero, Vector2f.UnitY);
        Assert.Equal(new Rect2f(new(-1, -3), new(1, 3)), frame.BoxBounds(new(3, 1)));
    }

    [Fact]
    public void EllipseBoundsMatchSampledOutline()
    {
        var vertices = new Vector2f[256];
        VertexGenerator2D.WriteEllipse(vertices, Vector2f.Zero, new(3, 1));
        Tilted.MapFromFrame(vertices);
        var sampled = Rect2f.FromPoints(vertices);
        var analytic = Tilted.EllipseBounds(new(3, 1));
        Assert.Equal(sampled.Min, analytic.Min, Loose);
        Assert.Equal(sampled.Max, analytic.Max, Loose);
    }

    [Fact]
    public void UnsetAxisReadsAsUnitX() => Assert.Equal(Vector2f.UnitX, OrientedFrame2D.AxisOrUnitX(default));

    [Fact]
    public void ValidateAxisRejectsNonUnit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrientedFrame2D.ValidateAxis(new(2, 0), "axis"));
        Assert.ThrowsAny<ArgumentException>(() => OrientedFrame2D.ValidateAxis(new(float.NaN, 0), "axis"));
        OrientedFrame2D.ValidateAxis(OrientedFrame2D.AxisFromAngle(1.3f), "axis");
    }
}
