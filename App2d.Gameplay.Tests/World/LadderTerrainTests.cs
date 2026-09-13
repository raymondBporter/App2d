using App2d.Collision;
using App2d.Gameplay.Assets;
using App2d.Gameplay.Player;
using App2d.Gameplay.World;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class LadderTerrainTests
{
    [Theory]
    [InlineData("kenney-grassland")]
    [InlineData("dark-cave")]
    public void LadderArtUpdatesAcrossChunkBoundaryAndNeverCreatesSolids(string tilesetId)
    {
        var map = new EditableTileMap2D(SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles, 32f, SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin, [tilesetId]);
        map.SetTileKind(4, 31, TileKind2D.Ladder);
        map.SetTileKind(4, 32, TileKind2D.Ladder);
        var metrics = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var level = new SideScrollerLevel2D(metrics, map, _ => 1);
        var scene = new Scene2D();
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        level.CreateEnvironment(scene, collision, physics, textures, 1u, 2u, 4u);
        level.UpdateStreaming(map.Origin + new Vector2(4f, 31f) * 32f);

        var top = textures.Load(LadderAssets2D.ResolvePath(textures, tilesetId, true));
        var middle = textures.Load(LadderAssets2D.ResolvePath(textures, tilesetId, false));
        Assert.Equal(2, scene.Count());
        Assert.Empty(physics.Bodies);
        Assert.Same(middle, ShaderAt(31).Texture);
        Assert.Same(top, ShaderAt(32).Texture);

        map.SetTileKind(4, 32, TileKind2D.Empty);
        level.FlushDirtyChunks();
        Assert.Single(scene);
        Assert.Same(top, ShaderAt(31).Texture);

        level.UpdateStreaming(map.WorldBounds.Max);
        Assert.Empty(scene);
        level.UpdateStreaming(map.Origin + new Vector2(4f, 31f) * 32f);
        Assert.Single(scene);
        Assert.Same(top, ShaderAt(31).Texture);
        Assert.Empty(physics.Bodies);

        SpriteShader2D ShaderAt(int y) => Assert.IsType<SpriteShader2D>(
            Assert.Single(scene, visual => visual.Transform.Position ==
                map.Origin + new Vector2(4.5f, y + 0.5f) * 32f).Shader);
    }
}
