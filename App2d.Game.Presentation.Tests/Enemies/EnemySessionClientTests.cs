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

public sealed class EnemySessionClientTests
{
    [Fact]
    public void SessionDeliversImmutableEnemyStatesAndStampedAttackEvents()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var player = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, metrics, Vector2.Zero, 2, 1, CombatFaction2D.Player);
        var combat = new CombatSystem2D(physics.CollisionSystem, new CombatantRegistry2D());
        var arsenal = new PersonArsenal2D(new EntityIdAllocator2D(), player.Body, metrics.GunMuzzleOffset, physics.CollisionSystem,
            1, 4, CombatFaction2D.Player, combat);
        player.AttachActions(arsenal);
        var brute = new BoilerBrute2D(EntityId2D.Create(), physics.CollisionSystem, physics, new Vector2(80f, 0f), -100f, 100f, 1, 4);
        var world = new EnemyWorld();
        world.Enemies.Register(brute, new TileChunk2D(0, 0));
        using var session = new SideScrollerSession2D(physics, player, arsenal, world, new RespawnState2D(Vector2.Zero, 5), combat);
        var endpoint = new SessionClient2D(session.CaptureSnapshot(), player.Id);
        var events = new List<EnemyOccurred2D>();
        SessionFrame2D? first = null;
        for (var i = 0; i < 120; i++)
        {
            var frame = session.Advance(endpoint.CreateInput(default));
            first ??= frame;
            Assert.True(endpoint.Apply(frame));
            Assert.False(endpoint.Apply(frame));
            Assert.All(frame.Events.OfType<EnemyOccurred2D>(), e =>
            {
                Assert.Equal(frame.Tick, e.Stamp.Tick);
                Assert.Equal(brute.Enemy.Id, e.Stamp.EntityId);
            });
            events.AddRange(frame.Events.OfType<EnemyOccurred2D>());
        }
        Assert.Single(events, e => e.Occurrence is HammerStarted2D);
        Assert.Single(events, e => e.Occurrence is HammerStruck2D);
        Assert.Empty(world.DrainEnemyEvents());
        var original = Assert.Single(first!.Enemies);
        brute.Enemy.WorldObject.Transform.Position = new Vector2(999f, 100f);
        Assert.Equal(original, Assert.Single(first.Enemies));
        Assert.NotEqual(original.Position, Assert.Single(session.CaptureEnemies()).Position);
    }

    private sealed class EnemyWorld : ISideScrollerSessionWorld2D
    {
        public EnemySystem2D Enemies { get; } = new();
        public Bounds2D Bounds => new(new Vector2(-10000f), new Vector2(10000f));
        public float GoalX => 9000f;
        public ImmutableArray<EnemyState2D> CaptureEnemies() => Enemies.CaptureStates();
        public ImmutableArray<EnemyEvent2D> DrainEnemyEvents() => Enemies.DrainEvents();
        public void UpdateStreaming(Vector2 position) => Enemies.UpdateStreaming(_ => true);
        public void UpdateMovingPlatforms(float dt) { }
        public void UpdateEnemies(float dt, Vector2 target) => Enemies.Update(dt, target);
        public void SyncEnemiesAfterPhysics() => Enemies.SyncAfterPhysics();
        public void ResolveDamage(Person2D player) => Enemies.TryResolvePlayerHits(player);
        public WorldThingSpec2D? UpdateSavePoints(float dt, Bounds2D bounds) => null;
        public void SetActiveSavePoint(long? id) { }
    }
}
