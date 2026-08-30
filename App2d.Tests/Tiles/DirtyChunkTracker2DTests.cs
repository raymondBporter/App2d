using App2d.Tiles;

namespace App2d.Tests.Tiles;

public sealed class DirtyChunkTracker2DTests
{
    [Fact]
    public void MarkingTheSameChunkTwiceTracksItOnce()
    {
        var tracker = new DirtyChunkTracker2D();

        tracker.Mark(new TileChunk2D(1, 1));
        tracker.Mark(new TileChunk2D(1, 1));

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void FlushRebuildsEachMarkedChunkOnceThenEmpties()
    {
        var tracker = new DirtyChunkTracker2D();
        tracker.Mark(new TileChunk2D(0, 0));
        tracker.Mark(new TileChunk2D(1, 0));
        tracker.Mark(new TileChunk2D(0, 0));

        var rebuilt = new List<TileChunk2D>();
        tracker.Flush(rebuilt.Add);

        Assert.Equal(2, rebuilt.Count);
        Assert.Contains(new TileChunk2D(0, 0), rebuilt);
        Assert.Contains(new TileChunk2D(1, 0), rebuilt);
        Assert.True(tracker.IsEmpty);
    }

    [Fact]
    public void FlushingTwiceDoesNothingTheSecondTime()
    {
        var tracker = new DirtyChunkTracker2D();
        tracker.Mark(new TileChunk2D(2, 1));
        tracker.Flush(_ => { });

        var rebuilt = new List<TileChunk2D>();
        tracker.Flush(rebuilt.Add);

        Assert.Empty(rebuilt);
    }

    [Fact]
    public void PaintingTilesWithOverlappingChunkNeighbourhoodsMarksExactlyTheUnion()
    {
        // 12x8 tiles at chunk size 4 -> a 3x2 chunk grid (6 chunks total).
        //
        // Tile (3,3) is a chunk corner: painted alone it dirties chunks
        // (0,0), (1,0), (0,1), (1,1) -- 4 chunks.
        // Tile (7,3) is also a chunk corner, one chunk column over: painted alone it
        // dirties (1,0), (2,0), (1,1), (2,1) -- 4 chunks.
        //
        // The two 4-chunk sets share (1,0) and (1,1), so neither tile alone produces
        // the map's full chunk count, and a broken (non-deduplicating) Mark would count
        // 8 raw events instead of the 6 distinct chunks in the union. A test that only
        // painted one tile, or two tiles with identical neighbourhoods, could pass even
        // if Mark stopped deduplicating -- this one cannot.
        var map = new EditableTileMap2D(12, 8, 32f, 4);
        var tracker = new DirtyChunkTracker2D();
        map.ChunkChanged += tracker.Mark;

        map.SetTileKind(3, 3, TileKind2D.Solid);
        map.SetTileKind(7, 3, TileKind2D.Solid);

        Assert.Equal(6, tracker.Count);
    }

    [Fact]
    public void FlushDuringRebuildDoesNotLoseChunksMarkedByTheRebuild()
    {
        var tracker = new DirtyChunkTracker2D();
        tracker.Mark(new TileChunk2D(0, 0));

        var rebuilt = new List<TileChunk2D>();
        tracker.Flush(chunk =>
        {
            rebuilt.Add(chunk);
            if (chunk == new TileChunk2D(0, 0))
                tracker.Mark(new TileChunk2D(5, 5));
        });

        Assert.Single(rebuilt);
        Assert.Equal(1, tracker.Count);
    }
}
