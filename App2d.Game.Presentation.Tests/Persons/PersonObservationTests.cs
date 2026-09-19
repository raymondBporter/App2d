using App2d.Gameplay.Persons.Actions;
using App2d.Core;
using App2d.Core.Animation;
using App2d.Gameplay.Assets;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Levels;
using App2d.Rendering;
using App2d.Rendering.Textures;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class PersonObservationTests
{
    private static PersonState2D Alive => new() { Id = EntityId2D.Create(), HitPoints = 5,
        MaximumHitPoints = 5, Facing = 1f, IsGrounded = true };

    [Fact]
    public void DeathAndRevivalReconstructWithoutEvents()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new PersonPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root));
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
        var alive = Alive;
        view.ApplyState(alive with { HitPoints = 0 }, 500, 0f, false, false);
        Assert.Same(textures.Load("characters/player-sword/animations/death/frame-0001.png"), shader.Texture);
        view.Advance(0.12f);
        Assert.NotSame(textures.Load("characters/player-sword/animations/death/frame-0001.png"), shader.Texture);
        view.ApplyState(alive, 520, 0f, false, false);
        Assert.Same(textures.Load("characters/player-sword/animations/idle/frame-0001.png"), shader.Texture);
    }

    [Theory]
    [InlineData(PlayerAttackKind2D.Melee, "sword", "sword-attack")]
    [InlineData(PlayerAttackKind2D.Downward, "sword", "sword-down-attack")]
    [InlineData(PlayerAttackKind2D.Punch, "unarmed", "punch")]
    [InlineData(PlayerAttackKind2D.Kick, "unarmed", "kick")]
    public void AttachDuringActionSeeksAuthoritativePhaseAndAdvancesBetweenMessages(
        PlayerAttackKind2D kind, string equipment, string clipName)
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new PersonPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root));
        view.Equip(Enum.Parse<EquipmentKind2D>(equipment, ignoreCase: true));
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
        var clip = CharacterAnimationAssets2D.LoadClip(textures, $"player-{equipment}", clipName);
        var expected = new AnimationPlayer2D<Texture2D>();
        expected.Play(clip);
        expected.PlaybackSpeed = clip.Duration / 0.4f;
        expected.Update(0.16f);
        var state = Alive with { Action = new(kind, 0.16f, 0.4f) };
        view.ApplyState(state, 500, 0f, false, true);
        Assert.Same(expected.CurrentFrame, shader.Texture);
        view.Advance(0.1f);
        expected.Update(0.1f);
        Assert.Same(expected.CurrentFrame, shader.Texture);
        view.ApplyState(state, 501, 0f, false, true); // Correction seeks instead of restarting at frame one.
        expected.Play(clip, restart: true);
        expected.Update(0.16f);
        Assert.Same(expected.CurrentFrame, shader.Texture);
        view.Advance(0.5f);
        Assert.False(view.IsPlayingShot);
        Assert.Contains(shader.Texture, CharacterAnimationAssets2D.LoadClip(textures, $"player-{equipment}", "idle").Frames);
    }

    [Fact]
    public void AttachingAfterGunRecoveryStillShowsRecoilAtAuthoredSpeed()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new PersonPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root));
        view.Equip(EquipmentKind2D.Gun);
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
        view.ApplyState(Alive with { Action = new(PlayerAttackKind2D.Shot, 0.10f, 0.06f) }, 500, 0f, false, false);
        Assert.True(view.IsPlayingShot);
        Assert.Contains(shader.Texture, CharacterAnimationAssets2D.LoadClip(textures, "player-gun", "magic-shot").Frames);
        view.Advance(0.10f);
        Assert.False(view.IsPlayingShot);
    }
}
