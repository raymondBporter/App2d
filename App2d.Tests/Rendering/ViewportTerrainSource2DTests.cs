using App2d.Core.Geometry;
using App2d.Gameplay.World;
using App2d.Rendering;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class ViewportTerrainSource2DTests
{
    [Theory]
    [InlineData(800, 600, 1.35f, 0f)]
    [InlineData(3840, 2160, 0.25f, 0f)]
    [InlineData(1600, 900, 0.5f, 0.7f)]
    public void CoversEveryVisibleChunkForZoomResizeAndRotation(int width, int height, float zoom, float rotation)
    {
        var map = CreateMap();
        using var source = new ViewportTerrainSource2D(map);
        var camera = new Camera2D { Position = map.WorldBounds.Center, Zoom = zoom, Rotation = rotation };
        camera.SetViewport(width, height);
        var terrain = source.Capture(camera.VisibleWorldBounds);

        for (var y = 0; y < map.ChunkRows; y++)
            for (var x = 0; x < map.ChunkColumns; x++)
            {
                var min = map.Origin + new Vector2(x, y) * map.ChunkSize * map.TileSize;
                var bounds = new Bounds2D(min, min + new Vector2(map.ChunkSize * map.TileSize));
                if (bounds.Intersects(camera.VisibleWorldBounds))
                    Assert.Contains(terrain, c => c.Chunk == new TileChunk2D(x, y));
            }
    }

    [Fact]
    public void ZoomingOutLoadsBeyondPhysicsRadiusAndZoomingInReleasesDistantChunks()
    {
        var map = CreateMap();
        using var source = new ViewportTerrainSource2D(map);
        var camera = new Camera2D { Position = map.WorldBounds.Center };
        camera.SetViewport(1600, 900);
        var original = source.Capture(camera.VisibleWorldBounds);
        Assert.Equal(original, source.Capture(camera.VisibleWorldBounds));
        camera.Zoom = 0.2f;
        var wide = source.Capture(camera.VisibleWorldBounds);
        var center = map.WorldToChunk(camera.Position);
        Assert.Contains(wide, c => Math.Abs(c.Chunk.X - center.X) > 2);
        Assert.True(wide.Length > original.Length);
        camera.Zoom = 1f;
        var narrow = source.Capture(camera.VisibleWorldBounds);
        Assert.Equal(original.Select(c => c.Chunk), narrow.Select(c => c.Chunk));
        Assert.All(original, c => Assert.Same(c, narrow.Single(n => n.Chunk == c.Chunk)));
    }

    [Fact]
    public void PaintingAnEdgeRefreshesBothChunkSnapshotsAndPreservesOldObservations()
    {
        var map = CreateMap();
        using var source = new ViewportTerrainSource2D(map);
        var before = source.Capture(map.WorldBounds);
        map.SetTileKind(31, 4, TileKind2D.Solid);
        map.SetTileKind(31, 5, TileKind2D.Solid);
        var after = source.Capture(map.WorldBounds);
        foreach (var chunk in new[] { new TileChunk2D(0, 0), new TileChunk2D(1, 0) })
        {
            var old = before.Single(c => c.Chunk == chunk);
            var updated = after.Single(c => c.Chunk == chunk);
            Assert.True(updated.Revision > old.Revision);
            Assert.Equal(TileKind2D.Empty, old.GetTileKind(31, 4));
            Assert.Equal(TileKind2D.Solid, updated.GetTileKind(31, 4));
        }
        Assert.Same(before.Single(c => c.Chunk == new TileChunk2D(2, 0)),
            after.Single(c => c.Chunk == new TileChunk2D(2, 0)));
        Assert.Equal(after, source.Capture(map.WorldBounds));
    }

    [Fact]
    public void PanningOutsideMapUnloadsTerrainAndReturningReloadsIt()
    {
        var map = CreateMap();
        using var source = new ViewportTerrainSource2D(map);
        var original = source.Capture(map.WorldBounds);
        Assert.Empty(source.Capture(new Bounds2D(new Vector2(-10000f), new Vector2(-9000f))));
        Assert.Equal(original.Length, source.Capture(map.WorldBounds).Length);
    }

    private static EditableTileMap2D CreateMap() => new(640, 96, 32f, 32, new Vector2(-512f, -640f));
}
