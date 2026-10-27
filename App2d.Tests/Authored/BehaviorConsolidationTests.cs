using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class BehaviorConsolidationTests
{
    [Fact]
    public void CharactersAndPassiveObjectsSharePartsWithoutDefaultCombatOnPassiveParts()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.Empty(catalog.Errors);
        Assert.Equal(11, catalog.Entities.Count);
        foreach (var entity in catalog.Entities.Values)
        {
            Assert.Same(catalog.Parts[entity.Id], entity.Asset);
            Assert.Contains(Path.DirectorySeparatorChar + "parts" + Path.DirectorySeparatorChar, catalog.PathOf(entity.Id));
            foreach (var collider in entity.Colliders.Append(entity.Movement))
                Assert.Same(entity.Asset.Geometry.Single(g => g.Id == collider.Id), collider.Definition);
            var behavior = JsonNode.Parse(entity.Asset.ToJson())!["behavior"]!;
            Assert.Equal(entity.Movement.Id, behavior["movement"]?.GetValue<string>() ?? ColliderUses.Movement);
            Assert.All(behavior["colliders"]!.AsObject(), pair => Assert.IsType<JsonValue>(pair.Value, exactMatch: false));
            Assert.Null(behavior["damage"]?["mass"]);
        }
        var passive = catalog.Parts["tumbling-crate"];
        Assert.NotNull(passive.Physics);
        Assert.Null(passive.Behavior);
        Assert.DoesNotContain(passive.Id, catalog.Entities.Keys);
        Assert.Null(Part.FromJson(passive.ToJson()).Behavior);
    }

    [Fact]
    public void ExplicitConversionKeepsDefaultsGuardPolicyAndTheLegacySocketPlacement()
    {
        const string legacy = """
            {"format":"app2d-entity","version":5,"id":"sample","name":"Sample","model":"person","motionSet":"standard",
             "behaviors":{"damage":{"health":7,"mass":2},"guard":{"openEvent":"open","blockSound":"block"}},
             "guard":{"collider":"shield"},"colliders":[{"id":"shield","use":"guard","socket":"right-grip","offsetX":0.15}]}
            """;
        Assert.Throws<JsonException>(() => Part.FromJson(legacy));
        var part = EntityPartConversion.Convert(legacy);
        Assert.Equal(7, part.Behavior!.Damage.Health);
        Assert.Equal(2, part.Behavior.Damage.KnockbackResistance);
        Assert.Equal(new GuardBehaviorDef { Collider = "shield", OpenEvent = "open", BlockSound = "block" }, part.Behavior.Guard);
        Assert.Null(part.Physics);
        Assert.Empty(part.Equipment);
        var shield = part.Geometry.Single(g => g.Id == "shield");
        Assert.Equal(Vector2.Zero, new(shield.OffsetX, shield.OffsetY));
        Assert.Equal(new Vector2(.3f, .3f), Assert.IsType<Rectangle2D>(shield.Geometry).Size);
        Assert.Equal(part.ToJson(), Part.FromJson(part.ToJson()).ToJson());
        Assert.Throws<JsonException>(() => EntityPartConversion.Convert(legacy.Replace("\"mass\":2", "\"unknownEffect\":2")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PartLocation.Origin)]
    public void AttachedGeometryCanUseThePartRootAsItsFrameOrTowardTarget(string? toward)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var part = StarterContent.SpearGuardEntity();
        var shape = part.Geometry.Single(g => g.Id == "spear-tip");
        shape.Prop = null;
        shape.Along = 0;
        shape.A = Humanoid.Head;
        shape.Frame = PartLocation.Origin;
        shape.B = toward;
        shape.Stretch = toward is not null;
        var entity = ResolvedEntity.Compile(part, catalog.Resolve, catalog.Animations.GetValueOrDefault, catalog.Parts.GetValueOrDefault);
        var local = PoseEvaluator.Sample(entity.Model, entity.Actions["attack"].Clip, .1f);
        var pose = new ActorPose(local, new(3, 4), 1);
        var collider = entity.Collider(shape.Id)!;
        var frame = collider.Frame(pose);
        Assert.True(Vector2.Distance(pose.World(Humanoid.Head).XY, frame.Origin.XY) < 1e-5f);
        Assert.True(Vector2.Distance(frame.At(new Vector2(.1f, .2f)).XY, collider.Place(pose).TransformPoint(new(.1f, .2f))) < 1e-5f);
        part.Geometry.Single(g => g.Id == part.Behavior!.Movement).Along = .1f;
        Assert.Throws<InvalidDataException>(part.Validate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void VisibleColliderGeometryUsesTheSameAnimatedFrameAndDrawingLayerDoesNotMoveCollision(int facing)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var part = StarterContent.SpearGuardEntity();
        var shape = part.Geometry.Single(g => g.Id == "spear-tip");
        shape.Material = new() { Fill = "#12ab34" };
        shape.OffsetX = .08f;
        shape.OffsetY = -.03f;
        shape.Angle = .4f;
        shape.ScaleX = shape.ScaleY = 1.2f;
        var entity = ResolvedEntity.Compile(part, catalog.Resolve, catalog.Animations.GetValueOrDefault, catalog.Parts.GetValueOrDefault);
        var action = entity.Actions["attack"];
        var local = PoseEvaluator.Sample(entity.Model, action.Clip, action.Hits[0].Start);
        var placed = new ActorPose(local, new(3, 4), facing);
        var collider = entity.Collider(shape.Id)!;
        var frame = collider.Frame(placed);
        var point = new Vector2(.13f, .09f);
        Assert.True(Vector2.Distance(frame.At(point).XY, collider.Place(placed).TransformPoint(point)) < 1e-5f);
        var before = collider.Region(placed);
        shape.Depth = -3;
        Assert.Equal(before.Pose, collider.Region(placed).Pose);

        var drawing = new PuppetDrawing();
        drawing.Build(entity, local);
        var drawn = drawing.Mesh.Vertices.ToArray().Where(v => v.Color.R == 0x12 && v.Color.G == 0xab && v.Color.B == 0x34).ToArray();
        Assert.NotEmpty(drawn);
        foreach (var vertex in drawn)
        {
            var world = placed.Place(new(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
            var localPoint = frame.ToLocal(world).XY;
            Assert.InRange(localPoint.X, -.211f, .211f);
            Assert.InRange(localPoint.Y, -.131f, .131f);
        }
        shape.Material = null;
        drawing.Build(entity, local);
        Assert.DoesNotContain(drawing.Mesh.Vertices.ToArray(), v => v.Color.R == 0x12 && v.Color.G == 0xab && v.Color.B == 0x34);
        Assert.Equal(before.Pose, collider.Region(placed).Pose);
    }
}
