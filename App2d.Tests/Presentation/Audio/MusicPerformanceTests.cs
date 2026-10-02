using App2d.Contracts.World;
using App2d.Audio;
using App2d.Presentation.Audio;
using System.Diagnostics;
using System.Numerics;
using Xunit.Abstractions;

namespace App2d.Presentation.Tests.Audio;

public sealed class MusicPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void ZoneUpdatesAndSelectionsAllocateNoManagedMemoryAfterWarmup()
    {
        var cue = MusicStreamTests.Cue();
        var soundtrack = new WorldSoundtrack2D(new Dictionary<string, MusicCue2D> { ["test"] = cue },
            new("test", "explore"), new Dictionary<string, MusicSelection2D> { ["b"] = new("test", "combat") });
        var content = LevelContent2D.Empty with
        {
            Zones = [
            new("a", "A", new(Vector2.Zero, new(10,10))), new("b", "B", new(new(10,0),new(20,10)))]
        };
        var director = new WorldMusicDirector2D(soundtrack, static (_, _) => { });
        for (var i = 0; i < 1000; i++) director.Update(content, new((i / 4 % 2) * 10 + 1, 1), .5f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) director.Update(content, new((i / 4 % 2) * 10 + 1, 1), .5f);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"10,000 zone updates including transitions: {allocated} bytes allocated.");
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void MixingAndControlChangesAllocateNoManagedMemoryWithPreallocatedSources()
    {
        var cue = MusicStreamTests.Cue(16384, 1024);
        using var mixer = new MusicMixer2D(new Dictionary<string, MusicCue2D> { ["a"] = cue, ["b"] = cue },
            c => new(c, [new MusicStreamTests.FakeStem(16384, static _ => .1f), new MusicStreamTests.FakeStem(16384, static _ => .2f)]));
        var buffer = new float[2048];
        for (var i = 0; i < 100; i++) { mixer.Select(i % 2 == 0 ? "a" : "b", "explore"); mixer.Read(buffer); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            mixer.Select(i / 100 % 2 == 0 ? "a" : "b", i / 50 % 2 == 0 ? "explore" : "combat");
            mixer.Volume = i % 2 == 0 ? .4f : .5f;
            mixer.Read(buffer);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"1,000 mixer blocks including fades, wraps and controls: {allocated} bytes allocated.");
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData("crown-of-embers")]
    [InlineData("copper-circuit")]
    public void RealDecoderAllocationsStayWithinTheRecordedBudget(string id)
    {
        var root = FindRoot();
        var cue = MusicCue2D.Load(Path.Combine(root, "Assets/Static/audio/music", id, "manifest.json"));
        using var stream = new MusicStream2D(cue);
        var buffer = new float[2048];
        stream.Restart("combat");
        for (var i = 0; i < 100; i++) stream.Read(buffer);
        const int blocks = 500;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < blocks; i++) stream.Read(buffer);
        var elapsed = Stopwatch.GetElapsedTime(start);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var seconds = blocks * 1024d / MusicCue2D.SampleRate;
        output.WriteLine($"{id}: {seconds:F2}s audio, {elapsed.TotalMilliseconds:F1}ms processing, {allocated:N0} allocated bytes ({allocated / seconds:N0} bytes/audio-second).");
        // Pin the known dependency cost, without pretending it is allocation-free.
        // Do not gate wall-clock time: a shared CI machine is not a benchmark rig.
        Assert.InRange(allocated / seconds, 0, 2_500_000);
    }

    [Fact]
    public async Task GameplayControlsDoNotWaitForTheDecoder()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var cue = MusicStreamTests.Cue(16384, 1024);
        using var mixer = new MusicMixer2D(new Dictionary<string, MusicCue2D> { ["a"] = cue },
            c => new(c, [new BlockingStem(entered, release), new MusicStreamTests.FakeStem(16384, static _ => 0)]));
        mixer.Select("a", "explore");
        var render = Task.Run(() => mixer.Read(new float[2048]));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Audio read never entered the decoder.");
            var control = Task.Run(() => { mixer.Select("a", "combat"); mixer.Volume = .25f; });
            await control.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(render.IsCompleted); // Controls completed while decoding remained blocked.
        }
        finally { release.Set(); await render.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    private sealed class BlockingStem(ManualResetEventSlim entered, ManualResetEventSlim release) : IMusicStem2D
    {
        public int Read(float[] buffer, int offset, int count)
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Test decoder was never released.");
            buffer.AsSpan(offset, count).Clear(); return count;
        }
        public void Rewind() { }
        public void Dispose() { }
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets/Static/audio/music"))) return dir.FullName;
        throw new DirectoryNotFoundException("Cannot locate music assets.");
    }
}
