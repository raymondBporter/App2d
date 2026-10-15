using App2d.Contracts.World;
using App2d.Core.Tiles;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class WorldTileMapTests
{
    [Fact]
    public void AdjacentMapsShareTerrainHalosCollisionAndCatalogs()
    {
        var left = new EditableTileMap2D(3, 4, 32, 2, new(-96, 0), ["grass"]);
        var right = new EditableTileMap2D(3, 4, 32, 2, Vector2.Zero, ["stone"]);
        left.SetTileKind(2, 0, TileKind2D.Solid);
        right.SetTileKind(0, 0, TileKind2D.Solid);
        using var world = new WorldTileMap2D([left, right]);
        Assert.Equal(6, world.Width);
        Assert.Equal(new Vector2(-96, 0), world.Origin);
        Assert.Equal((byte)0, world.GetTilesetIndex(2, 0));
        Assert.Equal((byte)1, world.GetTilesetIndex(3, 0));
        var snapshot = TerrainChunkState2D.Capture(world, new(1, 0), 1);
        Assert.True(snapshot.IsSolid(2, 0));
        Assert.True(snapshot.IsSolid(3, 0));
        var collision = Assert.Single(snapshot.Collisions);
        Assert.Equal(new Vector2(-32, 0), collision.Bounds.Min);
        Assert.Equal(new Vector2(32, 32), collision.Bounds.Max);
        var dirty = new HashSet<TileChunk2D>();
        world.ChunkChanged += c => dirty.Add(c);
        right.SetTileKind(0, 0, TileKind2D.Empty);
        Assert.Contains(new TileChunk2D(1, 0), dirty);
        Assert.Contains(new TileChunk2D(2, 0), dirty);
        Assert.False(world.IsSolid(3, 0));
        Assert.True(world.IsSolid(2, 0));
    }

    [Fact]
    public void SparseGapsAreEmptyAndDisposalUnsubscribes()
    {
        var left = new EditableTileMap2D(2, 2, 32, 2);
        var right = new EditableTileMap2D(2, 2, 32, 2, new(3200, 0));
        var world = new WorldTileMap2D([left, right]);
        Assert.Equal(102, world.Width);
        Assert.False(world.IsSolid(50, 0));
        Assert.Empty(world.BuildCollisionRectangles(new(25, 0)));
        var changes = 0;
        world.ChunkChanged += _ => changes++;
        world.Dispose();
        left.SetTileKind(1, 0, TileKind2D.Solid);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void OverlapAndMisalignedMapsAreRejected()
    {
        var first = new EditableTileMap2D(4, 4, 32, 2);
        Assert.Throws<ArgumentException>(() => new WorldTileMap2D([first, new(4, 4, 32, 2, new(64, 0))]));
        Assert.Throws<ArgumentException>(() => new WorldTileMap2D([first, new(4, 4, 32, 2, new(129, 0))]));
        Assert.Throws<ArgumentException>(() => new WorldTileMap2D([first, new(4, 4, 16, 2, new(128, 0))]));
    }
}
