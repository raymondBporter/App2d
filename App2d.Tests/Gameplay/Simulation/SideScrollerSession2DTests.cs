using App2d.CharacterStudio.CharacterSeeds.Diagnostics;
using App2d.Contracts.Combat;
using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Simulation;
using App2d.Core;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using System.Numerics;

namespace App2d.Tests.Gameplay.Simulation;

public sealed class SideScrollerSession2DTests
{
    [Fact]
    public void CommandsAdvanceExactlyOneFixedTickAndReturnIndependentValues()
    {
        using var game = new Fixture();
        var original = game.Session.CapturePlayers()[0];
        var first = game.Step(Command(move: 1f));
        var position = first.Players[0].Person.Position;
        for (var i = 0; i < 20; i++)
        {
            game.Step(Command(move: 1f));
        }

        Assert.Equal(21, game.Session.Tick);
        Assert.Equal(21, game.World.PlatformUpdates);
        Assert.Equal(21, game.World.EnemyUpdates);
        Assert.Equal(21, game.Actions.BeginFrames);
        Assert.Equal(SideScrollerSession2D.FixedDeltaSeconds, game.World.LastDelta);
        Assert.Equal(position, first.Players[0].Person.Position);
        Assert.Equal(Vector2.Zero, original.Person.Position);
        Assert.True(game.Session.CapturePlayers()[0].Person.Position.X > position.X);
    }

    [Fact]
    public void InvalidCommandsDoNotPartiallyAdvanceTheWorld()
    {
        using var game = new Fixture();
        var valid = new PlayerInput2D(game.Person.Id, default);
        Assert.Throws<ArgumentException>(() => game.Session.Advance(valid with { EntityId = EntityId2D.Create() }));
        Assert.Throws<ArgumentException>(() => game.Session.Advance(valid with { Command = Command(float.NaN) }));
        Assert.Throws<ArgumentException>(() => game.Session.Advance(valid with { Command = Command() with { ClimbY = float.PositiveInfinity } }));
        Assert.Equal(0, game.Session.Tick);
        Assert.Equal(0, game.World.PlatformUpdates);
        Assert.Equal(Vector2.Zero, game.Person.Position);
        game.Session.Advance(valid);
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
            Assert.Equal(a.Players[0] with
            {
                Person = a.Players[0].Person with
                {
                    Id = b.Players[0].Person.Id
                }
            }, b.Players[0]);
            Assert.Equal(a.Events.Select(e => e.GetType()), b.Events.Select(e => e.GetType()));
            Assert.Equal(a.Events.Select(e => (e.Stamp.Tick, e.Stamp.Sequence)),
                b.Events.Select(e => (e.Stamp.Tick, e.Stamp.Sequence)));
        }
    }

    [Fact]
    public void MissingInputRepeatsTheLastCommandAndDuplicatePlayersAreRejected()
    {
        using var game = new Fixture();
        var moving = game.Step(Command(move: 1f));
        Assert.Equal(1f, moving.Players[0].MoveX);
        var repeated = game.Session.Advance();
        Assert.Equal(2, repeated.Tick);
        Assert.Equal(1f, repeated.Players[0].MoveX);
        Assert.True(repeated.Players[0].Person.Position.X > moving.Players[0].Person.Position.X);
        var duplicate = new PlayerInput2D(game.Person.Id, default);
        Assert.Throws<ArgumentException>(() => game.Session.Advance(duplicate, duplicate));
        Assert.Equal(2, game.Session.Tick);
    }

    [Fact]
    public void EventsAreStampedWithTheirTickAndEntityAndAreNotRepeated()
    {
        using var game = new Fixture();
        var first = game.Step(Command(attack: true));
        var attack = Assert.Single(first.Events.OfType<AttackStarted2D>());
        Assert.Equal(1, attack.Stamp.Tick);
        Assert.Equal(game.Person.Id, attack.Stamp.EntityId);
        _ = game.Session.CapturePlayers();
        Assert.Empty(game.Step().Events.OfType<AttackStarted2D>());
        Assert.Single(first.Events.OfType<AttackStarted2D>());
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
        Assert.Equal(7, checkpointFrame.Players[0].CheckpointId);
        Assert.Equal(game.Person.Health.Maximum, activated.HitPoints);
        game.World.DamageNextStep = game.Person.Health.Maximum;
        var deathFrame = game.Step();
        Assert.Single(deathFrame.Events.OfType<Died2D>());
        Assert.False(deathFrame.Players[0].Person.IsAlive);
        Assert.True(deathFrame.Players[0].RespawnSeconds > 0f);

        SessionFrame2D? respawnFrame = null;
        var repeatedDeaths = 0;
        for (var i = 0; i < 140; i++)
        {
            var frame = game.Step();
            repeatedDeaths += frame.Events.OfType<Died2D>().Count();
            if (frame.Events.Any(e => e is Respawned2D))
            {
                respawnFrame = frame;
                break;
            }
        }
        Assert.NotNull(respawnFrame);
        Assert.Equal(0, repeatedDeaths);
        Assert.Equal(checkpointPosition, respawnFrame.Players[0].Person.Position);
        Assert.Equal(game.Person.Id, respawnFrame.Players[0].Person.Id);
        Assert.True(respawnFrame.Players[0].Person.IsAlive);
        Assert.Equal(0f, respawnFrame.Players[0].RespawnSeconds);
        Assert.False(deathFrame.Players[0].Person.IsAlive);
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
        for (var i = 0; i < 240; i++)
        {
            landings.AddRange(game.Step().Events.OfType<Landed2D>());
        }

        Assert.Single(landings);
        Assert.True(landings[0].ImpactSpeed > 0f);
    }

    [Fact]
    public void AirJumpReportsTakeoffPositionOnlyForTheExtraJump()
    {
        using var game = new Fixture();
        game.Step();
        game.Step();
        var groundJump = game.Step(Command(held: true));
        Assert.Single(groundJump.Events.OfType<JumpStarted2D>());
        Assert.Empty(groundJump.Events.OfType<AirJumpStarted2D>());
        for (var i = 0; i < 12; i++) game.Step(Command(held: true));
        game.Step();
        var takeoff = game.Person.Position;
        var airJump = game.Step(Command(held: true));
        var cloud = Assert.Single(airJump.Events.OfType<AirJumpStarted2D>());
        Assert.Equal(takeoff, cloud.Position);
        Assert.Equal(game.Person.Id, cloud.Stamp.EntityId);
        Assert.Single(airJump.Events.OfType<JumpStarted2D>());
        Assert.Empty(game.Step(Command(held: true)).Events.OfType<AirJumpStarted2D>());
        game.Step();
        Assert.Empty(game.Step(Command(held: true)).Events.OfType<AirJumpStarted2D>());
    }

    private static PersonCommand2D Command(float move = 0f, bool jump = false, bool held = false, bool attack = false) =>
        new()
        {
            MoveX = move,
            JumpHeld = jump || held,
            PrimaryHeld = attack
        };

    private sealed class Fixture : IDisposable
    {
        public Person2D Person { get; }
        public TestActions Actions { get; } = new();
        public TestWorld World { get; } = new();
        public SideScrollerSession2D Session { get; }
        public Fixture()
        {
            var metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
            var physics = new PhysicsWorld2D
            {
                Gravity = new Vector2(0f, -metrics.Gravity),
                MaxSubstepSeconds = SideScrollerSession2D.FixedDeltaSeconds
            };
            Person = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, metrics, Vector2.Zero,
                2, 1, CombatFaction2D.Player);
            var floor = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(10000f, 20f));
            floor.Transform.Position = new Vector2(0f, Person.WorldObject.WorldBounds.Bottom - 10f);
            physics.AddBody(floor, BodyMotionType2D.Static).CollisionLayer = 1;
            Person.AttachActions(Actions);
            Session = new SideScrollerSession2D(physics, Person, Actions, World,
                new RespawnState2D(Vector2.Zero, Person.Health.Maximum));
        }
        public SessionFrame2D Step(PersonCommand2D command = default) => Session.Advance(new PlayerInput2D(Person.Id, command));
        public void Dispose() => Session.Dispose();
    }

    private sealed class TestWorld : ISideScrollerSessionWorld2D
    {
        public Rect2D Bounds => new(new Vector2(-10000f), new Vector2(10000f));
        public float GoalX { get; set; } = 9000f;
        public int PlatformUpdates { get; private set; }
        public int EnemyUpdates { get; private set; }
        public float LastDelta { get; private set; }
        public int DamageNextStep { get; set; }
        public WorldThingSpec2D? PendingCheckpoint { get; set; }
        public void UpdateStreaming(Vector2 focus)
        {
        }
        public void UpdateWorldMotion(float dt)
        {
            PlatformUpdates++;
            LastDelta = dt;
        }
        public void UpdateEnemies(float dt, Vector2 target) => EnemyUpdates++;
        public void SyncEnemiesAfterPhysics()
        {
        }
        public void ResolveDamage(Person2D player)
        {
            if (DamageNextStep <= 0)
            {
                return;
            }

            player.TakeDamage(DamageNextStep, Vector2.Zero);
            DamageNextStep = 0;
        }
        public WorldThingSpec2D? UpdateSavePoints(float dt, Rect2D bounds)
        {
            var checkpoint = PendingCheckpoint;
            PendingCheckpoint = null;
            return checkpoint;
        }
        public void SetActiveSavePoint(long? id)
        {
        }
    }

    private sealed class TestActions : ISessionPlayerActions2D
    {
        public WeaponState2D CaptureWeaponState() => WeaponState2D.Empty;
        public event Action<WeaponEvent2D>? WeaponOccurred { add { } remove { } }
        public EquipmentKind2D Equipment => EquipmentKind2D.Unarmed;
        public bool IsMeleeAttackActive => false;
        public int BeginFrames { get; private set; }
        public int Interruptions { get; private set; }
        public event Action<UnarmedAttackKind2D, float>? UnarmedAttackStarted;
        public event Action<float>? MeleeAttackStarted { add { } remove { } }
        public event Action<float>? DownAttackStarted { add { } remove { } }
        public event Action? ShotStarted { add { } remove { } }
        public void InterruptPrimary() => Interruptions++;
        public void BeginFrame(float dt) => BeginFrames++;
        public void UpdateAfterPhysics(float dt, float facing)
        {
        }
        public float UsePrimary(float facing)
        {
            UnarmedAttackStarted?.Invoke(UnarmedAttackKind2D.Punch, 0.28f);
            return facing;
        }
        public float UseSecondary(float facing) => facing;
        public void Reset()
        {
        }
    }
}
