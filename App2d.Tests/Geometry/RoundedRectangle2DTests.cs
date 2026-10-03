using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision.Contacts;
using App2d.Core.Collision.Queries;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class RoundedRectangle2DTests
{
    [Fact]
    public void BoundsAreaContainmentAndSignedPointDistanceAreExact()
    {
        var rounded = new RoundedRectangle2D(new(-2, -1), new(2, 1), .5f);
        Assert.Equal(new Rect2D(new(-2, -1), new(2, 1)), ShapeBounds2D.Calculate(rounded));
        Assert.Equal(8f - (4f - MathF.PI) * .25f, rounded.Area, 5);
        Assert.True(rounded.ContainsPoint(new(2, 0)));
        Assert.False(rounded.ContainsPoint(new(2, 1)));
        Assert.Equal(MathF.Sqrt(.5f) - .5f, ShapeDistance2D.SignedDistance(new Vector2(2, 1), rounded), 5);
        Assert.Equal(-1f, ShapeDistance2D.SignedDistance(Vector2.Zero, rounded), 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoundedRectangle2D(new(-2, -1), new(2, 1), 1.01f));
    }

    [Fact]
    public void ConvexCoreGivesExactPairDistanceAndWorksWhenRadiusFillsTheBox()
    {
        var rounded = new RoundedRectangle2D(new(-2, -1), new(2, 1), .5f);
        var circle = new Circle2D(.25f, new(2.5f, 1.5f));
        Assert.Equal(MathF.Sqrt(2f) - .75f, ShapeDistance2D.SignedDistance(rounded, circle), 5);
        Assert.Equal(rounded.GetSupportPoint(Vector2.UnitX), new Vector2(2, .5f));

        var disk = new RoundedRectangle2D(new(-1, -1), new(1, 1), 1);
        Assert.Equal(1, WorldShape2D.ConvexCoreVertexCount(disk));
        Assert.Equal(1.75f, ShapeDistance2D.SignedDistance(disk, new Circle2D(.25f, new(3, 0))), 5);
    }

    [Fact]
    public void CollisionAndRayQueriesUseTheRoundedPerimeter()
    {
        var rounded = new SpatialObject2D(new RoundedRectangle2D(new(-2, -1), new(2, 1), .5f));
        var circle = new SpatialObject2D(new Circle2D(.3f, new(1.9f, .9f)));
        Assert.True(ShapeCollision2D.TryGetContact(rounded, circle, out var contact));
        Assert.True(contact.PenetrationDepth > 0);
        Assert.True(RayIntersection2D.TryIntersect(new Ray2D(new(3, 0), -Vector2.UnitX), rounded, 10, out var hit));
        Assert.Equal(1f, hit.Distance, 5);
        Assert.False(RayIntersection2D.TryIntersect(new Ray2D(new(3, 1.01f), -Vector2.UnitX), rounded, 10, out _));
    }

    [Fact]
    public void SavedCharacterBoxKeepsItsOutlineAsARoundedRectangleShape()
    {
        var model = ModelAuthoring.Empty("rounded-test", "Rounded test");
        ModelAuthoring.AddControl(model, null, Vector3.Zero, "root");
        var part = ModelAuthoring.AddPart(model, PuppetPartKinds.Box, "root");
        part.Width = 2; part.Height = 1;
        PartGeometry.ResizeRoundedRectangle(part, part.Width, part.Height);
        PartGeometry.SetRoundedRectangleRadius(part, .25f);
        var before = PartGeometry.Contour(part, _ => Vector3.Zero);

        var json = model.ToJson();
        Assert.Contains("\"kind\": \"rounded-rectangle\"", json);
        var restored = CharacterModel.FromJson(json).Parts.Single();
        Assert.IsType<RoundedRectangleShapeDefinition2D>(restored.Geometry);
        Assert.Equal(PuppetPartKinds.Box, restored.Kind);
        Assert.Equal(before, PartGeometry.Contour(restored, _ => Vector3.Zero));
    }
}
