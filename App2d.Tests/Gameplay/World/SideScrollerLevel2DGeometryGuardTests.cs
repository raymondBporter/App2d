using App2d.Contracts.Player;
using App2d.Core.Tiles;
using App2d.Gameplay.World;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class SideScrollerLevel2DGeometryGuardTests
{
    private static TraversalMetrics2D DefaultTraversal() =>
        (TraversalMetrics2D)Activator.CreateInstance(typeof(TraversalMetrics2D), nonPublic: true)!;

    private static EditableTileMap2D ValidMap() =>
        new(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);

    [Fact]
    public void MatchingGeometryConstructsSuccessfully()
    {
        var level = new SideScrollerLevel2D(DefaultTraversal(), ValidMap());
        Assert.NotNull(level);
    }

    [Fact]
    public void MismatchedTileSizeThrows()
    {
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            16f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);

        Assert.Throws<InvalidOperationException>(
            () => new SideScrollerLevel2D(DefaultTraversal(), map));
    }

    [Fact]
    public void AuthoredWidthIsAccepted()
    {
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles - 1,
            SideScrollerLevel2D.WorldHeightTiles,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);

        using var level = new SideScrollerLevel2D(DefaultTraversal(), map);
        Assert.Same(map, level.TileMap);
    }

    [Fact]
    public void AuthoredHeightIsAccepted()
    {
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles - 1,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);

        using var level = new SideScrollerLevel2D(DefaultTraversal(), map);
        Assert.Same(map, level.TileMap);
    }

    [Fact]
    public void AuthoredOriginIsAccepted()
    {
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin + new Vector2(1f, 0f));

        using var level = new SideScrollerLevel2D(DefaultTraversal(), map);
        Assert.Same(map, level.TileMap);
    }
}
