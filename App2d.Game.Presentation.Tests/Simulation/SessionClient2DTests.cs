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

public sealed class SessionClient2DTests
{
    [Fact]
    public void AttachAtNonzeroTickAndSendInputsWithoutWaitingForResponses()
    {
        using var game = new Fixture();
        for (var i = 0; i < 50; i++) game.Step();
        var snapshot = game.Session.CaptureSnapshot();
        var client = new SessionClient2D(snapshot, game.Person.Id);
        var inputs = Enumerable.Range(0, 3).Select(_ => client.CreateInput(default)).ToArray();
        Assert.Equal(new long[] { 51, 52, 53 }, inputs.Select(i => i.Tick));
        Assert.Equal(new long[] { 51, 52, 53 }, inputs.Select(i => i.Sequence));
        Assert.Equal(50, client.Tick);
        var frames = inputs.Select(input => game.Session.Advance(input)).ToArray();
        Assert.True(client.Apply(frames[0]));
        Assert.Equal(53, client.InputTick);
        Assert.Equal(54, client.CreateInput(default).Tick);
        Assert.True(client.Apply(frames[1]));
        Assert.True(client.Apply(frames[2]));
        Assert.Equal(53, client.LastInputSequence);
        Assert.False(client.Apply(frames[2]));
        Assert.Equal(50, snapshot.Tick);
    }

    [Fact]
    public void InvalidDeliveryCannotPartiallyChangeClientClocksOrObservation()
    {
        using var game = new Fixture();
        game.Step();
        var snapshot = game.Session.CaptureSnapshot();
        var client = new SessionClient2D(snapshot, game.Person.Id);
        var frame = game.Step();
        Assert.Throws<InvalidOperationException>(() => client.Apply(frame with { Tick = frame.Tick + 1 }));
        Assert.Throws<InvalidOperationException>(() => client.Apply(frame with
            { Players = [frame.Players[0] with { LastInputSequence = 0 }] }));
        Assert.False(client.TryApply(frame with { Tick = frame.Tick + 1 }, out var rejection));
        Assert.Equal(FrameRejection2D.Gap, rejection);
        Assert.False(client.TryApply(frame with { Players = [] }, out rejection));
        Assert.Equal(FrameRejection2D.MissingPlayer, rejection);
        Assert.Throws<InvalidOperationException>(() => client.Apply(frame with { World = null! }));
        Assert.Same(snapshot, client.Snapshot);
        Assert.Equal(snapshot.Tick, client.InputTick);
        Assert.True(client.Apply(frame));
    }

    [Fact]
    public void ClientReceivesEventsOnceAndStateQueriesDoNotConsumeThem()
    {
        using var game = new Fixture();
        var client = new SessionClient2D(game.Session.CaptureSnapshot(), game.Person.Id);
        var command = client.CreateInput(Command(attack: true));
        var first = game.Session.Advance(command);
        Assert.Single(first.Events.OfType<AttackStarted2D>());
        Assert.Equal(1, first.Events[0].Stamp.Tick);
        Assert.Equal(game.Person.Id, first.Events[0].Stamp.EntityId);
        _ = game.Session.CapturePlayers();
        _ = game.Session.CapturePlayers();
        Assert.True(client.Apply(first));
        Assert.False(client.Apply(first));

        var second = game.Session.Advance(client.CreateInput(default));
        Assert.True(client.Apply(second));
        Assert.Empty(second.Events.OfType<AttackStarted2D>());
        Assert.Single(first.Events.OfType<AttackStarted2D>());
        Assert.False(client.Apply(first));
        Assert.Equal(2, client.Tick);
    }

    private static PersonCommand2D Command(float move = 0f, bool jump = false, bool held = false, bool attack = false) =>
        new() { MoveX = move, JumpHeld = jump || held, PrimaryHeld = attack };

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
            Person = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, metrics, Vector2.Zero,
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
        public EquipmentKind2D Equipment => EquipmentKind2D.Unarmed;
        public bool IsMeleeAttackActive => false;
        public int BeginFrames { get; private set; }
        public int Interruptions { get; private set; }
        public event Action<UnarmedAttackKind2D, float>? UnarmedAttackStarted;
        public event Action<EquipmentKind2D>? EquipmentChanged { add { } remove { } }
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
