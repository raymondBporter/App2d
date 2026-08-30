using App2d.Tiles;
using System.Numerics;

namespace App2d.Tests.Tiles;

public sealed class TileMeshingCharacterizationTests
{
    [Fact]
    public void TileMapMergesSolidBlocksGreedily()
    {
        var map = new TileMap2D(4, 3, 2f);
        map.Fill(0, 0, 4, 2);
        map.SetSolid(0, 2);

        var rectangles = map.CollisionRectangles;

        Assert.Equal(2, rectangles.Count);
        Assert.Contains(rectangles, r => r.Min == Vector2.Zero && r.Max == new Vector2(8f, 4f));
        Assert.Contains(rectangles, r => r.Min == new Vector2(0f, 4f) && r.Max == new Vector2(2f, 6f));
    }

    [Fact]
    public void ProceduralMapKeepsOneWayRowsOneTileTall()
    {
        // 3x3 chunk: bottom row solid, middle row one-way, top empty.
        var map = new ProceduralTileMap2D(3, 3, 1f, 3, (x, y) => y switch
        {
            0 => TileKind2D.Solid,
            1 => TileKind2D.OneWay,
            _ => TileKind2D.Empty
        });

        var rectangles = map.BuildCollisionRectangles(new TileChunk2D(0, 0));

        Assert.Equal(2, rectangles.Count);
        Assert.Contains(rectangles, r => r.Kind == TileKind2D.Solid && r.Bounds.Max.Y == 1f);
        Assert.Contains(rectangles, r => r.Kind == TileKind2D.OneWay && r.Bounds.Min.Y == 1f && r.Bounds.Max.Y == 2f);
    }
}
