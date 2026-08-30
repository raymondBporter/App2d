using App2d.Core.Geometry;
using App2d.Tiles;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class SpikeTerrain2DTests
{
    [Fact]
    public void GeneratedSpikesSitAboveSolidGroundAndDamageOnlyTheirTile()
    {
        var traversal = (TraversalMetrics2D)Activator.CreateInstance(
            typeof(TraversalMetrics2D),
            nonPublic: true)!;
        var generator = new JumpableWorldGenerator2D(
            SideScrollerLevel2D.WorldSeed,
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal);
        var tileMap = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal.TileSize,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
        tileMap.Fill(generator.GetTileKind);
        var groundHeights = TileGroundHeights2D.Derive(tileMap);
        var level = new SideScrollerLevel2D(
            traversal,
            tileMap,
            x => groundHeights[Math.Clamp(x, 0, groundHeights.Length - 1)]);
        var spikes = FindSpikes(level);
        Assert.Contains(spikes, spike => spike.X < level.TileMap.Width / 2);
        Assert.Contains(spikes, spike => spike.X >= level.TileMap.Width / 2);
        var spike = spikes[0];

        Assert.True(level.TileMap.GetTileKind(spike.X, spike.Y - 1).IsSolid());

        var tileMin = level.TileMap.Origin +
            new System.Numerics.Vector2(spike.X, spike.Y) * level.TileMap.TileSize;
        var inside = new Bounds2D(
            tileMin + new System.Numerics.Vector2(8f, 0f),
            tileMin + new System.Numerics.Vector2(24f, 20f));
        Assert.True(level.TryGetSpikeSource(inside, out var sourceX));
        Assert.Equal(tileMin.X + level.TileMap.TileSize / 2f, sourceX);

        var clearTile = new Bounds2D(
            tileMin + new System.Numerics.Vector2(0f, level.TileMap.TileSize),
            tileMin + new System.Numerics.Vector2(16f, level.TileMap.TileSize + 16f));
        Assert.False(level.TryGetSpikeSource(clearTile, out _));
    }

    private static List<(int X, int Y)> FindSpikes(SideScrollerLevel2D level)
    {
        var spikes = new List<(int X, int Y)>();
        for (var x = 0; x < level.TileMap.Width; x++)
        {
            for (var y = 0; y < level.TileMap.Height; y++)
            {
                if (level.TileMap.GetTileKind(x, y).IsSpikes())
                    spikes.Add((x, y));
            }
        }

        return spikes;
    }
}
