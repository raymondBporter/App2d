using App2d.Gameplay.Player;
using App2d.Gameplay.World;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

/// <summary>
/// Nothing else reconciles a loaded map's geometry with the constants gameplay math
/// assumes (finding #1) -- a mismatched tile size, extent, or origin must fail loudly
/// at construction rather than silently misplacing colliders relative to spawn/goal/camera
/// math.
/// </summary>
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
        var level = new SideScrollerLevel2D(DefaultTraversal(), ValidMap(), _ => 1);
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
            () => new SideScrollerLevel2D(DefaultTraversal(), map, _ => 1));
    }

    [Fact]
    public void MismatchedWidthThrows()
    {
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles - 1,
            SideScrollerLevel2D.WorldHeightTiles,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);

        Assert.Throws<InvalidOperationException>(
            () => new SideScrollerLevel2D(DefaultTraversal(), map, _ => 1));
    }

    [Fact]
    public void MismatchedHeightThrows()
    {
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles - 1,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);

        Assert.Throws<InvalidOperationException>(
            () => new SideScrollerLevel2D(DefaultTraversal(), map, _ => 1));
    }

    [Fact]
    public void MismatchedOriginThrows()
    {
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin + new Vector2(1f, 0f));

        Assert.Throws<InvalidOperationException>(
            () => new SideScrollerLevel2D(DefaultTraversal(), map, _ => 1));
    }
}
