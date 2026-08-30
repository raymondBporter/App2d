using App2d.Levels;
using App2d.Tiles;

namespace App2d.Tests.Levels;

public sealed class TileRunCodec2DTests
{
    private static void AssertRoundTrips(TileKind2D[] tiles)
    {
        var encoded = TileRunCodec2D.Encode(tiles);
        var decoded = new TileKind2D[tiles.Length];
        TileRunCodec2D.Decode(encoded, decoded);
        Assert.Equal(tiles, decoded);
    }

    [Fact]
    public void EmptyChunkEncodesToFivePairs()
    {
        var tiles = new TileKind2D[1024];

        var encoded = TileRunCodec2D.Encode(tiles);

        Assert.Equal(10, encoded.Length);
        AssertRoundTrips(tiles);
    }

    [Fact]
    public void SingleKindChunkRoundTrips()
    {
        var tiles = new TileKind2D[1024];
        Array.Fill(tiles, TileKind2D.Solid);

        AssertRoundTrips(tiles);
    }

    [Fact]
    public void AlternatingWorstCaseRoundTripsAtTwoBytesPerTile()
    {
        var tiles = new TileKind2D[1024];
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = i % 2 == 0 ? TileKind2D.Solid : TileKind2D.Empty;

        var encoded = TileRunCodec2D.Encode(tiles);

        Assert.Equal(2048, encoded.Length);
        AssertRoundTrips(tiles);
    }

    [Fact]
    public void RunsLongerThanTwoHundredFiftyFiveSplitAcrossPairs()
    {
        var tiles = new TileKind2D[300];
        Array.Fill(tiles, TileKind2D.OneWay);

        var encoded = TileRunCodec2D.Encode(tiles);

        Assert.Equal(4, encoded.Length);
        Assert.Equal(255, encoded[1]);
        Assert.Equal(45, encoded[3]);
        AssertRoundTrips(tiles);
    }

    [Fact]
    public void CombinedFlagKindsSurviveRoundTrip()
    {
        var tiles = new TileKind2D[8];
        Array.Fill(tiles, TileKind2D.Solid | TileKind2D.Grippable);
        tiles[3] = TileKind2D.Spikes;

        AssertRoundTrips(tiles);
    }

    [Fact]
    public void ClippedEdgeChunkExtentRoundTrips()
    {
        // A 2x6 edge chunk, not a full 32x32 one.
        var tiles = new TileKind2D[12];
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = i < 4 ? TileKind2D.Solid : TileKind2D.Empty;

        AssertRoundTrips(tiles);
    }

    [Fact]
    public void DecodeIntoWrongSizedDestinationThrows()
    {
        var encoded = TileRunCodec2D.Encode(new TileKind2D[16]);
        var tooSmall = new TileKind2D[8];

        Assert.Throws<ArgumentOutOfRangeException>(() => TileRunCodec2D.Decode(encoded, tooSmall));
    }

    [Fact]
    public void DecodeRejectsOddLengthPayload()
    {
        var tiles = new TileKind2D[4];

        Assert.Throws<ArgumentException>(() => TileRunCodec2D.Decode([1, 2, 3], tiles));
    }

    [Fact]
    public void EncodeRejectsEmptyInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TileRunCodec2D.Encode([]));
    }
}
