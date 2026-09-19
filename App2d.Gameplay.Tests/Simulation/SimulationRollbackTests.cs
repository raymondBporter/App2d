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
using App2d.Tiles;
using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using Xunit;

namespace App2d.Gameplay.Tests.Simulation;

public sealed class SimulationRollbackTests
{
    private static PersonCommand2D Hold => new() { PrimaryHeld = true };
    private static PersonCommand2D Press => Hold; // A press is a hold after a release.
    private static PersonCommand2D Switch => new() { SwitchHeld = true };
    private static PersonCommand2D Move(float x) => new() { MoveX = x };

    [Fact]
    public void ChargeProjectileCreationExpiryAndSlotReuseReplayWithIdenticalIds()
    {
        using var game = new Fixture(gravity: false);
        game.Step(Switch);
        game.Step(Press);
        game.Steps(30, Hold);
        Assert.True(game.Arsenal.IsChargingPrimary);
        var commands = Enumerable.Range(0, 330).Select(i => i == 100 || i == 200 ? Press :
            i == 99 || i == 199 ? default : Hold).ToArray();
        var frames = AssertReplay(game, commands);
        Assert.Equal(3, frames.SelectMany(f => f.Events).OfType<WeaponOccurred2D>().Count(e => e.Occurrence is GunFired2D));
        var ids = frames.SelectMany(f => f.Players[0].Weapons.Projectiles).Select(p => p.Id).Distinct().ToArray();
        Assert.Equal(3, ids.Length);
        Assert.All(ids, id => Assert.True(id.IsValid));
        // Checkpoint with a live projectile retains its remaining lifetime and slot state.
        game.Step(default);
        game.Step(Press);
        game.Steps(75, Hold);
        Assert.NotEmpty(game.Session.CapturePlayers()[0].Weapons.Projectiles);
        AssertReplay(game, Enumerable.Repeat(default(PersonCommand2D), 190));
    }

    [Fact]
    public void MeleeHitHistoryBufferedSwingsAndDeathsSurviveRewind()
    {
        using var game = new Fixture(gravity: false, things:
            [new(11, WorldThingKind2D.GreenDinosaur, null, true, new Vector2(75f, 45f))]);
        game.Step(Press);
        var hit = false;
        for (var i = 0; i < 20 && !hit; i++)
            hit = game.Step().Events.OfType<CombatDamageOccurred2D>().Any();
        Assert.True(hit);
        var target = Assert.Single(game.Level.EnemySystem.Combatants);
        Assert.True(target.Health.Current < target.Health.Maximum);
        game.Step(default);
        game.Step(Press); // Buffered while the first swing is active.
        var health = target.Health.Current;
        var frames = AssertReplay(game, Enumerable.Repeat(default(PersonCommand2D), 120));
        Assert.True(target.Health.Current <= health);
        Assert.NotEmpty(frames);
    }

    [Fact]
    public void AllAuthoredEnemyKindsReplayMidAttackWithIdenticalFactsAndPhysics()
    {
        using var game = new Fixture(things:
        [
            new(11, WorldThingKind2D.BoilerBrute, null, true, new Vector2(120f, 50f)),
            new(12, WorldThingKind2D.Rival, null, true, new Vector2(400f, 45f)),
            new(13, WorldThingKind2D.Shieldback, null, true, new Vector2(700f, 24f)),
            new(14, WorldThingKind2D.GreenDinosaur, null, true, new Vector2(900f, 34f)),
            new(15, WorldThingKind2D.TumbleProp, null, true, new Vector2(1100f, 34f))
        ]);
        game.Steps(52);
        Assert.Equal(5, game.Session.CaptureEnemies().Length);
        Assert.Contains(game.Session.CaptureEnemies(), e => e.IsAttacking);
        var frames = AssertReplay(game, Enumerable.Range(0, 260).Select(i => i < 100 ? default : Move(1f)));
        Assert.NotEmpty(frames.SelectMany(f => f.Events).OfType<EnemyOccurred2D>());
    }

    [Fact]
    public void PlatformContactCarryingReversalAndDropThroughReplay()
    {
        using var game = new Fixture(platform: true);
        var platform = Assert.Single(game.Level.MovingPlatforms);
        game.Player.WorldObject.Transform.Position = platform.Start + new Vector2(0f, 8f + game.Metrics.PlayerColliderSize.Y / 2f);
        game.Steps(8);
        Assert.True(game.Player.IsGrounded);
        Assert.NotEmpty(game.Physics.LastContacts);
        AssertReplay(game, Enumerable.Repeat(default(PersonCommand2D), 150));
        game.Player.WorldObject.Transform.Position = platform.WorldObject.Transform.Position + new Vector2(0f, 8f + game.Metrics.PlayerColliderSize.Y / 2f);
        game.Player.Body.LinearVelocity = Vector2.Zero;
        game.Steps(4);
        game.Step(new PersonCommand2D { DownHeld = true, JumpHeld = true });
        Assert.True(game.Player.Body.IgnoredOneWayPlatformCount > 0);
        AssertReplay(game, Enumerable.Repeat(default(PersonCommand2D), 120));
    }

    [Fact]
    public void LadderLatchJumpHoldDashAndLandingReplay()
    {
        using var game = new Fixture(ladder: true);
        var climb = new PersonCommand2D { ClimbY = 1 };
        game.Steps(30, climb);
        Assert.True(game.Player.IsClimbingLadder);
        var commands = Enumerable.Range(0, 180).Select(i => i == 10
            ? climb with { JumpHeld = true, MoveX = 1 }
            : i < 10 ? climb : Move(1) with { JumpHeld = i < 30, DashHeld = i == 35 });
        AssertReplay(game, commands);
    }

    [Fact]
    public void StreamingRestoresTerrainIdsOrderRevisionsAndEnemyActivation()
    {
        using var game = new Fixture(gravity: false, things:
            [new(11, WorldThingKind2D.Rival, null, true, new Vector2(6000f, 50f))]);
        game.Steps(3);
        var old = game.Session.CaptureCheckpoint();
        var oldValue = Describe(old);
        var inputs = Enumerable.Range(0, 1000).Select(_ => Move(1)).ToArray();
        var originalTerrain = game.Session.CaptureContent().Terrain.Select(c => c.Chunk).ToArray();
        var frames = AssertReplay(game, inputs);
        Assert.NotEqual(originalTerrain, frames[^1].Content.Terrain.Select(c => c.Chunk).ToArray());
        Assert.Equal(oldValue, Describe(old));
        game.Session.RestoreCheckpoint(old);
        Assert.Equal(oldValue, Describe(game.Session.CaptureCheckpoint()));
    }

    [Fact]
    public void CheckpointEntryDeathRespawnGoalAndPauseAreRestoredWithoutCallbacks()
    {
        using var game = new Fixture(gravity: false, things:
        [new(11, WorldThingKind2D.SavePoint, null, true, new Vector2(240f, 45f)),
         new(12, WorldThingKind2D.Goal, null, true, new Vector2(450f, 0f))]);
        game.Player.WorldObject.Transform.Position = new Vector2(240f, 45f);
        Assert.Single(game.Step().Events.OfType<CheckpointActivated2D>());
        var entered = game.Session.CaptureCheckpoint();
        var enteredValue = Describe(entered);
        AssertReplay(game, Enumerable.Repeat(default(PersonCommand2D), 5));
        Assert.True(game.Player.TakeDamage(5, Vector2.Zero));
        game.Step();
        Assert.False(game.Player.IsAlive);
        var dead = game.Session.CaptureCheckpoint();
        var callbacks = 0;
        game.Player.Died += () => callbacks++;
        game.Player.Damaged += () => callbacks++;
        game.Arsenal.EquipmentChanged += _ => callbacks++;
        game.Session.RestoreCheckpoint(entered);
        Assert.Equal(0, callbacks);
        Assert.Equal(enteredValue, Describe(game.Session.CaptureCheckpoint()));
        game.Session.RestoreCheckpoint(dead);
        var frames = AssertReplay(game, Enumerable.Repeat(Move(1), 300));
        Assert.Single(frames.SelectMany(f => f.Events).OfType<Respawned2D>());
        Assert.Single(frames.SelectMany(f => f.Events).OfType<GoalReached2D>());
        game.Session.SetPaused(true);
        var paused = game.Session.CaptureCheckpoint();
        game.Session.SetPaused(false);
        game.Step();
        game.Session.RestoreCheckpoint(paused);
        Assert.True(game.Session.IsPaused);
        Assert.Throws<InvalidOperationException>(() => game.Step());
    }

    [Fact]
    public void InvalidatedOrForeignCheckpointsAreRejectedBeforeMutation()
    {
        using var first = new Fixture();
        using var second = new Fixture();
        var checkpoint = first.Session.CaptureCheckpoint();
        Assert.Throws<InvalidOperationException>(() => second.Session.RestoreCheckpoint(checkpoint));
        first.Map.SetTileKind(400, 40, TileKind2D.Solid); // Even an unloaded edit invalidates history.
        var before = first.Session.CapturePlayers()[0];
        Assert.Throws<InvalidOperationException>(() => first.Session.RestoreCheckpoint(checkpoint));
        Assert.Equal(before, first.Session.CapturePlayers()[0]);
        first.Level.FlushDirtyChunks();
        checkpoint = first.Session.CaptureCheckpoint();
        first.Level.ReloadMovingPlatforms([]);
        Assert.Throws<InvalidOperationException>(() => first.Session.RestoreCheckpoint(checkpoint));
        checkpoint = first.Session.CaptureCheckpoint();
        first.Physics.AddBody(new SpatialObject2D(new Circle2D(1)), BodyMotionType2D.Static);
        Assert.Throws<InvalidOperationException>(() => first.Session.RestoreCheckpoint(checkpoint));
    }

    [Fact]
    public void HistoryIsBoundedAndReplaysRetainedInputsWithOriginalSequences()
    {
        using var game = new Fixture();
        var history = new SessionReplayBuffer2D(game.Session, capacity: 12);
        var frames = new List<SessionFrame2D>();
        for (var i = 0; i < 30; i++)
            frames.Add(history.Advance(new PlayerInput2D(game.Player.Id, game.Session.Tick + 1, (i + 1) * 3, Move(1))));
        Assert.Equal(12, history.Count);
        Assert.Equal(18, history.OldestTick);
        var before = Describe(game.Session.CaptureCheckpoint());
        var replayed = history.ReplayFrom(18);
        Assert.Equal(12, replayed.Length);
        for (var i = 0; i < 12; i++) AssertFrame(frames[i + 18], replayed[i]);
        Assert.Equal(before, Describe(game.Session.CaptureCheckpoint()));
        Assert.Throws<ArgumentOutOfRangeException>(() => history.ReplayFrom(17));
        Assert.Equal(before, Describe(game.Session.CaptureCheckpoint()));
    }

    [Fact]
    public void IdenticalDefinitionsAllocateIdenticalIdsAndRewindingOneSessionLeavesTheOtherAlone()
    {
        using var first = new Fixture(gravity: false);
        using var second = new Fixture(gravity: false);
        first.Step(Switch);
        second.Step(Switch);
        var checkpoint = first.Session.CaptureCheckpoint();
        first.Step(Press); first.Steps(71, Hold);
        var firstId = Assert.Single(first.Session.CapturePlayers()[0].Weapons.Projectiles).Id;
        second.Step(Press); second.Steps(71, Hold);
        var secondId = Assert.Single(second.Session.CapturePlayers()[0].Weapons.Projectiles).Id;
        first.Session.RestoreCheckpoint(checkpoint);
        first.Step(Press); first.Steps(71, Hold);
        Assert.Equal(firstId, Assert.Single(first.Session.CapturePlayers()[0].Weapons.Projectiles).Id);
        // Deterministic allocation: a server and a predicting client built the same way agree on IDs.
        Assert.Equal(firstId, secondId);
        Assert.Equal(secondId, Assert.Single(second.Session.CapturePlayers()[0].Weapons.Projectiles).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MidJumpAndMidDashCheckpointsRetainMovementTimers(bool dash)
    {
        using var game = new Fixture();
        game.Steps(4);
        game.Step(Move(1) with { JumpHeld = !dash, DashHeld = dash });
        game.Steps(3, Move(1) with { JumpHeld = !dash });
        Assert.True(dash ? game.Player.IsDashing : game.Player.IsSustainingJump);
        AssertReplay(game, Enumerable.Range(0, 180).Select(i => Move(1) with { JumpHeld = i < 15 }));
    }

    [Fact]
    public void WallGripAndRelatchDelayReplay()
    {
        using var game = new Fixture();
        for (var y = 20; y < 34; y++) game.Map.SetTileKind(18, y, TileKind2D.Solid | TileKind2D.Grippable);
        game.Level.FlushDirtyChunks();
        game.Player.WorldObject.Transform.Position = new Vector2(
            64f - game.Metrics.PlayerColliderSize.X / 2 - game.Metrics.PlayerColliderCenterOffsetX - 0.5f, 160f);
        game.Player.Body.LinearVelocity = new Vector2(0, -10);
        game.Steps(4, Move(1));
        Assert.True(game.Player.IsWallGripping);
        AssertReplay(game, Enumerable.Range(0, 100).Select(i => Move(1) with { JumpHeld = i >= 3 && i < 20 }));
    }

    [Fact]
    public void DownwardBounceStateAndHitSuppressionReplay()
    {
        using var game = new Fixture(gravity: false, things:
            [new(11, WorldThingKind2D.GreenDinosaur, null, true, new Vector2(0, 45))]);
        game.Player.WorldObject.Transform.Position = new Vector2(0, 100);
        game.Step(new PersonCommand2D { PrimaryHeld = true, DownHeld = true });
        Assert.True(game.Player.DownAttackBouncedThisFrame);
        AssertReplay(game, Enumerable.Repeat(default(PersonCommand2D), 90));
    }

    [Fact]
    public void LethalProjectileImpactRestoresEnemyHealthAndDefeatCount()
    {
        using var game = new Fixture(gravity: false, things:
            [new(11, WorldThingKind2D.GreenDinosaur, null, true, new Vector2(240, 45))]);
        var enemy = Assert.Single(game.Level.EnemySystem.Combatants);
        enemy.Health.Damage(2);
        game.Step(Switch);
        game.Step(Press);
        game.Steps(60, Hold);
        var beforeHit = game.Session.CaptureCheckpoint();
        var frames = AssertReplay(game, Enumerable.Repeat(Hold, 100));
        Assert.False(enemy.IsAlive);
        Assert.Contains(frames.SelectMany(f => f.Events).OfType<CombatDamageOccurred2D>(), e => e.Damage.WasKilled);
        var dead = game.Session.CaptureCheckpoint();
        game.Session.RestoreCheckpoint(beforeHit);
        Assert.True(enemy.IsAlive);
        game.Session.RestoreCheckpoint(dead);
        Assert.False(enemy.IsAlive);
    }

    [Fact]
    public void AccumulatedForcesSpinAndBodySettingsAreRestored()
    {
        using var game = new Fixture(things:
            [new(11, WorldThingKind2D.TumbleProp, null, true, new Vector2(200, 60))]);
        var prop = Assert.Single(game.Level.EnemySystem.Combatants).Body;
        prop.LinearVelocity = new(22, 30);
        prop.AngularVelocity = 3f;
        prop.AddForce(new(600, 300));
        prop.AddTorque(125f);
        game.Player.Body.AddForce(new(150, 30));
        var snapshot = game.Session.CaptureCheckpoint();
        var before = Describe(snapshot);
        game.Steps(2);
        prop.Friction = 0.9f;
        prop.FreezeRotation = true;
        prop.Mass = 3f;
        game.Physics.Gravity = Vector2.Zero;
        game.Physics.CollisionSystem.CellSize = 64;
        game.Session.RestoreCheckpoint(snapshot);
        Assert.Equal(before, Describe(game.Session.CaptureCheckpoint()));
        AssertReplay(game, Enumerable.Repeat(default(PersonCommand2D), 90));
    }

    [Fact]
    public void RemovingTerrainClearsContactsAndDropThroughReferences()
    {
        using var game = new Fixture();
        game.Steps(4);
        var floor = game.Physics.Bodies.First(b => b.EntityId == EntityId2D.None);
        game.Player.Body.IgnoreOneWayPlatform(floor);
        game.Level.UpdateStreaming(new Vector2(14000, 45));
        Assert.Equal(0, game.Player.Body.IgnoredOneWayPlatformCount);
        Assert.DoesNotContain(game.Physics.LastContacts, c => c.First == floor || c.Second == floor);
        // Capturing after editor-style streaming no longer retains detached physics objects.
        game.Session.CaptureCheckpoint();
    }

    [Fact]
    public void CaptureDuringTickAndReplayAfterOutsidePauseAreRejected()
    {
        using var game = new Fixture();
        var snapshot = game.Session.CaptureCheckpoint();
        game.Player.JumpStarted += () =>
        {
            Assert.Throws<InvalidOperationException>(() => game.Session.CaptureCheckpoint());
            Assert.Throws<InvalidOperationException>(() => game.Session.RestoreCheckpoint(snapshot));
        };
        game.Steps(4);
        game.Step(new PersonCommand2D { JumpHeld = true });
        var history = new SessionReplayBuffer2D(game.Session);
        game.Session.SetPaused(true);
        Assert.Throws<InvalidOperationException>(() => history.ReplayFrom(history.OldestTick));
        Assert.True(game.Session.IsPaused);
    }

    private static SessionFrame2D[] AssertReplay(Fixture game, IEnumerable<PersonCommand2D> source)
    {
        var commands = source.ToArray();
        var checkpoint = game.Session.CaptureCheckpoint();
        var savedValue = Describe(checkpoint);
        var frames = new SessionFrame2D[commands.Length];
        var states = new string[commands.Length];
        for (var i = 0; i < commands.Length; i++)
        {
            frames[i] = game.Step(commands[i]);
            states[i] = Describe(game.Session.CaptureCheckpoint());
        }
        Assert.Equal(savedValue, Describe(checkpoint));
        game.Session.RestoreCheckpoint(checkpoint);
        Assert.Equal(savedValue, Describe(game.Session.CaptureCheckpoint()));
        for (var i = 0; i < commands.Length; i++)
        {
            AssertFrame(frames[i], game.Step(commands[i]));
            Assert.Equal(states[i], Describe(game.Session.CaptureCheckpoint()));
        }
        return frames;
    }

    private static void AssertFrame(SessionFrame2D expected, SessionFrame2D actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.Players.Length, actual.Players.Length);
        for (var i = 0; i < expected.Players.Length; i++)
        {
            Assert.Equal(expected.Players[i] with { Weapons = default }, actual.Players[i] with { Weapons = default });
            Assert.Equal(expected.Players[i].Weapons with { Projectiles = [] }, actual.Players[i].Weapons with { Projectiles = [] });
            Assert.Equal(expected.Players[i].Weapons.Projectiles.ToArray(), actual.Players[i].Weapons.Projectiles.ToArray());
        }
        // The content revision is a local cache counter; compare what it describes.
        Assert.Equal(Describe(expected.Content with { Revision = 0 }), Describe(actual.Content with { Revision = 0 }));
        Assert.Equal(expected.Events.ToArray(), actual.Events.ToArray());
        Assert.Equal(expected.Enemies.ToArray(), actual.Enemies.ToArray());
        Assert.Equal(Describe(expected.World), Describe(actual.World));
    }

    // Inspect opaque checkpoint values in tests only. No reflection is used by capture/restore.
    // Comparing internals catches timer/history drift even before it becomes visible in a frame.
    private static string Describe(object? value)
    {
        if (value is null) return "null";
        var type = value.GetType();
        if (value is string text) return text;
        if (type.IsPrimitive || type.IsEnum || value is Guid)
            return Convert.ToString(value, CultureInfo.InvariantCulture)!;
        if (value is Vector2 vector) return $"({vector.X:R},{vector.Y:R})";
        if (value is IEnumerable items) return "[" + string.Join(",", items.Cast<object?>().Select(Describe)) + "]";
        return type.Name + "{" + string.Join(",", type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract" && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name).Select(p => p.Name + ":" + Describe(p.GetValue(value)))) + "}";
    }

    private sealed class Fixture : IDisposable
    {
        public TraversalMetrics2D Metrics { get; } = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        public EditableTileMap2D Map { get; }
        public PhysicsWorld2D Physics { get; } = new();
        public SideScrollerLevel2D Level { get; }
        public Person2D Player { get; }
        public PersonArsenal2D Arsenal { get; }
        public SideScrollerSession2D Session { get; }
        public Fixture(bool gravity = true, WorldThingSpec2D[]? things = null, bool platform = false, bool ladder = false)
        {
            Map = new(640, 96, 32f, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
            for (var x = 0; x < 640; x++) Map.SetTileKind(x, 19, TileKind2D.Solid);
            if (ladder) for (var y = 20; y < 38; y++) Map.SetTileKind(16, y, TileKind2D.Ladder);
            Physics.Gravity = gravity ? new(0, -Metrics.Gravity) : Vector2.Zero;
            Physics.MaxSubstepSeconds = SideScrollerSession2D.FixedDeltaSeconds;
            var spawn = new Vector2(ladder ? 16f - Metrics.PlayerColliderCenterOffsetX : 0f, Metrics.PlayerColliderSize.Y / 2 + 1f);
            var authored = new[] { new WorldThingSpec2D(1, WorldThingKind2D.PlayerSpawn, null, true, spawn) }.Concat(things ?? []).ToArray();
            Level = new(Metrics, Map, _ => 20, platform ?
                [new(2, "Lift", true, new(0, 96), new(64, 64), new(200, 16), 96, 0xFF25D2BEu)] : [], authored);
            var ids = new EntityIdAllocator2D();
            Level.CreateSimulation(Physics.CollisionSystem, Physics, ids, 1, 2, 4);
            var registry = new CombatantRegistry2D();
            var combat = new CombatSystem2D(Physics.CollisionSystem, registry);
            Level.CreateAuthoredWorldThings(combat);
            Player = new(ids.Allocate(), Physics.CollisionSystem, Physics, Metrics, spawn, 2, 1, CombatFaction2D.Player, tileMap: Map);
            registry.Register(Player);
            Arsenal = new(ids, Player.Body, Metrics.GunMuzzleOffset, Physics.CollisionSystem, 1, 4, CombatFaction2D.Player, combat,
                bounds => Level.TryGetSpikeSource(bounds, out _));
            Player.AttachActions(Arsenal);
            Session = new(Physics, Player, Arsenal, new SideScrollerSessionWorld2D(Level,
                new ContactDamageSystem2D(Physics.CollisionSystem, 4, registry)), new(spawn, 5), combat);
        }
        public SessionFrame2D Step(PersonCommand2D command = default) => Session.Advance(new PlayerInput2D(Player.Id, Session.Tick + 1, Session.Tick + 1, command));
        public void Steps(int count, PersonCommand2D command = default) { for (var i = 0; i < count; i++) Step(command); }
        public void Dispose() { Session.Dispose(); Level.Dispose(); }
    }
}
