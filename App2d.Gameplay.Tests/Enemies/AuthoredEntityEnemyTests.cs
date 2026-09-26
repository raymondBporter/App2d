using App2d.Core.Characters;
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

/// <summary>Authored entities in the real game: spawning, the shared final pose, combat and rollback.</summary>
public sealed class AuthoredEntityEnemyTests
{
    private static readonly string CharactersRoot = Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters"));
    private static readonly AuthoredCatalog Authored = AuthoredCatalog.Load(Path.Combine(CharactersRoot, "authored"));

    private static SideScrollerSimulation2D Game()
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        return SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [],
        [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 40)),
         new(2, WorldThingKind2D.Shieldback, null, true, new(-290, 42)),
         new(3, WorldThingKind2D.BoilerBrute, null, true, new(-80, 42)),
         new(5, WorldThingKind2D.GreenDinosaur, null, true, new(210, 42))])
        { AuthoredCharacters = Authored, PlayerMaximumHealth = 30 });
    }

    [Fact]
    public void CoveredPlacementsSpawnAuthoredEntitiesAndCarryTheirFinalPose()
    {
        using var game = Game();
        var states = game.Session.CaptureEnemies();
        Assert.Equal(new[] { "spear-guard", "maul-brute", "stalker-pest" }, states.Select(s => s.TypeId));
        var guard = Assert.IsType<AuthoredEntityEnemy2D>(game.Level.EnemySystem.Combatants[0]);
        Assert.IsType<AuthoredEntityEnemy2D>(game.Level.EnemySystem.Combatants[1]);
        Assert.IsType<AuthoredEntityEnemy2D>(game.Level.EnemySystem.Combatants[2]);
        // Presentation receives the very pose object collision reads, never a second clock.
        Assert.Same(guard.Pose, states[0].AuthoredPose);
        Assert.Same(Authored.Entities["spear-guard"], states[0].AuthoredEntity);
    }

    [Fact]
    public void TheGuardThrustsAtThePlayerAndCombatReplaysExactly()
    {
        using var game = Game();
        for (var i = 0; i < 30; i++) game.Session.Advance();
        var checkpoint = game.Session.CaptureCheckpoint();
        string[] Run() => Enumerable.Range(0, 480).Select(i =>
        {
            var tick = game.Session.Tick + 1;
            var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick, new PersonCommand2D { MoveX = i < 60 ? .3f : 0 }));
            var guard = frame.Enemies[0];
            return JsonSerializer.Serialize(new { frame.Tick, Health = game.Player.Health.Current, guard.Position, guard.ActionId, guard.ActionSeconds, Points = guard.AuthoredPose!.Local.Points.Values.ToArray() },
                new JsonSerializerOptions { IncludeFields = true });
        }).ToArray();
        var first = Run(); var damaged = game.Player.Health.Current;
        game.Session.RestoreCheckpoint(checkpoint); var second = Run();
        Assert.Equal(first, second);
        Assert.Contains(first, json => json.Contains("\"ActionId\":\"attack\""));
        Assert.True(damaged < 30, "the spear reached the player");
    }

    [Fact]
    public void PlayerAttacksLandOnPoseDerivedHurtRegionsAboveTheMovementBox()
    {
        using var game = Game();
        var guard = Assert.IsType<AuthoredEntityEnemy2D>(game.Level.EnemySystem.Combatants[0]);
        var head = EntityCollision.Hurt(guard.Entity, guard.Pose).Single(r => r.Id == "head");
        var center = head.Points.Aggregate(Vector2.Zero, (a, b) => a + b) / head.Points.Count * AuthoredWorld.PixelsPerUnit;
        Assert.True(center.Y > guard.WorldObject.WorldBounds.Max.Y - 30, "the head region comes from the pose, not the movement box");
        var hit = new App2d.Core.SpatialObject2D(App2d.Core.Geometry.AxisAlignedRectangle2D.FromSize(new(2)));
        hit.Transform.Position = center;
        Assert.True(game.Combat.ResolveAttack(hit, game.Player.Id, 900, CombatFaction2D.Player, SideScrollerLayers2D.Enemy, 1, _ => Vector2.Zero));
        Assert.Equal(guard.Entity.Asset.Health - 1, guard.Health.Current);
        Assert.False(game.Combat.ResolveAttack(hit, game.Player.Id, 900, CombatFaction2D.Player, SideScrollerLayers2D.Enemy, 1, _ => Vector2.Zero));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheGunnersBoltsLeaveThePistolDamageThePlayerAndStopAtTerrain(bool wall)
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var gunner = new AuthoredEntityEnemy2D(App2d.Core.EntityId2D.Create(), Authored.Entities["cinder-gunner"], physics, new(0, 31), 1, 4);
        gunner.SetSimulationEnabled(true);
        var player = new Person2D(App2d.Core.EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(150, 40), 2, 1, CombatFaction2D.Player, 30);
        if (wall)
        {
            var shape = new App2d.Core.SpatialObject2D(App2d.Core.Geometry.AxisAlignedRectangle2D.FromSize(new(5, 400))); shape.Transform.Position = new(95, 40);
            var body = physics.AddBody(shape, BodyMotionType2D.Static); body.CollisionLayer = 1; body.CollisionMask = 6;
        }
        var sawBolt = false;
        for (var i = 0; i < 360; i++)
        {
            gunner.Update(1f / 120, player.Position); gunner.SyncAfterPhysics(); gunner.TryResolvePlayerHit(player);
            var bolts = gunner.CaptureState().Bolts;
            if (bolts.Length > 0 && !sawBolt)
            {
                sawBolt = true;
                var gun = gunner.Entity.Equipment.Single();
                var muzzle = ActorPose.PropPoint(gunner.Pose.Socket(gun.Socket), gun.Prop, gun.Prop.Muzzle!.Value) * AuthoredWorld.PixelsPerUnit;
                Assert.True(Vector2.Distance(new(muzzle.X, muzzle.Y), bolts[0].Position) < 12, "the bolt leaves the drawn muzzle");
                Assert.True(bolts[0].Velocity.X > 0, "toward the player");
            }
        }
        Assert.True(sawBolt);
        if (wall) Assert.Equal(30, player.Health.Current); else Assert.True(player.Health.Current < 30);
    }

    [Fact]
    public void RivalPlacementsSpawnTheGunnerAndItsBoltsReplayExactly()
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [],
        [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 40)), new(4, WorldThingKind2D.Rival, null, true, new(-200, 42))])
        { AuthoredCharacters = Authored, PlayerMaximumHealth = 30 });
        Assert.Equal("cinder-gunner", Assert.Single(game.Session.CaptureEnemies()).TypeId);
        for (var i = 0; i < 60; i++) game.Session.Advance();
        var checkpoint = game.Session.CaptureCheckpoint();
        string[] Run() => [.. Enumerable.Range(0, 240).Select(_ => JsonSerializer.Serialize(game.Session.CaptureEnemies().Select(e => (e.Position, e.Bolts.Select(b => b.Position).ToArray())).ToArray(), new JsonSerializerOptions { IncludeFields = true })
            + (game.Session.Advance() is var frame ? "" : ""))];
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint); var second = Run();
        Assert.Equal(first, second);
        Assert.Contains(first, json => json.Contains("\"X\"") && json.Contains("[{"));
        Assert.True(game.Player.Health.Current < 30, "the gunner's shots land");
    }

    [Fact]
    public void TheMaulSlamsForFiveInsideItsWindowAndShrugsOffKnockback()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var maul = new AuthoredEntityEnemy2D(App2d.Core.EntityId2D.Create(), Authored.Entities["maul-brute"], physics, new(0, 36), 1, 4);
        maul.SetSimulationEnabled(true);
        var player = new Person2D(App2d.Core.EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(40, 40), 2, 1, CombatFaction2D.Player, 30);
        var slam = maul.Entity.Actions["attack"]; var landedAt = -1.0; var cues = new List<string>();
        for (var i = 0; i < 400 && landedAt < 0; i++)
        {
            maul.Update(1f / 120, player.Position); maul.SyncAfterPhysics(); maul.TryResolvePlayerHit(player);
            cues.AddRange(maul.DrainEvents().OfType<EntityCue2D>().Select(c => c.Cue));
            if (player.Health.Current < 30) landedAt = maul.CaptureState().AttackElapsedSeconds;
        }
        Assert.Equal(25, player.Health.Current);
        Assert.InRange(landedAt, slam.Hits[0].Start, slam.Hits[0].Finish + 1 / 120f);
        Assert.Contains("heavy", cues);

        maul.TakeDamage(1, new(300, 0));
        Assert.Equal(100, maul.Body.LinearVelocity.X, 3); // mass 3 divides knockback
    }

    [Fact]
    public void TheSpearHitboxFollowsThePropInBothFacings()
    {
        foreach (var side in new[] { -1, 1 })
        {
            var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
            var guard = new AuthoredEntityEnemy2D(App2d.Core.EntityId2D.Create(), Authored.Entities["spear-guard"], physics, new(0, 42), 1, 4);
            guard.SetSimulationEnabled(true);
            var target = new Vector2(side * 80, 42); var sawHitbox = false;
            for (var i = 0; i < 240; i++)
            {
                guard.Update(1f / 120, target); guard.SyncAfterPhysics();
                foreach (var box in guard.GetActiveAttackHitboxes())
                {
                    sawHitbox = true;
                    var spear = guard.Entity.Equipment[0];
                    var tip = ActorPose.PropPoint(guard.Pose.Socket(spear.Socket), spear.Prop, spear.Prop.Tip) * AuthoredWorld.PixelsPerUnit;
                    var bounds = box.WorldBounds;
                    Assert.InRange(tip.X, bounds.Min.X, bounds.Max.X); Assert.InRange(tip.Y, bounds.Min.Y, bounds.Max.Y);
                    Assert.Equal(side, Math.Sign(bounds.Center.X - guard.WorldObject.Transform.Position.X));
                }
            }
            Assert.True(sawHitbox);
        }
    }

    [Fact]
    public void AHitStaggersAndKeepsTheKnockbackThenTheControllerRecovers()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var guard = new AuthoredEntityEnemy2D(App2d.Core.EntityId2D.Create(), Authored.Entities["spear-guard"], physics, new(0, 42), 1, 4);
        guard.SetSimulationEnabled(true);
        var target = new Vector2(400, 42);
        guard.Update(1f / 120, target); guard.SyncAfterPhysics();
        Assert.True(guard.TakeDamage(1, new(-200, 0)));
        var stagger = guard.Entity.Clip(EntityControllers.Hit)!.Duration;
        for (var t = 0f; t < stagger - .05f; t += 1f / 120)
        {
            guard.Update(1f / 120, target); guard.SyncAfterPhysics();
            Assert.Equal(-200, guard.Body.LinearVelocity.X);
            Assert.Equal(EntityControllers.Hit, guard.CaptureState().ActionId);
        }
        for (var i = 0; i < 30; i++) { guard.Update(1f / 120, target); guard.SyncAfterPhysics(); }
        Assert.True(guard.Body.LinearVelocity.X > 0, "after the stagger it walks toward the target again");
        Assert.Equal(EntityControllers.Walk, guard.CaptureState().ActionId);
    }

    [Theory, InlineData("spear-guard"), InlineData("stalker-pest")]
    public void DeathPlaysItsClipOnceAndHoldsTheLastFrame(string id)
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var enemy = new AuthoredEntityEnemy2D(App2d.Core.EntityId2D.Create(), Authored.Entities[id], physics, new(0, 42), 1, 4);
        enemy.SetSimulationEnabled(true);
        enemy.Update(1f / 120, new(400, 42)); enemy.SyncAfterPhysics();
        Assert.True(enemy.TakeDamage(enemy.Health.Current, Vector2.Zero));
        var death = enemy.Entity.Clip(EntityControllers.Death)!;
        for (var i = 0; i < (int)(death.Duration * 120) + 60; i++) { enemy.Update(1f / 120, new(400, 42)); enemy.SyncAfterPhysics(); }
        var state = enemy.CaptureState();
        Assert.Equal(EntityControllers.Death, state.ActionId);
        Assert.Equal(death.Duration, state.ActionSeconds, 3);
        var held = enemy.Pose.Local.Points.Values.ToArray();
        enemy.Update(1f / 120, new(400, 42)); enemy.SyncAfterPhysics();
        Assert.Equal(held, enemy.Pose.Local.Points.Values.ToArray());
    }
}
