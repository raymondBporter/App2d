using App2d.Levels;
using App2d.Core;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.World.Presentation;
using App2d.Rendering;
using App2d.Rendering.Textures;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Immutable;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

/// <summary>Exercises all enemy views using only client observations.</summary>
internal static class EnemyRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory)
    {
        var scene = new Scene2D();
        using var view = new EnemyPresentation2D(scene, textures,
            TraversalMetricsLoader2D.Load(textures.ContentRoot), new SilentSounds());
        var rivalId = EntityId2D.Create();
        ImmutableArray<EnemyState2D> states =
        [
            new(EntityId2D.Create(), EnemyKind2D.Shieldback, new(-280f, 0f), new(118f, 0f), 0f, 1f, true, true) { MoveSpeed = 118f },
            new(EntityId2D.Create(), EnemyKind2D.GreenDinosaur, new(-130f, 0f), new(-82f, 0f), 0f, -1f, true, true) { MoveSpeed = 82f },
            new(EntityId2D.Create(), EnemyKind2D.BoilerBrute, new(35f, 0f), Vector2.Zero, 0f, 1f, true, true)
                { MoveSpeed = 62f, IsAttacking = true, AttackElapsedSeconds = 0.55f },
            new(rivalId, EnemyKind2D.Rival, new(220f, 0f), Vector2.Zero, 0f, -1f, true, true)
            {
                IsAttacking = true,
                Person = default(PersonState2D) with { Id = rivalId, Position = new(220f, 0f), Facing = -1f,
                    HitPoints = 12, MaximumHitPoints = 12, IsGrounded = true }
            },
            new(EntityId2D.Create(), EnemyKind2D.TumbleProp, new(340f, 0f), Vector2.Zero, 0.4f, 1f, true, true)
        ];
        view.Update(states, [new RivalAttackStarted2D(rivalId, new(220f, 0f), UnarmedAttackKind2D.Kick, 0.42f)], 0.18f, 1);
        using var renderer = new Renderer2D(new Camera2D { Zoom = 1.35f, Position = new(20f, 25f) }, device);
        using var target = new RenderTarget2D(device, 1100, 450);
        device.SetRenderTarget(target);
        renderer.BeginFrame(1100, 450, default);
        renderer.Clear(new XnaColor(145, 176, 190));
        renderer.Draw(scene);
        renderer.DrawScreenLabel("CLIENT ENEMY VIEWS / HAMMER IMPACT + RIVAL KICK", new Vector2(24f, 24f));
        renderer.EndFrame();
        device.SetRenderTarget(null);
        using (var stream = File.Create(Path.Combine(directory, "enemy-client-views.png")))
            target.SaveAsPng(stream, 1100, 450);
        view.Update(states.Select(s => s with { IsEnabled = false }).ToImmutableArray(), [], 0f, 1);
        if (scene.Any(item => item.IsVisible))
            throw new InvalidOperationException("A streamed-out enemy left its visual visible.");
        view.Update([], [], 0f, 2);
        if (scene.Any()) throw new InvalidOperationException("Removed enemy views left scene objects behind.");
    }

    private sealed class SilentSounds : ISoundEffectSink2D
    {
        public void Play(SoundEffect2D effect) { }
    }
}
