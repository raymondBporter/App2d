using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Direction2DTests
{
    [Theory]
    [InlineData(float.Epsilon)]
    [InlineData(1)]
    [InlineData(1e30f)]
    [InlineData(float.MaxValue)]
    public void NormalizesFiniteVectorsAcrossFloatRange(float scale)
    {
        var direction = new Direction2D(new(scale, scale));
        Assert.True(direction.IsValid);
        Assert.Equal(1f / MathF.Sqrt(2f), direction.Vector.X, 5);
        Assert.Equal(direction.Vector.X, direction.Vector.Y);
        Assert.Equal(1f, direction.Vector.Length(), 5);
    }

    [Fact]
    public void GeometryAcceptsDirectionTypeAndPreservesExistingVectorApi()
    {
        var direction = new Direction2D(new(3, 4));
        var ray = Ray2D.FromDirection(new(2, 3), direction);
        var line = Line2D.FromDirection(new(2, 3), direction);
        Assert.Equal(direction, ray.UnitDirection);
        Assert.Equal(direction, line.UnitDirection);
        Assert.Equal(direction.Vector, ray.Direction);
        Assert.Equal(direction.Vector, line.Direction);
        Assert.Equal(new Ray2D(new(2, 3), new Vector2(3, 4)), ray);
        Assert.Equal(new Line2D(new(2, 3), new Vector2(3, 4)), line);
        Assert.Equal(new Vector2(5, 7), ray.GetPoint(5));
        Assert.Equal(direction, Direction2D.FromPoints(new(2, 3), new(5, 7)));
    }

    [Fact]
    public void AnglePerpendicularAndScalingHaveClearDirectionSemantics()
    {
        var up = Direction2D.FromAngle(MathF.PI / 2);
        Assert.Equal(MathF.PI / 2, up.AngleRadians, 5);
        AssertClose(-Vector2.UnitX, up.PerpCcw.Vector);
        AssertClose(Vector2.UnitX, up.PerpCw.Vector);
        AssertClose(-Vector2.UnitY, up.Reversed.Vector);
        AssertClose(new Vector2(0, 3), up.ScaledBy(3));
        AssertClose(new Vector2(0, -3), up.ScaledBy(-3));
    }

    [Fact]
    public void InvalidDirectionsCannotEnterGeometryOrBeUsed()
    {
        Assert.False(default(Direction2D).IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Direction2D(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Direction2D(new(float.NaN, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Direction2D(new(float.PositiveInfinity, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Direction2D.FromAngle(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Direction2D.FromPoints(Vector2.One, Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(Direction2D).ScaledBy(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = default(Direction2D).AngleRadians);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ray2D.FromDirection(default, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => Line2D.FromDirection(default, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Direction2D(Vector2.UnitX).ScaledBy(float.NaN));
    }

    private static void AssertClose(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) <= .000001f, $"Expected {expected}, actual {actual}.");
}
