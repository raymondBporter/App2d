using App2d.Gameplay.Audio;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Audio;

public sealed class SpatialSoundEffectTests
{
    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(96f, 1f)]
    [InlineData(448f, 0.5f)]
    [InlineData(800f, 0f)]
    [InlineData(2000f, 0f)]
    public void FalloffHasFullVolumeCoreAndSmoothFadeToSilence(float distance, float expected)
    {
        Assert.Equal(expected, SoundFalloff2D.Default.Evaluate(new(distance, 0f), Vector2.Zero), 5);
        Assert.Equal(expected, SoundFalloff2D.Default.Evaluate(new(0f, -distance), Vector2.Zero), 5);
    }

    [Fact]
    public void DistanceIsRelativeAndUsesBothAxes()
    {
        var falloff = new SoundFalloff2D(0f, 1000f);
        Assert.Equal(0.5f, falloff.Evaluate(new(400f, 600f), new(100f, 200f)), 5);
        Assert.Equal(0.5f, falloff.Evaluate(new(100f, 200f), new(400f, 600f)), 5);
    }

    [Theory]
    [InlineData(-1f, 800f)]
    [InlineData(96f, 96f)]
    [InlineData(100f, 50f)]
    [InlineData(0f, 0f)]
    [InlineData(float.NaN, 800f)]
    [InlineData(0f, float.PositiveInfinity)]
    public void InvalidRadiiAreRejected(float inner, float outer) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundFalloff2D(inner, outer));

    [Fact]
    public void InaudibleOneShotsDoNotConsumeVoicesAndHammerCarriesFarther()
    {
        var output = new RecordingSink();
        var sounds = new SpatialSoundEffectSink2D(output, () => Vector2.Zero);
        sounds.PlayAt(SoundEffect2D.GunImpact, new(900f, 0f));
        Assert.Empty(output.Voices);
        sounds.PlayAt(SoundEffect2D.HammerImpact, new(900f, 0f));
        Assert.InRange(Assert.Single(output.Voices).Volume, 0.01f, 1f);
    }

    [Fact]
    public void ActiveOneShotFollowsListenerWithoutRestarting()
    {
        var listener = Vector2.Zero;
        var output = new RecordingSink();
        var sounds = new SpatialSoundEffectSink2D(output, () => listener);
        sounds.PlayAt(SoundEffect2D.EnemyHurt, Vector2.Zero);
        var playback = Assert.Single(output.Voices);
        Assert.Equal(1f, playback.Volume);
        listener = new(448f, 0f);
        sounds.Update();
        Assert.Equal(0.5f, playback.Volume, 5);
        Assert.True(playback.LastRamp > 0f);
        listener = new(1000f, 0f);
        sounds.Update();
        Assert.Equal(0f, playback.Volume);
        listener = Vector2.Zero;
        sounds.Update();
        Assert.Equal(1f, playback.Volume);
        Assert.Single(output.Voices);
    }

    [Fact]
    public void SourceMotionAndVolumeChangesMultiplyRatherThanOverwriteFalloff()
    {
        var output = new RecordingSink();
        var sounds = new SpatialSoundEffectSink2D(output, () => Vector2.Zero);
        var voice = sounds.BeginAt(SoundEffect2D.GunCharge, new(448f, 0f), 0.6f);
        var playback = Assert.Single(output.Voices);
        Assert.Equal(0.3f, playback.Volume, 5);
        var copiedHandle = voice;
        copiedHandle.SetVolumeScale(0.4f, 0.1f);
        Assert.Equal(0.2f, playback.Volume, 5);
        voice.SetPosition(Vector2.Zero);
        sounds.Update();
        Assert.Equal(0.4f, playback.Volume, 5);
        voice.SetPosition(new(448f, 0f));
        sounds.Update();
        Assert.Equal(0.2f, playback.Volume, 5);
    }

    [Fact]
    public void InitiallySilentControllableVoiceCanMoveIntoRange()
    {
        var output = new RecordingSink();
        var sounds = new SpatialSoundEffectSink2D(output, () => Vector2.Zero);
        var voice = sounds.BeginAt(SoundEffect2D.GunCharge, new(2000f, 0f));
        var playback = Assert.Single(output.Voices);
        Assert.Equal(0f, playback.Volume);
        voice.SetPosition(Vector2.Zero);
        sounds.Update();
        Assert.Equal(1f, playback.Volume);
        Assert.Single(output.Voices);
    }

    [Fact]
    public void GlobalSoundsBypassFalloff()
    {
        var output = new RecordingSink();
        var sounds = new SpatialSoundEffectSink2D(output, () => new(10000f, 10000f));
        sounds.Play(SoundEffect2D.GoalReached);
        sounds.Begin(SoundEffect2D.PlayerJump, 0.4f);
        sounds.Update();
        Assert.Equal(SoundEffect2D.GoalReached, Assert.Single(output.GlobalEffects));
        Assert.Equal(0.4f, Assert.Single(output.Voices).Volume);
    }

    [Fact]
    public void StoppingDoesNotLetSpatialUpdatesOverwriteFadeOut()
    {
        var output = new RecordingSink();
        var listener = Vector2.Zero;
        var sounds = new SpatialSoundEffectSink2D(output, () => listener);
        var handle = sounds.BeginAt(SoundEffect2D.GunCharge, Vector2.Zero);
        var playback = Assert.Single(output.Voices);
        handle.Stop(0.1f);
        Assert.Equal(0.1f, playback.StopFade);
        listener = new(448f, 0f);
        sounds.Update();
        handle.SetVolumeScale(0.8f);
        Assert.Equal(0, playback.VolumeChanges);
    }

    [Fact]
    public void CompletedVoicesAreRemoved()
    {
        var output = new RecordingSink();
        var listener = Vector2.Zero;
        var sounds = new SpatialSoundEffectSink2D(output, () => listener);
        var handle = sounds.BeginAt(SoundEffect2D.GunCharge, Vector2.Zero);
        var playback = Assert.Single(output.Voices);
        playback.IsPlaying = false;
        Assert.False(handle.IsPlaying);
        sounds.Update();
        // A completed voice is removed, even if a backend subsequently reuses its slot.
        playback.IsPlaying = true;
        listener = new(800f, 0f);
        sounds.Update();
        Assert.Equal(0, playback.VolumeChanges);
    }

    [Fact]
    public void DefaultVoiceAndLegacySinkRemainSafe()
    {
        var voice = default(SoundEffectVoice2D);
        voice.SetPosition(Vector2.Zero);
        voice.SetVolumeScale(0.5f);
        voice.Stop();
        Assert.False(voice.IsPlaying);
        ISoundEffectSink2D sink = new LegacySink();
        sink.PlayAt(SoundEffect2D.EnemyHurt, Vector2.One);
        sink.BeginAt(SoundEffect2D.GunCharge, Vector2.One);
        Assert.Equal(2, ((LegacySink)sink).Count);
    }

    private sealed class LegacySink : ISoundEffectSink2D
    {
        public int Count { get; private set; }
        public void Play(SoundEffect2D effect) => Count++;
    }

    private sealed class RecordingSink : ISoundEffectSink2D
    {
        public List<SoundEffect2D> GlobalEffects { get; } = [];
        public List<RecordingVoice> Voices { get; } = [];
        public void Play(SoundEffect2D effect) => GlobalEffects.Add(effect);
        public SoundEffectVoice2D Begin(SoundEffect2D effect, float initialVolumeScale = 1f)
        {
            var voice = new RecordingVoice { Volume = initialVolumeScale };
            Voices.Add(voice);
            return new SoundEffectVoice2D(voice);
        }
    }

    private sealed class RecordingVoice : ISoundEffectVoice2D
    {
        public bool IsPlaying { get; set; } = true;
        public float Volume { get; set; }
        public float LastRamp { get; private set; }
        public float StopFade { get; private set; }
        public int VolumeChanges { get; private set; }
        public void SetVolumeScale(float volumeScale, float rampSeconds)
        {
            Volume = volumeScale;
            LastRamp = rampSeconds;
            VolumeChanges++;
        }
        public void Stop(float fadeOutSeconds) => StopFade = fadeOutSeconds;
    }
}
