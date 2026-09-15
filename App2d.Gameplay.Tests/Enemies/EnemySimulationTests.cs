using App2d.Levels;
using App2d.Core;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Core.Geometry;
using System.Collections.Immutable;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Enemies;

public sealed class EnemySimulationTests
{
    [Fact]
    public void HammerUsesGameplayTimeAndHitsOnlyOnceDuringItsDamageWindow()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var brute = CreateBrute(physics);
        var target = new Vector2(62f, -10f);
        var player = new Person2D(physics.CollisionSystem, physics,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), target, 2, 1, CombatFaction2D.Player);
        brute.Update(0.35f, new Vector2(1000f)); // Expire initial cooldown out of range.
        brute.Update(0f, target);
        Assert.IsType<HammerStarted2D>(Assert.Single(brute.DrainEvents()));
        brute.Update(0.49f, target);
        Assert.False(brute.IsHammerActive);
        brute.TryResolveHammerHit(player);
        Assert.Equal(5, player.Health.Current);
        brute.Update(0.02f, target);
        Assert.True(brute.IsHammerActive);
        var strike = Assert.IsType<HammerStruck2D>(Assert.Single(brute.DrainEvents()));
        Assert.Equal(brute.Enemy.Id, strike.EntityId);
        Assert.Equal(target, strike.Position);
        brute.TryResolveHammerHit(player);
        Assert.Equal(3, player.Health.Current);
        player.Reset(target);
        brute.TryResolveHammerHit(player);
        Assert.Equal(5, player.Health.Current); // Per-attack hit registration survives target reset.
        brute.Update(0.10f, target);
        Assert.False(brute.IsHammerActive);
        Assert.Empty(brute.DrainEvents());
        brute.Update(0.20f, target);
        Assert.False(brute.CaptureState().IsAttacking);
        Assert.Empty(brute.GetActiveAttackHitboxes());
    }

    [Fact]
    public void StreamingFreezesAttackTimeAndDamageInterruptsBeforeTheStrike()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var brute = CreateBrute(physics);
        brute.Update(0.35f, new Vector2(1000f));
        brute.Update(0.2f, new Vector2(62f, 0f));
        brute.DrainEvents();
        var before = brute.CaptureState();
        brute.SetSimulationEnabled(false);
        brute.Update(1f, Vector2.Zero);
        Assert.Equal(before.AttackElapsedSeconds, brute.CaptureState().AttackElapsedSeconds);
        Assert.False(brute.IsHammerActive);
        brute.SetSimulationEnabled(true);
        Assert.True(brute.Enemy.TakeDamage(1, Vector2.Zero));
        brute.Update(0f, Vector2.Zero);
        Assert.False(brute.CaptureState().IsAttacking);
        Assert.Empty(brute.DrainEvents());
        Assert.True(before.IsAttacking);
        Assert.Equal(0.2f, before.AttackElapsedSeconds);
    }

    [Fact]
    public void EnemySnapshotsPreserveIdentityAndValuesAcrossStreamingAndPhysics()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var brute = CreateBrute(physics);
        var enemies = new EnemySystem2D();
        enemies.Register(brute, new TileChunk2D(0, 0));
        var inactive = Assert.Single(enemies.CaptureStates());
        Assert.False(inactive.IsEnabled);
        enemies.UpdateStreaming(_ => true);
        enemies.Update(0.1f, new Vector2(1000f));
        physics.Step(0.1f);
        enemies.SyncAfterPhysics();
        var moving = Assert.Single(enemies.CaptureStates());
        Assert.Equal(inactive.Id, moving.Id);
        Assert.NotEqual(inactive.Position, moving.Position);
        Assert.True(moving.IsEnabled);
        enemies.UpdateStreaming(_ => false);
        Assert.False(Assert.Single(enemies.CaptureStates()).IsEnabled);
        Assert.True(moving.IsEnabled);
        Assert.Equal(Vector2.Zero, inactive.Position);
    }

    [Fact]
    public void RivalReportsAttackAndDamageFactsWithoutConstructingAView()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var combat = new CombatSystem2D(physics.CollisionSystem, new CombatantRegistry2D());
        var rival = new RivalEnemy2D(physics.CollisionSystem, physics,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), combat,
            Vector2.Zero, -100f, 100f, 1, 2, 4);
        rival.Person.ApplyCommand(new PersonCommand2D(default, UsePrimaryAction: true, SwitchEquipment: false), 0f);
        var attack = Assert.IsType<RivalAttackStarted2D>(Assert.Single(rival.DrainEvents()));
        Assert.Equal(rival.Person.Id, attack.EntityId);
        Assert.True(rival.CaptureState().IsAttacking);
        Assert.True(rival.Person.TakeDamage(12, Vector2.Zero));
        var events = rival.DrainEvents();
        Assert.Single(events.OfType<RivalDamaged2D>());
        Assert.Single(events.OfType<RivalDied2D>());
        Assert.Empty(rival.DrainEvents());
        Assert.False(rival.CaptureState().IsAlive);
    }

    private static BoilerBrute2D CreateBrute(PhysicsWorld2D physics) => new(
        physics.CollisionSystem, physics, Vector2.Zero, -100f, 100f, 1, 4);

}
