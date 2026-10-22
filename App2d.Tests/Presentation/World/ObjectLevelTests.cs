using App2d.CharacterSeeds.Diagnostics;
using App2d.Core;
using App2d.Core.Collision;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Tiles;
using App2d.Gameplay.World;
using App2d.Presentation.World.Presentation;

namespace App2d.Tests.Presentation.World;

public sealed class ObjectLevelTests
{
    [Fact]
    public void LevelReloadsObjectsAndPresentationDropsTheirVisuals()
    {
        var asset = ObjectAsset2D.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json"));
        using var level = new SideScrollerLevel2D(TraversalMetricsLoader2D.Load(TestAssetPath.Root), CreateMap(), objects: [new("one", asset, new(128f, -64f))]);
        var scene = new Scene2D();
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        level.CreateSimulation(collision, physics, new EntityIdAllocator2D(), 1, 2, 4);
        using var presentation = new WorldPresentation2D(scene, textures);
        presentation.Update(level.CaptureContent(), level.CaptureWorld(), 0f);
        var visual = Assert.Single(scene);
        var first = Assert.Single(level.Objects);
        var revision = level.CaptureContent().Revision;
        level.ReloadObjects([new("two", asset, new(0f, 0f))]);
        Assert.DoesNotContain(first.Body!, physics.Bodies);
        Assert.NotEqual(first.Id, Assert.Single(level.Objects).Id);
        Assert.NotEqual(revision, level.CaptureContent().Revision);
        presentation.Update(level.CaptureContent(), level.CaptureWorld(), 0f);
        Assert.DoesNotContain(visual, scene);
        level.ReloadObjects([]);
        Assert.Empty(level.Objects);
    }

    private static EditableTileMap2D CreateMap() => new(SideScrollerLevel2D.WorldWidthTiles, SideScrollerLevel2D.WorldHeightTiles, 32f, SideScrollerLevel2D.ChunkSizeTiles, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
}
