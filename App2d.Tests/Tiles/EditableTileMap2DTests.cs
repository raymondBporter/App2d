using App2d.Tiles;
using System.Numerics;

namespace App2d.Tests.Tiles;

public sealed class EditableTileMap2DTests
{
    private static EditableTileMap2D Seed(
        int width,
        int height,
        float tileSize,
        int chunkSize,
        Func<int, int, TileKind2D> source)
    {
        var map = new EditableTileMap2D(width, height, tileSize, chunkSize);
        map.Fill(source);
        return map;
    }

    [Fact]
    public void GrippableFlagKeepsTileSolidAndReachesMergedCollision()
    {
        const TileKind2D grippableSolid = TileKind2D.Solid | TileKind2D.Grippable;
        var map = Seed(3, 1, 32f, 3, (x, _) => x < 2 ? grippableSolid : TileKind2D.Solid);

        Assert.True(map.IsSolid(0, 0));
        Assert.True(map.GetTileKind(0, 0).IsGrippable());

        var rectangles = map.BuildCollisionRectangles(new TileChunk2D(0, 0));
        Assert.Equal(2, rectangles.Count);
        var grippable = Assert.Single(rectangles, rectangle => rectangle.Kind.IsGrippable());
        Assert.Equal(grippableSolid, grippable.Kind);
        Assert.Equal(64f, grippable.Bounds.Size.X);
    }

    [Fact]
    public void ModifierFlagAloneDoesNotCreateCollision()
    {
        var map = Seed(1, 1, 32f, 1, (_, _) => TileKind2D.Grippable);

        Assert.False(map.IsSolid(0, 0));
        Assert.Empty(map.BuildCollisionRectangles(new TileChunk2D(0, 0)));
    }

    [Fact]
    public void SpikeTileIsHazardWithoutBecomingSolidCollision()
    {
        var map = Seed(1, 1, 32f, 1, (_, _) => TileKind2D.Spikes);

        Assert.True(map.GetTileKind(0, 0).IsSpikes());
        Assert.False(map.IsSolid(0, 0));
        Assert.Empty(map.BuildCollisionRectangles(new TileChunk2D(0, 0)));
    }

    [Fact]
    public void SetTileKindRoundTrips()
    {
        var map = new EditableTileMap2D(4, 4, 32f, 2);

        map.SetTileKind(2, 3, TileKind2D.OneWay);

        Assert.Equal(TileKind2D.OneWay, map.GetTileKind(2, 3));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(2, 2));
    }

    [Fact]
    public void OutOfBoundsReadsReturnEmptyInsteadOfThrowing()
    {
        var map = new EditableTileMap2D(4, 4, 32f, 2);
        map.Fill((_, _) => TileKind2D.Solid);

        Assert.Equal(TileKind2D.Empty, map.GetTileKind(-1, 0));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(0, -1));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(4, 0));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(0, 4));
        Assert.False(map.IsSolid(99, 99));
    }

    [Fact]
    public void SetTileKindOutsideMapThrows()
    {
        var map = new EditableTileMap2D(4, 4, 32f, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetTileKind(4, 0, TileKind2D.Solid));
    }

    [Fact]
    public void ChunkChangedFiresForTheOwningChunkOnlyWhenTheKindChanges()
    {
        var map = new EditableTileMap2D(8, 8, 32f, 4);
        var changed = new List<TileChunk2D>();
        map.ChunkChanged += chunk => changed.Add(chunk);

        map.SetTileKind(5, 6, TileKind2D.Solid);
        map.SetTileKind(5, 6, TileKind2D.Solid);

        Assert.Single(changed);
        Assert.Equal(new TileChunk2D(1, 1), changed[0]);
    }

    [Fact]
    public void SetTileKindOnAnInteriorTileRaisesOneChunkChangedEvent()
    {
        // Map is 3x3 chunks of size 4; tile (5,6) sits well inside chunk (1,1).
        var map = new EditableTileMap2D(12, 12, 32f, 4);
        var changed = new List<TileChunk2D>();
        map.ChunkChanged += chunk => changed.Add(chunk);

        map.SetTileKind(5, 6, TileKind2D.Solid);

        Assert.Single(changed);
        Assert.Equal(new TileChunk2D(1, 1), changed[0]);
    }

    [Fact]
    public void SetTileKindOnAChunkCornerRaisesFourChunkChangedEvents()
    {
        // Chunk size 4; tile (3,3) is the last tile of chunk (0,0) and its 3x3
        // neighbourhood reaches into chunks (1,0), (0,1) and (1,1) too.
        var map = new EditableTileMap2D(12, 12, 32f, 4);
        var changed = new List<TileChunk2D>();
        map.ChunkChanged += chunk => changed.Add(chunk);

        map.SetTileKind(3, 3, TileKind2D.Solid);

        Assert.Equal(4, changed.Count);
        Assert.Equal(
            [
                new TileChunk2D(0, 0),
                new TileChunk2D(0, 1),
                new TileChunk2D(1, 0),
                new TileChunk2D(1, 1)
            ],
            changed.OrderBy(c => c.X).ThenBy(c => c.Y));
    }

    [Fact]
    public void SetTileKindOnAChunkEdgeRaisesTwoChunkChangedEvents()
    {
        // Chunk size 4; tile (3,5) sits on the vertical boundary between chunks
        // (0,1) and (1,1) but is not on a horizontal boundary.
        var map = new EditableTileMap2D(12, 12, 32f, 4);
        var changed = new List<TileChunk2D>();
        map.ChunkChanged += chunk => changed.Add(chunk);

        map.SetTileKind(3, 5, TileKind2D.Solid);

        Assert.Equal(2, changed.Count);
        Assert.Equal(
            [new TileChunk2D(0, 1), new TileChunk2D(1, 1)],
            changed.OrderBy(c => c.X).ThenBy(c => c.Y));
    }

    [Fact]
    public void SetTileKindOnTheMapsOuterCornerRaisesOneClampedChunkChangedEvent()
    {
        var map = new EditableTileMap2D(12, 12, 32f, 4);
        var changed = new List<TileChunk2D>();
        map.ChunkChanged += chunk => changed.Add(chunk);

        map.SetTileKind(0, 0, TileKind2D.Solid);

        Assert.Single(changed);
        Assert.Equal(new TileChunk2D(0, 0), changed[0]);
    }

    [Fact]
    public void ClippedChunkExtentsCoverPartialEdgeChunks()
    {
        var map = new EditableTileMap2D(10, 6, 32f, 4);

        Assert.Equal(3, map.ChunkColumns);
        Assert.Equal(2, map.ChunkRows);
        Assert.Equal(4, map.ChunkWidth(0));
        Assert.Equal(2, map.ChunkWidth(2));
        Assert.Equal(2, map.ChunkHeight(1));
    }

    [Fact]
    public void GetChunkTilesReadsTheChunkInRowMajorOrder()
    {
        var map = new EditableTileMap2D(4, 4, 32f, 2);
        map.SetTileKind(2, 2, TileKind2D.Solid);
        map.SetTileKind(3, 2, TileKind2D.Spikes);

        var buffer = new TileKind2D[4];
        var tiles = map.GetChunkTiles(new TileChunk2D(1, 1), buffer);

        Assert.Equal(TileKind2D.Solid, tiles[0]);
        Assert.Equal(TileKind2D.Spikes, tiles[1]);
        Assert.Equal(TileKind2D.Empty, tiles[2]);
        Assert.Equal(TileKind2D.Empty, tiles[3]);
    }

    [Fact]
    public void WorldToChunkClampsOutsideTheMap()
    {
        var map = new EditableTileMap2D(8, 8, 10f, 4, new Vector2(-40f, -40f));

        Assert.Equal(new TileChunk2D(0, 0), map.WorldToChunk(new Vector2(-1000f, -1000f)));
        Assert.Equal(new TileChunk2D(1, 1), map.WorldToChunk(new Vector2(1000f, 1000f)));
    }
}
