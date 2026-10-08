using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class EntityRegionShapeTests
{
    [Fact]
    public void CircleOverlapUsesTheExactRadiusInsteadOfItsDrawnOutlineOrBounds()
    {
        var circle = EntityRegion.Circle("round", Vector2.Zero, 1);
        Assert.False(circle.Overlaps(EntityRegion.Box("corner", new(.9f, .9f), new(.2f)), Vector2.Zero, Vector2.Zero));
        Assert.True(circle.Overlaps(EntityRegion.Box("touch", new(1.1f, 0), new(.2f)), Vector2.Zero, Vector2.Zero));
        Assert.IsType<Circle2D>(circle.Shape);
    }

    [Fact]
    public void CapsuleOverlapSupportsCrossingTranslationAndTouching()
    {
        var horizontal = EntityRegion.Capsule("horizontal", new(-1, 0), new(1, 0), .25f);
        var vertical = EntityRegion.Capsule("vertical", new(0, -1), new(0, 1), .25f);
        Assert.True(horizontal.Overlaps(vertical, Vector2.Zero, Vector2.Zero));
        Assert.True(horizontal.Overlaps(horizontal, Vector2.Zero, new(0, .5f)));
        Assert.False(horizontal.Overlaps(horizontal, Vector2.Zero, new(0, .51f)));
        Assert.IsType<Capsule2D>(horizontal.Scaled(40).Shape);
        Assert.True(horizontal.Scaled(40).Overlaps(vertical.Scaled(40), Vector2.Zero, Vector2.Zero));
    }

    [Fact]
    public void RegionTestsTheActualPhysicsShapeInsteadOfItsBoundingBox()
    {
        var region = EntityRegion.Box("attack", Vector2.Zero, new(2));
        var target = new SpatialObject2D(new Circle2D(.1f));
        target.Transform.Position = new(1.1f, 1.1f);
        Assert.True(region.Shape is Rectangle2D);
        Assert.False(region.Overlaps(target));
        target.Transform.Position = new(1.09f, 0);
        Assert.True(region.Overlaps(target));
    }

    [Fact]
    public void AuthoredHitShapeSurvivesJsonAndBuildsAnExactRegion()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var asset = EntityAsset.FromJson(catalog.Entities["hero"].Asset.ToJson());
        var window = asset.Actions.SelectMany(action => action.Hits).First();
        window.Shape = new CircleShapeDefinition2D { Center = new(0, 0), Radius = .2f };
        var restored = EntityAsset.FromJson(asset.ToJson());
        var entity = ResolvedEntity.Compile(restored, catalog.Resolve, catalog.Animations.GetValueOrDefault, catalog.Props.GetValueOrDefault);
        var hit = entity.Actions.Values.SelectMany(action => action.Hits).First(h => h.Window.Id == window.Id);
        var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, entity.Actions.Values.First(action => action.Hits.Contains(hit)).Clip, hit.Start), new(3, 0), 1);
        Assert.IsType<Circle2D>(EntityCollision.Attack(entity, pose, hit).Shape);

        window.Shape = new CircleShapeDefinition2D { Center = new(0, 0), Radius = 0 };
        Assert.Contains("shape", Assert.Throws<InvalidDataException>(asset.Validate).Message);
        window.Shape = new HalfSpaceShapeDefinition2D { Normal = new(0, 1), Offset = 0 };
        Assert.Contains("convex", Assert.Throws<InvalidDataException>(asset.Validate).Message);
        Assert.Throws<JsonException>(() => EntityAsset.FromJson(restored.ToJson().Replace("\"kind\": \"circle\"", "\"kind\": \"blob\"")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void AuthoredCapsuleFollowsTheProjectedAnchorAxis(int facing)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var asset = EntityAsset.FromJson(catalog.Entities["spear-guard"].Asset.ToJson());
        var window = asset.Actions.SelectMany(action => action.Hits).First();
        window.Shape = new CapsuleShapeDefinition2D { Start = new(-.3f, 0), End = new(.3f, 0), Radius = .1f };
        var entity = ResolvedEntity.Compile(asset, catalog.Resolve, catalog.Animations.GetValueOrDefault, catalog.Props.GetValueOrDefault);
        var attack = entity.Actions["attack"];
        var hit = attack.Hits.First(h => h.Window.Id == window.Id);
        var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, attack.Clip, hit.Start), new(3, 0), facing);
        var region = EntityCollision.Attack(entity, pose, hit);
        var capsule = Assert.IsType<Capsule2D>(region.Shape);
        var frame = EntityCollision.Anchor(entity, pose, hit.Window);
        var worldAxis = region.Pose.TransformPoint(capsule.End) - region.Pose.TransformPoint(capsule.Start);
        Assert.True(Vector2.Dot(worldAxis, frame.Axis) > 0);
        Assert.Equal(.6f, worldAxis.Length(), 3);
        Assert.Equal(.1f, capsule.Radius, 3);
        Assert.True(Vector2.Dot(region.Pose.YAxis, frame.Across) >= 0, "the frame keeps its across direction when the actor is mirrored");
    }

    [Fact]
    public void RegionsExposeTheirShapeBoundsAndOutline()
    {
        var box = EntityRegion.Box("hurt", new(2, 3), new(4, 2));
        Assert.IsType<Rectangle2D>(box.Shape);
        Assert.Equal(new Rect2D(new(0, 2), new(4, 4)), box.Bounds);
        Assert.Equal(4, box.Outline().Length);
        var circle = EntityRegion.Circle("round", new(1, 1), .5f).Scaled(2);
        Assert.Equal(2f, circle.Pose.Scale);
        Assert.Equal(.5f, Assert.IsType<Circle2D>(circle.Shape).Radius);
        Assert.Equal(new Rect2D(new(1, 1), new(3, 3)), circle.Bounds);
        Assert.Equal(24, circle.Outline().Length);
        Assert.Equal(26, EntityRegion.Capsule("reach", default, Vector2.UnitX, .1f).Outline().Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => EntityRegion.Box("flat", default, new(1, 0)));
    }

    [Fact]
    public void PlacedRegionsRotateMirrorAndScaleThroughTheirPose()
    {
        var region = new EntityRegion("r", Rectangle2D.FromSize(new Vector2(2, 1)), Similarity2D.FromAxis(new(5, 5), Vector2.UnitY));
        Assert.Equal(new Rect2D(new(4.5f, 4), new(5.5f, 6)), region.Bounds);
        var probe = new SpatialObject2D(new Circle2D(.05f));
        probe.Transform.Position = new(5, 5.9f);
        Assert.True(region.Overlaps(probe));
        probe.Transform.Position = new(5.8f, 5);
        Assert.False(region.Overlaps(probe));
        Assert.True(region.Overlaps(EntityRegion.Box("other", new(5, 6.2f), new(1, .5f))));
        var placed = region.ToSpatialObject().WorldBounds;
        Assert.True(Vector2.Distance(placed.Min, region.Bounds.Min) < 1e-4f && Vector2.Distance(placed.Max, region.Bounds.Max) < 1e-4f);
        Assert.Equal(new Rect2D(new(9, 8), new(11, 12)), region.Scaled(2).Bounds);
        Assert.All(region.Outline(), point => Assert.True(region.Bounds.InflatedBy(1e-4f, 1e-4f).Contains(point)));
        var mirrored = new EntityRegion("m", new Capsule2D(new(0, 0), new(1, 0), .1f), Similarity2D.FromAxis(Vector2.Zero, new(-1, 0), mirror: true));
        Assert.Equal(new Vector2(-1, .5f), mirrored.Pose.TransformPoint(new(1, .5f)));
    }
}
