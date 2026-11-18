using App2d.Core.Collision;
using App2d.Core.Collision.Queries;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using App2d.Core.Spatial;

namespace App2d.Tests.Collision;

public sealed class CompositeShapeTests
{
    private static readonly ShapeCollisionContactProvider2D Provider = new();

    // An L: vertical bar plus horizontal foot. The notch is the empty upper-right.
    private static CompositeShape2D LShape() => CompositeShape2D.Builder.Create([
        Rectangle2D.Builder.Create(new Vector2f(-2f, -2f), new Vector2f(0f, 2f)),
        Rectangle2D.Builder.Create(new Vector2f(0f, -2f), new Vector2f(2f, 0f))]);

    private static SpatialObject2D At(IShape2D shape, Vector2f position)
    {
        var worldObject = new SpatialObject2D(shape)
        {
            Position = position
        };
        return worldObject;
    }

    [Fact]
    public void NotchOfAnLIsEmpty()
    {
        // A small circle sitting fully inside the L's notch must NOT collide —
        // a convex-hull treatment would wrongly report contact here.
        var l = At(LShape(), Vector2f.Zero);
        var circle = At(Circle2D.Builder.Create(0.5f), new Vector2f(1.2f, 1.2f));

        Assert.False(Provider.TryGetContact(circle, l, out _));
    }

    [Fact]
    public void FootOfTheLCollides()
    {
        var l = At(LShape(), Vector2f.Zero);
        var circle = At(Circle2D.Builder.Create(0.5f), new Vector2f(1.2f, -0.3f));

        Assert.True(Provider.TryGetContact(circle, l, out var contact));
        Assert.True(contact.PenetrationDepth > 0f);
    }

    [Fact]
    public void CompositeVsCompositeCollides()
    {
        var first = At(LShape(), Vector2f.Zero);
        var second = At(LShape(), new Vector2f(3.5f, -1f));

        Assert.True(Provider.TryGetContact(first, second, out var contact));
        Assert.True(contact.PenetrationDepth > 0f);
    }

    [Fact]
    public void RaycastHitsTheNearestPart()
    {
        var dumbbell = At(CompositeShape2D.Builder.Create([
            Circle2D.Builder.Create(1f, new Vector2f(-3f, 0f)),
            Circle2D.Builder.Create(1f, new Vector2f(3f, 0f))]), new Vector2f(10f, 0f));

        var found = new[] { dumbbell }.Raycast(new Ray2D(Vector2f.Zero, Vector2f.UnitX), 20f, out var hit);

        Assert.True(found);
        Assert.Equal(6f, hit.Distance, 3); // nearest sphere surface at x = 10 - 3 - 1
    }

    [Fact]
    public void CalculatedBoundsIsTheUnionOfParts()
    {
        var bounds = ShapeBounds2D.Calculate(LShape());
        Assert.Equal(new Vector2f(-2f, -2f), bounds.Min);
        Assert.Equal(new Vector2f(2f, 2f), bounds.Max);
    }

    [Fact]
    public void AreaSumsParts() => Assert.Equal(8f + 4f, LShape().Area, 3);
}
