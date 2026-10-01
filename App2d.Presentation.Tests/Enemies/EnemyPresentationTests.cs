using App2d.Contracts.Enemies;
using App2d.Core;
using App2d.Core.Physics;
using App2d.Presentation.Audio;
using App2d.Presentation.World.Presentation;
using App2d.Gameplay;
using App2d.Levels;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;
using Xunit;

namespace App2d.Presentation.Tests.Enemies;

public sealed class EnemyPresentationTests
{
    [Fact]
    public void ThrowerReleasesHeldStoneRendersRockAndCleansUpOnStreaming()
    {
        var catalog = App2d.Core.Characters.Authored.AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));
        var entity = catalog.Entities["rock-thrower"];
        var pose = new App2d.Core.Characters.Authored.ActorPose(
            App2d.Core.Characters.Authored.PoseEvaluator.Sample(entity.Model, entity.Actions["attack"].Clip, 1, false), Vector2.Zero, 1);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D(); var sounds = new RecordingSounds();
        using var view = new EnemyPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root), sounds);
        var state = new EnemyState2D(EntityId2D.Create(), EnemyKind2D.Authored, Vector2.Zero, Vector2.Zero, 0, 1, true, true)
            { TypeId = entity.Id, AuthoredEntity = entity, AuthoredPose = pose, ActionId = "attack", ActionSeconds = 1 };
        view.ApplyState([state], [], 1);
        var shader = Assert.IsType<App2d.Rendering.Characters.AuthoredCharacterShader>(Assert.Single(scene).Shader);
        Assert.Contains(shader.Props, p => p.Prop.Id == "throwing-rock");
        state = state with { ActionSeconds = 1.2f, Bolts = [new(new(70, 80), new(100, 120), new(14.4f), 3) { Gravity = 600 }] };
        view.ApplyState([state], [new EntityCue2D(state.Id, Vector2.Zero, "rock-throw")], 2);
        Assert.DoesNotContain(shader.Props, p => p.Prop.Id == "throwing-rock");
        Assert.Contains(scene, o => o.Shape is App2d.Core.Shapes.ConvexPolygon2D);
        Assert.Contains(sounds.Played, cue => cue.Item1 == SoundEffect2D.SwordSwing);
        view.ApplyState([state with { IsEnabled = false }], [], 3);
        Assert.DoesNotContain(scene, o => o.IsVisible);
        view.ApplyState([], [], 4);
        Assert.Empty(scene);
    }

    [Fact]
    public void HammerAdvancesBetweenMessagesWithoutReplayingSound()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSounds();
        using var view = new EnemyPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root), sounds);
        var state = new EnemyState2D(EntityId2D.Create(), EnemyKind2D.BoilerBrute,
            Vector2.Zero, Vector2.Zero, 0f, 1f, true, true)
        { IsAttacking = true, AttackElapsedSeconds = 0.15f, AttackDurationSeconds = 0.8f };
        view.ApplyState([state], [new HammerStarted2D(state.Id, state.Position)], 500);
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
        Assert.Same(textures.Load("characters/boiler-brute/animations/hammer-attack/frame-0002.png"), shader.Texture);
        view.Advance(0.2f);
        Assert.Same(textures.Load("characters/boiler-brute/animations/hammer-attack/frame-0004.png"), shader.Texture);
        view.ApplyState([state with { AttackDurationSeconds = 0.4f }], [], 501);
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
            Person = default(App2d.Contracts.Persons.PersonState2D) with
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
        { MoveSpeed = 62f, IsAttacking = true, AttackElapsedSeconds = 0.55f, AttackDurationSeconds = 0.8f };
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
        var physics = new PhysicsWorld2D();
        var prop = new TumbleProp2D(EntityId2D.Create(), physics, Vector2.Zero, 1, 4);
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
