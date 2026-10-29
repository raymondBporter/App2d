using App2d.CharacterSeeds.Diagnostics;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Tiles;
using App2d.Gameplay.World;
using App2d.Presentation.World.Presentation;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Tests.Gameplay.World;

public sealed class AuthoredObject2DTests
{
    private static string SamplePath => Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json");

    [Fact]
    public void JsonAssetRoundTripsItsShapeMaterialBodySurfaceAndGenericCurve()
    {
        var asset = Part.Load(SamplePath);
        var restored = Part.FromJson(asset.ToJson());
        Assert.Equal(asset.ToJson(), restored.ToJson());
        Assert.IsType<Rectangle2D>(restored.Geometry[0].Geometry);
        Assert.IsType<CubicBezier2D>(restored.Motion!.Curve);
        Assert.Equal("#25d2be", restored.Geometry[0].Material!.Fill);
        Assert.True(restored.Physics!.TransfersContactMotion);
        Assert.Equal(Vector2.UnitY, restored.Surface!.OneWayNormal);
        var origin = new Vector2(30, 40);
        var physics = new PhysicsWorld2D();
        using var instance = new AuthoredObject2D(EntityId2D.Create(), new("test", restored, origin), physics, 1, uint.MaxValue);
        Assert.Equal(BodyMotionType2D.Kinematic, instance.Body!.MotionType);
        Assert.Equal(origin, instance.WorldObject.Transform.Position);
        instance.Update(1.5f);
        physics.Step(1.5f);
        Assert.InRange(Vector2.Distance(new(110, 88), instance.WorldObject.Transform.Position), 0, .001f);
        Assert.Equal(Vector2.UnitY, PlatformCollisionRules2D.Attach(physics).Surface(instance.Body).OneWaySurfaceNormal);
    }

    [Theory]
    [InlineData("progress-name", typeof(JsonException))]
    [InlineData("empty-progress", typeof(ArgumentException))]
    [InlineData("period", typeof(ArgumentOutOfRangeException))]
    [InlineData("shape", typeof(ArgumentException))]
    [InlineData("dynamic-path", typeof(InvalidDataException))]
    [InlineData("normal", typeof(ArgumentOutOfRangeException))]
    [InlineData("unknown-field", typeof(JsonException))]
    [InlineData("no-shape", typeof(InvalidDataException))]
    [InlineData("material-without-geometry", typeof(InvalidDataException))]
    [InlineData("no-collision", typeof(InvalidDataException))]
    [InlineData("retired-collision", typeof(JsonException))]
    public void InvalidAssetDefinitionsAreRejected(string problem, Type exceptionType)
    {
        var json = JsonNode.Parse(File.ReadAllText(SamplePath))!;
        switch (problem)
        {
            case "progress-name": json["motion"]!["progress"] = new JsonArray("smoth"); break;
            case "empty-progress": json["motion"]!["progress"] = new JsonArray(); break;
            case "period": json["motion"]!["periodSeconds"] = 0; break;
            case "shape": json["geometry"]![0]!["geometry"]!["max"]!["x"] = -100; break;
            case "dynamic-path": json["physics"]!["motionType"] = "Dynamic"; break;
            case "normal": json["surface"]!["oneWayNormal"]!["y"] = 0; break;
            case "unknown-field": json["motion"]!["speeed"] = 3; break;
            case "no-shape": json.AsObject().Remove("geometry"); json.AsObject().Remove("material"); break;
            case "material-without-geometry": json["geometry"]![0]!.AsObject().Remove("geometry"); break;
            case "no-collision": json["geometry"]![0]!.AsObject().Remove("collision"); break;
            case "retired-collision": json["collision"] = json["geometry"]![0]!["geometry"]!.DeepClone(); break;
        }
        var error = Record.Exception(() => Part.FromJson(json.ToJsonString()));
        Assert.NotNull(error);
        Assert.IsType(exceptionType, error, exactMatch: false);
    }

    [Fact]
    public void JsonPlatformCarriesARiderAcrossACompleteCycleAndDisposesItsBody()
    {
        var asset = Part.Load(SamplePath);
        asset = asset with
        {
            Motion = asset.Motion! with { Curve = LineSegmentCurve2D.Builder.Create(Vector2.Zero, new(20, 0)), PeriodSeconds = 4 }
        };
        var physics = new PhysicsWorld2D { Gravity = new(0, -100) };
        var instance = new AuthoredObject2D(EntityId2D.Create(), new("lift", asset, Vector2.Zero), physics, 1, uint.MaxValue);
        var riderObject = new SpatialObject2D(Rectangle2D.Builder.CreateFromSize(10, 10));
        riderObject.Transform.Position = new(0, 11);
        var rider = physics.AddBody(riderObject, BodyMotionType2D.Dynamic);
        rider.Restitution = 0;
        for (var frame = 0; frame < 600; frame++)
        {
            instance.Update(1f / 120);
            physics.Step(1f / 120);
            Assert.InRange(MathF.Abs(riderObject.Transform.Position.Y - 11), 0, .001f);
            Assert.InRange(MathF.Abs(riderObject.Transform.Position.X - instance.WorldObject.Transform.Position.X), 0, .001f);
        }
        instance.Dispose();
        instance.Dispose();
        Assert.DoesNotContain(instance.Body!, physics.Bodies);
        Assert.Throws<ObjectDisposedException>(() => instance.Update(.1f));
    }

    [Fact]
    public void SnapshotPresentationPreservesGenericGeometryAndOnlyMovesWhenStateArrives()
    {
        var asset = Part.Load(SamplePath) with { Geometry = [new() { Id = "circle", A = "origin", Collision = new(), Geometry = Circle2D.Builder.Create(12), Material = new() { Fill = "#25d2be" } }] };
        var placement = new ObjectPlacement2D("circle-lift", asset, new(-200, 100));
        var physics = new PhysicsWorld2D();
        var map = new EditableTileMap2D(32, 32, 32, 32, new(-512, -640), ["dark-cave"]);
        using var level = new SideScrollerLevel2D(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, objects: [placement]);
        level.CreateSimulation(physics.CollisionSystem, physics, new(), 1, 2, 4);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new WorldPresentation2D(scene, textures);
        view.ApplyState(level.CaptureContent(), level.CaptureWorld());
        var visual = Assert.Single(scene);
        Assert.IsType<Circle2D>(asset.Geometry[0].Geometry);
        var shader = Assert.IsType<PartShader2D>(visual.Shader);
        Assert.Same(asset, shader.Part);
        level.UpdateWorldMotion(.5f);
        physics.Step(.5f);
        Assert.Equal(placement.Position, visual.Transform.Position);
        view.ApplyState(level.CaptureContent(), level.CaptureWorld());
        Assert.Equal(Assert.Single(level.Objects).WorldObject.Transform.Position, visual.Transform.Position);
        view.ApplyState(App2d.Contracts.World.LevelContent2D.Empty, App2d.Contracts.World.WorldState2D.Empty);
        Assert.Empty(scene);
    }

    [Fact]
    public void SeparateCollisionEntrySuppliesTheBodyAndArtworkStillDraws()
    {
        var asset = SeparateCollision();
        var physics = new PhysicsWorld2D();
        using var instance = new AuthoredObject2D(EntityId2D.Create(), new("lift", asset, Vector2.Zero), physics, 1, uint.MaxValue);
        Assert.IsType<Circle2D>(instance.WorldObject.Shape);
        Assert.IsType<Rectangle2D>(instance.CaptureDefinition().Part.Geometry[0].Geometry);
    }

    [Fact]
    public void CollisionOnlyObjectHasABodyAndNoVisual()
    {
        var asset = SeparateCollision();
        asset.Geometry.RemoveAt(0);
        var physics = new PhysicsWorld2D();
        using var instance = new AuthoredObject2D(EntityId2D.Create(), new("sensor", asset, Vector2.Zero), physics, 1, uint.MaxValue);
        Assert.NotNull(instance.Body);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new WorldPresentation2D(scene, textures);
        view.ApplyState(new LevelContent2D(1, [], [], null) { Objects = [instance.CaptureDefinition()] }, WorldState2D.Empty with { Objects = [instance.CaptureState()] });
        Assert.Empty(scene);
    }

    [Fact]
    public void CollisionShapeRoundTripsThroughJson()
    {
        var asset = SeparateCollision();
        var restored = Part.FromJson(asset.ToJson());
        Assert.IsType<Circle2D>(restored.Geometry[1].Geometry);
        Assert.Null(restored.Geometry[1].Material);
        Assert.IsType<Rectangle2D>(restored.Geometry[0].Geometry);
    }

    [Fact]
    public void RenderingAndPhysicsAreIndependentOptIns()
    {
        var source = Part.Load(SamplePath);
        var physics = new PhysicsWorld2D();
        using var visualOnly = new AuthoredObject2D(EntityId2D.Create(), new("art", source with { Physics = null, Surface = null }, Vector2.Zero), physics, 1, uint.MaxValue);
        Assert.Null(visualOnly.Body);
        visualOnly.Update(1);
        Assert.NotEqual(Vector2.Zero, visualOnly.WorldObject.Transform.Position);
        using var collisionOnly = new AuthoredObject2D(EntityId2D.Create(), new("invisible", source with { Geometry = [source.Geometry[0] with { Material = null }] }, Vector2.Zero), physics, 1, uint.MaxValue);
        Assert.NotNull(collisionOnly.Body);
        Assert.Null(Assert.Single(collisionOnly.CaptureDefinition().Part.Geometry).Material);
    }

    private static Part SeparateCollision()
    {
        var asset = Part.Load(SamplePath);
        asset.Geometry[0].Collision = null;
        asset.Geometry.Add(new() { Id = "body", Geometry = Circle2D.Builder.Create(4), Collision = new() });
        return asset;
    }
}
