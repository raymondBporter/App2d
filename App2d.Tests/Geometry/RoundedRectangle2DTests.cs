using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class RoundedRectangle2DTests
{
    [Fact]
    public void BoundsAreaContainmentAndSignedPointDistanceAreExact()
    {
        var rounded = RoundedRectangle2D.Builder.Create(new(-2, -1), new(2, 1), .5f);
        Assert.Equal(new Rect2D(new(-2, -1), new(2, 1)), ShapeBounds2D.Calculate(rounded));
        Assert.Equal(8f - (4f - MathF.PI) * .25f, rounded.Area, 5);
        Assert.True(rounded.ContainsPoint(new(2, 0)));
        Assert.False(rounded.ContainsPoint(new(2, 1)));
        Assert.Equal(MathF.Sqrt(.5f) - .5f, ShapeDistance2D.SignedDistance(new Vector2(2, 1), rounded), 5);
        Assert.Equal(-1f, ShapeDistance2D.SignedDistance(Vector2.Zero, rounded), 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => RoundedRectangle2D.Builder.Create(new(-2, -1), new(2, 1), 1.01f));
    }

    [Fact]
    public void ConvexCoreGivesExactPairDistanceAndWorksWhenRadiusFillsTheBox()
    {
        var rounded = RoundedRectangle2D.Builder.Create(new(-2, -1), new(2, 1), .5f);
        var circle = Circle2D.Builder.Create(.25f, new(2.5f, 1.5f));
        Assert.Equal(MathF.Sqrt(2f) - .75f, ShapeDistance2D.SignedDistance(rounded, circle), 5);
        Assert.Equal(rounded.GetSupportPoint(Vector2.UnitX), new Vector2(2, .5f));

        var disk = RoundedRectangle2D.Builder.Create(new(-1, -1), new(1, 1), 1);
        Assert.Equal(1, disk.GetVertCount());
        Assert.Equal(1.75f, ShapeDistance2D.SignedDistance(disk, Circle2D.Builder.Create(.25f, new(3, 0))), 5);
    }

    [Fact]
    public void CollisionAndRayQueriesUseTheRoundedPerimeter()
    {
        var rounded = new SpatialObject2D(RoundedRectangle2D.Builder.Create(new(-2, -1), new(2, 1), .5f));
        var circle = new SpatialObject2D(Circle2D.Builder.Create(.3f, new(1.9f, .9f)));
        Assert.True(ShapeCollision2D.TryGetContact(rounded, circle, out var contact));
        Assert.True(contact.PenetrationDepth > 0);
        Assert.True(SpatialRaycast2D.TryIntersect(new Ray2D(new(3, 0), -Vector2.UnitX), rounded, 10, out var hit));
        Assert.Equal(1f, hit.Distance, 5);
        Assert.False(SpatialRaycast2D.TryIntersect(new Ray2D(new(3, 1.01f), -Vector2.UnitX), rounded, 10, out _));
    }

    [Fact]
    public void SavedCharacterBoxKeepsItsOutlineAsARoundedRectangleShape()
    {
        var model = ModelAuthoring.Empty("rounded-test", "Rounded test");
        ModelAuthoring.AddControl(model, null, Vector3.Zero, "root");
        var part = ModelAuthoring.AddPart(model, RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f), "root");
        part.Width = 2;
        part.Height = 1;
        part.Geometry = ((RoundedRectangle2D.Builder)part.Geometry!).WithSize(new(part.Width, part.Height)) with
        {
            Radius = .25f
        };
        var before = PartContours2D.Contour(part, _ => Vector3.Zero);

        var json = model.ToJson();
        Assert.Contains("\"kind\": \"rounded-rectangle\"", json);
        var restored = CharacterModel.FromJson(json).Parts.Single();
        Assert.IsType<RoundedRectangle2D.Builder>(restored.Geometry);
        Assert.Equal(ShapeKinds2D.RoundedRectangle, restored.Geometry!.Kind);
        Assert.Equal(before, PartContours2D.Contour(restored, _ => Vector3.Zero));
    }
}
