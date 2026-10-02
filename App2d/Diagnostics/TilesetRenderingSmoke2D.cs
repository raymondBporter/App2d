using App2d.Contracts.Simulation;
using App2d.Core;
using App2d.Core.Assets;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Physics;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Presentation.Persons;
using App2d.Presentation.World.Presentation;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Rendering.Vegetation;
using App2d.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

/// <summary>
/// Renders a ground + wall tileset pair through the real terrain renderer on a fixed sample
/// layout, so tileset art is judged as the game draws it rather than as a mock-up.
/// </summary>
internal static class TilesetRenderingSmoke2D
{
    // Top row first. D ground, S wall, G grippable wall, = one-way, ^ spikes, H ladder.
    private static readonly string[] Layout =
    [
        "......................",
        "......................",
        "......................",
        "......................",
        "...........SS.........",
        "......................",
        "....====H......SSSSSSS",
        "........H......GSSSSSS",
        "........H......GSSSSSS",
        "DDDDDDDDDD...DDSSSSSSS",
        "DDDDDDDDDD^^^DDSSSSSSS",
        "DDDDDDDDDDDDDDDSSSSSSS",
    ];
    private const int Left = 16;
    private const int Bottom = 16;

    /// <summary>Walks the camera along the authored level, slightly zoomed out to fit tall structures.</summary>
    public static void RunLevel(GraphicsDevice device, TextureCache2D textures, string directory)
    {
        Directory.CreateDirectory(directory);
        var map = LevelBootstrap2D.Load().TileMap;
        using var level = new SideScrollerLevel2D(TraversalMetricsLoader2D.Load(textures.ContentRoot), map);
        var collision = new CollisionSystem2D();
        level.CreateSimulation(collision, new PhysicsWorld2D(collision), new EntityIdAllocator2D(), 1u, 2u, 4u);
        var scene = new Scene2D();
        using var world = new WorldPresentation2D(scene, textures);
        var camera = new Camera2D { Zoom = 1f };
        using var renderer = new Renderer2D(camera, device);
        using var target = new RenderTarget2D(device, 1280, 720, false, SurfaceFormat.Color, DepthFormat.None);
        var step = 1280f / camera.Zoom;
        var index = 0;
        for (var x = map.Origin.X + step / 2f; x < map.WorldBounds.Max.X; x += step, index++)
        {
            camera.Position = new(x, map.Origin.Y + 18f * 32f);
            level.UpdateStreaming(camera.Position);
            world.Update(level.CaptureContent(), level.CaptureWorld(), 1f / 120f);
            device.SetRenderTarget(target);
            renderer.BeginFrame(1280, 720, default);
            renderer.Clear(new Color(170, 208, 226));
            world.DrawTrees(renderer, camera.VisibleWorldBounds);
            renderer.Draw(scene, int.MinValue, 0);
            world.DrawGrass(renderer, camera.VisibleWorldBounds, VegetationLayer2D.Back);
            renderer.Draw(scene, 1, int.MaxValue);
            world.DrawGrass(renderer, camera.VisibleWorldBounds, VegetationLayer2D.Front);
            renderer.EndFrame();
            device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(directory, $"level-{index:00}.png"));
            target.SaveAsPng(stream, 1280, 720);
        }
        Console.WriteLine($"Level strip written: {index} views in {Path.GetFullPath(directory)}");
    }

    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory, string groundId, string wallId)
    {
        Directory.CreateDirectory(directory);
        var map = new EditableTileMap2D(SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles, 32f, SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin, [groundId, wallId]);
        for (var row = 0; row < Layout.Length; row++)
            for (var column = 0; column < Layout[row].Length; column++)
            {
                var (kind, tileset) = Layout[row][column] switch
                {
                    'D' => (TileKind2D.Solid, 0),
                    'S' => (TileKind2D.Solid, 1),
                    'G' => (TileKind2D.Solid | TileKind2D.Grippable, 1),
                    '=' => (TileKind2D.OneWay, 0),
                    '^' => (TileKind2D.Spikes, 0),
                    'H' => (TileKind2D.Ladder, 0),
                    _ => (TileKind2D.Empty, 0)
                };
                map.SetTile(Left + column, Bottom + Layout.Length - 1 - row, new TileCell2D(kind, (byte)tileset));
            }

        var origin = SideScrollerLevel2D.WorldOrigin;
        var groundTop = origin.Y + (Bottom + 3) * 32f;
        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        var traversal = TraversalMetricsLoader2D.Load(textures.ContentRoot);
        using var game = SideScrollerSimulation2D.Create(new(traversal, map, [],
            [new(1, WorldThingKind2D.PlayerSpawn, "Start", true, new(origin.X + (Left + 2.4f) * 32f, groundTop + 8f))])
        { AuthoredCharacters = catalog });
        var scene = new Scene2D();
        using var world = new WorldPresentation2D(scene, textures);
        using var player = new AuthoredPersonPresentation2D(scene, PersonMoves.From(catalog), traversal);
        var center = new Vector2(origin.X + (Left + Layout[0].Length / 2f) * 32f, origin.Y + (Bottom + Layout.Length / 2f) * 32f);
        // Zoom 1.35 shows world pixels at the size the game camera gives them on a 1080p screen.
        var camera = new Camera2D { Zoom = 1.35f, Position = center };
        using var renderer = new Renderer2D(camera, device);
        using var target = new RenderTarget2D(device, 1280, 720, false, SurfaceFormat.Color, DepthFormat.None);
        for (var i = 0; i < 60; i++) Step();
        Save($"{groundId}+{wallId}.png");
        camera.Zoom = 2.7f;
        camera.Position = new(origin.X + (Left + 6f) * 32f, origin.Y + (Bottom + 3.5f) * 32f);
        Save($"{groundId}+{wallId}-closeup-left.png");
        camera.Position = new(origin.X + (Left + 15f) * 32f, origin.Y + (Bottom + 5.5f) * 32f);
        Save($"{groundId}+{wallId}-closeup-right.png");
        Console.WriteLine($"Tileset smoke written: {Path.GetFullPath(directory)}");

        void Step()
        {
            var tick = game.Session.Tick + 1;
            var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick, default));
            var state = frame.Players[0];
            world.Update(frame.Content, frame.World, 1f / 120f);
            player.Equip(state.Equipment);
            player.ApplyState(state.Person, tick, state.MoveX, false, state.IsMeleeAttackActive);
            player.Advance(1f / 120f);
        }

        void Save(string name)
        {
            device.SetRenderTarget(target);
            renderer.BeginFrame(1280, 720, default);
            renderer.Clear(new Color(170, 208, 226));
            world.DrawTrees(renderer, camera.VisibleWorldBounds);
            renderer.Draw(scene, int.MinValue, 0);
            world.DrawGrass(renderer, camera.VisibleWorldBounds, VegetationLayer2D.Back);
            renderer.Draw(scene, 1, int.MaxValue);
            world.DrawGrass(renderer, camera.VisibleWorldBounds, VegetationLayer2D.Front);
            renderer.EndFrame();
            device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(directory, name));
            target.SaveAsPng(stream, 1280, 720);
        }
    }
}
