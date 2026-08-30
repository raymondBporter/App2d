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
    public void PaintingAcrossAChunkBorderCoalescesIntoTheAffectedChunks()
    {
        // 8x8 tiles at chunk size 4 -> a 2x2 chunk grid. Painting tile (3,3) is a
        // chunk corner, so EditableTileMap2D raises ChunkChanged for all four chunks.
        var map = new EditableTileMap2D(8, 8, 32f, 4);
        var tracker = new DirtyChunkTracker2D();
        map.ChunkChanged += tracker.Mark;

        map.SetTileKind(3, 3, TileKind2D.Solid);
        map.SetTileKind(4, 3, TileKind2D.Solid);

        // Both tiles sit on the same chunk seam, so the dirty set stays at four
        // chunks no matter how many events fired.
        Assert.Equal(4, tracker.Count);
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
