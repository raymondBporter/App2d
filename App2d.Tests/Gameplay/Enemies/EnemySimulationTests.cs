using App2d.CharacterStudio.CharacterSeeds.Diagnostics;
using App2d.Contracts.Enemies;
using App2d.Contracts.Persons;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Physics;
using App2d.Core.Tiles;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using System.Numerics;

namespace App2d.Tests.Gameplay.Enemies;

public sealed class EnemySimulationTests
{
    private static readonly AuthoredCatalog Authored = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);

    [Fact]
    public void StreamingFreezesAttackTimeAndDamageInterruptsBeforeTheStrike()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var brute = CreateBrute(physics);
        var target = new Vector2(40f, 0f);
        brute.Update(0.2f, target);
        brute.SyncAfterPhysics();
        brute.DrainEvents();
        var before = brute.CaptureState();
        brute.SetSimulationEnabled(false);
        brute.Update(1f, target);
        brute.SyncAfterPhysics();
        Assert.Equal(before.AttackElapsedSeconds, brute.CaptureState().AttackElapsedSeconds);
        Assert.Empty(brute.GetActiveAttackHitboxes());
        brute.SetSimulationEnabled(true);
        Assert.True(brute.TakeDamage(1, Vector2.Zero));
        brute.Update(0f, target);
        brute.SyncAfterPhysics();
        Assert.False(brute.CaptureState().IsAttacking);
        Assert.Empty(brute.GetActiveAttackHitboxes());
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
        enemies.Update(0.1f, new Vector2(400f, 0f));
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
        var rival = new RivalEnemy2D(new EntityIdAllocator2D(), physics.CollisionSystem, physics,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), combat,
            Vector2.Zero, -100f, 100f, 1, 2, 4);
        rival.Person.ApplyCommand(new PersonCommand2D { PrimaryHeld = true }, 0f);
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

    private static AuthoredEntityEnemy2D CreateBrute(PhysicsWorld2D physics)
    {
        var brute = new AuthoredEntityEnemy2D(EntityId2D.Create(), Authored.Entities["maul-brute"], physics, Vector2.Zero, 1, 4);
        brute.SetSimulationEnabled(true);
        return brute;
    }
}
