using App2d.Contracts.Enemies;
using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Assets;
using App2d.Core.Audio;
using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Presentation.Audio;
using App2d.Presentation.World.Presentation;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.CharacterStudio.CharacterSeeds.Diagnostics;

/// <summary>Exercises all enemy views using only client observations.</summary>
internal static class EnemyRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory)
    {
        var scene = new Scene2D();
        using var view = new EnemyPresentation2D(scene, textures,
            TraversalMetricsLoader2D.Load(textures.ContentRoot), new SilentSounds());
        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        var rivalId = EntityId2D.Create();
        ImmutableArray<EnemyState2D> states =
        [
            new(EntityId2D.Create(), EnemyKind2D.Shieldback, new(-280f, 0f), new(118f, 0f), 0f, 1f, true, true) { MoveSpeed = 118f },
            Observe("stalker-pest", new(-130f, 0f), -1, null, .18f),
            Observe("maul-brute", new(35f, 0f), 1, "attack", .55f),
            new(rivalId, EnemyKind2D.Rival, new(220f, 0f), Vector2.Zero, 0f, -1f, true, true)
            {
                IsAttacking = true,
                Person = default(PersonState2D) with { Id = rivalId, Position = new(220f, 0f), Facing = -1f,
                    HitPoints = 12, MaximumHitPoints = 12, IsGrounded = true }
            },
        ];
        view.Update(states, [new RivalAttackStarted2D(rivalId, new(220f, 0f), UnarmedAttackKind2D.Kick, 0.42f)], 0.18f, 1);
        using var renderer = new Renderer2D(new Camera2D { Zoom = 1.35f, Position = new(20f, 25f) }, device);
        using var target = RenderCapture2D.CreateTarget(device, 1100, 450, DepthFormat.None, 0);
        target.SavePng(Path.Combine(directory, "enemy-client-views.png"), new Color4b(145, 176, 190), () =>
        {
            renderer.BeginFrame(1100, 450, default);
            renderer.Draw(scene);
            renderer.DrawScreenLabel("CLIENT ENEMY VIEWS / HAMMER IMPACT + RIVAL KICK", new Vector2(24f, 24f));
            renderer.EndFrame();
        });

        view.Update([.. states.Select(s => s with { IsEnabled = false })], [], 0f, 1);
        if (scene.Any(item => item.IsVisible))
        {
            throw new InvalidOperationException("A streamed-out enemy left its visual visible.");
        }

        view.Update([], [], 0f, 2);
        if (scene.Any())
        {
            throw new InvalidOperationException("Removed enemy views left scene objects behind.");
        }

        EnemyState2D Observe(string entityId, Vector2 position, int facing, string? actionId, float seconds)
        {
            var entity = catalog.Entities[entityId];
            var clip = actionId is null ? entity.Clip(EntityControllers.Walk) : entity.Actions[actionId].Clip;
            return new EnemyState2D(EntityId2D.Create(), EnemyKind2D.Authored, position, Vector2.Zero, 0, facing, true, true)
            {
                TypeId = entityId,
                AuthoredEntity = entity,
                AuthoredPose = new ActorPose(PoseEvaluator.Sample(entity.Model, clip, seconds, actionId is null),
                    GameWorldUnits2D.WorldToAuthored(position), facing),
                ActionId = actionId ?? EntityControllers.Walk,
                ActionSeconds = seconds,
                IsAttacking = actionId is not null
            };
        }
    }

    private sealed class SilentSounds : ISoundEffectSink2D<SoundEffect2D>
    {
        public void Play(SoundEffect2D effect)
        {
        }
    }
}
