using App2d.Contracts.Combat;
using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Player;
using App2d.Contracts.Simulation;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class PlayerSpellTests
{
    private static PersonCommand2D Cast => new() { CastHeld = true };
    private static PersonCommand2D Heal => new() { HealHeld = true };

    [Fact]
    public void EnergyRechargesWithTimeClampsAtMaximumAndDoesNotBankAtFull()
    {
        using var f = new Fixture(new() { StartingEnergy = 0 });
        f.Steps(119); // Includes the fixture's initial settling tick: one second total.
        Assert.Equal(10, f.Spells.Energy);
        f.Steps(240);
        Assert.Equal(30, f.Spells.Energy);
        f.Steps(1200);
        Assert.Equal(90, f.Spells.Energy);
        f.Steps(30, Cast);
        Assert.Equal(60, f.Spells.Energy);
        f.Steps(11);
        Assert.Equal(60, f.Spells.Energy);
        f.Step(default);
        Assert.Equal(61, f.Spells.Energy);
    }

    [Fact]
    public void RechargeContinuesDuringHealingAndCasting()
    {
        using var f = new Fixture(new() { StartingEnergy = 30 });
        f.Steps(30, Cast);
        Assert.Equal(2, f.Spells.Energy);
        f.Steps(329);
        Assert.Equal(30, f.Spells.Energy);
        f.Player.Health.Damage(6);
        f.Steps(120, Heal);
        Assert.Equal(30, f.Player.Health.Current);
        Assert.Equal(10, f.Spells.Energy);
    }

    [Fact]
    public void FractionalRechargeSurvivesRollbackAndPauseAndResetsOnRespawn()
    {
        using var f = new Fixture(new() { StartingEnergy = 0, EnergyPerSecond = 7 });
        f.Steps(50);
        var checkpoint = f.Session.CaptureCheckpoint();
        var energy = Enumerable.Range(0, 200).Select(_ => f.Step(default).Players[0].Person.Spells.Energy).ToArray();
        f.Session.RestoreCheckpoint(checkpoint);
        f.Session.SetPaused(true);
        Assert.Equal(2, f.Spells.Energy);
        f.Session.SetPaused(false);
        var replay = Enumerable.Range(0, 200).Select(_ => f.Step(default).Players[0].Person.Spells.Energy).ToArray();
        Assert.Equal(energy, replay);
        f.Player.Reset(Vector2.Zero);
        f.Steps(17);
        Assert.Equal(0, f.Spells.Energy);
        f.Step(default);
        Assert.Equal(1, f.Spells.Energy);
    }

    [Fact]
    public void ShortHoldFiresOnceCostsOneCastAndLeavesMeleeEquipped()
    {
        using var f = new Fixture();
        f.Steps(29, Cast);
        Assert.True(f.Spells.ChargeProgress > .9f);
        Assert.Equal(90, f.Spells.Energy);
        f.Step(Cast);
        Assert.Single(f.Events.OfType<GunFired2D>());
        Assert.Equal(60, f.Spells.Energy);
        Assert.Equal(EquipmentKind2D.Sword, f.Arsenal.Equipment);
        f.Steps(60, Cast);
        Assert.Single(f.Events.OfType<GunFired2D>());
        f.Step(default);
        f.Step(new() { PrimaryHeld = true });
        Assert.True(f.Arsenal.IsMeleeAttackActive);
    }

    [Fact]
    public void ReleasingEarlyDoesNotSpendEnergyAndEmptyMeterCannotFire()
    {
        using var f = new Fixture(new() { StartingEnergy = 30, EnergyPerSecond = 0 });
        f.Steps(15, Cast); f.Step(default);
        Assert.Equal(30, f.Spells.Energy);
        Assert.Empty(f.Events.OfType<GunFired2D>());
        f.Steps(30, Cast);
        Assert.Equal(0, f.Spells.Energy);
        f.Steps(30); f.Steps(60, Cast);
        Assert.Single(f.Events.OfType<GunFired2D>());
        Assert.False(f.Player.CaptureState().IsChargingPrimary);
    }

    [Fact]
    public void HealingCommitsAtCompletionAndHoldingRepeatsUntilFull()
    {
        using var f = new Fixture();
        f.Player.Health.Damage(10);
        f.Steps(119, Heal);
        Assert.Equal(20, f.Player.Health.Current);
        Assert.Equal(90, f.Spells.Energy);
        f.Step(Heal);
        Assert.Equal(26, f.Player.Health.Current);
        Assert.Equal(60, f.Spells.Energy);
        f.Steps(120, Heal);
        Assert.Equal(30, f.Player.Health.Current);
        Assert.Equal(30, f.Spells.Energy);
        f.Steps(180, Heal);
        Assert.Equal(30, f.Spells.Energy);
        Assert.Equal(new[] { 6, 4 }, f.Events.OfType<HealCompleted2D>().Select(e => e.Amount));
    }

    [Theory]
    [InlineData("release")]
    [InlineData("move")]
    [InlineData("jump")]
    [InlineData("dash")]
    [InlineData("damage")]
    [InlineData("pause")]
    [InlineData("floor")]
    public void InterruptedHealingNeverRestoresHealthOrSpendsEnergy(string reason)
    {
        using var f = new Fixture();
        f.Player.Health.Damage(10);
        f.Steps(60, Heal);
        Assert.True(f.Spells.IsHealing);
        var command = reason switch
        {
            "release" => default,
            "move" => Heal with { MoveX = 1 },
            "jump" => Heal with { JumpHeld = true },
            "dash" => Heal with { DashHeld = true },
            _ => Heal
        };
        if (reason == "damage") Assert.True(f.Player.TakeDamage(1, Vector2.Zero));
        if (reason == "pause") { f.Session.SetPaused(true); f.Session.SetPaused(false); }
        if (reason == "floor") f.Floor.IsCollider = false;
        f.Step(command);
        Assert.False(f.Spells.IsHealing);
        Assert.Equal(90, f.Spells.Energy);
        Assert.Empty(f.Events.OfType<HealCompleted2D>());
        if (reason is "damage" or "pause")
        {
            f.Steps(150, Heal);
            Assert.False(f.Spells.IsHealing); // Release required after a forced interruption.
        }
    }

    [Fact]
    public void ProjectileAndMeleeHitsDoNotRestoreEnergy()
    {
        using var f = new Fixture(new() { StartingEnergy = 30, EnergyPerSecond = 0 });
        var target = f.Target(new(65, 0));
        f.Steps(30, Cast);
        Assert.True(target.Health.Current < target.Health.Maximum);
        Assert.Equal(0, f.Spells.Energy); // Projectile damage cannot refund itself.
        f.Steps(150);
        f.Step(new() { PrimaryHeld = true }); f.Steps(35);
        Assert.Equal(26, target.Health.Current); // Both the projectile and melee dealt damage.
        Assert.Equal(0, f.Spells.Energy);
        // Another attack into invulnerability makes contact but earns nothing.
        f.Steps(10); f.Step(new() { PrimaryHeld = true }); f.Steps(35);
        Assert.Equal(0, f.Spells.Energy);
    }

    [Fact]
    public void MidHealRollbackRestoresProgressHealthEnergyAndCompletionEvents()
    {
        using var f = new Fixture();
        f.Player.Health.Damage(12);
        f.Steps(51, Heal);
        var checkpoint = f.Session.CaptureCheckpoint();
        var states = Enumerable.Range(0, 200).Select(_ => f.Step(Heal)).ToArray();
        f.Session.RestoreCheckpoint(checkpoint);
        var replay = Enumerable.Range(0, 200).Select(_ => f.Step(Heal)).ToArray();
        for (var i = 0; i < states.Length; i++)
        {
            Assert.Equal(states[i].Players[0].Person, replay[i].Players[0].Person);
            Assert.Equal(states[i].Events.ToArray(), replay[i].Events.ToArray());
        }
        Assert.Equal(30, f.Player.Health.Current);
        Assert.Equal(30, f.Spells.Energy);
    }

    [Fact]
    public void CustomTimingAndSharedEnergyLimitBothSpells()
    {
        using var f = new Fixture(new() { ShotChargeSeconds = .1f, HealSeconds = .5f, StartingEnergy = 60, EnergyPerSecond = 0 });
        f.Player.Health.Damage(12);
        f.Steps(12, Cast); f.Steps(25);
        Assert.Equal(30, f.Spells.Energy);
        f.Steps(60, Heal);
        Assert.Equal(24, f.Player.Health.Current);
        Assert.Equal(0, f.Spells.Energy);
        f.Steps(100, Heal);
        Assert.False(f.Spells.IsHealing);
        Assert.Equal(24, f.Player.Health.Current);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly EntityIdAllocator2D _ids = new();
        private readonly CombatantRegistry2D _registry = new();
        private readonly List<Person2D> _targets = [];
        private readonly TraversalMetrics2D _metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        public PhysicsWorld2D Physics { get; } = new() { Gravity = Vector2.Zero };
        public Person2D Player { get; }
        public PersonArsenal2D Arsenal { get; }
        public SideScrollerSession2D Session { get; }
        public PhysicsBody2D Floor { get; }
        public List<WeaponEvent2D> Events { get; } = [];
        public SpellState2D Spells => Player.CaptureState().Spells;

        public Fixture(SpellTuning2D? tuning = null)
        {
            Player = new(_ids.Allocate(), Physics.CollisionSystem, Physics, _metrics, Vector2.Zero, 2, 1, CombatFaction2D.Player, 30);
            _registry.Register(Player);
            Floor = Physics.AddBody(new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(1000, 20))), BodyMotionType2D.Static);
            Floor.WorldObject.Transform.Position = new(0, Player.WorldObject.WorldBounds.Bottom - 10);
            Floor.CollisionLayer = 1; Floor.CollisionMask = 2;
            var combat = new CombatSystem2D(Physics.CollisionSystem, _registry);
            Arsenal = new(_ids, Player.Body, _metrics.GunMuzzleOffset, Physics.CollisionSystem, 1, 4,
                CombatFaction2D.Player, combat, health: Player.Health, spells: tuning ?? new() { EnergyPerSecond = 0 });
            Player.AttachActions(Arsenal);
            Arsenal.WeaponOccurred += Events.Add;
            Session = new(Physics, Player, Arsenal, new EmptyWorld(), new(Vector2.Zero, 30), combat);
            Step(default);
        }

        public Person2D Target(Vector2 position)
        {
            var target = new Person2D(_ids.Allocate(), Physics.CollisionSystem, Physics, _metrics, position, 4, 0, CombatFaction2D.Enemy, 30);
            target.Body.MotionType = BodyMotionType2D.Static;
            _registry.Register(target);
            _targets.Add(target);
            return target;
        }

        public SessionFrame2D Step(PersonCommand2D command)
        {
            foreach (var target in _targets) target.BeginFrame(SideScrollerSession2D.FixedDeltaSeconds);
            return Session.Advance(new PlayerInput2D(Player.Id, Session.Tick + 1, Session.Tick + 1, command));
        }
        public void Steps(int count, PersonCommand2D command = default) { for (var i = 0; i < count; i++) Step(command); }
        public void Dispose() => Session.Dispose();
    }

    private sealed class EmptyWorld : ISideScrollerSessionWorld2D
    {
        private sealed record State : WorldSimulationState2D
        {
            public override System.Collections.Immutable.ImmutableArray<int> TerrainColliderIds => [];
        }
        public WorldSimulationState2D CaptureSimulation() => new State();
        public void ValidateSimulation(WorldSimulationState2D state) => Assert.IsType<State>(state);
        public void RestoreSimulation(WorldSimulationState2D state) => ValidateSimulation(state);
        public Rect2D Bounds => new(new(-10000), new(10000));
        public float GoalX => 9000;
        public void UpdateStreaming(Vector2 position) { }
        public void UpdateMovingPlatforms(float dt) { }
        public void UpdateEnemies(float dt, Vector2 position) { }
        public void SyncEnemiesAfterPhysics() { }
        public void ResolveDamage(Person2D player) { }
        public WorldThingSpec2D? UpdateSavePoints(float dt, Rect2D bounds) => null;
        public void SetActiveSavePoint(long? id) { }
    }
}
