using App2d.Collision;
using App2d.Gameplay.Player;
using App2d.Gameplay.World;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using SkiaSharp;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class AuthoredMovingPlatform2DTests
{
    [Fact]
    public void LevelConstructsAndReloadsMovingPlatformsFromAuthoredSpecs()
    {
        var traversal = (TraversalMetrics2D)Activator.CreateInstance(
            typeof(TraversalMetrics2D),
            nonPublic: true)!;
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin,
            ["dark-cave"]);
        var authored = new MovingPlatformSpec2D(
            41,
            "Lift",
            true,
            new Vector2(128f, -64f),
            new Vector2(96f, 0f),
            new Vector2(80f, 14f),
            48f,
            new SKColor(37, 210, 190));
        var level = new SideScrollerLevel2D(traversal, map, _ => 1, [authored]);
        var scene = new Scene2D();
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision);
        using var textures = new TextureCache2D(TestAssetPath.Root);

        level.CreateEnvironment(scene, collision, physics, textures, 1, 2, 4);

        var platform = Assert.Single(level.MovingPlatforms);
        Assert.Equal(authored.Position, platform.Start);
        Assert.Equal(authored.Position + authored.Travel, platform.End);
        Assert.Equal(authored.Position, platform.WorldObject.Transform.Position);
        Assert.Contains(platform.Body, physics.Bodies);

        level.ReloadMovingPlatforms([]);
        Assert.Empty(level.MovingPlatforms);
        Assert.DoesNotContain(platform.Body, physics.Bodies);
        Assert.DoesNotContain(platform.WorldObject, scene);
    }
}
