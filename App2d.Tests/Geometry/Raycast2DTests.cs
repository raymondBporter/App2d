using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Raycast2DTests
{
    [Fact]
    public void RawRectangleCastReportsTheEntryFaceAndRespectsMaxDistance()
    {
        Assert.True(Raycast2D.TryRectangle(new(-5, 0), Vector2.UnitX, new(-1), new(1), 10, out var hit));
        Assert.Equal(4f, hit.Distance, 5);
        Assert.Equal(new Vector2(-1, 0), hit.Point);
        Assert.Equal(-Vector2.UnitX, hit.Normal);
        Assert.False(Raycast2D.TryRectangle(new(-5, 0), Vector2.UnitX, new(-1), new(1), 3, out _));
        Assert.True(Raycast2D.TryRectangle(new(0, .5f), Vector2.UnitY, new(-1), new(1), 10, out var exit));
        Assert.Equal(Vector2.UnitY, exit.Normal);
        Assert.Equal(.5f, exit.Distance, 5);
        Assert.True(Raycast2D.IntersectsRectangle(new(-5, 0), Vector2.UnitX, new(-1), new(1), 10));
        Assert.False(Raycast2D.IntersectsRectangle(new(-5, 2), Vector2.UnitX, new(-1), new(1), 10));
        Assert.True(Raycast2D.IntersectsRectangle(new(-5, 2), Vector2.UnitX, new(float.NegativeInfinity), new(float.PositiveInfinity), 10));
    }

    [Fact]
    public void RawHalfSpaceCircleAndEllipseCastsAgree()
    {
        Assert.True(Raycast2D.TryHalfSpace(new(0, 5), -Vector2.UnitY, Vector2.UnitY, 1, 10, out var plane));
        Assert.Equal(4f, plane.Distance, 5);
        Assert.Equal(Vector2.UnitY, plane.Normal);
        Assert.False(Raycast2D.TryHalfSpace(new(0, 5), Vector2.UnitX, Vector2.UnitY, 1, 10, out _));

        Assert.True(Raycast2D.TryCircle(new(-5, 0), Vector2.UnitX, default, 2, 10, out var circle));
        Assert.True(Raycast2D.TryEllipse(new(-5, 0), Vector2.UnitX, default, new(2, 2), 10, out var ellipse));
        Assert.Equal(circle.Distance, ellipse.Distance, 5);
        Assert.Equal(circle.Normal, ellipse.Normal);
        Assert.True(Vector2.Distance(circle.Point, ellipse.Point) < 1e-5f);

        // Non-unit directions report the ray parameter, not the Euclidean distance.
        Assert.True(Raycast2D.TryCircle(new(-5, 0), new(2, 0), default, 2, 10, out var scaled));
        Assert.Equal(1.5f, scaled.Distance, 5);
        Assert.Equal(new Vector2(-2, 0), scaled.Point);
    }
}
