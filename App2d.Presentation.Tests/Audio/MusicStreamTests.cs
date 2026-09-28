using App2d.Audio;
using System.Collections.Immutable;
using Xunit;

namespace App2d.Presentation.Tests.Audio;

public sealed class MusicStreamTests
{
    internal static MusicCue2D Cue(long frames = 16, int bar = 4) => new("Test", frames, bar, ["a", "b"],
        new Dictionary<string, ImmutableArray<float>> { ["explore"] = [1, 0], ["drive"] = [.5f, .5f], ["combat"] = [0, 1] }.ToImmutableDictionary());

    [Fact]
    public void MutedLayerAdvancesAndMoodFadesFromNextBarWithoutRestarting()
    {
        var a = new FakeStem(16, _ => 0);
        var b = new FakeStem(16, f => f);
        using var stream = new MusicStream2D(Cue(), [a, b]);
        stream.Restart("explore");
        var pre = new float[6]; stream.Read(pre);
        Assert.Equal(3, b.Position);
        stream.SetMood("combat");
        var block = new float[14]; stream.Read(block);
        float[] expected = [0, 0, 1.25f, 3, 5.25f, 8, 9];
        Assert.Equal(expected, Enumerable.Range(0, 7).Select(i => block[i * 2]));
        Assert.Equal(10, stream.FramePosition);
        Assert.Equal(a.Position, b.Position);
        Assert.Equal(1, b.Rewinds); // Only initial start, never on mood change.
    }

    [Fact]
    public void LoopWrapsInsideReadAndPreservesTheFirstSample()
    {
        using var stream = new MusicStream2D(Cue(), [new FakeStem(16, f => f), new FakeStem(16, _ => 100)]);
        stream.Restart("explore");
        var audio = new float[38]; stream.Read(audio);
        Assert.Equal(Enumerable.Range(0, 19).Select(i => (float)(i % 16)), Enumerable.Range(0, 19).Select(i => audio[i * 2]));
        Assert.Equal(3, stream.FramePosition);
    }

    [Fact]
    public void RepeatedRequestsDoNotPostponeMoodAndLatestPendingRequestWins()
    {
        using var stream = new MusicStream2D(Cue(), [new FakeStem(16, _ => 0), new FakeStem(16, _ => 1)]);
        stream.Restart("explore"); stream.SetMood("combat");
        var sample = new float[2];
        for (var i = 0; i < 9; i++) { stream.SetMood("combat"); stream.Read(sample); }
        Assert.Equal(1, sample[0], 5);
        stream.SetMood("explore"); stream.SetMood("drive");
        for (var i = 0; i < 8; i++) stream.Read(sample);
        Assert.Equal(.5f, sample[0], 5);
    }

    [Fact]
    public void EarlyEofFailsInsteadOfSilentlyDesynchronizingStems()
    {
        using var stream = new MusicStream2D(Cue(), [new FakeStem(16, _ => 1), new FakeStem(3, _ => 1)]);
        stream.Restart("explore");
        Assert.Throws<InvalidDataException>(() => stream.Read(new float[12]));
    }

    [Fact]
    public void MixerCrossfadesAndCoalescesRapidPieceChangesAndRampsMute()
    {
        var first = Cue(441000, 44100); var second = Cue(441000, 44100);
        using var mixer = new MusicMixer2D(new Dictionary<string, MusicCue2D> { ["a"] = first, ["b"] = second },
            cue => new(cue, [new FakeStem(441000, _ => ReferenceEquals(cue, first) ? .2f : .4f), new FakeStem(441000, _ => .1f)]));
        mixer.Volume = 1; mixer.Select("a", "explore");
        var seconds = new float[44100 * 2 * 3]; mixer.Read(seconds);
        Assert.Equal(0, seconds[0]); Assert.Equal(.2f, seconds[^1], 5);
        mixer.Select("b", "explore");
        var secondOfAudio = new float[44100 * 2]; mixer.Read(secondOfAudio);
        Assert.Equal(.2f, secondOfAudio[0], 5); Assert.InRange(secondOfAudio[^1], .299f, .301f);
        mixer.Select("a", "explore"); mixer.Read(secondOfAudio);
        Assert.InRange(secondOfAudio[^1], .399f, .401f);
        mixer.Read(seconds); Assert.Equal(.2f, seconds[^1], 5);
        mixer.Volume = 0; mixer.Read(secondOfAudio);
        Assert.Equal(.2f, secondOfAudio[0], 5); Assert.Equal(0, secondOfAudio[^1]);
        Assert.All(secondOfAudio, sample => Assert.InRange(sample, 0, .201f));
    }

    internal sealed class FakeStem(int frames, Func<int, float> sample) : IMusicStem2D
    {
        public int Position { get; private set; }
        public int Rewinds { get; private set; }
        public int Read(float[] buffer, int offset, int count)
        {
            var take = Math.Min(frames - Position, count / 2);
            for (var i = 0; i < take; i++) buffer[offset + i * 2] = buffer[offset + i * 2 + 1] = sample(Position + i);
            Position += take; return take * 2;
        }
        public void Rewind() { Position = 0; Rewinds++; }
        public void Dispose() { }
    }
}
