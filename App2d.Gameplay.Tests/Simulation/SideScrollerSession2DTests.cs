using App2d.Levels;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Physics;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Simulation;

public sealed class SideScrollerSession2DTests
{
    [Fact]
    public void CommandsAdvanceExactlyOneFixedTickAndReturnIndependentValues()
    {
        using var game = new Fixture();
        var original = game.Session.CaptureState();
        var first = game.Step(Command(move: 1f));
        var position = first.Player.Person.Position;
        for (var i = 0; i < 20; i++) game.Step(Command(move: 1f));

        Assert.Equal(21, game.Session.Tick);
        Assert.Equal(21, game.World.PlatformUpdates);
        Assert.Equal(21, game.World.EnemyUpdates);
        Assert.Equal(21, game.Actions.BeginFrames);
        Assert.Equal(SideScrollerSession2D.FixedDeltaSeconds, game.World.LastDelta);
        Assert.Equal(position, first.Player.Person.Position);
        Assert.Equal(Vector2.Zero, original.Person.Position);
        Assert.True(game.Session.CaptureState().Person.Position.X > position.X);
    }

    [Fact]
    public void InvalidCommandsDoNotPartiallyAdvanceTheWorld()
    {
        using var game = new Fixture();
        var valid = new PlayerInput2D(game.Person.Id, 1, 1, default);
        Assert.Throws<ArgumentException>(() => game.Session.Advance(valid with { EntityId = EntityId2D.Create() }));
        Assert.Throws<ArgumentException>(() => game.Session.Advance(valid with { Tick = 2 }));
        Assert.Throws<ArgumentException>(() => game.Session.Advance(valid with { Sequence = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Session.Advance(valid with { Command = Command(float.NaN) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Session.Advance(valid with
        { Command = Command() with { Movement = default(PersonMovementIntent2D) with { ClimbY = float.PositiveInfinity } } }));
        Assert.Equal(0, game.Session.Tick);
        Assert.Equal(0, game.World.PlatformUpdates);
        Assert.Equal(Vector2.Zero, game.Person.Position);
        game.Session.Advance(valid);
        Assert.Throws<ArgumentException>(() => game.Session.Advance(valid));
        Assert.Equal(1, game.Session.Tick);
    }

    [Fact]
    public void RecordedCommandsReproduceMovementAndEventsInAnIndependentSimulation()
    {
        using var first = new Fixture();
        using var second = new Fixture();
        for (var tick = 0; tick < 240; tick++)
        {
            var command = Command(move: tick < 100 ? 1f : -1f,
                jump: tick == 5, held: tick >= 5 && tick < 25,
                attack: tick == 50);
            var a = first.Step(command);
            var b = second.Step(command);
            Assert.Equal(a.Player with { Person = a.Player.Person with { Id = b.Player.Person.Id } }, b.Player);
            Assert.Equal(a.Events.Select(e => e.GetType()), b.Events.Select(e => e.GetType()));
            Assert.Equal(a.Events.Select(e => (e.Stamp.Tick, e.Stamp.Sequence)),
                b.Events.Select(e => (e.Stamp.Tick, e.Stamp.Sequence)));
        }
    }

    [Fact]
    public void PausingDoesNotAdvanceTimeAndInterruptsActionsOnlyOnEntry()
    {
        using var game = new Fixture();
        game.Session.SetPaused(true);
        game.Session.SetPaused(true);
        Assert.Equal(1, game.Actions.Interruptions);
        Assert.Throws<InvalidOperationException>(() => game.Step());
        Assert.Equal(0, game.Session.Tick);
        game.Session.SetPaused(false);
        Assert.Equal(1, game.Step().Tick);
    }

    [Fact]
    public void CheckpointDeathAndRespawnAreSessionDecisionsWithValueEvents()
    {
        using var game = new Fixture();
        var checkpointPosition = new Vector2(200f, 50f);
        game.World.PendingCheckpoint = new WorldThingSpec2D(7, WorldThingKind2D.SavePoint,
            "test", true, checkpointPosition);
        var checkpointFrame = game.Step();
        var activated = Assert.Single(checkpointFrame.Events.OfType<CheckpointActivated2D>());
        Assert.Equal(7, activated.CheckpointId);
        Assert.Equal(7, checkpointFrame.Player.CheckpointId);
        Assert.Equal(game.Person.Health.Maximum, activated.HitPoints);
        game.World.DamageNextStep = game.Person.Health.Maximum;
        var deathFrame = game.Step();
        Assert.Single(deathFrame.Events.OfType<Died2D>());
        Assert.False(deathFrame.Player.Person.IsAlive);
        Assert.True(deathFrame.Player.RespawnSeconds > 0f);

        SessionFrame2D? respawnFrame = null;
        var repeatedDeaths = 0;
        for (var i = 0; i < 140; i++)
        {
            var frame = game.Step();
            repeatedDeaths += frame.Events.OfType<Died2D>().Count();
            if (frame.Events.Any(e => e is Respawned2D)) { respawnFrame = frame; break; }
        }
        Assert.NotNull(respawnFrame);
        Assert.Equal(0, repeatedDeaths);
        Assert.Equal(checkpointPosition, respawnFrame.Player.Person.Position);
        Assert.Equal(game.Person.Id, respawnFrame.Player.Person.Id);
        Assert.True(respawnFrame.Player.Person.IsAlive);
        Assert.Equal(0f, respawnFrame.Player.RespawnSeconds);
        Assert.False(deathFrame.Player.Person.IsAlive);
    }

    [Fact]
    public void LandingReportsImpactAndGoalDoesNotRepeatEveryTick()
    {
        using var game = new Fixture();
        game.World.GoalX = -1f;
        Assert.Single(game.Step().Events.OfType<GoalReached2D>());
        Assert.Empty(game.Step().Events.OfType<GoalReached2D>());
        var jump = game.Step(Command(jump: true, held: true));
        Assert.Single(jump.Events.OfType<JumpStarted2D>());
        var landings = new List<Landed2D>();
        for (var i = 0; i < 240; i++) landings.AddRange(game.Step().Events.OfType<Landed2D>());
        Assert.Single(landings);
        Assert.True(landings[0].ImpactSpeed > 0f);
    }

    private static PersonCommand2D Command(float move = 0f, bool jump = false, bool held = false, bool attack = false) =>
        new(new PersonMovementIntent2D(move, jump, held, false, false, false), attack, false);

    private sealed class Fixture : IDisposable
    {
        public Person2D Person { get; }
        public TestActions Actions { get; } = new();
        public TestWorld World { get; } = new();
        public SideScrollerSession2D Session { get; }
        public Fixture()
        {
            var metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
            var physics = new PhysicsWorld2D { Gravity = new Vector2(0f, -metrics.Gravity),
                MaxSubstepSeconds = SideScrollerSession2D.FixedDeltaSeconds };
            Person = new Person2D(physics.CollisionSystem, physics, metrics, Vector2.Zero,
                2, 1, CombatFaction2D.Player);
            var floor = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(10000f, 20f)));
            floor.Transform.Position = new Vector2(0f, Person.WorldObject.WorldBounds.Bottom - 10f);
            physics.AddBody(floor, BodyMotionType2D.Static).CollisionLayer = 1;
            Person.AttachActions(Actions);
            Session = new SideScrollerSession2D(physics, Person, Actions, World,
                new RespawnState2D(Vector2.Zero, Person.Health.Maximum));
        }
        public SessionFrame2D Step(PersonCommand2D command = default) => Session.Advance(
            new PlayerInput2D(Person.Id, Session.Tick + 1, Session.Tick + 1, command));
        public void Dispose() => Session.Dispose();
    }

    private sealed class TestWorld : ISideScrollerSessionWorld2D
    {
        public Bounds2D Bounds => new(new Vector2(-10000f), new Vector2(10000f));
        public float GoalX { get; set; } = 9000f;
        public int PlatformUpdates { get; private set; }
        public int EnemyUpdates { get; private set; }
        public float LastDelta { get; private set; }
        public int DamageNextStep { get; set; }
        public WorldThingSpec2D? PendingCheckpoint { get; set; }
        public void UpdateStreaming(Vector2 focus) { }
        public void UpdateMovingPlatforms(float dt) { PlatformUpdates++; LastDelta = dt; }
        public void UpdateEnemies(float dt, Vector2 target) => EnemyUpdates++;
        public void SyncEnemiesAfterPhysics() { }
        public void ResolveDamage(Person2D player)
        {
            if (DamageNextStep <= 0) return;
            player.TakeDamage(DamageNextStep, Vector2.Zero);
            DamageNextStep = 0;
        }
        public WorldThingSpec2D? UpdateSavePoints(float dt, Bounds2D bounds)
        {
            var checkpoint = PendingCheckpoint;
            PendingCheckpoint = null;
            return checkpoint;
        }
        public void SetActiveSavePoint(long? id) { }
    }

    private sealed class TestActions : ISessionPlayerActions2D
    {
        public WeaponState2D CaptureWeaponState() => WeaponState2D.Empty;
        public event Action<WeaponEvent2D>? WeaponOccurred { add { } remove { } }
        public string EquipmentId => "unarmed";
        public bool IsMeleeAttackActive => false;
        public int BeginFrames { get; private set; }
        public int Interruptions { get; private set; }
        public event Action<UnarmedAttackKind2D, float>? UnarmedAttackStarted;
        public event Action<string>? EquipmentChanged { add { } remove { } }
        public event Action<float>? MeleeAttackStarted { add { } remove { } }
        public event Action<float>? DownAttackStarted { add { } remove { } }
        public event Action? ShotStarted { add { } remove { } }
        public void InterruptPrimary() => Interruptions++;
        public void BeginFrame(float dt) => BeginFrames++;
        public void UpdateAfterPhysics(float dt, float facing) { }
        public float UsePrimary(float facing)
        {
            UnarmedAttackStarted?.Invoke(UnarmedAttackKind2D.Punch, 0.28f);
            return facing;
        }
        public float UseSecondary(float facing) => facing;
        public void SelectNext() { }
        public void Reset() { }
    }
}
