using App2d.Tiles;

namespace App2d.Tests.Tiles;

public sealed class TileEditSession2DTests
{
    private static EditableTileMap2D NewMap() => new(16, 16, 32f, 4);

    [Fact]
    public void PaintingInsideAStrokeChangesTheMap()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(2, 3, TileKind2D.Solid);
        var chunks = session.EndStroke();

        Assert.Equal(TileKind2D.Solid, map.GetTileKind(2, 3));
        Assert.Equal(new TileChunk2D(0, 0), Assert.Single(chunks));
    }

    [Fact]
    public void EndStrokeReportsOnlyChunksWhoseDataChanged()
    {
        // Tile (3,3) is a chunk corner, so the map raises ChunkChanged for 4 chunks —
        // but only chunk (0,0) actually owns the changed tile. Persistence must write
        // one chunk, not four; the extra events exist for visual invalidation only.
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(3, 3, TileKind2D.Solid);
        var chunks = session.EndStroke();

        Assert.Equal(new TileChunk2D(0, 0), Assert.Single(chunks));
    }

    [Fact]
    public void PaintingTheSameKindIsNotRecorded()
    {
        var map = NewMap();
        map.SetTileKind(1, 1, TileKind2D.Solid);
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(1, 1, TileKind2D.Solid);
        var chunks = session.EndStroke();

        Assert.Empty(chunks);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact]
    public void OutOfBoundsPaintingIsIgnored()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(-1, 0, TileKind2D.Solid);
        session.Paint(0, 99, TileKind2D.Solid);
        var chunks = session.EndStroke();

        Assert.Empty(chunks);
    }

    [Fact]
    public void UndoRestoresEveryTileTheStrokeChanged()
    {
        var map = NewMap();
        map.SetTileKind(5, 5, TileKind2D.OneWay);
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(5, 5, TileKind2D.Solid);
        session.Paint(6, 5, TileKind2D.Solid);
        session.EndStroke();

        session.Undo();

        Assert.Equal(TileKind2D.OneWay, map.GetTileKind(5, 5));
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(6, 5));
        Assert.Equal(0, session.UndoCount);
    }

    [Fact]
    public void UndoReturnsTheSameChunksThePaintingDirtied()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(3, 3, TileKind2D.Solid);
        session.Paint(4, 4, TileKind2D.Solid);
        var painted = session.EndStroke();

        var undone = session.Undo();

        Assert.Equal(painted.OrderBy(c => c.X).ThenBy(c => c.Y), undone.OrderBy(c => c.X).ThenBy(c => c.Y));
    }

    [Fact]
    public void UndoWithNothingRecordedIsANoOp()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        Assert.Empty(session.Undo());
    }

    [Fact]
    public void UndoUnwindsStrokesInReverseOrder()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(1, 1, TileKind2D.Solid);
        session.EndStroke();

        session.BeginStroke();
        session.Paint(1, 1, TileKind2D.Spikes);
        session.EndStroke();

        session.Undo();
        Assert.Equal(TileKind2D.Solid, map.GetTileKind(1, 1));

        session.Undo();
        Assert.Equal(TileKind2D.Empty, map.GetTileKind(1, 1));
    }

    [Fact]
    public void PaintLineFillsEveryTileBetweenTheEndpoints()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.PaintLine(0, 0, 4, 0, TileKind2D.Solid);
        session.EndStroke();

        for (var x = 0; x <= 4; x++)
            Assert.Equal(TileKind2D.Solid, map.GetTileKind(x, 0));
    }

    [Fact]
    public void PaintLineHandlesDiagonalsWithoutGaps()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.PaintLine(0, 0, 3, 3, TileKind2D.Solid);
        session.EndStroke();

        Assert.Equal(TileKind2D.Solid, map.GetTileKind(0, 0));
        Assert.Equal(TileKind2D.Solid, map.GetTileKind(3, 3));
        // Every step is orthogonally or diagonally adjacent: no skipped tiles.
        Assert.Equal(TileKind2D.Solid, map.GetTileKind(1, 1));
        Assert.Equal(TileKind2D.Solid, map.GetTileKind(2, 2));
    }

    [Fact]
    public void PaintOutsideAStrokeThrows()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);

        Assert.Throws<InvalidOperationException>(() => session.Paint(0, 0, TileKind2D.Solid));
    }

    [Fact]
    public void UndoRestoresThePreStrokeValueWhenOneStrokePaintsATileTwice()
    {
        var map = NewMap();
        map.SetTileKind(2, 2, TileKind2D.OneWay);
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(2, 2, TileKind2D.Solid);
        session.Paint(2, 2, TileKind2D.Empty);
        session.EndStroke();

        session.Undo();

        // Not TileKind2D.Solid: reverse replay must reach the value from before the
        // stroke began, not the intermediate one.
        Assert.Equal(TileKind2D.OneWay, map.GetTileKind(2, 2));
    }

    [Fact]
    public void UndoDuringAStrokeThrows()
    {
        var map = NewMap();
        var session = new TileEditSession2D(map);
        session.BeginStroke();

        Assert.Throws<InvalidOperationException>(() => session.Undo());
    }

    [Fact]
    public void UndoRestoresBothTileTypeAndTileset()
    {
        var map = new EditableTileMap2D(8, 8, 32f, 4, tilesetIds: ["cave", "moss"]);
        map.SetTile(2, 2, new TileCell2D(TileKind2D.OneWay, 0));
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.Paint(2, 2, new TileCell2D(TileKind2D.Solid, 1));
        session.EndStroke();
        session.Undo();

        Assert.Equal(new TileCell2D(TileKind2D.OneWay, 0), map.GetTile(2, 2));
    }

    [Fact]
    public void FloodFillUsesFourWayConnectivityAndStopsAtDifferentCells()
    {
        var map = new EditableTileMap2D(5, 5, 32f, 5, tilesetIds: ["cave", "moss"]);
        map.SetTile(1, 1, new TileCell2D(TileKind2D.Solid, 0));
        map.SetTile(2, 1, new TileCell2D(TileKind2D.Solid, 0));
        map.SetTile(1, 2, new TileCell2D(TileKind2D.Solid, 0));
        map.SetTile(3, 3, new TileCell2D(TileKind2D.Solid, 0));
        map.SetTile(2, 2, new TileCell2D(TileKind2D.Solid, 1));
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.FloodFill(1, 1, new TileCell2D(TileKind2D.OneWay, 1));
        session.EndStroke();

        Assert.Equal(new TileCell2D(TileKind2D.OneWay, 1), map.GetTile(1, 1));
        Assert.Equal(new TileCell2D(TileKind2D.OneWay, 1), map.GetTile(2, 1));
        Assert.Equal(new TileCell2D(TileKind2D.OneWay, 1), map.GetTile(1, 2));
        Assert.Equal(new TileCell2D(TileKind2D.Solid, 1), map.GetTile(2, 2));
        Assert.Equal(new TileCell2D(TileKind2D.Solid, 0), map.GetTile(3, 3));
    }

    [Fact]
    public void FloodFillTreatsAllEmptyTilesAsOneRegionRegardlessOfRetainedTileset()
    {
        var map = new EditableTileMap2D(3, 1, 32f, 3, tilesetIds: ["cave", "moss"]);
        map.SetTile(1, 0, new TileCell2D(TileKind2D.Empty, 1));
        var session = new TileEditSession2D(map);

        session.BeginStroke();
        session.FloodFill(0, 0, new TileCell2D(TileKind2D.Solid, 0));
        session.EndStroke();

        for (var x = 0; x < 3; x++)
            Assert.Equal(TileKind2D.Solid, map.GetTileKind(x, 0));
    }
}
