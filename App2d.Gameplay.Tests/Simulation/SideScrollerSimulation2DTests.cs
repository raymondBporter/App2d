using App2d.Levels;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Simulation;

public sealed class SideScrollerSimulation2DTests
{
    [Fact]
    public void TwoSimulationsFromOneDefinitionAgreeOnIdentitiesAndFrames()
    {
        using var server = SideScrollerSimulation2D.Create(Definition());
        using var client = SideScrollerSimulation2D.Create(Definition());
        Assert.Equal(server.Session.PlayerIds.ToArray(), client.Session.PlayerIds.ToArray());
        Assert.Equal(server.Level.EnemySystem.Combatants.Select(c => c.Id),
            client.Level.EnemySystem.Combatants.Select(c => c.Id));
        Assert.Equal(server.Level.MovingPlatforms.Select(p => p.Id), client.Level.MovingPlatforms.Select(p => p.Id));
        Assert.Equal(server.Ids.Next, client.Ids.Next);

        var playerId = server.Session.PlayerIds[0];
        for (var tick = 1; tick <= 240; tick++)
        {
            var command = new PersonCommand2D
            {
                MoveX = tick < 120 ? 1f : -1f,
                JumpHeld = tick is >= 10 and < 30,
                PrimaryHeld = tick is >= 40 and < 130,
            };
            var input = new PlayerInput2D(playerId, tick, tick, command);
            var a = server.Session.Advance(input);
            var b = client.Session.Advance(input);
            Assert.Equal(a.Players.ToArray(), b.Players.ToArray());
            Assert.Equal(a.Events.ToArray(), b.Events.ToArray());
            Assert.Equal(a.Enemies.ToArray(), b.Enemies.ToArray());
            Assert.Equal(a.World.MovingPlatforms.ToArray(), b.World.MovingPlatforms.ToArray());
            Assert.Equal(a.World.Checkpoints.ToArray(), b.World.Checkpoints.ToArray());
        }
        Assert.NotEmpty(server.Session.CaptureContent().Terrain);
    }

    [Fact]
    public void SavedProgressResumesAtItsCheckpointOnlyWhenItStillExists()
    {
        using var resumed = SideScrollerSimulation2D.Create(Definition() with { SavedProgress = new(2, 3) });
        var player = resumed.Session.CapturePlayers()[0];
        Assert.Equal(2, player.CheckpointId);
        Assert.Equal(3, player.Person.HitPoints);
        Assert.Equal(Checkpoint, player.Person.Position);

        using var fresh = SideScrollerSimulation2D.Create(Definition() with { SavedProgress = new(99, 3) });
        var freshPlayer = fresh.Session.CapturePlayers()[0];
        Assert.Null(freshPlayer.CheckpointId);
        Assert.Equal(fresh.Definition.PlayerMaximumHealth, freshPlayer.Person.HitPoints);
        Assert.Equal(fresh.Level.SpawnPoint, freshPlayer.Person.Position);
    }

    private static readonly Vector2 Spawn = new(-368f, 40f);
    private static readonly Vector2 Checkpoint = new(-150f, 40f);

    private static SideScrollerSessionDefinition2D Definition()
    {
        var map = new EditableTileMap2D(SideScrollerLevel2D.WorldWidthTiles, SideScrollerLevel2D.WorldHeightTiles,
            32f, SideScrollerLevel2D.ChunkSizeTiles, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < map.Width; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        WorldThingSpec2D[] things =
        [
            new(1, WorldThingKind2D.PlayerSpawn, null, true, Spawn),
            new(2, WorldThingKind2D.SavePoint, null, true, Checkpoint),
            new(3, WorldThingKind2D.Goal, null, true, new Vector2(6000f, 0f)),
            new(4, WorldThingKind2D.Shieldback, null, true, new Vector2(-100f, 50f)),
            new(5, WorldThingKind2D.BoilerBrute, null, true, new Vector2(100f, 50f)),
            new(6, WorldThingKind2D.Rival, null, true, new Vector2(300f, 50f)),
            new(7, WorldThingKind2D.TumbleProp, null, true, new Vector2(500f, 50f)),
        ];
        MovingPlatformSpec2D[] platforms =
            [new(41, "Lift", true, new Vector2(-200f, 100f), new Vector2(96f, 0f), new Vector2(80f, 14f), 48f, 0xFF25D2BEu)];
        return new SideScrollerSessionDefinition2D(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, platforms, things);
    }
}
