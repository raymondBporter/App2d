using App2d.Core.Geometry.Serialization;
using App2d.Core.Shapes;

namespace App2d.Tests.Geometry;

#pragma warning disable APP2D001 // Object initializers are the unchecked construction path under test.
public sealed class OrientedShapeEqualityTests
{
    [Fact]
    public void RectangleEqualityIgnoresHowTheDefaultAxisWasSet()
    {
        var built = Rectangle2D.Builder.FromSize(4, 2, new(1, 1));
        var initialized = new Rectangle2D { Center = new(1, 1), HalfSize = new(2, 1) };
        Assert.Equal(built, initialized);
        Assert.Equal(built.GetHashCode(), initialized.GetHashCode());
        Assert.Equal(built, GeometryJson2D.ShapeFromJson(initialized.ToJson()));
        Assert.NotEqual(built, Rectangle2D.Builder.FromSize(4, 2, new(1, 1), .1f));
    }

    [Fact]
    public void RoundedRectangleEqualityIgnoresHowTheDefaultAxisWasSet()
    {
        var built = RoundedRectangle2D.Builder.FromSize(4, 2, .5f, new(1, 1));
        var initialized = new RoundedRectangle2D { Center = new(1, 1), HalfSize = new(2, 1), Radius = .5f };
        Assert.Equal(built, initialized);
        Assert.Equal(built.GetHashCode(), initialized.GetHashCode());
        Assert.Equal(built, GeometryJson2D.ShapeFromJson(initialized.ToJson()));
    }

    [Fact]
    public void EllipseEqualityIgnoresHowTheDefaultAxisWasSet()
    {
        var built = Ellipse2D.Builder.Create(new(2, 1), new(1, 1));
        var initialized = new Ellipse2D { Radii = new(2, 1), Center = new(1, 1) };
        Assert.Equal(built, initialized);
        Assert.Equal(built.GetHashCode(), initialized.GetHashCode());
        Assert.Equal(built, GeometryJson2D.ShapeFromJson(initialized.ToJson()));
    }
}
