using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class DistanceSquaredTests
{
    [Fact]
    public void SquaredQueriesMatchTheSquareOfTheirDistanceQueries()
    {
        var random = new Random(3);
        Vector2[] square = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        for (var i = 0; i < 200; i++)
        {
            var point = Random();
            var start = Random();
            var end = Random();
            var otherStart = Random();
            var otherEnd = Random();
            var direction = new Vector2(random.NextSingle() * 4 - 2, random.NextSingle() * 4 - 2);
            if (direction.LengthSquared() < .01f) direction = Vector2.UnitX;
            var rectangle = Rect2D.FromPoints(start, end);

            Near(Distance2D.Distance(point, start), Distance2D.DistanceSquared(point, start));
            Near(Distance2D.DistanceToSegment(point, start, end), Distance2D.DistanceSquaredToSegment(point, start, end));
            Near(Distance2D.DistanceBetweenSegments(start, end, otherStart, otherEnd), Distance2D.DistanceSquaredBetweenSegments(start, end, otherStart, otherEnd));
            Near(Distance2D.DistanceToLine(point, start, direction), Distance2D.DistanceSquaredToLine(point, start, direction));
            Near(Distance2D.DistanceToRay(point, start, direction), Distance2D.DistanceSquaredToRay(point, start, direction));
            Near(Distance2D.DistanceToRectangle(point, rectangle.Min, rectangle.Max), Distance2D.DistanceSquaredToRectangle(point, rectangle.Min, rectangle.Max));
            Near(rectangle.DistanceTo(point), rectangle.DistanceSquaredTo(point));
            Near(Distance2D.DistanceToConvexPolygon(point, square), Distance2D.DistanceSquaredToConvexPolygon(point, square));
            Near(Vector2.Distance(point, ClosestPoint2D.OnPolygonPerimeter(point, square)), Distance2D.DistanceSquaredToPolygonPerimeter(point, square));
        }

        Vector2 Random() => new(random.NextSingle() * 10 - 5, random.NextSingle() * 10 - 5);

        static void Near(float distance, float squared) => Assert.InRange(squared, distance * distance - 1e-3f, distance * distance + 1e-3f);
    }

    [Fact]
    public void SquaredQueriesAreZeroInsideFilledPrimitivesButNotOnPerimeters()
    {
        Vector2[] square = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        Assert.Equal(0f, Distance2D.DistanceSquaredToRectangle(new(.5f, -.5f), new(-1), new(1)));
        Assert.Equal(0f, Distance2D.DistanceSquaredToConvexPolygon(new(.5f, -.5f), square));
        Assert.Equal(.25f, Distance2D.DistanceSquaredToPolygonPerimeter(new(.5f, 0), square), 5);
        Assert.Equal(25f, Distance2D.DistanceSquaredToRectangle(new(4, 5), new(-1), new(1)), 5);
        Assert.Equal(16f, Distance2D.DistanceSquaredToRay(new(-4, 0), default, Vector2.UnitX), 5);
        Assert.Equal(0f, Distance2D.DistanceSquaredToLine(new(-4, 0), default, Vector2.UnitX), 5);
    }
}
