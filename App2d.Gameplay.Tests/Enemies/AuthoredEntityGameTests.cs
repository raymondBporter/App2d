using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;
using System.Text.Json;
using Xunit;

namespace App2d.Gameplay.Tests.Enemies;

public sealed class AuthoredEntityGameTests
{
    private static EntityCatalog Catalog() => new(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters")));
    private static SideScrollerSimulation2D Game(EntityCatalog catalog)
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        return SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [],
        [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 40)),
         new(2, WorldThingKind2D.Shieldback, null, true, new(-290, 42)),
         new(3, WorldThingKind2D.BoilerBrute, null, true, new(-80, 42)),
         new(4, WorldThingKind2D.Rival, null, true, new(90, 42)),
         new(5, WorldThingKind2D.GreenDinosaur, null, true, new(210, 42))])
        { Characters = catalog, PlayerMaximumHealth = 30 });
    }
    [Fact]
    public void ExistingPlacementsBecomeAuthoredActorsAndReplayCombatExactly()
    {
        using var game = Game(Catalog());
        Assert.Equal(new[] { "needle", "maul", "cinder", "scrap-hound" }, game.Session.CaptureEnemies().Select(s => s.TypeId));
        Assert.All(game.Level.EnemySystem.Combatants, actor => Assert.IsType<AuthoredEnemy2D>(actor));
        for (var i = 0; i < 70; i++) game.Session.Advance();
        var checkpoint = game.Session.CaptureCheckpoint();
        string[] Run() => Enumerable.Range(0, 240).Select(i =>
        {
            var tick = game.Session.Tick + 1;
            var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick,
                new PersonCommand2D { MoveX = i < 80 ? .25f : 0, PrimaryHeld = i % 100 < 65 }));
            return JsonSerializer.Serialize(frame with { Content = frame.Content with { Revision = 0 } }, new JsonSerializerOptions { IncludeFields = true });
        }).ToArray();
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint); var second = Run();
        Assert.Equal(first, second);
        Assert.Contains(first, json => json.Contains("EntityId") && json.Contains("attack"));
    }
    [Fact]
    public void SwordUsesAuthoredTimingAndPoseRegionsIncludeHeadsOutsideMovementBody()
    {
        var catalog = Catalog();
        using var game = Game(catalog);
        game.Arsenal.UsePrimary(1);
        Assert.Equal(catalog.Types["player"].Actions["attack"].Duration, game.Arsenal.CaptureActionState().DurationSeconds);
        var enemy = Assert.IsType<AuthoredEnemy2D>(game.Level.EnemySystem.Combatants[0]);
        var type = enemy.Type; var pose = new EntityPose(catalog.Libraries[type.Library]);
        pose.Evaluate(type, type.Actions["idle"], 0, false);
        var head = pose.Hurt.Single(r => r.Id == "head");
        var root = enemy.WorldObject.Transform.Position - new Vector2(0, type.Movement.Height * 20);
        var point = head.Points.Aggregate(Vector2.Zero, (a, b) => a + b) / head.Points.Count * 40 + root;
        var hit = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new(2)));
        hit.Transform.Position = point;
        Assert.True(game.Combat.ResolveAttack(hit, game.Player.Id, 777, CombatFaction2D.Player, SideScrollerLayers2D.Enemy, 1, _ => Vector2.Zero));
        Assert.Equal(type.Health - 1, enemy.Health.Current);
        Assert.False(game.Combat.ResolveAttack(hit, game.Player.Id, 778, CombatFaction2D.Player, SideScrollerLayers2D.World, 1, _ => Vector2.Zero));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CinderProjectilesDamagePlayerButStopAtTerrain(bool wall)
    {
        var catalog = Catalog(); var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var enemy = new AuthoredEnemy2D(EntityId2D.Create(), catalog, "cinder", physics.CollisionSystem, physics, new(0, 31), 1, 4);
        enemy.SetSimulationEnabled(true);
        var player = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(150, 40), 2, 1, CombatFaction2D.Player, 30);
        if (wall)
        {
            var shape = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new(5, 400))); shape.Transform.Position = new(95, 40);
            var body = physics.AddBody(shape, BodyMotionType2D.Static); body.CollisionLayer = 1; body.CollisionMask = 6;
        }
        for (var i = 0; i < 360; i++)
        { enemy.Update(1f / 120, player.Position); enemy.SyncAfterPhysics(); enemy.TryResolvePlayerHit(player); }
        if (wall) Assert.Equal(30, player.Health.Current); else Assert.True(player.Health.Current < 30);
        var before = enemy.CaptureState(); enemy.SetSimulationEnabled(false); enemy.Update(2, player.Position);
        Assert.Equal(before.ActionSeconds, enemy.CaptureState().ActionSeconds);
        Assert.Empty(enemy.CaptureState().Bolts);
    }
}
