using App2d.CharacterSeeds.Diagnostics;
using App2d.Core;
using App2d.Core.Audio;
using App2d.Core.Characters.Authored;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Rendering.Textures;
using App2d.Gameplay.Enemies;
using App2d.Presentation.Audio;
using App2d.Presentation.World.Presentation;
using System.Numerics;

namespace App2d.Tests.Presentation.Enemies;

public sealed class StalkerPresentationTests
{
    [Fact]
    public void DrawsTheAuthoredPoseAndFollowsStreamingAndRemoval()
    {
        var scene = new Scene2D();
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var authored = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);
        var dinosaur = new AuthoredEntityEnemy2D(EntityId2D.Create(), authored.Entities["stalker-pest"], physics, Vector2.Zero, 1, 4);
        dinosaur.SetSimulationEnabled(true);
        using var textures = new TextureCache2D(TestAssetPath.Root);

        using var presentation = new EnemyPresentation2D(scene, textures,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), new SilentSounds());
        dinosaur.Update(0.2f, new Vector2(400f, 0f));
        physics.Step(0.2f);
        dinosaur.SyncAfterPhysics();
        Assert.Empty(scene);
        var state = dinosaur.CaptureState();
        presentation.Update([state], [], 0.2f, 1);

        var visual = Assert.Single(scene);
        Assert.True(visual.IsVisible);
        Assert.Same(state.AuthoredPose!.Local, Assert.IsType<AuthoredCharacterShader>(visual.Shader).Pose);
        Assert.True(dinosaur.Body.LinearVelocity.X > 0f);

        dinosaur.SetSimulationEnabled(false);
        presentation.Update([dinosaur.CaptureState()], [], 0f, 2);

        Assert.False(visual.IsVisible);
        presentation.Update([], [], 0f, 3);
        Assert.Empty(scene);
    }
    private sealed class SilentSounds : ISoundEffectSink2D<SoundEffect2D>
    {
        public void Play(SoundEffect2D effect)
        {
        }
    }
}
