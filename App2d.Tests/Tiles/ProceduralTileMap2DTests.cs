using App2d.Tiles;

namespace App2d.Tests.Tiles;

public sealed class ProceduralTileMap2DTests
{
    [Fact]
    public void GrippableFlagKeepsTileSolidAndReachesMergedCollision()
    {
        const TileKind2D grippableSolid = TileKind2D.Solid | TileKind2D.Grippable;
        var map = new ProceduralTileMap2D(
            width: 3,
            height: 1,
            tileSize: 32f,
            chunkSize: 3,
            (x, _) => x < 2 ? grippableSolid : TileKind2D.Solid);

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
        var map = new ProceduralTileMap2D(
            width: 1,
            height: 1,
            tileSize: 32f,
            chunkSize: 1,
            (_, _) => TileKind2D.Grippable);

        Assert.False(map.IsSolid(0, 0));
        Assert.Empty(map.BuildCollisionRectangles(new TileChunk2D(0, 0)));
    }

    [Fact]
    public void SpikeTileIsHazardWithoutBecomingSolidCollision()
    {
        var map = new ProceduralTileMap2D(
            width: 1,
            height: 1,
            tileSize: 32f,
            chunkSize: 1,
            (_, _) => TileKind2D.Spikes);

        Assert.True(map.GetTileKind(0, 0).IsSpikes());
        Assert.False(map.IsSolid(0, 0));
        Assert.Empty(map.BuildCollisionRectangles(new TileChunk2D(0, 0)));
    }
}
