using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.World.Presentation;
using App2d.Levels;
using App2d.Rendering;
using App2d.Rendering.Characters;
using App2d.Rendering.Textures;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Immutable;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

internal static class AuthoredRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory)
    {
        var catalog = new EntityCatalog(AssetPaths.Characters);
        var scene = new Scene2D();
        using var view = new EnemyPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(textures.ContentRoot), new Silent(), catalog);
        var ids = new[] { "needle", "maul", "cinder", "scrap-hound" };
        var playerShader = new PointCharacterShader(catalog, catalog.Types["player"]);
        var player = new WorldObject2D(AxisAlignedRectangle2D.FromSize(new(10)), playerShader) { ZIndex = 1 };
        player.Transform.Scale = new(40); player.Transform.Position = new(-280, 0); scene.Add(player);
        using var renderer = new Renderer2D(new Camera2D { Zoom = 1.6f, Position = new(20, 42) }, device);
        using var target = new RenderTarget2D(device, 1400, 500, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        foreach (var action in new[] { "idle", "walk", "attack", "death" })
        foreach (var facing in new[] { 1f, -1f })
        {
            var states = ids.Select((id, i) => new EnemyState2D(new EntityId2D(i + 1), EnemyKind2D.Rival,
                new(-130 + i * 155, catalog.Types[id].Movement.Height * 20), Vector2.Zero, 0, facing, true, action != "death")
            { TypeId = id, ActionId = action, ActionSeconds = catalog.Types[id].Actions[action].Duration * .5f }).ToImmutableArray();
            view.ApplyState(states, [], 60); playerShader.Action = action; playerShader.Seconds = catalog.Types["player"].Actions[action].Duration * .5f; playerShader.FacingLeft = facing < 0;
            device.SetRenderTarget(target); renderer.BeginFrame(1400, 500, default); renderer.Clear(new Color(145, 176, 190));
            renderer.Draw(new WorldObject2D(AxisAlignedRectangle2D.FromSize(new(1200, 2)), new SolidColorShader(Color.DarkSlateGray)));
            renderer.Draw(scene);
            renderer.DrawScreenLabel("PLAYER / NEEDLE / MAUL / CINDER / SCRAP HOUND", new(24, 24));
            renderer.EndFrame(); device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(directory, $"authored-{action}-{(facing > 0 ? "right" : "left")}.png")); target.SaveAsPng(stream, target.Width, target.Height);
        }
        view.ApplyState([], [], 61);
        if (scene.Count() != 1) throw new InvalidOperationException("Authored enemy views leaked scene objects.");
        RunEntities(device, view, scene, renderer, target, directory);
        view.ApplyState([], [], 62);
        if (scene.Count() != 1) throw new InvalidOperationException("Authored entity views leaked scene objects.");
    }

    /// <summary>Authored entities through the game's presentation: each frame draws the animator's own final pose, props included.</summary>
    private static void RunEntities(GraphicsDevice device, EnemyPresentation2D view, Scene2D scene, Renderer2D renderer, RenderTarget2D target, string directory)
    {
        var authored = AuthoredCatalog.Load(Path.Combine(AssetPaths.Characters, "authored"));
        if (authored.Errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, authored.Errors));
        var ids = new[] { "spear-guard", "stalker-pest", "player" };
        foreach (var (phase, action, seconds) in new[] { ("idle", (string?)null, .5f), ("walk", null, .4f), ("anticipation", "attack", .25f), ("active", "attack", .45f), ("recovery", "attack", .75f) })
        foreach (var facing in new[] { 1, -1 })
        {
            var states = ids.Select((id, i) =>
            {
                var entity = authored.Entities[id]; var animator = new EntityAnimator(entity); var feet = new Vector2((-110 + i * 170) / EntityCatalog.WorldUnits, 0);
                if (action is not null && animator.TryStart(action)) for (var t = 0f; t < seconds; t += 1 / 120f) animator.Step(1 / 120f, feet, facing, "idle", 0, false, []);
                else for (var t = 0f; t < seconds; t += 1 / 120f) animator.Step(1 / 120f, feet, facing, phase, phase == "walk" ? .012f : 0, false, []);
                return new EnemyState2D(new EntityId2D(100 + i), EnemyKind2D.Authored, feet * EntityCatalog.WorldUnits, Vector2.Zero, 0, facing, true, true)
                { TypeId = id, AuthoredEntity = entity, AuthoredPose = animator.Pose };
            }).ToImmutableArray();
            view.ApplyState(states, [], 70);
            device.SetRenderTarget(target); renderer.BeginFrame(1400, 500, default); renderer.Clear(new Color(145, 176, 190));
            renderer.Draw(new WorldObject2D(AxisAlignedRectangle2D.FromSize(new(1200, 2)), new SolidColorShader(Color.DarkSlateGray)));
            renderer.Draw(scene);
            renderer.DrawScreenLabel("AUTHORED: SPEAR GUARD / STALKER PEST / PLAYER", new(24, 24));
            renderer.EndFrame(); device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(directory, $"entity-{phase}-{(facing > 0 ? "right" : "left")}.png")); target.SaveAsPng(stream, target.Width, target.Height);
        }
    }
    private sealed class Silent : ISoundEffectSink2D { public void Play(SoundEffect2D effect) { } }
}
