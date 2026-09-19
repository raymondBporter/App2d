using App2d.Core;
using App2d.Levels;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Gameplay.World.Presentation;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using System.Collections.Immutable;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class WorldPresentationTests
{
    [Fact]
    public void CameraTerrainSurvivesSimulationStreamingChangesAndUnloadsWhenOutOfView()
    {
        var map = CreateMap();
        map.SetTileKind(4, 4, TileKind2D.Solid);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new WorldPresentation2D(scene, textures);
        ImmutableArray<TerrainChunkState2D> visible =
            [TerrainChunkState2D.Capture(map, new TileChunk2D(0, 0), 1)];
        view.SetVisibleTerrain(visible);
        var visuals = scene.ToArray();
        Assert.NotEmpty(visuals);
        view.ApplyState(LevelContent2D.Empty, WorldState2D.Empty);
        view.ApplyState(LevelContent2D.Empty with { Revision = 2 }, WorldState2D.Empty);
        view.SetVisibleTerrain(visible);
        Assert.Equal(visuals, scene.ToArray());
        view.SetVisibleTerrain([]);
        Assert.Empty(scene);
    }

    [Fact]
    public void WorldViewsStayIndependentUntilNewStateArrivesAndRemoveReplacedPlatforms()
    {
        var map = CreateMap();
        map.SetTileKind(4, 4, TileKind2D.Solid);
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        using var level = CreateLevel(map, [Platform()]);
        level.CreateSimulation(physics.CollisionSystem, physics, new EntityIdAllocator2D(), 1, 2, 4);
        var content = level.CaptureContent();
        var original = level.CaptureWorld();
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new WorldPresentation2D(scene, textures);
        view.Update(content, original, 0f);
        var visuals = scene.ToArray();
        view.Update(content, original, 0f);
        Assert.Equal(visuals, scene.ToArray()); // No rebuilding unchanged chunks.
        var platformVisual = Assert.Single(scene, v => v.Transform.Position == Platform().Position);
        var platform = Assert.Single(level.MovingPlatforms);
        Assert.NotSame(platform.WorldObject, platformVisual);
        level.UpdateMovingPlatforms(0.1f);
        physics.Step(0.1f);
        Assert.Equal(Platform().Position, platformVisual.Transform.Position);
        view.Update(level.CaptureContent(), level.CaptureWorld(), 0f);
        Assert.Equal(platform.WorldObject.Transform.Position, platformVisual.Transform.Position);
        Assert.NotEqual(original.MovingPlatforms[0].Position, platformVisual.Transform.Position);
        level.ReloadMovingPlatforms([Platform() with { Size = new Vector2(100f, 12f) }]);
        Assert.NotEqual(platform.Id, Assert.Single(level.CaptureContent().MovingPlatforms).Id);
        view.Update(level.CaptureContent(), level.CaptureWorld(), 0f);
        Assert.DoesNotContain(platformVisual, scene);
        view.Update(LevelContent2D.Empty, WorldState2D.Empty, 0f);
        Assert.Empty(scene);
    }

    private static EditableTileMap2D CreateMap() => new(SideScrollerLevel2D.WorldWidthTiles,
        SideScrollerLevel2D.WorldHeightTiles, 32f, SideScrollerLevel2D.ChunkSizeTiles,
        SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
    private static SideScrollerLevel2D CreateLevel(EditableTileMap2D map, MovingPlatformSpec2D[]? platforms = null) =>
        new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, _ => 1, platforms);
    private static MovingPlatformSpec2D Platform() => new(41, "Lift", true,
        new Vector2(-200f, 100f), new Vector2(96f, 0f), new Vector2(80f, 14f), 48f, 0xFF25D2BEu);
}
