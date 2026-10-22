using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.MassProperties;
using App2d.Core.Geometry.Operations.Transforms;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;
using static App2d.Tests.Geometry.VectorComparers;

namespace App2d.Tests.Geometry;

public sealed class Rectangle2DTests
{
    // A 6 by 2 box at (2, 1), rotated 30 degrees.
    private static readonly Rectangle2D Tilted = Rectangle2D.Builder.FromSize(6, 2, new(2, 1), MathF.PI / 6);

    [Fact]
    public void BuildersWithoutAnAngleStayAxisAligned()
    {
        var box = Rectangle2D.Builder.Create(new(-1, -2), new(3, 1));
        Assert.Equal(new Vector2(1, -.5f), box.Center);
        Assert.Equal(new Vector2(4, 3), box.Size);
        Assert.Equal(Vector2.UnitX, box.Axis);
        Assert.True(box.IsAxisAligned);
        Assert.Equal(box, Rectangle2D.Builder.FromSize(4, 3, new(1, -.5f)));
        Assert.Equal(Vector2.UnitX, default(Rectangle2D).Axis);
    }

    [Fact]
    public void ContainsOnlyWhenRotated()
    {
        // Three units out along the rotated long axis; the unrotated box is only one unit tall there.
        var onAxis = new Vector2(2 + 3 * MathF.Cos(MathF.PI / 6) * .99f, 1 + 3 * MathF.Sin(MathF.PI / 6) * .99f);
        Assert.True(Tilted.ContainsPoint(onAxis));
        Assert.False(Rectangle2D.Builder.FromSize(6, 2, new(2, 1)).ContainsPoint(onAxis));
        Assert.False(Tilted.ContainsPoint(new(5, 1)));
    }

    [Fact]
    public void CornersSupportAndBoundsAgree()
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        Tilted.WriteCorners(corners);
        var bounds = ShapeBounds2D.Calculate(Tilted);
        Assert.Equal(Rect2D.FromPoints(corners).Min, bounds.Min, Near);
        Assert.Equal(Rect2D.FromPoints(corners).Max, bounds.Max, Near);
        Assert.Equal(corners[2], Tilted.GetSupportPoint(Tilted.Axis + Tilted.Frame.Perp), Near);
        Assert.Equal(12f, Tilted.Area, 5);
        Assert.Equal(MathF.Sqrt(40), Tilted.Diameter, 5);
        Assert.Equal(MathF.PI / 6, Tilted.AngleRadians, 5);
    }

    [Fact]
    public void NinetyDegreesMatchesTheSwappedBox()
    {
        var turned = Rectangle2D.Builder.FromSize(6, 2, Vector2.Zero, MathF.PI / 2);
        var bounds = ShapeBounds2D.Calculate(turned);
        Assert.Equal(new Vector2(-1, -3), bounds.Min, Near);
        Assert.Equal(new Vector2(1, 3), bounds.Max, Near);
    }

    [Fact]
    public void SignedDistanceAndMassIgnoreRotation()
    {
        var flat = Rectangle2D.Builder.FromSize(6, 2, new(2, 1));
        Assert.Equal(ShapeDistance2D.SignedDistance(new Vector2(2, 1), flat), ShapeDistance2D.SignedDistance(new Vector2(2, 1), Tilted), 5);
        var a = ShapeMassProperties2D.Calculate(flat);
        var b = ShapeMassProperties2D.Calculate(Tilted);
        Assert.Equal(a.Mass, b.Mass, 5);
        Assert.Equal(a.MomentOfInertia, b.MomentOfInertia, 5);
        Assert.Equal(a.CenterOfMass.X, b.CenterOfMass.X, 5);
    }

    [Fact]
    public void ScaledKeepsTheAxis()
    {
        var scaled = Assert.IsType<Rectangle2D>(ShapeTransforms2D.Scaled(Tilted, 2));
        Assert.Equal(Tilted.Axis, scaled.Axis);
        Assert.Equal(Tilted.HalfSize * 2, scaled.HalfSize);
        Assert.Equal(Tilted.Center * 2, scaled.Center);
    }

    [Fact]
    public void NonUnitAxisFailsValidation()
    {
        Assert.ThrowsAny<ArgumentException>(() => (Tilted with { Axis = new(2, 0) }).Validate());
        Assert.ThrowsAny<ArgumentException>(() => Rectangle2D.Builder.FromSize(0, 2));
        Assert.Equal(Vector2.UnitY, Rectangle2D.Builder.Create(Vector2.Zero, Vector2.One, new(0, 5)).Axis);
    }

    [Fact]
    public void RotatedBoxCollidesLikeTheSamePolygon()
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        Tilted.WriteCorners(corners);
        var polygon = ConvexPolygon2D.Builder.Create(corners.ToArray());
        // Centered 0.2 outside the long edge in the box's own frame, so a 0.5 radius overlaps by 0.3.
        var probe = Circle2D.Builder.Create(.5f, Tilted.Frame.FromFrame(new(2.5f, 1.2f)));
        var hitBox = ShapeCollision2D.TryGetContact(new SpatialObject2D(Tilted), new SpatialObject2D(probe), out var boxContact);
        var hitPolygon = ShapeCollision2D.TryGetContact(new SpatialObject2D(polygon), new SpatialObject2D(probe), out var polygonContact);
        Assert.True(hitPolygon);
        Assert.Equal(hitPolygon, hitBox);
        Assert.Equal(polygonContact.PenetrationDepth, boxContact.PenetrationDepth, 4);
    }
}
