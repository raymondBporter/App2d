using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Spatial;

namespace App2d.Tests.Physics;

public sealed class HalfSpaceConstraint2DTests
{
    [Fact]
    public void ResolutionUsesWorldUnitsAndStopsAtContactWithATransformedPlane()
    {
        var wall = new SpatialObject2D(HalfSpace2D.Builder.Create(Vector2f.UnitX, 1))
        {
            Scale = 2,
            Rotation = MathF.PI / 2,
            Position = new(3, 5)
        };
        var box = new SpatialObject2D(Rectangle2D.Builder.CreateFromSize(2, 2));
        box.Transform.Scale = new(-2, 2);
        box.Position = new(3, 8);

        Assert.True(HalfSpaceConstraint2D.ConstrainOutside(box, wall, out var contact));
        Assert.Equal(1, contact.PenetrationDepth, 5);
        Assert.True(Vector2f.Distance(new(3, 9), box.Position) < .00001f);
        Assert.Equal(7, box.WorldBounds.Bottom, 5);
        Assert.False(HalfSpaceConstraint2D.ConstrainOutside(box, wall, out _));
    }
}
