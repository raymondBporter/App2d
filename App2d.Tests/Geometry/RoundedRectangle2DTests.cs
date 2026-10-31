using App2d.CharacterStudio.Authoring;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;
using static App2d.Tests.Geometry.VectorComparers;

namespace App2d.Tests.Geometry;

public sealed class RoundedRectangle2DTests
{
    // A 4 by 2 box with 0.5 corners at (1, 1), rotated 30 degrees.
    private static readonly RoundedRectangle2D TiltedRounded = RoundedRectangle2D.Builder.FromSize(4, 2, .5f, new(1, 1), MathF.PI / 6);

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
        part.Geometry = ((RoundedRectangle2D)part.Geometry!).WithSize(new(2, 1)) with
        {
            Radius = .25f
        };
        var before = PartContours2D.Contour(part, _ => Vector3.Zero);

        var json = model.ToJson();
        Assert.Contains("\"kind\": \"rounded-rectangle\"", json);
        var restored = CharacterModel.FromJson(json).Parts.Single();
        Assert.IsType<RoundedRectangle2D>(restored.Geometry);
        Assert.Equal(ShapeKinds2D.RoundedRectangle, restored.Geometry!.Kind);
        Assert.Equal(before, PartContours2D.Contour(restored, _ => Vector3.Zero));
    }

    [Fact]
    public void RotatedRoundedRectangleMatchesItsOutline()
    {
        var count = TiltedRounded.GetOutlineVertCount(cornerSegments: 64);
        var outline = new Vector2[count];
        TiltedRounded.GetOutlineVerts(outline, cornerSegments: 64);
        var bounds = ShapeBounds2D.Calculate(TiltedRounded);
        Assert.Equal(Rect2D.FromPoints(outline).Min, bounds.Min, Loose);
        Assert.Equal(Rect2D.FromPoints(outline).Max, bounds.Max, Loose);
        foreach (var v in outline) Assert.InRange(ShapeDistance2D.SignedDistance(v, TiltedRounded), -1e-3f, 1e-3f);
        Assert.Equal(RoundedRectangle2D.Builder.FromSize(4, 2, .5f).Area, TiltedRounded.Area, 5);
    }

    [Fact]
    public void CoreVerticesAreTheMappedInnerCorners()
    {
        Span<Vector2> core = stackalloc Vector2[4];
        var verts = TiltedRounded.GetVerts(core, out var radius);
        Assert.Equal(4, verts.Length);
        Assert.Equal(.5f, radius);
        Assert.Equal(TiltedRounded.Frame.FromFrame(-TiltedRounded.InnerHalfSize), verts[0], Near);
    }

    [Fact]
    public void CollapsedRoundedRectangleRotatesLikeACapsule()
    {
        var pill = RoundedRectangle2D.Builder.FromSize(4, 2, 1f, Vector2.Zero, MathF.PI / 4);
        Span<Vector2> core = stackalloc Vector2[4];
        Assert.Equal(2, pill.GetVerts(core, out _).Length);
        Assert.True(pill.GetSupportPoint(Vector2.UnitX).IsFinite());
        Assert.Equal(MathF.Sqrt(2) / 2 + 1f, ShapeBounds2D.Calculate(pill).Max.X, 4);
    }

    [Fact]
    public void WithSizeKeepsCenterAndAxis()
    {
        var resized = TiltedRounded.WithSize(new(8, 1));
        Assert.Equal(TiltedRounded.Center, resized.Center);
        Assert.Equal(TiltedRounded.Axis, resized.Axis);
        Assert.Equal(.5f, resized.Radius);
    }

    [Fact]
    public void RoundedJsonRoundTripsAndReadsMinMax()
    {
        var back = Assert.IsType<RoundedRectangle2D>(GeometryJson2D.ShapeFromJson(TiltedRounded.ToJson()).Validated());
        Assert.Equal(TiltedRounded.AngleRadians, back.AngleRadians, 5);
        Assert.Equal(.5f, back.Radius);
        var legacy = Assert.IsType<RoundedRectangle2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"rounded-rectangle\",\"min\":{\"x\":-2,\"y\":-1},\"max\":{\"x\":2,\"y\":1},\"radius\":0.4}").Validated());
        Assert.Equal(Vector2.Zero, legacy.Center);
        Assert.Equal(new Vector2(2, 1), legacy.HalfSize);
    }
}
