using App2d.CharacterStudio.CharacterSeeds.Diagnostics;
using App2d.Contracts.Enemies;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Audio;
using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Rendering.Textures;
using App2d.Presentation.Audio;
using App2d.Presentation.World.Presentation;
using System.Numerics;

namespace App2d.Tests.Presentation.Enemies;

public sealed class EnemyPresentationTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);

    [Fact]
    public void ThrowerReleasesHeldStoneRendersRockAndCleansUpOnStreaming()
    {
        var entity = Catalog.Entities["rock-thrower"];
        var pose = new ActorPose(
            PoseEvaluator.Sample(entity.Model, entity.Actions["attack"].Clip, 1, false), Vector2.Zero, 1);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSounds();
        using var view = new EnemyPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root), sounds);
        var state = new EnemyState2D(EntityId2D.Create(), EnemyKind2D.Authored, Vector2.Zero, Vector2.Zero, 0, 1, true, true)
        {
            TypeId = entity.Id,
            AuthoredEntity = entity,
            AuthoredPose = pose,
            ActionId = "attack",
            ActionSeconds = 1
        };
        view.ApplyState([state], [], 1);
        var shader = Assert.IsType<AuthoredCharacterShader>(Assert.Single(scene).Shader);
        Assert.Contains(shader.Props, p => p.Prop.Id == "throwing-rock");
        state = state with
        {
            ActionSeconds = 1.2f,
            Bolts = [new(new(70, 80), new(100, 120), new(14.4f), 3) { Gravity = 600 }]
        };
        view.ApplyState([state], [new EntityCue2D(state.Id, Vector2.Zero, "rock-throw")], 2);
        Assert.DoesNotContain(shader.Props, p => p.Prop.Id == "throwing-rock");
        Assert.Contains(scene, o => o.Shape is Core.Shapes.ConvexPolygon2D);
        Assert.Contains(sounds.Played, cue => cue.Item1 == SoundEffect2D.SwordSwing);
        view.ApplyState([state with { IsEnabled = false }], [], 3);
        Assert.DoesNotContain(scene, o => o.IsVisible);
        view.ApplyState([], [], 4);
        Assert.Empty(scene);
    }

    [Fact]
    public void AuthoredAttackKeepsTheSimulationPoseBetweenUpdatesWithoutReplayingSound()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSounds();
        using var view = new EnemyPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root), sounds);
        var state = AuthoredAttack("maul-brute", .15f);
        view.ApplyState([state], [new EntityCue2D(state.Id, state.Position, "heavy")], 500);
        var shader = Assert.IsType<AuthoredCharacterShader>(Assert.Single(scene).Shader);
        Assert.Same(state.AuthoredPose!.Local, shader.Pose);
        view.Advance(0.2f);
        Assert.Same(state.AuthoredPose.Local, shader.Pose);
        var next = AuthoredAttack("maul-brute", .35f) with { Id = state.Id };
        view.ApplyState([next], [], 501);
        Assert.Same(next.AuthoredPose!.Local, shader.Pose);
        Assert.NotSame(state.AuthoredPose.Local, shader.Pose);
        Assert.Equal((SoundEffect2D.HammerImpact, state.Position), Assert.Single(sounds.Played));
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
            Person = default(Contracts.Persons.PersonState2D) with
            {
                Id = id,
                Facing = 1f,
                HitPoints = 12,
                MaximumHitPoints = 12,
                IsGrounded = true
            }
        };
        view.Update([state], [], 0f, 1);
        Assert.Equal(2, scene.Count());
        view.Update([], [], 0f, 2);
        Assert.Empty(scene);
    }

    [Theory]
    [InlineData("maul-brute")]
    [InlineData("stalker-pest")]
    public void AuthoredEnemiesUseTheirFinalPoseAndCleanUpOnStreamingOrRemoval(string entityId)
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSounds();
        using var view = new EnemyPresentation2D(scene, textures,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), sounds);
        var state = AuthoredAttack(entityId, .55f, new Vector2(100f, 0f), -1);
        var position = new Vector2(38f, -10f);
        view.Update([state], [new EntityCue2D(state.Id, position, "heavy")], 3f, 1);
        var visual = Assert.Single(scene);
        var shader = Assert.IsType<AuthoredCharacterShader>(visual.Shader);
        Assert.Same(state.AuthoredPose!.Local, shader.Pose);
        Assert.Equal(state.Position, visual.Transform.Position);
        Assert.Equal(-1, shader.Facing);
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
    public void ShieldbackStillSupportsTheSpriteFallbackAndCleansUpItsView()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new EnemyPresentation2D(scene, textures,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), new RecordingSounds());
        var state = new EnemyState2D(EntityId2D.Create(), EnemyKind2D.Shieldback,
            new Vector2(100f, 0f), new Vector2(118f, 0f), 0f, -1f, true, true)
        { MoveSpeed = 118f };

        view.ApplyState([state], [], 1);
        var visual = Assert.Single(scene);
        var shader = Assert.IsType<SpriteShader2D>(visual.Shader);
        Assert.Same(textures.Load("characters/shieldback/animations/walk/frame-0001.png"), shader.Texture);
        Assert.True(shader.FlipX);
        Assert.Equal(new Vector2(100f, 10f), visual.Transform.Position);
        view.ApplyState([state with { IsEnabled = false }], [], 2);
        Assert.False(visual.IsVisible);
        view.ApplyState([], [], 3);
        Assert.Empty(scene);
    }

    private static EnemyState2D AuthoredAttack(string entityId, float seconds, Vector2 position = default, int facing = 1)
    {
        var entity = Catalog.Entities[entityId];
        return new EnemyState2D(EntityId2D.Create(), EnemyKind2D.Authored, position, Vector2.Zero, 0, facing, true, true)
        {
            TypeId = entityId,
            AuthoredEntity = entity,
            AuthoredPose = new ActorPose(PoseEvaluator.Sample(entity.Model, entity.Actions["attack"].Clip, seconds, false),
                GameWorldUnits2D.WorldToAuthored(position), facing),
            ActionId = "attack",
            ActionSeconds = seconds,
            IsAttacking = true
        };
    }

    private sealed class RecordingSounds : ISoundEffectSink2D<SoundEffect2D>
    {
        public List<(SoundEffect2D, Vector2)> Played { get; } = [];
        public void Play(SoundEffect2D effect) => Assert.Fail("Enemy sounds require an occurrence position.");
        public void PlayAt(SoundEffect2D effect, Vector2 position) => Played.Add((effect, position));
    }
}
