using System.Numerics;
using App2d.Engine.Geometry;

namespace App2d.Tests.Geometry;

public sealed class ShapeAreaTests
{
    [Fact]
    public void CircleAreaIsPiRSquared() => Assert.Equal(MathF.PI * 4f, new Circle2D(2f).Area, 3);

    [Fact]
    public void RectangleAreaIsWidthTimesHeight() => Assert.Equal(12f, Rectangle2D.FromSize(new Vector2(4f, 3f)).Area, 3);

    [Fact]
    public void CapsuleAreaIsRectanglePlusEndCircle() => Assert.Equal(2f * 1f * 4f + MathF.PI * 1f, new Capsule2D(new Vector2(0f, 0f), new Vector2(4f, 0f), 1f).Area, 3);

    [Fact]
    public void PolygonAreaMatchesShoelace() => Assert.Equal(4f, new ConvexPolygon2D([new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 2f), new Vector2(0f, 2f)]).Area, 3);

    [Fact]
    public void HalfSpaceAreaIsInfinite() => Assert.Equal(float.PositiveInfinity, new HalfSpace2D(Vector2.UnitY, 0f).Area);
}
