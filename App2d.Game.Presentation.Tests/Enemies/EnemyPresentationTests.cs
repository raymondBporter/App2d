using App2d.Levels;
using App2d.Core;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Player;
using App2d.Gameplay.World.Presentation;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Enemies;

public sealed class EnemyPresentationTests
{
    [Fact]
    public void HammerAdvancesBetweenMessagesWithoutReplayingSound()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSounds();
        using var view = new EnemyPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root), sounds);
        var state = new EnemyState2D(EntityId2D.Create(), EnemyKind2D.BoilerBrute,
            Vector2.Zero, Vector2.Zero, 0f, 1f, true, true) { IsAttacking = true, AttackElapsedSeconds = 0.15f };
        view.ApplyState([state], [new HammerStarted2D(state.Id, state.Position)], 500);
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
        Assert.Same(textures.Load("characters/boiler-brute/animations/hammer-attack/frame-0002.png"), shader.Texture);
        view.Advance(0.2f);
        Assert.Same(textures.Load("characters/boiler-brute/animations/hammer-attack/frame-0004.png"), shader.Texture);
        Assert.Single(sounds.Played);
    }

    [Fact]
    public void RemovingARivalRemovesBothItsPersonSpriteAndMarker()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new EnemyPresentation2D(scene, textures,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), new RecordingSounds());
        var id = EntityId2D.Create();
        var state = new EnemyState2D(id, EnemyKind2D.Rival, Vector2.Zero, Vector2.Zero, 0f, 1f, true, true)
        {
            Person = default(App2d.Gameplay.Persons.PersonState2D) with
                { Id = id, Facing = 1f, HitPoints = 12, MaximumHitPoints = 12, IsGrounded = true }
        };
        view.Update([state], [], 0f, 1);
        Assert.Equal(2, scene.Count());
        view.Update([], [], 0f, 2);
        Assert.Empty(scene);
    }

    [Fact]
    public void HammerPoseUsesSnapshotTimeAndDisabledOrRemovedActorsLeaveNoVisuals()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSounds();
        using var view = new EnemyPresentation2D(scene, textures,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), sounds);
        var state = new EnemyState2D(EntityId2D.Create(), EnemyKind2D.BoilerBrute,
            new Vector2(100f, 0f), Vector2.Zero, 0f, -1f, true, true)
            { MoveSpeed = 62f, IsAttacking = true, AttackElapsedSeconds = 0.55f };
        var position = new Vector2(38f, -10f);
        view.Update([state], [new HammerStruck2D(state.Id, position)], 3f, 1);
        var visual = Assert.Single(scene);
        var shader = Assert.IsType<SpriteShader2D>(visual.Shader);
        Assert.Same(textures.Load("characters/boiler-brute/animations/hammer-attack/frame-0006.png"), shader.Texture);
        Assert.True(shader.FlipX);
        Assert.Equal((SoundEffect2D.HammerImpact, position), Assert.Single(sounds.Played));
        view.Update([state with { IsEnabled = false }], [], 0f, 1);
        Assert.False(visual.IsVisible);
        view.Update([state], [], 0f, 2);
        Assert.True(visual.IsVisible);
        Assert.Single(sounds.Played);
        view.Update([], [], 0f, 3);
        Assert.Empty(scene);
    }

    [Fact]
    public void TumblePropVisualCopiesValuesInsteadOfSharingThePhysicsObject()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new EnemyPresentation2D(scene, textures,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), new RecordingSounds());
        var physics = new App2d.Physics.PhysicsWorld2D();
        var prop = new TumbleProp2D(physics, Vector2.Zero, 1, 4);
        var original = prop.CaptureState();
        view.Update([original], [], 0f, 0);
        var visual = Assert.Single(scene);
        Assert.NotSame(prop.WorldObject, visual);
        prop.WorldObject.Transform.Position = new Vector2(80f, 20f);
        prop.WorldObject.Transform.Rotation = 0.4f;
        Assert.Equal(Vector2.Zero, visual.Transform.Position);
        view.Update([prop.CaptureState()], [], 0f, 1);
        Assert.Equal(new Vector2(80f, 20f), visual.Transform.Position);
        Assert.Equal(0.4f, visual.Transform.Rotation);
        Assert.Equal(Vector2.Zero, original.Position);
        prop.SetSimulationEnabled(false);
        view.Update([prop.CaptureState()], [], 0f, 2);
        Assert.False(visual.IsVisible);
    }

    private sealed class RecordingSounds : ISoundEffectSink2D
    {
        public List<(SoundEffect2D, Vector2)> Played { get; } = [];
        public void Play(SoundEffect2D effect) => Assert.Fail("Enemy sounds require an occurrence position.");
        public void PlayAt(SoundEffect2D effect, Vector2 position) => Played.Add((effect, position));
    }
}
