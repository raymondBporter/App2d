using App2d.Levels;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class WorldStateTests
{
    [Fact]
    public void TerrainSnapshotsKeepOldTilesAndHaloWhileDirtyChunksGetNewRevisions()
    {
        var map = CreateMap();
        map.SetTileKind(31, 4, TileKind2D.Solid);
        map.SetTileKind(32, 4, TileKind2D.Ladder);
        var physics = new PhysicsWorld2D();
        using var level = CreateLevel(map);
        level.CreateSimulation(physics.CollisionSystem, physics, 1, 2, 4);
        var original = level.CaptureState();
        var left = Assert.Single(original.Terrain, c => c.Chunk == new TileChunk2D(0, 0));
        var right = Assert.Single(original.Terrain, c => c.Chunk == new TileChunk2D(1, 0));
        Assert.Equal(TileKind2D.Ladder, left.GetTileKind(32, 4)); // Halo crosses the boundary.
        Assert.Equal(original.Terrain, level.CaptureState().Terrain); // Cached immutable collection.
        map.SetTileKind(32, 4, TileKind2D.Solid);
        level.FlushDirtyChunks();
        var edited = level.CaptureState();
        var changedLeft = Assert.Single(edited.Terrain, c => c.Chunk == left.Chunk);
        var changedRight = Assert.Single(edited.Terrain, c => c.Chunk == right.Chunk);
        Assert.NotEqual(left.Revision, changedLeft.Revision);
        Assert.NotEqual(right.Revision, changedRight.Revision);
        Assert.Equal(TileKind2D.Solid, changedLeft.GetTileKind(32, 4));
        Assert.Equal(TileKind2D.Ladder, left.GetTileKind(32, 4));
        Assert.Empty(right.Collisions);
        Assert.Single(changedRight.Collisions);
        level.UpdateStreaming(map.WorldBounds.Max);
        Assert.DoesNotContain(level.CaptureState().Terrain, c => c.Chunk == left.Chunk);
        level.UpdateStreaming(level.SpawnPoint);
        Assert.NotEqual(changedLeft.Revision, Assert.Single(level.CaptureState().Terrain, c => c.Chunk == left.Chunk).Revision);
    }

    [Fact]
    public void ProductionLevelRunsInSessionWithoutGraphicsAndReturnsCheckpointAndPlatformState()
    {
        var metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var map = CreateMap();
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var registry = new CombatantRegistry2D();
        var combat = new CombatSystem2D(physics.CollisionSystem, registry);
        var spawn = new Vector2(-368f, 40f);
        var checkpoint = new Vector2(-150f, 40f);
        WorldThingSpec2D[] things =
        [
            new(1, WorldThingKind2D.PlayerSpawn, null, true, spawn),
            new(2, WorldThingKind2D.SavePoint, null, true, checkpoint),
            new(3, WorldThingKind2D.Goal, null, true, new Vector2(6000f, 0f)),
            new(4, WorldThingKind2D.Shieldback, null, true, new Vector2(600f, 50f)),
            new(5, WorldThingKind2D.GreenDinosaur, null, true, new Vector2(800f, 50f)),
            new(6, WorldThingKind2D.BoilerBrute, null, true, new Vector2(1000f, 50f)),
            new(7, WorldThingKind2D.Rival, null, true, new Vector2(1200f, 50f)),
            new(8, WorldThingKind2D.TumbleProp, null, true, new Vector2(1400f, 50f))
        ];
        using var level = new SideScrollerLevel2D(metrics, map, _ => 1, [Platform()], things);
        level.CreateSimulation(physics.CollisionSystem, physics, 1, 2, 4);
        level.CreateAuthoredWorldThings(combat);
        var player = new Person2D(physics.CollisionSystem, physics, metrics, spawn, 2, 1, CombatFaction2D.Player, tileMap: map);
        registry.Register(player);
        var arsenal = new PersonArsenal2D(player.Body, metrics.GunMuzzleOffset, physics.CollisionSystem, 1, 4, CombatFaction2D.Player, combat);
        player.AttachActions(arsenal);
        using var session = new SideScrollerSession2D(physics, player, arsenal,
            new SideScrollerSessionWorld2D(level, new ContactDamageSystem2D(physics.CollisionSystem, 4, registry)), new RespawnState2D(spawn, 5), combat);
        var initial = session.CaptureWorld();
        for (var i = 0; i < 10; i++) Step();
        player.WorldObject.Transform.Position = checkpoint;
        var entered = Step();
        Assert.Single(entered.Events.OfType<CheckpointActivated2D>());
        Assert.True(Assert.Single(entered.World.Checkpoints).IsActive);
        Assert.False(Assert.Single(initial.Checkpoints).IsActive);
        Assert.NotEqual(initial.MovingPlatforms[0].Position, entered.World.MovingPlatforms[0].Position);
        Assert.Equal(5, entered.Enemies.Length);
        Assert.Equal(things[2].Position, entered.World.GoalPosition);
        Assert.Empty(Step().Events.OfType<CheckpointActivated2D>());

        var enemyIds = level.EnemySystem.Combatants.Select(e => e.Id).ToArray();
        level.Dispose();
        Assert.Same(player.Body, Assert.Single(physics.Bodies));
        Assert.All(enemyIds, id => Assert.Null(registry.Find(id)));
        Assert.NotEmpty(initial.Terrain); // Disposal cannot invalidate an earlier observation.

        SessionFrame2D Step() => session.Advance(new PlayerInput2D(player.Id, session.Tick + 1, session.Tick + 1, default));
    }

    private static EditableTileMap2D CreateMap() => new(SideScrollerLevel2D.WorldWidthTiles,
        SideScrollerLevel2D.WorldHeightTiles, 32f, SideScrollerLevel2D.ChunkSizeTiles,
        SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
    private static SideScrollerLevel2D CreateLevel(EditableTileMap2D map, MovingPlatformSpec2D[]? platforms = null) =>
        new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, _ => 1, platforms);
    private static MovingPlatformSpec2D Platform() => new(41, "Lift", true,
        new Vector2(-200f, 100f), new Vector2(96f, 0f), new Vector2(80f, 14f), 48f, 0xFF25D2BEu);
}
