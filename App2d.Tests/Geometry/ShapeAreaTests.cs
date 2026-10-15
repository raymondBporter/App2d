using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeAreaTests
{
    [Fact]
    public void CircleAreaIsPiRSquared() => Assert.Equal(MathF.PI * 4f, Circle2D.Builder.Create(2f).Area, 3);

    [Fact]
    public void RectangleAreaIsWidthTimesHeight() => Assert.Equal(12f, Rectangle2D.Builder.CreateFromSize(new Vector2(4f, 3f)).Area, 3);

    [Fact]
    public void CapsuleAreaIsRectanglePlusEndCircle() => Assert.Equal(2f * 1f * 4f + MathF.PI * 1f, Capsule2D.Builder.Create(new Vector2(0f, 0f), new Vector2(4f, 0f), 1f).Area, 3);

    [Fact]
    public void PolygonAreaMatchesShoelace() => Assert.Equal(4f, ConvexPolygon2D.Builder.Create([new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 2f), new Vector2(0f, 2f)]).Area, 3);

    [Fact]
    public void HalfSpaceAreaIsInfinite() => Assert.Equal(float.PositiveInfinity, HalfSpace2D.Builder.Create(Vector2.UnitY, 0f).Area);
}
