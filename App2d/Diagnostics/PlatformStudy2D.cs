using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Game.Presentation.Audio;
using App2d.Game.Presentation.World.Presentation;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

/// <summary>
/// Real simulation and renderer: the player and an enemy standing on moving platforms, exported as frames. Planted feet
/// should ride the platform; a still platform is the reference for how an enemy stands.
/// </summary>
internal static class PlatformStudy2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory)
    {
        var authored = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        var traversal = TraversalMetricsLoader2D.Load(textures.ContentRoot);
        var camera = new Camera2D { Zoom = 2.2f };
        using var renderer = new Renderer2D(camera, device);
        using var target = new RenderTarget2D(device, 400, 300, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var scenarios = new (string Name, Vector2 Focus, MovingPlatformSpec2D Platform, bool Enemy)[]
        {
            ("player-sinking", new(-300, 70), new(10, null, true, new(-300, 90), new(0, -80), new(120, 12), 70, 0xFF25D2BE), false),
            ("player-sideways", new(-240, 70), new(10, null, true, new(-300, 60), new(140, 0), new(120, 12), 90, 0xFF25D2BE), false),
            ("enemy-still", new(300, 70), new(10, null, true, new(300, 40), new(0, -1), new(160, 12), .001f, 0xFF25D2BE), true),
            ("enemy-sinking", new(300, 70), new(10, null, true, new(300, 90), new(0, -80), new(160, 12), 70, 0xFF25D2BE), true),
        };
        foreach (var (name, focus, spec, withEnemy) in scenarios)
        {
            var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
            for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
            var things = new List<WorldThingSpec2D> { new(1, WorldThingKind2D.PlayerSpawn, null, true, withEnemy ? new(-600, 40) : spec.Position + new Vector2(0, 90)) };
            if (withEnemy) things.Add(new(2, WorldThingKind2D.Shieldback, null, true, spec.Position + new Vector2(0, 110)));
            using var game = SideScrollerSimulation2D.Create(new(traversal, map, [spec], things) { AuthoredCharacters = authored, PlayerMaximumHealth = 30 });
            var scene = new Scene2D();
            using var player = new AuthoredPersonPresentation2D(scene, PersonMoves.From(authored), traversal);
            using var enemies = new EnemyPresentation2D(scene, textures, traversal, new Silent());
            var ground = new WorldObject2D(AxisAlignedRectangle2D.FromSize(new(5000, 2)), new SolidColorShader(new Color(70, 88, 93)));
            scene.Add(ground);
            var platform = new WorldObject2D(AxisAlignedRectangle2D.FromSize(spec.Size), new SolidColorShader(new Color(37, 210, 190)));
            scene.Add(platform);
            camera.Position = focus;
            for (var step = 0; step < 240; step++)
            {
                var tick = game.Session.Tick + 1;
                var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick, new PersonCommand2D()));
                var state = frame.Players[0];
                player.Equip(state.Equipment);
                player.ApplyState(state.Person, frame.Tick, state.MoveX, false, state.IsMeleeAttackActive);
                enemies.ApplyState(frame.Enemies, [], frame.Tick);
                platform.Transform.Position = frame.World.MovingPlatforms[0].Position;
                if (step % 4 != 0) continue;
                device.SetRenderTarget(target);
                renderer.BeginFrame(400, 300, default); renderer.Clear(new Color(145, 176, 190)); renderer.Draw(scene);
                renderer.EndFrame(); device.SetRenderTarget(null);
                using var stream = File.Create(Path.Combine(directory, $"{name}-{step / 4:000}.png"));
                target.SaveAsPng(stream, 400, 300);
            }
        }
    }

    private sealed class Silent : ISoundEffectSink2D { public void Play(SoundEffect2D effect) { } }
}
