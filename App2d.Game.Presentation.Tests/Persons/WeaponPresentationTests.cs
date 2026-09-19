using App2d.Core;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Persons.Presentation;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class WeaponPresentationTests
{
    [Fact]
    public void AcceptedEventsDoNotAdvanceTimeAndEffectsExpireWithoutAnotherMessage()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSink();
        using var view = new WeaponPresentation2D(scene, textures, sounds);
        view.ApplyState(WeaponState2D.Empty, EquipmentKind2D.Gun, [new GunFired2D(Vector2.Zero)]);
        Assert.Contains(scene, item => item.IsVisible);
        for (var i = 0; i < 10; i++) view.ApplyState(WeaponState2D.Empty, EquipmentKind2D.Gun, []);
        Assert.Contains(scene, item => item.IsVisible);
        view.Advance(0.2f);
        Assert.DoesNotContain(scene, item => item.IsVisible);
        Assert.Single(sounds.Played, p => p.Effect == SoundEffect2D.GunFire);
    }

    [Fact]
    public void AttachingAndSuspendingAViewManagesChargeVoiceFromState()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSink();
        using var view = new WeaponPresentation2D(scene, textures, sounds);
        var charging = new WeaponState2D(true, 0.5f, new Vector2(40f, 5f), []);
        view.Update(charging, EquipmentKind2D.Gun, [], 0f);
        var voice = Assert.Single(sounds.Voices);
        Assert.True(voice.IsPlaying);
        Assert.Equal(charging.MuzzlePosition, voice.Position);
        view.Update(charging with { MuzzlePosition = new Vector2(80f, 5f) }, EquipmentKind2D.Gun, [], 0.01f);
        Assert.Single(sounds.Voices);
        Assert.Equal(new Vector2(80f, 5f), voice.Position);
        view.Suspend();
        Assert.False(voice.IsPlaying);
        Assert.DoesNotContain(scene, item => item.IsVisible);
        view.Update(WeaponState2D.Empty, EquipmentKind2D.Gun, [], 0.01f);
        Assert.Single(sounds.Voices);
        Assert.DoesNotContain(scene, item => item.IsVisible);
    }

    [Fact]
    public void OldTrailsFadeIndependentlyOfNewProjectilesAndImmediateImpactsStillPlay()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        var sounds = new RecordingSink();
        var view = new WeaponPresentation2D(scene, textures, sounds);
        var first = new ProjectileState2D(EntityId2D.Create(), new Vector2(100f, 0f), new Vector2(1250f, 0f), Vector2.Zero);
        var boltTexture = textures.Load("effects/gun/bolt.png");
        var state = new WeaponState2D(false, 0f, Vector2.Zero, [first]);
        view.Update(state, EquipmentKind2D.Gun, [], 0f);
        Assert.Equal(2, scene.Count(item => item.IsVisible)); // Bolt and trail.
        var second = first with { Id = EntityId2D.Create(), Position = new Vector2(20f, 0f) };
        view.Update(state with { Projectiles = [second] }, EquipmentKind2D.Gun, [new ProjectileImpact2D(first.Position, first.Id)], 0.01f);
        Assert.Equal(3, scene.Count(item => item.IsVisible)); // New bolt/trail plus fading old trail.
        Assert.Single(scene, item => item.IsVisible && ((SpriteShader2D)item.Shader).Texture == boltTexture);
        view.Update(state with { Projectiles = [second] }, EquipmentKind2D.Gun, [], 0.06f);
        Assert.Equal(2, scene.Count(item => item.IsVisible));
        view.Update(WeaponState2D.Empty, EquipmentKind2D.Gun, [new GunFired2D(Vector2.Zero),
            new ProjectileImpact2D(new Vector2(40f, 0f), EntityId2D.Create())], 0.01f);
        Assert.Equal(2, sounds.Played.Count(p => p.Effect == SoundEffect2D.GunImpact));
        Assert.Contains((SoundEffect2D.GunFire, Vector2.Zero), sounds.Played);
        view.Update(WeaponState2D.Empty, EquipmentKind2D.Gun, [], 0.1f);
        Assert.DoesNotContain(scene, item => item.IsVisible);
        view.Dispose();
        Assert.Empty(scene);
    }

    private sealed class RecordingSink : ISoundEffectSink2D
    {
        public List<Voice> Voices { get; } = [];
        public List<(SoundEffect2D Effect, Vector2 Position)> Played { get; } = [];
        public void Play(SoundEffect2D effect) => Played.Add((effect, Vector2.Zero));
        public void PlayAt(SoundEffect2D effect, Vector2 position) => Played.Add((effect, position));
        public SoundEffectVoice2D BeginAt(SoundEffect2D effect, Vector2 position, float initialVolumeScale = 1f)
        {
            var voice = new Voice();
            voice.SetPosition(position);
            Voices.Add(voice);
            return new SoundEffectVoice2D(voice);
        }
    }

    private sealed class Voice : ISoundEffectVoice2D
    {
        public bool IsPlaying { get; private set; } = true;
        public Vector2 Position { get; private set; }
        public void SetPosition(Vector2 position) => Position = position;
        public void SetVolumeScale(float volume, float seconds) { }
        public void Stop(float seconds) => IsPlaying = false;
    }
}
