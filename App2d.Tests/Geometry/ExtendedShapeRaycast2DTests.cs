using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Shapes;

namespace App2d.Tests.Geometry;

public sealed class ExtendedShapeRaycast2DTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(.73f)]
    public void RoundedRectangleHitsTheActualCircularCornerWithASmoothNormal(float rotation)
    {
        var rectangle = RoundedRectangle2D.Builder.Create(new(3, -2), new(1.5f, 1), .5f,
            new(MathF.Cos(rotation), MathF.Sin(rotation)));
        const float angle = .297f;
        var normal = new Vector2f(MathF.Cos(angle), MathF.Sin(angle));
        var point = new Vector2f(1, .5f) + normal * .5f;
        var frame = rectangle.Frame;
        var origin = frame.FromFrame(point + normal * 3);
        var direction = frame.FromFrameDirection(normal * -2);
        Assert.True(ShapeRaycast2D.TryIntersect(origin, direction, rectangle, 2, out var hit));
        Near(frame.FromFrame(point), hit.Point);
        Near(frame.FromFrameDirection(normal), hit.Normal);
        Assert.Equal(1.5f, hit.Distance, 5);
        Assert.False(ShapeRaycast2D.TryIntersect(origin, direction, rectangle, 1.4f, out _));
    }

    [Fact]
    public void RoundedRectangleReportsExitsAndRejectsTheMissingSquareCorner()
    {
        var rectangle = RoundedRectangle2D.Builder.Create(Vector2f.Zero, new(2, 1), .5f, Vector2f.UnitX);
        Assert.True(ShapeRaycast2D.TryIntersect(Vector2f.Zero, Vector2f.UnitX, rectangle, 5, out var exit));
        Assert.Equal(new Vector2f(2, 0), exit.Point);
        Assert.Equal(Vector2f.UnitX, exit.Normal);
        Assert.False(ShapeRaycast2D.TryIntersect(new(1.99f, .99f), Vector2f.Normalize(Vector2f.One), rectangle, 5, out _));
        Assert.True(ShapeRaycast2D.TryIntersect(new(-5, 1), Vector2f.UnitX, rectangle, 5, out var tangent));
        Near(new(-1.5f, 1), tangent.Point);
        Near(Vector2f.UnitY, tangent.Normal);
    }

    [Theory]
    [InlineData(1f, 1f, 1f)]
    [InlineData(1f, 2f, 1f)]
    [InlineData(2f, 1f, 1f)]
    [InlineData(2f, 1f, 0f)]
    public void CollapsedInnerCoresStillRaycastAsCirclesCapsulesOrRectangles(float halfX, float halfY, float radius)
    {
        var rectangle = RoundedRectangle2D.Builder.Create(Vector2f.Zero, new(halfX, halfY), radius, Vector2f.UnitX);
        Assert.True(ShapeRaycast2D.TryIntersect(new(-5, 0), Vector2f.UnitX, rectangle, 10, out var hit));
        Near(new(-halfX, 0), hit.Point);
        Near(-Vector2f.UnitX, hit.Normal);
        Assert.True(ShapeRaycast2D.TryIntersect(Vector2f.Zero, Vector2f.UnitY, rectangle, 10, out var exit));
        Near(new(0, halfY), exit.Point);
        Near(Vector2f.UnitY, exit.Normal);
    }

    [Fact]
    public void InfiniteLinesAndForwardRaysHaveIsolatedIntersectionsWithoutArtificialBounds()
    {
        var line = new Line2D(new(2, 3), Vector2f.UnitY);
        Assert.True(Raycast2D.TryLine(Vector2f.Zero, new(2, 0), line, 1, out var hit));
        Assert.Equal(new Vector2f(2, 0), hit.Point);
        Assert.Equal(1, hit.Distance);
        Assert.Equal(-Vector2f.UnitX, hit.Normal);
        var away = new Ray2D(new(2, 3), Vector2f.UnitY);
        Assert.False(Raycast2D.TryRay(Vector2f.Zero, Vector2f.UnitX, away, 5, out _));
        var toward = new Ray2D(new(2, 3), -Vector2f.UnitY);
        Assert.True(Raycast2D.TryRay(Vector2f.Zero, Vector2f.UnitX, toward, 5, out var crossing));
        Assert.Equal(new Vector2f(2, 0), crossing.Point);
        Assert.False(Raycast2D.TryLine(Vector2f.Zero, Vector2f.UnitY, line, 5, out _));
        Assert.False(Raycast2D.TryRay(Vector2f.Zero, Vector2f.UnitX, new(new(1, 0), Vector2f.UnitX), 5, out _));
    }

    private static void Near(Vector2f expected, Vector2f actual) => Assert.True(Vector2f.Distance(expected, actual) < .00002f, $"Expected {expected}, got {actual}");
}
