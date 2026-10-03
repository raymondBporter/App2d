using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeUtilityTests
{
    public static TheoryData<IConvexShape2D> Shapes =>
    [
        new Circle2D(1.5f, new(1, 2)),
        new Ellipse2D(new(2, 1), new(-1, .5f)),
        new Capsule2D(new(-1, 0), new(1, .5f), .3f),
        new Rectangle2D(new(-1, -2), new(3, 1)),
        new AxisAlignedRectangle2D(new(-1, -2), new(3, 1)),
        new Triangle2D(new(0, 0), new(2, 0), new(1, 3)),
        new ConvexPolygon2D([new(0, 0), new(2, 0), new(3, 1), new(1, 2)]),
    ];

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ScalingKeepsTheTypeAndScalesEveryOutlinePointAboutTheOrigin(IConvexShape2D shape)
    {
        var scaled = WorldShape2D.Scaled(shape, 2.5f);
        Assert.IsType(shape.GetType(), scaled);
        Assert.Equal(shape.Area * 2.5f * 2.5f, scaled.Area, 3);
        var original = Outline(shape);
        var doubled = Outline(scaled);
        for (var i = 0; i < original.Length; i++) Assert.True(Vector2.Distance(original[i] * 2.5f, doubled[i]) < 1e-4f);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldShape2D.Scaled(shape, 0));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void OutlinePointsLieOnTheBoundaryAndBoundingCirclesEncloseThem(IConvexShape2D shape)
    {
        var outline = Outline(shape);
        Assert.Equal(WorldShape2D.OutlineVertexCount(shape, 24), outline.Length);
        var (center, radius) = ShapeBounds2D.CalculateBoundingCircle(shape);
        var bounds = ShapeBounds2D.Calculate(shape);
        foreach (var point in outline)
        {
            Assert.InRange(ShapeDistance2D.SignedDistance(point, shape), -1e-4f, 1e-4f);
            Assert.True(Vector2.Distance(point, center) <= radius + 1e-4f);
            Assert.True(bounds.Contains(point) || bounds.DistanceTo(point) < 1e-4f);
        }
        Assert.True(radius <= bounds.HalfSize.Length() + 1e-4f);
    }

    [Fact]
    public void BoundingCirclesAreExactForRoundShapesAndBoxDiagonals()
    {
        Assert.Equal((new Vector2(1, 2), 1.5f), ShapeBounds2D.CalculateBoundingCircle(new Circle2D(1.5f, new(1, 2))));
        Assert.Equal((new Vector2(0, 0), MathF.Sqrt(2)), ShapeBounds2D.CalculateBoundingCircle(Rectangle2D.FromSize(new Vector2(2, 2))));
        Assert.Equal((new Vector2(0, 0), 1.5f), ShapeBounds2D.CalculateBoundingCircle(new Capsule2D(new(-1, 0), new(1, 0), .5f)));
        Assert.Equal((new Vector2(0, 0), 2f), ShapeBounds2D.CalculateBoundingCircle(new Ellipse2D(new(2, 1))));
        var (Center, Radius) = ShapeBounds2D.CalculateBoundingCircle(new CompositeShape2D([new Circle2D(1, new(-3, 0)), new Circle2D(1, new(3, 0))]));
        Assert.Equal(Vector2.Zero, Center);
        Assert.Equal(4f, Radius, 5);
        Assert.Equal(float.PositiveInfinity, ShapeBounds2D.CalculateBoundingCircle(new HalfSpace2D(Vector2.UnitY, 0)).Radius);
    }

    [Fact]
    public void BoundingCircleStaysValidWhenTheObjectRotates()
    {
        var box = new SpatialObject2D(new Rectangle2D(new(-3, -1), new(3, 1)));
        var (center, radius) = ShapeBounds2D.CalculateBoundingCircle(box.Shape);
        Span<Vector2> corners = stackalloc Vector2[4];
        for (var i = 0; i < 12; i++)
        {
            box.Transform.Rotation = i * MathF.Tau / 12;
            var worldCenter = Vector2.Transform(center, box.Transform.LocalToWorldMatrix);
            WorldShape2D.WriteWorldPerimeter(box.Shape, box.CollisionPose, corners);
            foreach (var corner in corners) Assert.True(Vector2.Distance(corner, worldCenter) <= radius + 1e-4f);
        }
    }

    private static Vector2[] Outline(IShape2D shape)
    {
        var vertices = new Vector2[WorldShape2D.OutlineVertexCount(shape, 24)];
        WorldShape2D.WriteOutline(shape, vertices, 24);
        return vertices;
    }
}
