using App2d.Contracts.World;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Tiles;
using App2d.Gameplay.World;
using App2d.Presentation.World.Presentation;
using App2d.Rendering;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class ViewportTerrainSource2DTests
{
    [Fact]
    public void PartialEdgeChunksDoNotExtendTheVisibleMapFootprint()
    {
        var map = new EditableTileMap2D(10, 6, 10f, 4, new(-30, -20));
        using var source = new ViewportTerrainSource2D(map);
        var all = ReadTerrain(source, map.WorldBounds);
        Assert.Equal(6, all.Count);
        Assert.Contains(all, c => c.Chunk == new TileChunk2D(2, 1));
        // The final chunk extends to X=90, but terrain ends at X=70 plus one tile of artwork padding.
        Assert.True(source.QueryTerrain(Rect2f.Builder.Create(new(81, 0), new(89, 10))).IsEmpty);
        Assert.Contains(ReadTerrain(source, Rect2f.Builder.Create(new(69, 0), new(70, 10))), c => c.Chunk.X == 2);
    }

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
        var terrain = ReadTerrain(source, camera.VisibleWorldBounds);

        for (var y = 0; y < map.ChunkRows; y++)
        {
            for (var x = 0; x < map.ChunkColumns; x++)
            {
                var min = map.Origin + new Vector2f(x, y) * map.ChunkSize * map.TileSize;
                var bounds = Rect2f.Builder.Create(min, min + new Vector2f(map.ChunkSize * map.TileSize));
                if (bounds.Intersects(camera.VisibleWorldBounds))
                {
                    Assert.Contains(terrain, c => c.Chunk == new TileChunk2D(x, y));
                }
            }
        }
    }

    [Fact]
    public void ZoomingOutSelectsDistantChunksAndZoomingInKeepsTheirSnapshotsCached()
    {
        var map = CreateMap();
        using var source = new ViewportTerrainSource2D(map);
        var camera = new Camera2D { Position = map.WorldBounds.Center };
        camera.SetViewport(1600, 900);
        var original = ReadTerrain(source, camera.VisibleWorldBounds);
        Assert.Equal(original, ReadTerrain(source, camera.VisibleWorldBounds));
        camera.Zoom = 0.2f;
        var wide = ReadTerrain(source, camera.VisibleWorldBounds);
        var center = map.WorldToChunk(camera.Position);
        Assert.Contains(wide, c => Math.Abs(c.Chunk.X - center.X) > 2);
        Assert.True(wide.Count > original.Count);
        camera.Zoom = 1f;
        var narrow = ReadTerrain(source, camera.VisibleWorldBounds);
        Assert.Equal(original.Select(c => c.Chunk), narrow.Select(c => c.Chunk));
        Assert.All(original, c => Assert.Same(c, narrow.Single(n => n.Chunk == c.Chunk)));
        camera.Zoom = 0.2f;
        Assert.Equal(wide, ReadTerrain(source, camera.VisibleWorldBounds));
    }

    [Fact]
    public void PaintingAnEdgeRefreshesBothChunkSnapshotsAndPreservesOldObservations()
    {
        var map = CreateMap();
        using var source = new ViewportTerrainSource2D(map);
        var before = ReadTerrain(source, map.WorldBounds);
        map.SetTileKind(31, 4, TileKind2D.Solid);
        map.SetTileKind(31, 5, TileKind2D.Solid);
        var after = ReadTerrain(source, map.WorldBounds);
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
        Assert.Equal(after, ReadTerrain(source, map.WorldBounds));
    }

    [Fact]
    public void PanningOutsideMapSelectsNothingAndReturningReusesSnapshots()
    {
        var map = CreateMap();
        using var source = new ViewportTerrainSource2D(map);
        var original = ReadTerrain(source, map.WorldBounds);
        Assert.True(source.QueryTerrain(Rect2f.Builder.Create(new Vector2f(-10000f), new Vector2f(-9000f))).IsEmpty);
        Assert.Equal(original, ReadTerrain(source, map.WorldBounds));
    }

    [Fact]
    public void LocalTerrainVisualsSurvivePanningAndSimulationStreamingUntilEdited()
    {
        var map = new EditableTileMap2D(24, 32, 32f, 8, Vector2f.Zero, ["dark-cave"]);
        map.SetTileKind(4, 4, TileKind2D.Solid);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var terrain = new ViewportTerrainSource2D(map);
        using var view = new WorldPresentation2D(scene, textures, terrain);
        var chunk = new TileChunk2D(0, 0);
        var snapshot = terrain.GetChunk(chunk);
        var visuals = view.GetTerrainVisuals(snapshot);
        Assert.NotEmpty(visuals);
        Assert.Empty(scene);

        view.ApplyState(LevelContent2D.Empty, WorldState2D.Empty);
        view.ApplyState(LevelContent2D.Empty with { Revision = 2 }, WorldState2D.Empty);
        var outside = Rect2f.Builder.Create(map.WorldBounds.Max + new Vector2f(128), map.WorldBounds.Max + new Vector2f(256));
        Assert.True(terrain.QueryTerrain(outside).IsEmpty);
        Assert.Same(snapshot, terrain.GetChunk(chunk));
        Assert.Same(visuals, view.GetTerrainVisuals(snapshot));

        map.SetTileKind(4, 4, TileKind2D.Empty);
        var updated = terrain.GetChunk(chunk);
        Assert.True(updated.Revision > snapshot.Revision);
        Assert.Empty(view.GetTerrainVisuals(updated));
        Assert.Empty(scene);
    }

    [Fact]
    public void TreeCanopyQueriesItsOffscreenRootAndAnEditInAnotherChunkRechecksClearance()
    {
        var map = new EditableTileMap2D(24, 32, 32f, 8, Vector2f.Zero, ["dark-cave"]);
        for (var x = 3; x <= 5; x++)
            map.SetTileKind(x, 5, TileKind2D.Solid);

        using var terrain = new ViewportTerrainSource2D(map);
        using var vegetation = new VegetationPresentation2D(terrain);
        var root = new TileChunk2D(0, 0);
        var visible = Rect2f.Builder.Create(new(128, 320), new(160, 352));
        Assert.False(terrain.QueryTerrain(visible).Contains(new(root.X, root.Y)));
        Assert.True(vegetation.QueryTreeChunks(visible).Contains(new(root.X, root.Y)));

        var trees = vegetation.GetTrees(root);
        Assert.True(Assert.Single(trees).Bounds.Intersects(visible));
        Assert.Same(trees, vegetation.GetTrees(root));
        var unaffected = new TileChunk2D(2, 0);
        var unaffectedTrees = vegetation.GetTrees(unaffected);

        map.SetTileKind(4, 12, TileKind2D.Solid);
        Assert.Empty(vegetation.GetTrees(root));
        Assert.Same(unaffectedTrees, vegetation.GetTrees(unaffected));

        map.SetTileKind(4, 12, TileKind2D.Empty);
        Assert.Equal(Assert.Single(trees).Bounds, Assert.Single(vegetation.GetTrees(root)).Bounds);
    }

    [Fact]
    public void WorldDrawRendersOffscreenCanopyAndQueriesTerrainOutsideTheScene()
    {
        var map = new EditableTileMap2D(24, 32, 32f, 8, Vector2f.Zero, ["dark-cave"]);
        for (var x = 3; x <= 5; x++)
            map.SetTileKind(x, 5, TileKind2D.Solid);

        using var graphics = new GraphicsTestContext();
        var camera = new Camera2D { Position = new(144, 335), Zoom = 2f };
        using var renderer = new Renderer2D(camera, graphics.Device);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        using var terrain = new ViewportTerrainSource2D(map);
        var scene = new Scene2D();
        using var world = new WorldPresentation2D(scene, textures, terrain);

        var clear = Color4b.Magenta;
        Assert.Contains(Render(), color => color != clear);
        Assert.False(terrain.QueryTerrain(renderer.View.VisibleWorldBounds).Contains(new(0, 0)));
        map.SetTileKind(4, 14, TileKind2D.Solid);
        Assert.All(Render(), color => Assert.Equal(clear, color));

        camera.Position = new(144, 176);
        // Rows below the top of the tile contain terrain artwork, below any grass blades.
        Assert.Contains(Render().Skip(32 * 128), color => color != clear);
        Assert.Empty(scene);

        Color4b[] Render()
        {
            renderer.BeginFrame(128, 128, default);
            renderer.Clear(clear);
            world.Draw(renderer);
            renderer.EndFrame();
            return graphics.ReadPixels();
        }
    }

    private static List<TerrainChunkState2D> ReadTerrain(ViewportTerrainSource2D source, Rect2f bounds)
    {
        var terrain = new List<TerrainChunkState2D>();
        foreach (var cell in source.QueryTerrain(bounds))
            terrain.Add(source.GetChunk(new(cell.X, cell.Y)));
        return terrain;
    }

    private static EditableTileMap2D CreateMap() => new(640, 96, 32f, 32, new Vector2f(-512f, -640f));
}
