using App2d.Core.Geometry;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class VegetationTests
{
    [Fact]
    public void GrassOnlyGrowsOnExposedPlainSolidsIncludingAcrossChunkEdges()
    {
        var map = Map();
        map.SetTileKind(10, 19, TileKind2D.Solid | TileKind2D.Spikes);
        map.SetTileKind(11, 19, TileKind2D.OneWay);
        map.SetTileKind(12, 20, TileKind2D.Ladder);
        map.SetTileKind(13, 20, TileKind2D.Solid);
        Assert.True(VegetationPlacement2D.HasGrass(map, 9, 19));
        for (var x = 10; x <= 13; x++) Assert.False(VegetationPlacement2D.HasGrass(map, x, 19));
        map.SetTileKind(31, 31, TileKind2D.Solid);
        var chunk = TerrainChunkState2D.Capture(map, new(0, 0), 1);
        Assert.True(VegetationPlacement2D.HasGrass(chunk, 31, 31));
        map.SetTileKind(31, 32, TileKind2D.Spikes);
        chunk = TerrainChunkState2D.Capture(map, new(0, 0), 2);
        Assert.False(VegetationPlacement2D.HasGrass(chunk, 31, 31));
    }

    [Fact]
    public void CutsAreIdempotentSurviveStreamingAndRestoreWithTheSession()
    {
        using var game = Create();
        var session = game.Session;
        var before = session.CaptureCheckpoint();
        var original = session.CaptureSnapshot();
        var cell = new GrassCell2D(6, 19);
        var bounds = VegetationPlacement2D.GrassBounds(game.Level.TileMap, cell);
        // A small strike inside the tuft should affect only that tile.
        game.Level.CutGrass(new(bounds.Center - new Vector2(1f), bounds.Center + new Vector2(1f)));
        var cut = session.CaptureSnapshot();
        Assert.Equal(cell, Assert.Single(cut.World.CutGrass));
        game.Level.CutGrass(bounds);
        Assert.Same(cut.World.CutGrass, session.CaptureSnapshot().World.CutGrass);
        Assert.Empty(original.World.CutGrass);
        var after = session.CaptureCheckpoint();
        game.Level.UpdateStreaming(game.Level.TileMap.WorldBounds.Max);
        game.Level.UpdateStreaming(game.Level.SpawnPoint);
        Assert.Contains(cell, session.CaptureSnapshot().World.CutGrass);
        session.RestoreCheckpoint(before);
        Assert.Empty(session.CaptureSnapshot().World.CutGrass);
        session.RestoreCheckpoint(after);
        Assert.Contains(cell, session.CaptureSnapshot().World.CutGrass);
    }

    [Theory]
    [InlineData(EquipmentKind2D.Sword, true)]
    [InlineData(EquipmentKind2D.Gun, false)]
    [InlineData(EquipmentKind2D.Unarmed, false)]
    public void OnlyTheActiveSwordDamageWindowCutsGrass(EquipmentKind2D equipment, bool shouldCut)
    {
        using var game = Create();
        while (game.Arsenal.Equipment != equipment) game.Arsenal.SelectNext();
        for (var i = 0; i < 80; i++) Step(default);
        Assert.Empty(game.Session.CaptureWorld().CutGrass);
        Step(new() { PrimaryHeld = true });
        Assert.Empty(game.Session.CaptureWorld().CutGrass); // Windup, not damage.
        for (var i = 0; i < 50; i++) Step(new() { PrimaryHeld = true });
        Assert.Equal(shouldCut, game.Session.CaptureWorld().CutGrass.Count > 0);
        if (shouldCut)
        {
            var cuts = game.Session.CaptureWorld().CutGrass;
            for (var i = 0; i < 30; i++) Step(default);
            Assert.Same(cuts, game.Session.CaptureWorld().CutGrass);
        }
        void Step(PersonCommand2D command)
        {
            var tick = game.Session.Tick + 1;
            game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick, command));
        }
    }

    [Fact]
    public void AirborneDownwardSwordCutsWithoutBouncingOffGrass()
    {
        using var game = Create();
        var tick = game.Session.Tick + 1;
        game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick,
            new() { PrimaryHeld = true, DownHeld = true }));
        Assert.NotEmpty(game.Session.CaptureWorld().CutGrass);
        Assert.False(game.Player.DownAttackBouncedThisFrame);
    }

    [Fact]
    public void StrikeBelowTheCutLineLeavesTheGrassAlone()
    {
        using var game = Create();
        var cell = new GrassCell2D(6, 19);
        var bounds = VegetationPlacement2D.GrassBounds(game.Level.TileMap, cell);
        game.Level.CutGrass(new(new(bounds.Center.X - 1f, 1f), new(bounds.Center.X + 1f, bounds.Bottom)));
        Assert.Empty(game.Session.CaptureWorld().CutGrass);
        game.Level.CutGrass(new(new(bounds.Center.X - 1f, bounds.Bottom), new(bounds.Center.X + 1f, bounds.Bottom + 1f)));
        Assert.Contains(cell, game.Session.CaptureWorld().CutGrass);
    }

    [Fact]
    public void StrikesAboveOrBelowTheGrassDoNotCutIt()
    {
        using var game = Create();
        var bounds = VegetationPlacement2D.GrassBounds(game.Level.TileMap, new(6, 19));
        game.Level.CutGrass(new(bounds.Min - new Vector2(0f, 100f), bounds.Max - new Vector2(0f, 100f)));
        game.Level.CutGrass(new(bounds.Min + new Vector2(0f, 100f), bounds.Max + new Vector2(0f, 100f)));
        Assert.Empty(game.Session.CaptureWorld().CutGrass);
    }

    private static EditableTileMap2D Map()
    {
        var map = new EditableTileMap2D(SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles, 32f, SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin, ["kenney-grassland"]);
        for (var x = 0; x < map.Width; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        return map;
    }

    private static SideScrollerSimulation2D Create() => SideScrollerSimulation2D.Create(
        new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), Map(), [],
            [new(1, WorldThingKind2D.PlayerSpawn, "Start", true, new(-368f, 40f))]));
}
