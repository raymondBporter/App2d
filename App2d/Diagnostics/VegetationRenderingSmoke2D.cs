using App2d.Core.Characters.Authored;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Gameplay.World.Presentation;
using App2d.Levels;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

/// <summary>Exercises actual authored sword damage and captures the resulting foliage.</summary>
internal static class VegetationRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory)
    {
        var map = new EditableTileMap2D(SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles, 32f, SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin, ["kenney-grassland"]);
        for (var x = 0; x < map.Width; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        var catalog = AuthoredCatalog.Load(Path.Combine(AssetPaths.Characters, "authored"));
        var traversal = TraversalMetricsLoader2D.Load(textures.ContentRoot);
        using var game = SideScrollerSimulation2D.Create(new(traversal, map, [],
            [new(1, WorldThingKind2D.PlayerSpawn, "Start", true, new(-368f, 40f))])
            { AuthoredCharacters = catalog });
        var scene = new Scene2D();
        using var world = new WorldPresentation2D(scene, textures);
        using var player = new AuthoredPersonPresentation2D(scene, PersonMoves.From(catalog), traversal);
        var camera = new Camera2D { Zoom = 2.2f, Position = new(-260f, 98f) };
        using var renderer = new Renderer2D(camera, device);
        using var target = new RenderTarget2D(device, 1280, 720, false, SurfaceFormat.Color, DepthFormat.None);
        for (var i = 0; i < 90; i++) Step(default);
        Save("vegetation-before.png");
        var cutFrame = -1;
        for (var i = 0; i < 180; i++)
        {
            Step(new() { PrimaryHeld = i == 0 });
            if (cutFrame < 0 && game.Session.CaptureWorld().CutGrass.Count > 0)
            {
                cutFrame = i;
                Save("vegetation-sword-cut.png");
            }
            if (cutFrame >= 0 && i - cutFrame is 18 or 42 or 84 or 132)
                Save($"vegetation-flight-{i - cutFrame:000}.png");
        }
        if (cutFrame < 0) throw new InvalidOperationException("The authored sword never cut the grass.");
        Save("vegetation-after.png");

        void Step(PersonCommand2D command)
        {
            var tick = game.Session.Tick + 1;
            var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick, command));
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
            renderer.Clear(new Color(103, 196, 235));
            world.DrawTrees(renderer, camera.VisibleWorldBounds);
            renderer.Draw(scene);
            world.DrawGrass(renderer, camera.VisibleWorldBounds);
            renderer.EndFrame();
            device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(directory, name));
            target.SaveAsPng(stream, 1280, 720);
        }
    }
}
