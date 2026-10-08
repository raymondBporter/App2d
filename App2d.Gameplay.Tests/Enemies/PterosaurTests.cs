using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Enemies;

public sealed class PterosaurTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));
    private static (AuthoredEntityEnemy2D Enemy, PhysicsWorld2D Physics, Person2D Player) Create(int facing = 1)
    {
        Assert.Empty(Catalog.Errors);
        var physics = new PhysicsWorld2D { Gravity = new(0, -1200) };
        var floor = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(2000, 20)));
        floor.Transform.Position = new(0, -10); physics.AddBody(floor, BodyMotionType2D.Static).CollisionLayer = 1;
        var enemy = new AuthoredEntityEnemy2D(EntityId2D.Create(), Catalog.Entities["flying-pterosaur"], physics, new(-facing * 96, 154), 1, 4);
        enemy.SetSimulationEnabled(true);
        var player = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(0, 26), 2, 1, CombatFaction2D.Player, 30);
        return (enemy, physics, player);
    }
    private static void Tick(AuthoredEntityEnemy2D enemy, PhysicsWorld2D physics, Vector2 target, int count = 1)
    {
        for (var i = 0; i < count; i++) { enemy.Update(1f / 120, target); physics.Step(1f / 120); enemy.SyncAfterPhysics(); }
    }

    [Fact]
    public void HoverIgnoresGravityAndKeepsAnimatingWhenThePlayerIsFarAway()
    {
        var (enemy, physics, _) = Create(); var before = enemy.Pose.Local.Points["near-tip"];
        Tick(enemy, physics, new(1000, 26), 35);
        Assert.Equal(0, enemy.Body.GravityScale); Assert.Equal(new Vector2(-96, 154), enemy.WorldObject.Transform.Position);
        Assert.False(enemy.CaptureState().IsAttacking); Assert.NotEqual(before, enemy.Pose.Local.Points["near-tip"]);
    }

    [Fact]
    public void AnAirborneTargetStillGetsAGroundedReachableRecovery()
    {
        var (enemy, physics, player) = Create(); Tick(enemy, physics, player.Position, 30);
        Tick(enemy, physics, new(0, 180), 225);
        Assert.InRange(enemy.WorldObject.Transform.Position.Y, 25.9f, 26.1f);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
    }

    [Fact]
    public void TargetIsSampledAtCommitRatherThanAtActionStart()
    {
        var (enemy, physics, player) = Create(); Tick(enemy, physics, player.Position, 30);
        Tick(enemy, physics, new(40, 26), 35);
        Assert.InRange(enemy.Body.LinearVelocity.X, 270, 273);
        var committed = enemy.Body.LinearVelocity;
        Tick(enemy, physics, new(-300, 26), 10);
        Assert.Equal(committed.X, enemy.Body.LinearVelocity.X); Assert.Equal(1, enemy.Pose.Facing);
    }

    [Theory, InlineData(-1), InlineData(1)]
    public void DiveCommitsAtTheTellAndEveryAttackHasALowVulnerablePass(int facing)
    {
        var (enemy, physics, player) = Create(facing);
        Tick(enemy, physics, player.Position, 35);
        Assert.Equal(new Vector2(-facing * 96, 154), enemy.WorldObject.Transform.Position);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
        Tick(enemy, physics, player.Position, 35);
        var direction = enemy.Body.LinearVelocity;
        Assert.True(direction.X * facing > 0); Assert.True(direction.Y < 0);
        Tick(enemy, physics, new(-facing * 300, 26), 20);
        Assert.Equal(direction.X, enemy.Body.LinearVelocity.X); Assert.Equal(facing, enemy.Pose.Facing);
        Tick(enemy, physics, new(-facing * 300, 26), 100);
        Assert.InRange(enemy.WorldObject.Transform.Position.Y, 25.9f, 26.1f);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
        // Broad wings do not make the body invulnerable; a grounded-height sword reaches the low pass.
        var registry = new CombatantRegistry2D(); registry.Register(enemy); registry.Register(player);
        var combat = new CombatSystem2D(physics.CollisionSystem, registry);
        var sword = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(70, 50)));
        sword.Transform.Position = new(enemy.WorldObject.Transform.Position.X, 30);
        Assert.True(combat.ResolveAttack(sword, player.Id, 1, CombatFaction2D.Player, 4, 1, _ => Vector2.Zero));
        Assert.Equal(3, enemy.Health.Current); Assert.False(enemy.CaptureState().IsAttacking);
    }

    [Theory, InlineData(-1, false), InlineData(1, false), InlineData(-1, true), InlineData(1, true)]
    public void SwoopHitsOnceAndALateDodgeClearsTheFixedLane(int facing, bool dodge)
    {
        var (enemy, physics, player) = Create(facing);
        for (var i = 0; i < 320; i++)
        {
            if (dodge && i == 60) player.WorldObject.Transform.Position = new(-facing * 220, 26);
            Tick(enemy, physics, player.Position);
            var before = player.Health.Current; enemy.TryResolvePlayerHit(player);
            if (before != player.Health.Current) Assert.InRange(enemy.CaptureState().ActionSeconds, .48f, 1.13f);
        }
        Assert.Equal(dodge ? 30 : 28, player.Health.Current);
        Assert.Contains(enemy.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "pterosaur-screech");
    }

    [Theory, InlineData(30, false), InlineData(85, false), InlineData(85, true)]
    public void HitsInterruptWindupAndDiveAndDeathDisablesFlight(int ticks, bool kill)
    {
        var (enemy, physics, player) = Create(); Tick(enemy, physics, player.Position, ticks);
        enemy.TakeDamage(kill ? 4 : 1, new(-80, 0));
        Assert.Empty(enemy.GetActiveAttackHitboxes());
        Assert.Equal(kill ? "death" : "hit", enemy.CaptureState().ActionId);
        Tick(enemy, physics, player.Position, 15); Assert.False(enemy.CaptureState().IsAttacking);
        if (kill) { Assert.False(enemy.Body.IsCollider); Assert.Equal(Vector2.Zero, enemy.Body.LinearVelocity); }
    }

    [Fact]
    public void ANewObstacleStopsTheCommittedDiveWithoutDamageThroughTerrain()
    {
        var (enemy, physics, player) = Create(); Tick(enemy, physics, player.Position, 58);
        var wall = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(12, 240)));
        wall.Transform.Position = new(-40, 120); physics.AddBody(wall, BodyMotionType2D.Static).CollisionLayer = 1;
        for (var i = 0; i < 200; i++) { Tick(enemy, physics, player.Position); enemy.TryResolvePlayerHit(player); }
        Assert.True(enemy.WorldObject.Transform.Position.X < -45);
        Assert.Equal(30, player.Health.Current); Assert.Empty(enemy.GetActiveAttackHitboxes());
        Assert.Contains(enemy.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "flight-stop");
    }

    [Fact]
    public void FlightUsesTheRealSpawnerAndReplaysExactlyAcrossDiveAndRecovery()
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [],
            [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 26)),
             new(2, WorldThingKind2D.Pterosaur, null, true, new(-464, 154))])
            { AuthoredCharacters = Catalog, PlayerMaximumHealth = 100 });
        Assert.Equal("flying-pterosaur", Assert.Single(game.Session.CaptureEnemies()).TypeId);
        for (var i = 0; i < 55; i++) game.Session.Advance();
        var checkpoint = game.Session.CaptureCheckpoint();
        string[] Run() => [.. Enumerable.Range(0, 450).Select(_ =>
        {
            game.Session.Advance(); var state = Assert.Single(game.Session.CaptureEnemies());
            return $"{state.Position};{state.Velocity};{state.ActionId};{state.ActionSeconds};{game.Player.Health.Current}";
        })];
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint); Assert.Equal(first, Run());
        Assert.True(game.Player.Health.Current < 100);
    }
}
