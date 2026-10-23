using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using System.Numerics;
using static App2d.Tests.Geometry.VectorComparers;

namespace App2d.Tests.Geometry;

public sealed class OrientedFrame2DTests
{
    private static readonly OrientedFrame2D Tilted = new(new(2, 1), OrientedFrame2D.AxisFromAngle(MathF.PI / 6));

    [Fact]
    public void RoundTripsPointsAndDirections()
    {
        var p = new Vector2(-3, 4);
        Assert.Equal(p, Tilted.FromFrame(Tilted.ToFrame(p)), Near);
        Assert.Equal(p, Tilted.FromFrameDirection(Tilted.ToFrameDirection(p)), Near);
        Assert.Equal(Vector2.Transform(new(1, 0), Tilted.Matrix), Tilted.FromFrame(new(1, 0)), Near);
    }

    [Fact]
    public void AxisPointsAlongLocalX() => Assert.Equal(Tilted.Center + Tilted.Axis, Tilted.FromFrame(Vector2.UnitX), Near);

    [Fact]
    public void BoxBoundsMatchCorners()
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        VertexGenerator2D.WriteRectangle(corners, new(-3, -1), new(3, 1));
        Tilted.MapFromFrame(corners);
        var expected = Rect2D.FromPoints(corners);
        var actual = Tilted.BoxBounds(new(3, 1));
        Assert.Equal(expected.Min, actual.Min, Near);
        Assert.Equal(expected.Max, actual.Max, Near);
    }

    [Fact]
    public void NinetyDegreesSwapsBoxExtentsExactly()
    {
        var frame = new OrientedFrame2D(Vector2.Zero, Vector2.UnitY);
        Assert.Equal(new Rect2D(new(-1, -3), new(1, 3)), frame.BoxBounds(new(3, 1)));
    }

    [Fact]
    public void EllipseBoundsMatchSampledOutline()
    {
        var vertices = new Vector2[256];
        VertexGenerator2D.WriteEllipse(vertices, Vector2.Zero, new(3, 1));
        Tilted.MapFromFrame(vertices);
        var sampled = Rect2D.FromPoints(vertices);
        var analytic = Tilted.EllipseBounds(new(3, 1));
        Assert.Equal(sampled.Min, analytic.Min, Loose);
        Assert.Equal(sampled.Max, analytic.Max, Loose);
    }

    [Fact]
    public void UnsetAxisReadsAsUnitX() => Assert.Equal(Vector2.UnitX, OrientedFrame2D.AxisOrUnitX(default));

    [Fact]
    public void ValidateAxisRejectsNonUnit()
    {
        Assert.Throws<ArgumentException>(() => OrientedFrame2D.ValidateAxis(new(2, 0), "axis"));
        Assert.Throws<ArgumentException>(() => OrientedFrame2D.ValidateAxis(new(float.NaN, 0), "axis"));
        OrientedFrame2D.ValidateAxis(OrientedFrame2D.AxisFromAngle(1.3f), "axis");
    }
}
