using App2d.CharacterStudio.CharacterSeeds.Diagnostics;
using App2d.Contracts.Simulation;
using App2d.Contracts.World;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Tiles;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Gameplay.World.Vegetation;
using App2d.Levels;
using App2d.Presentation.World.Presentation;
using App2d.Things;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class PhysicalPartTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);

    [Theory]
    [InlineData("dumbbell", TileKind2D.Solid)]
    [InlineData("tumbling-crate", TileKind2D.Solid)]
    [InlineData("dumbbell", TileKind2D.OneWay)]
    [InlineData("tumbling-crate", TileKind2D.OneWay)]
    public void PlacedPartsLandOnStreamedTerrain(string partId, TileKind2D floor)
    {
        var map = new EditableTileMap2D(64, 16, 32, 32, Vector2.Zero, ["dark-cave"]);
        for (var x = 0; x < map.Width; x++) map.SetTileKind(x, 0, floor);
        var things = new List<WorldThingSpec2D>
        {
            new(1, WorldThingKind2D.PlayerSpawn, null, true, new(100, 100))
        };
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, things)
        {
            AuthoredCharacters = Catalog,
            Objects = [new("prop", Catalog.Parts[partId], new(300, 160))]
        });
        var item = Assert.Single(game.Level.Objects);
        var touched = false;
        for (var i = 0; i < 360; i++)
        {
            game.Session.Advance(new PlayerInput2D(game.Player.Id, default));
            touched |= game.Physics.LastContacts.Any(c => ReferenceEquals(c.First, item.Body) || ReferenceEquals(c.Second, item.Body));
        }
        Assert.True(touched, "The placed part never contacted the streamed floor.");
        Assert.InRange(item.WorldObject.WorldBounds.Min.Y, 31, 35);
    }

    [Fact]
    public void WorldPartsLandOnGeneratedTreeBranches()
    {
        var tree = new GiantTree2D(new(500, 0), new()
        {
            Height = 320,
            TrunkWidth = 32,
            BranchLength = new(250, 250),
            BranchGap = new(80, 80)
        }, 3107);
        var branch = tree.Branches.MaxBy(b => b.Bounds.Max.Y);
        var map = new EditableTileMap2D(64, 32, 32, 32, Vector2.Zero, ["dark-cave"]);
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map,
            [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(100, 100))])
        {
            Objects = [new("prop", Catalog.Parts["dumbbell"], new(branch.Bounds.Center.X, branch.Bounds.Max.Y + 120))],
            GiantTrees = [tree]
        });
        var item = Assert.Single(game.Level.Objects);
        for (var i = 0; i < 360; i++) game.Session.Advance(new PlayerInput2D(game.Player.Id, default));
        Assert.InRange(item.WorldObject.WorldBounds.Min.Y, branch.Bounds.Max.Y - 1, branch.Bounds.Max.Y + 3);
    }

    [Theory]
    [InlineData("dumbbell")]
    [InlineData("tumbling-crate")]
    public void JsonPartsFallCollideRotateAndLoseSpeedThroughContactFriction(string id)
    {
        var (Speed, Spin) = Run(0);
        var rough = Run(.65f);
        Assert.True(rough.Speed < Speed - 5, $"rough={rough.Speed}, slippery={Speed}");

        (float Speed, float Spin) Run(float friction)
        {
            var asset = Part.FromJson(Catalog.Parts[id].ToJson());
            asset.Physics!.Friction = friction;
            var physics = new PhysicsWorld2D { Gravity = new(0, -240) };
            var ground = physics.AddBody(new SpatialObject2D(Rectangle2D.Builder.Create(new(-1000, -30), new(1000, 0))), BodyMotionType2D.Static);
            ground.Friction = friction; ground.Restitution = 0;
            using var item = new AuthoredObject2D(new(1), new(id, asset, new(0, 120)), physics, 1, uint.MaxValue);
            Assert.False((object)item is ICombatant2D);
            item.WorldObject.Transform.Rotation = .31f;
            item.Body!.LinearVelocity = new(80, 0);
            physics.Step(1f / 120);
            Assert.True(item.WorldObject.Transform.Position.Y < 120);
            var touched = false;
            var spin = 0f;
            for (var i = 0; i < 480; i++)
            {
                item.Update(1f / 120); physics.Step(1f / 120);
                touched |= physics.LastContacts.Count > 0;
                spin = Math.Max(spin, Math.Abs(item.Body.AngularVelocity));
            }
            Assert.True(touched);
            Assert.True(spin > .1f, $"No contact-driven spin: {spin}");
            Assert.InRange(item.WorldObject.Transform.Position.Y, 0, 80);
            return (Math.Abs(item.Body.LinearVelocity.X), spin);
        }
    }

    [Theory]
    [InlineData(BodyMotionType2D.Dynamic)]
    [InlineData(BodyMotionType2D.Kinematic)]
    [InlineData(BodyMotionType2D.Static)]
    public void PauseResumeAndStateRestoreKeepMotionTypeVelocitiesAndRegistrations(BodyMotionType2D motion)
    {
        var asset = Part.FromJson(Catalog.Parts["dumbbell"].ToJson());
        asset.Physics!.MotionType = motion;
        var physics = new PhysicsWorld2D();
        var item = new AuthoredObject2D(new(1), new("one", asset, new(20, 50)), physics, 1, uint.MaxValue);
        item.Body!.LinearVelocity = new(30, 10); item.Body.AngularVelocity = .8f;
        item.Update(.2f); physics.Step(.2f);
        var active = item.CaptureState();
        item.SetSimulationEnabled(false);
        var paused = item.CaptureState();
        for (var i = 0; i < 5; i++) { item.Update(.1f); physics.Step(.1f); }
        Assert.Equal(paused, item.CaptureState());
        Assert.False(item.Body.IsCollider);
        Assert.Equal(BodyMotionType2D.Static, item.Body.MotionType);
        item.SetSimulationEnabled(true);
        Assert.Equal(motion, item.Body.MotionType);
        Assert.Equal(active, item.CaptureState());
        item.WorldObject.Transform.Position = new(500, 500);
        item.RestoreState(paused);
        Assert.Equal(paused, item.CaptureState());
        item.RestoreState(active);
        Assert.Equal(active, item.CaptureState());
        Assert.Equal(motion, item.Body.MotionType);
        Assert.Single(physics.Bodies);
        item.Dispose(); item.Dispose();
        Assert.Empty(physics.Bodies);
        Assert.Empty(physics.CollisionSystem.Colliders);
    }

    [Fact]
    public void ShippedCavernSpawnsAllFiveSharedDumbbellsAsOrdinaryObjects()
    {
        using var world = WorldFile2D.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "world.json"));
        var things = world.PositionThings.Where(t => t.TypeKey != LevelDatabase2D.ObjectTypeKey)
            .Select(ThingTypeRegistry2D.ToRuntime).ToArray();
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), world.TileMap, things)
        {
            AuthoredCharacters = Catalog,
            Objects = world.Objects
        });
        var parts = game.Level.Objects.Where(o => o.CaptureDefinition().Part.Id == "dumbbell").ToArray();
        Assert.Equal([new Vector2(368, -226), new(3216, -194), new(7696, -226), new(15056, -162), new(19248, -130)],
            parts.Select(o => o.WorldObject.Transform.Position).OrderBy(p => p.X));
        var expected = Catalog.Parts["dumbbell"].ToJson();
        Assert.All(parts, part =>
        {
            Assert.NotNull(part.Body);
            Assert.Equal(expected, part.CaptureDefinition().Part.ToJson());
        });
        Assert.All(world.PositionThings, thing => Assert.NotEqual("tumble-prop", thing.TypeKey));
        Assert.Equal([1017L, 1018L, 1019L, 1020L, 1021L],
            world.PositionThings.Where(t => t.Asset == "parts/dumbbell.json").Select(t => t.ThingId));
    }

    [Fact]
    public void PlacedPartsUseWorldOwnershipStreamingAndReloadWithoutCombatants()
    {
        var map = new EditableTileMap2D(320, 32, 32, 32, Vector2.Zero, ["dark-cave"]);
        var cratePlacement = new ObjectPlacement2D("crate", Catalog.Parts["tumbling-crate"], new(400, 100));
        var dumbbellPlacement = new ObjectPlacement2D("dumbbell", Catalog.Parts["dumbbell"], new(200, 100));
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map,
            [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(100, 100))])
        { AuthoredCharacters = Catalog, Objects = [cratePlacement, dumbbellPlacement] });
        Assert.Empty(game.Level.EnemySystem.Combatants);
        Assert.Empty(game.Session.CaptureEnemies());
        Assert.Equal(2, game.Level.Objects.Count);
        Assert.Equal(["dumbbell", "tumbling-crate"], game.Level.CaptureContent().Objects.Select(o => o.Part.Id).Order());
        var objects = game.Level.Objects.ToArray();
        game.Level.UpdateStreaming(new(9000, 100));
        Assert.All(objects, o => Assert.False(o.IsEnabled));
        game.Level.UpdateStreaming(new(100, 100));
        Assert.All(objects, o => Assert.Equal(BodyMotionType2D.Dynamic, o.Body!.MotionType));
        game.Level.ReloadObjects([cratePlacement, dumbbellPlacement]);
        Assert.Equal(2, game.Level.Objects.Count);
        Assert.All(game.Level.Objects, o => Assert.DoesNotContain(o.Id, objects.Select(old => old.Id)));
        Assert.All(objects, o => Assert.DoesNotContain(o.Body!, game.Physics.Bodies));
        var bodies = game.Level.Objects.Select(o => o.Body!).ToArray();
        game.Level.Dispose();
        Assert.All(bodies, body => Assert.DoesNotContain(body, game.Physics.Bodies));
    }

    [Theory]
    [InlineData("dumbbell")]
    [InlineData("tumbling-crate")]
    public void PresentationMatchesScaledPivotedPhysicsAndWaitsForSnapshots(string id)
    {
        var asset = Part.FromJson(Catalog.Parts[id].ToJson());
        asset.Scale = 1.7f; asset.Pivot = "mount";
        asset.Locations = [new() { Id = "mount", X = 7, Y = -4 }];
        var physics = new PhysicsWorld2D();
        using var item = new AuthoredObject2D(new(1), new(id, asset, new(100, 80)), physics, 1, uint.MaxValue);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new WorldPresentation2D(scene, textures);
        var content = LevelContent2D.Empty with { Objects = [item.CaptureDefinition()] };
        var state = WorldState2D.Empty with { Objects = [item.CaptureState()] };
        view.ApplyState(content, state);
        var visual = Assert.Single(scene);
        var shader = Assert.IsType<PartShader2D>(visual.Shader);
        Assert.Same(asset, shader.Part);
        Assert.InRange(Vector2.Distance(visual.LocalBounds.Min, item.WorldObject.LocalBounds.Min), 0, .01f);
        Assert.InRange(Vector2.Distance(visual.LocalBounds.Max, item.WorldObject.LocalBounds.Max), 0, .01f);
        item.WorldObject.Transform.Rotation = .7f;
        item.WorldObject.Transform.Position = new(200, 30);
        Assert.Equal(new Vector2(100, 80), visual.Transform.Position);
        view.ApplyState(content, state with { Objects = [item.CaptureState()] });
        Assert.Equal(item.WorldObject.Transform.Matrix, visual.Transform.Matrix);
        item.SetSimulationEnabled(false);
        view.ApplyState(content, state with { Objects = [item.CaptureState()] });
        Assert.False(visual.IsVisible);
        view.ApplyState(LevelContent2D.Empty, WorldState2D.Empty);
        Assert.Empty(scene);
    }

    [Fact]
    public void PathClockRestoresAndPausesWithTheBody()
    {
        var part = Part.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json"));
        var physics = new PhysicsWorld2D();
        using var item = new AuthoredObject2D(new(1), new("lift", part, Vector2.Zero), physics, 1, uint.MaxValue);
        item.Update(.75f); physics.Step(.75f);
        var state = item.CaptureState();
        item.Update(.25f); physics.Step(.25f);
        var next = item.CaptureState();
        item.RestoreState(state);
        item.SetSimulationEnabled(false);
        item.Update(5); physics.Step(5);
        item.SetSimulationEnabled(true);
        item.Update(.25f); physics.Step(.25f);
        Assert.Equal(next, item.CaptureState());
    }

    [Fact]
    public void SteadyStatePartPresentationDoesNotAllocateOrMutateObservations()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        using var view = new WorldPresentation2D(new Scene2D(), textures);
        var part = Catalog.Parts["dumbbell"];
        var content = LevelContent2D.Empty with { Objects = [new(new(1), "dumbbell", part)] };
        var state = WorldState2D.Empty with { Objects = [new(new(1), Vector2.Zero, .3f)] };
        for (var i = 0; i < 1000; i++) view.ApplyState(content, state);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) view.ApplyState(content, state);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(Vector2.Zero, state.Objects[0].Position);
    }
}
