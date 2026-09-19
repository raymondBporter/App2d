using App2d.Levels;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Physics;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Simulation;

public sealed class WeaponSessionTests
{
    [Fact]
    public void SnapshotPreservesShotPhaseAfterRecoveryAndAcrossRollback()
    {
        using var game = new Fixture();
        var shot = game.Fire();
        Assert.Equal(PlayerAttackKind2D.Shot, shot.Players[0].Person.Action.Kind);
        Assert.True(shot.Players[0].Person.Action.IsActive);
        for (var i = 0; i < 12; i++) game.Step();
        var snapshot = game.Session.CaptureSnapshot();
        Assert.Equal(EquipmentKind2D.Gun, snapshot.Players[0].Equipment);
        Assert.False(snapshot.Players[0].Person.Action.IsActive);
        Assert.InRange(snapshot.Players[0].Person.Action.ElapsedSeconds, 0.099f, 0.101f);
        var checkpoint = game.Session.CaptureCheckpoint();
        var next = game.Step();
        game.Session.RestoreCheckpoint(checkpoint);
        var replayed = game.Step();
        Assert.Equal(next.Players[0].Person.Action, replayed.Players[0].Person.Action);
        Assert.Equal(snapshot.Tick + 1, replayed.Tick);
        Assert.Empty(replayed.Events.OfType<AttackStarted2D>());
    }

    private static PersonCommand2D Hold => new() { PrimaryHeld = true };

    [Fact]
    public void RealWeaponsRunWithoutPresentationAndOldFramesSurviveProjectileReuse()
    {
        using var game = new Fixture();
        var shot = game.Fire();
        var first = Assert.Single(shot.Players[0].Weapons.Projectiles);
        var fired = Assert.Single(shot.Events.OfType<WeaponOccurred2D>());
        Assert.IsType<GunFired2D>(fired.Occurrence);
        Assert.Equal(game.Player.Id, fired.Stamp.EntityId);
        Assert.Equal(shot.Tick, fired.Stamp.Tick);
        var next = game.Step();
        Assert.Equal(first.Id, Assert.Single(next.Players[0].Weapons.Projectiles).Id);
        Assert.NotEqual(first.Position, next.Players[0].Weapons.Projectiles[0].Position);
        for (var i = 0; i < 200; i++) game.Step();
        Assert.Empty(game.Session.CapturePlayers()[0].Weapons.Projectiles);
        var second = Assert.Single(game.Fire().Players[0].Weapons.Projectiles);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first, Assert.Single(shot.Players[0].Weapons.Projectiles));
        Assert.IsType<GunFired2D>(Assert.Single(shot.Events.OfType<WeaponOccurred2D>()).Occurrence);
    }

    [Fact]
    public void SameTickSpawnAndImpactStillDeliverFactsWhenNoProjectileSurvivesTheFrame()
    {
        using var game = new Fixture();
        var target = new Person2D(EntityId2D.Create(), game.Physics.CollisionSystem, game.Physics, game.Metrics,
            game.Metrics.GunMuzzleOffset + new Vector2(15f, 0f), 4, 0, CombatFaction2D.Enemy, maximumHealth: 2);
        target.Body.MotionType = BodyMotionType2D.Static;
        game.Registry.Register(target);
        var frame = game.Fire();
        Assert.Empty(frame.Players[0].Weapons.Projectiles);
        Assert.False(target.IsAlive);
        var facts = frame.Events.OfType<WeaponOccurred2D>().Select(e => e.Occurrence).ToArray();
        Assert.IsType<GunFired2D>(facts[0]);
        var impact = Assert.IsType<ProjectileImpact2D>(facts[1]);
        Assert.True(impact.ProjectileId.IsValid);
        var damage = Assert.Single(frame.Events.OfType<CombatDamageOccurred2D>());
        Assert.Equal(target.Id, damage.Stamp.EntityId);
        Assert.Equal(target.Id, damage.Damage.TargetId);
        Assert.Equal(target.Position, damage.Damage.Position);
        Assert.True(damage.Damage.WasKilled);
        Assert.Equal(frame.Events.Length, frame.Events.Select(e => e.Stamp.Sequence).Distinct().Count());
        Assert.Empty(game.Step().Events.OfType<CombatDamageOccurred2D>());
    }

    [Fact]
    public void ChargeCancellationIsDeliveredOnceAndPauseClearsOngoingState()
    {
        using var game = new Fixture();
        var start = game.Step(Hold);
        Assert.IsType<ChargeStarted2D>(Assert.Single(start.Events.OfType<WeaponOccurred2D>()).Occurrence);
        for (var i = 0; i < 30; i++) game.Step(Hold);
        var cancelled = game.Step();
        var fact = Assert.IsType<ChargeCancelled2D>(Assert.Single(cancelled.Events.OfType<WeaponOccurred2D>()).Occurrence);
        Assert.InRange(fact.Progress, 0.4f, 0.5f);
        Assert.False(cancelled.Players[0].Weapons.IsCharging);
        Assert.Empty(game.Step().Events.OfType<WeaponOccurred2D>());
        game.Step(Hold);
        game.Session.SetPaused(true);
        Assert.False(game.Session.CapturePlayers()[0].Weapons.IsCharging);
        game.Session.SetPaused(false);
        Assert.False(game.Step(Hold).Players[0].Weapons.IsCharging);
    }

    private sealed class Fixture : IDisposable
    {
        public TraversalMetrics2D Metrics { get; } = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        public PhysicsWorld2D Physics { get; } = new() { Gravity = Vector2.Zero };
        public CombatantRegistry2D Registry { get; } = new();
        public Person2D Player { get; }
        public SideScrollerSession2D Session { get; }

        public Fixture()
        {
            Player = new Person2D(EntityId2D.Create(), Physics.CollisionSystem, Physics, Metrics, Vector2.Zero, 2, 1, CombatFaction2D.Player);
            Registry.Register(Player);
            var combat = new CombatSystem2D(Physics.CollisionSystem, Registry);
            var arsenal = new PersonArsenal2D(new EntityIdAllocator2D(), Player.Body, Metrics.GunMuzzleOffset,
                Physics.CollisionSystem, 1, 4, CombatFaction2D.Player, combat);
            Player.AttachActions(arsenal);
            arsenal.SelectNext();
            Session = new SideScrollerSession2D(Physics, Player, arsenal, new EmptyWorld(),
                new RespawnState2D(Vector2.Zero, Player.Health.Maximum), combat);
        }

        public SessionFrame2D Fire()
        {
            Step(Hold);
            for (var i = 1; i < 71; i++) Step(Hold);
            return Step(Hold);
        }
        public SessionFrame2D Step(PersonCommand2D command = default) => Session.Advance(
            new PlayerInput2D(Player.Id, Session.Tick + 1, Session.Tick + 1, command));
        public void Dispose() => Session.Dispose();
    }

    private sealed class EmptyWorld : ISideScrollerSessionWorld2D
    {
        private sealed record EmptyState : WorldSimulationState2D
        {
            public override System.Collections.Immutable.ImmutableArray<int> TerrainColliderIds => [];
        }
        public WorldSimulationState2D CaptureSimulation() => new EmptyState();
        public void ValidateSimulation(WorldSimulationState2D state) => Assert.IsType<EmptyState>(state);
        public void RestoreSimulation(WorldSimulationState2D state) => ValidateSimulation(state);
        public Bounds2D Bounds => new(new Vector2(-10000f), new Vector2(10000f));
        public float GoalX => 9000f;
        public void UpdateStreaming(Vector2 position) { }
        public void UpdateMovingPlatforms(float dt) { }
        public void UpdateEnemies(float dt, Vector2 position) { }
        public void SyncEnemiesAfterPhysics() { }
        public void ResolveDamage(Person2D player) { }
        public WorldThingSpec2D? UpdateSavePoints(float dt, Bounds2D bounds) => null;
        public void SetActiveSavePoint(long? id) { }
    }
}
