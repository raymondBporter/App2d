using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeRaycast2DTests
{
    [Theory]
    [InlineData(.5f)]
    [InlineData(2f)]
    public void UnnormalizedDirectionsPreserveRayParametersAndDistanceLimits(float speed)
    {
        var circle = Circle2D.Builder.Create(1, new(6, 0));
        var parameter = 5 / speed;
        Assert.True(ShapeRaycast2D.TryIntersect(Vector2f.Zero, new(speed, 0), circle, parameter, out var hit));
        Assert.Equal(parameter, hit.Distance, 5);
        Assert.Equal(new Vector2f(5, 0), hit.Point);
        Assert.Equal(-Vector2f.UnitX, hit.Normal);
        Assert.False(ShapeRaycast2D.TryIntersect(Vector2f.Zero, new(speed, 0), circle, parameter - .1f, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcavePolygonRaysReachTheNotchBoundaryInEitherWinding(bool reverse)
    {
        Vector2f[] perimeter = [new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)];
        if (reverse)
        {
            Array.Reverse(perimeter);
        }

        Assert.True(Raycast2D.TrySimplePolygon(new(2, 2), -Vector2f.UnitX, perimeter, 10, out var raw));
        Assert.True(ShapeRaycast2D.TryIntersect(new(2, 2), -Vector2f.UnitX, SimplePolygon2D.Builder.Create(perimeter), 10, out var shape));
        Assert.Equal(new Vector2f(1, 2), raw.Point);
        Assert.Equal(Vector2f.UnitX, raw.Normal);
        Assert.Equal(1, raw.Distance);
        Assert.Equal(raw, shape);
    }
}
