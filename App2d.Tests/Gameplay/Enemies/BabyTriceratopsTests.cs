using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Tiles;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using System.Numerics;

namespace App2d.Tests.Gameplay.Enemies;

public sealed class BabyTriceratopsTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));
    private static (AuthoredEntityEnemy2D Enemy, PhysicsWorld2D Physics, Person2D Player) Create(int facing = 1, float floorWidth = 1000)
    {
        Assert.True(Catalog.Errors.Count == 0, string.Join("\n", Catalog.Errors));
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var floor = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(floorWidth, 20)));
        floor.Transform.Position = new(0, -10);
        physics.AddBody(floor, BodyMotionType2D.Static).CollisionLayer = 1;
        var enemy = new AuthoredEntityEnemy2D(EntityId2D.Create(), Catalog.Entities["baby-triceratops"], physics, new(0, 19), 1, 4);
        enemy.SetSimulationEnabled(true);
        var player = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(facing * 140, 26), 2, 1, CombatFaction2D.Player, 30);
        return (enemy, physics, player);
    }
    private static void Tick(AuthoredEntityEnemy2D enemy, PhysicsWorld2D physics, Vector2 target, int count)
    {
        for (var i = 0; i < count; i++) { enemy.Update(1f / 120, target); physics.Step(1f / 120); enemy.SyncAfterPhysics(); }
    }

    [Theory, InlineData(-1), InlineData(1)]
    public void WindupIsStationaryRushCommitsAndRecoveryStops(int facing)
    {
        var (enemy, physics, player) = Create(facing);
        Tick(enemy, physics, player.Position, 90);
        Assert.Equal(0, enemy.Body.LinearVelocity.X);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
        Tick(enemy, physics, new(-facing * 140, 26), 65);
        Assert.Equal(facing, enemy.Pose.Facing);
        Assert.Equal(facing * 200, enemy.Body.LinearVelocity.X);
        Assert.NotEmpty(enemy.GetActiveAttackHitboxes());
        Tick(enemy, physics, new(-facing * 140, 26), 115);
        Assert.Equal(0, enemy.Body.LinearVelocity.X);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
        Assert.Equal(facing, enemy.Pose.Facing);
        Assert.Contains(enemy.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "charge-scrape");
    }

    [Theory, InlineData(-1, false), InlineData(1, false), InlineData(-1, true), InlineData(1, true)]
    public void ChargeDamagesOnceAndCanBeJumpedOver(int facing, bool dodge)
    {
        var (enemy, physics, player) = Create(facing);
        for (var i = 0; i < 340; i++)
        {
            if (dodge && i == 90) player.WorldObject.Transform.Position += new Vector2(0, 120);
            Tick(enemy, physics, player.Position, 1);
            var before = player.Health.Current; enemy.TryResolvePlayerHit(player);
            if (before != player.Health.Current) Assert.InRange(enemy.CaptureState().ActionSeconds, 1, 1.85f);
        }
        Assert.Equal(dodge ? 30 : 27, player.Health.Current);
    }

    [Theory, InlineData(60, false), InlineData(150, false), InlineData(150, true)]
    public void NormalDamageInterruptsBothPreparationAndRush(int at, bool kill)
    {
        var (enemy, physics, player) = Create();
        Tick(enemy, physics, player.Position, at);
        enemy.TakeDamage(kill ? 8 : 1, new(-100, 0));
        Assert.Empty(enemy.GetActiveAttackHitboxes());
        Assert.Equal(kill ? "death" : "hit", enemy.CaptureState().ActionId);
        Tick(enemy, physics, player.Position, 20);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
        Assert.False(enemy.CaptureState().IsAttacking);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void LedgesAndWallsStopTheChargeAndDisableItsHitRegion(bool wall)
    {
        var (enemy, physics, player) = Create(floorWidth: wall ? 1000 : 80);
        if (wall)
        {
            var obstacle = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(12, 120)));
            obstacle.Transform.Position = new(80, 60);
            physics.AddBody(obstacle, BodyMotionType2D.Static).CollisionLayer = 1;
        }
        Tick(enemy, physics, player.Position, 200);
        Assert.Equal(0, enemy.Body.LinearVelocity.X);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
        Assert.True(enemy.WorldObject.Transform.Position.X < (wall ? 50 : 12));
        Tick(enemy, physics, player.Position, 10);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
    }

    [Fact]
    public void PlacementAndChargeRunInTheRealTerrainSimulation()
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [],
            [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 26)),
             new(2, WorldThingKind2D.BabyTriceratops, null, true, new(-218, 19))])
        { AuthoredCharacters = Catalog, PlayerMaximumHealth = 100 });
        Assert.Equal("baby-triceratops", Assert.Single(game.Session.CaptureEnemies()).TypeId);
        for (var i = 0; i < 150; i++) game.Session.Advance();
        string[] Run() => [.. Enumerable.Range(0, 240).Select(_ =>
        {
            game.Session.Advance(); var state = Assert.Single(game.Session.CaptureEnemies());
            return $"{state.Position};{state.Velocity};{state.ActionId};{state.ActionSeconds};{game.Player.Health.Current}";
        })];
        Run();
        Assert.True(game.Player.Health.Current < 100);
    }
}
