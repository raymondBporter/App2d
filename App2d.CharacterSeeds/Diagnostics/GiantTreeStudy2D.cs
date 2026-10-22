using App2d.Contracts.Persons;
using App2d.Contracts.Simulation;
using App2d.Core.Assets;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Vegetation;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Presentation.Persons;
using App2d.Presentation.World;
using App2d.Presentation.World.Presentation;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.CharacterSeeds.Diagnostics;

/// <summary>Exports the real grove and a real player standing halfway up a generated branch.</summary>
internal static class GiantTreeStudy2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory)
    {
        var loaded = LevelBootstrap2D.Load();
        if (loaded.GiantTrees.IsEmpty)
        {
            throw new InvalidOperationException("Place a tree in giant-trees.json first.");
        }

        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        var traversal = TraversalMetricsLoader2D.Load(textures.ContentRoot);
        var tree = loaded.GiantTrees[0];
        var branch = tree.Branches[tree.Branches.Length / 2];
        var edge = tree.Root.X + branch.Side * tree.Settings.TrunkWidth * 0.5f;
        var tip = branch.Side < 0 ? branch.Bounds.Left : branch.Bounds.Right;
        var spawn = new Vector2((edge + tip) * 0.5f, branch.Bounds.Top + traversal.PlayerColliderSize.Y * 0.5f + 12f);
        using var game = SideScrollerSimulation2D.Create(new(traversal, loaded.TileMap,
            [new(1, WorldThingKind2D.PlayerSpawn, "Tree study", true, spawn)])
        {
            AuthoredCharacters = catalog,
            GiantTrees = loaded.GiantTrees
        });
        var scene = new Scene2D();
        var background = new ParallaxBackground2D();
        SideScrollerBackground2D.AddTo(background);
        using var world = new WorldPresentation2D(scene, textures);
        using var player = new AuthoredPersonPresentation2D(scene, PersonMoves.From(catalog), traversal);
        using var terrain = new ViewportTerrainSource2D(loaded.TileMap);
        var camera = new Camera2D();
        using var renderer = new Renderer2D(camera, device);
        using var target = RenderCapture2D.CreateTarget(device, 1280, 720, DepthFormat.None, 0);
        for (var i = 0; i < 120; i++)
        {
            var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, new PersonCommand2D()));
            var state = frame.Players[0];
            world.Update(frame.Content, frame.World, 1f / 120f);
            player.Equip(state.Equipment);
            player.ApplyState(state.Person, frame.Tick, state.MoveX, false, state.IsMeleeAttackActive);
            player.Advance(1f / 120f);
        }
        if (!game.Player.IsGrounded || Math.Abs(game.Player.WorldObject.WorldBounds.Bottom - branch.Bounds.Top) > 1)
        {
            throw new InvalidOperationException("The player did not settle on the generated branch.");
        }

        var bounds = new Rect2D(new(loaded.GiantTrees.Min(t => t.Bounds.Left), loaded.GiantTrees.Min(t => t.Bounds.Bottom)),
            new(loaded.GiantTrees.Max(t => t.Bounds.Right), loaded.GiantTrees.Max(t => t.Bounds.Top)));
        Save("giant-trees-overview.png", bounds.Center,
            Math.Min(1180f / bounds.Width, 620f / bounds.Height), "GIANT TREES  /  43-58 TILES TALL  /  EVERY BRANCH IS A PLATFORM");
        Save("giant-tree-branch.png", game.Player.WorldObject.Transform.Position + new Vector2(0, 100), 1.35f,
            "REAL PLAYER, REAL BRANCH COLLISION  /  JUMP THROUGH FROM BELOW, LAND ON TOP");
        Console.WriteLine($"Giant tree study: {loaded.GiantTrees.Length} trees, {game.Level.GiantTreeBranches.Count} branch platforms; player grounded at Y={branch.Bounds.Top:0.0}.");

        void Save(string name, Vector2 focus, float zoom, string caption)
        {
            camera.Zoom = zoom;
            camera.Position = focus;
            target.SavePng(Path.Combine(directory, name), new Color(172, 204, 180), () =>
            {
                renderer.BeginFrame(1280, 720, default);
                world.SetVisibleTerrain(terrain.Capture(camera.VisibleWorldBounds));
                renderer.Draw(background);
                world.DrawTrees(renderer, camera.VisibleWorldBounds);
                renderer.Draw(scene, int.MinValue, 0);
                world.DrawGrass(renderer, camera.VisibleWorldBounds, VegetationLayer2D.Back);
                renderer.Draw(scene, 1, int.MaxValue);
                world.DrawGrass(renderer, camera.VisibleWorldBounds, VegetationLayer2D.Front);
                renderer.DrawScreenLabel(caption, new(24, 20));
                renderer.EndFrame();
            });
        }
    }
}
