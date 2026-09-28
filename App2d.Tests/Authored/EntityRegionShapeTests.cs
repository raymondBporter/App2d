using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class EntityRegionShapeTests
{
    [Fact]
    public void CircleOverlapUsesTheExactRadiusInsteadOfItsDrawnOutlineOrBounds()
    {
        var circle = EntityRegion.Circle("round", Vector2.Zero, 1);
        Assert.False(circle.Overlaps(EntityRegion.Box("corner", new(.9f, .9f), new(.2f)), Vector2.Zero, Vector2.Zero));
        Assert.True(circle.Overlaps(EntityRegion.Box("touch", new(1.1f, 0), new(.2f)), Vector2.Zero, Vector2.Zero));
        Assert.IsType<Circle2D>(circle.ToShape());
    }

    [Fact]
    public void CapsuleOverlapSupportsCrossingTranslationAndTouching()
    {
        var horizontal = EntityRegion.Capsule("horizontal", new(-1, 0), new(1, 0), .25f);
        var vertical = EntityRegion.Capsule("vertical", new(0, -1), new(0, 1), .25f);
        Assert.True(horizontal.Overlaps(vertical, Vector2.Zero, Vector2.Zero));
        Assert.True(horizontal.Overlaps(horizontal, Vector2.Zero, new(0, .5f)));
        Assert.False(horizontal.Overlaps(horizontal, Vector2.Zero, new(0, .51f)));
        Assert.IsType<Capsule2D>(horizontal.Scaled(40).ToShape());
        Assert.True(horizontal.Scaled(40).Overlaps(vertical.Scaled(40), Vector2.Zero, Vector2.Zero));
    }

    [Fact]
    public void RegionTestsTheActualPhysicsShapeInsteadOfItsBoundingBox()
    {
        var region = EntityRegion.Box("attack", Vector2.Zero, new(2));
        var target = new SpatialObject2D(new Circle2D(.1f));
        target.Transform.Position = new(1.1f, 1.1f);
        Assert.True(region.ToShape() is ConvexPolygon2D);
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
        window.Shape = "circle";
        window.Width = .4f;
        var restored = EntityAsset.FromJson(asset.ToJson());
        var entity = ResolvedEntity.Compile(restored, catalog.Resolve, catalog.Animations.GetValueOrDefault, catalog.Props.GetValueOrDefault);
        var hit = entity.Actions.Values.SelectMany(action => action.Hits).First(h => h.Window.Id == window.Id);
        var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, entity.Actions.Values.First(action => action.Hits.Contains(hit)).Clip, hit.Start), new(3, 0), 1);
        Assert.IsType<Circle2D>(EntityCollision.Attack(entity, pose, hit).ToShape());

        window.Shape = "capsule";
        window.Width = .2f;
        window.Height = .3f;
        Assert.Contains("capsule width", Assert.Throws<InvalidDataException>(asset.Validate).Message);
        window.Shape = "unknown";
        Assert.Contains("shape must be", Assert.Throws<InvalidDataException>(asset.Validate).Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void AuthoredCapsuleFollowsTheProjectedAnchorAxis(int facing)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var asset = EntityAsset.FromJson(catalog.Entities["spear-guard"].Asset.ToJson());
        var window = asset.Actions.SelectMany(action => action.Hits).First();
        window.Shape = "capsule";
        window.Width = .8f;
        window.Height = .2f;
        var entity = ResolvedEntity.Compile(asset, catalog.Resolve, catalog.Animations.GetValueOrDefault, catalog.Props.GetValueOrDefault);
        var attack = entity.Actions["attack"];
        var hit = attack.Hits.First(h => h.Window.Id == window.Id);
        var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, attack.Clip, hit.Start), new(3, 0), facing);
        var capsule = Assert.IsType<Capsule2D>(EntityCollision.Attack(entity, pose, hit).ToShape());
        var anchorAxis = EntityCollision.Anchor(entity, pose, hit.Window).Axis;
        Assert.True(Vector2.Dot(capsule.End - capsule.Start, anchorAxis) > 0);
        Assert.Equal(.6f, Vector2.Distance(capsule.Start, capsule.End), 3);
        Assert.Equal(.1f, capsule.Radius, 3);
    }
}
