using App2d.Contracts.World;
using App2d.Audio;
using App2d.Presentation.Audio;
using App2d.Levels;
using System.Numerics;

namespace App2d.Presentation.Tests.Audio;

public sealed class WorldMusicTests
{
    [Theory]
    [InlineData("piece")]
    [InlineData("mood")]
    [InlineData("fallback")]
    public void RepeatedShortBoundaryVisitsNeverAccumulateIntoAMusicChange(string change)
    {
        var content = LevelContent2D.Empty with
        {
            Zones = [
            new("a", "A", new(Vector2.Zero, new(10, 10))),
            new("b", "B", new(new(10, 0), new(20, 10)))]
        };
        var initial = new MusicSelection2D("orchestra", "explore");
        var target = change == "mood" ? new MusicSelection2D("orchestra", "combat") : new("rock", "drive");
        var soundtrack = new WorldSoundtrack2D(
            new Dictionary<string, MusicCue2D> { ["orchestra"] = MusicStreamTests.Cue(), ["rock"] = MusicStreamTests.Cue() },
            change == "fallback" ? target : initial,
            new Dictionary<string, MusicSelection2D> { ["a"] = initial, ["b"] = target });
        var requests = new List<MusicSelection2D>();
        var director = new WorldMusicDirector2D(soundtrack, (p, m) => requests.Add(new(p, m)));
        var home = new Vector2(9.99f, 1);
        var across = new Vector2(change == "fallback" ? -.01f : 10.01f, 1);
        director.Update(content, home, 0);

        // Almost a minute in the candidate region in total, but never 0.75s
        // continuously. Every retreat must cancel the entire pending dwell.
        for (var visit = 0; visit < 80; visit++)
        {
            director.Update(content, across, 0);
            director.Update(content, across, .74f);
            director.Update(content, home, .01f);
        }
        Assert.Equal(initial, Assert.Single(requests));

        director.Update(content, across, 0);
        director.Update(content, across, .5f);
        director.Update(content, across, .249f);
        Assert.Single(requests);
        director.Update(content, across, .002f);
        Assert.Equal(2, requests.Count);
        Assert.Equal(target, requests[1]);
        for (var frame = 0; frame < 600; frame++) director.Update(content, across, 1f / 60);
        Assert.Equal(2, requests.Count); // Staying there never requests another restart.
    }

    [Fact]
    public void CrossingZonesWithTheSameMusicNeverReselectsTheTrack()
    {
        var content = LevelContent2D.Empty with
        {
            Zones = [
            new("a", "A", new(Vector2.Zero, new(10, 10))),
            new("b", "B", new(new(10, 0), new(20, 10)))]
        };
        var soundtrack = new WorldSoundtrack2D(new Dictionary<string, MusicCue2D> { ["orchestra"] = MusicStreamTests.Cue() },
            new("orchestra", "explore"), new Dictionary<string, MusicSelection2D>());
        var requests = 0;
        var director = new WorldMusicDirector2D(soundtrack, (_, _) => requests++);
        for (var frame = 0; frame < 600; frame++)
            director.Update(content, new(frame % 2 == 0 ? 9.99f : 10.01f, 1), 1f / 60);
        Assert.Equal(1, requests);
    }

    [Fact]
    public void ZoneDwellAvoidsBoundaryChatterAndFallbackAndOverridesWork()
    {
        var a = new WorldZone2D("a", "A", new(Vector2.Zero, new(10, 10)));
        var b = new WorldZone2D("b", "B", new(new(10, 0), new(20, 10)));
        var content = LevelContent2D.Empty with { Zones = [a, b] };
        var soundtrack = new WorldSoundtrack2D(new Dictionary<string, MusicCue2D> { ["orchestra"] = MusicStreamTests.Cue(), ["rock"] = MusicStreamTests.Cue() },
            new("orchestra", "explore"), new Dictionary<string, MusicSelection2D> { ["b"] = new("rock", "drive") });
        var selected = new List<MusicSelection2D>();
        var director = new WorldMusicDirector2D(soundtrack, (p, m) => selected.Add(new(p, m)));
        director.Update(content, new(1, 1), 0);
        Assert.Equal(new("orchestra", "explore"), Assert.Single(selected));
        for (var i = 0; i < 10; i++) director.Update(content, new(i % 2 == 0 ? 11 : 9, 1), .1f);
        Assert.Single(selected);
        director.Update(content, new(11, 1), 0);
        director.Update(content, new(11, 1), .8f);
        Assert.Equal(new("rock", "drive"), selected[^1]);
        director.Update(content, new(50, 50), 0); director.Update(content, new(50, 50), 1);
        Assert.Equal(new("orchestra", "explore"), selected[^1]); Assert.Null(director.CurrentZone);
        director.PieceOverride = "rock"; director.MoodOverride = "combat";
        director.Update(content, new(1, 1), 0); director.Update(content, new(1, 1), 1);
        Assert.Equal(new("rock", "combat"), selected[^1]);
        Assert.Throws<ArgumentException>(() => director.PieceOverride = "missing");
        Assert.Throws<ArgumentException>(() => director.MoodOverride = "missing");
        director.PieceOverride = "auto"; director.MoodOverride = "auto";
        director.Update(content, new(1, 1), 0); director.Update(content, new(1, 1), 1);
        Assert.Equal(new("orchestra", "explore"), selected[^1]);
    }

    [Theory]
    [InlineData("crown-of-embers")]
    [InlineData("copper-circuit")]
    public void ShippedOggStemsDecodeAndLoopExactlyWithBoundedBuffers(string id)
    {
        var root = FindRoot();
        var zones = WorldZoneFile2D.Load(Path.Combine(root, "Assets/Static/levels/cavern/zones.json"));
        var soundtrack = WorldSoundtrack2D.Load(Path.Combine(root, "Assets/Static/audio/music"), zones);
        var cue = soundtrack.Cues[id];
        using var stream = new MusicStream2D(cue);
        stream.Restart("combat");
        var first = new float[1024 * 2]; stream.Read(first);
        Assert.Contains(first, x => x != 0);
        var buffer = new float[997 * 2];
        var remaining = cue.LoopFrames - 1024;
        while (remaining > 0)
        {
            var count = (int)Math.Min(remaining, 997);
            stream.Read(buffer.AsSpan(0, count * 2));
            Assert.All(buffer.AsSpan(0, count * 2).ToArray(), x => Assert.True(float.IsFinite(x) && Math.Abs(x) < 1));
            remaining -= count;
        }
        Assert.Equal(0, stream.FramePosition);
        var looped = new float[first.Length]; stream.Read(looped);
        for (var i = 0; i < first.Length; i++) Assert.Equal(first[i], looped[i], 5);
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets/Static/audio/music"))) return dir.FullName;
        throw new DirectoryNotFoundException("Cannot locate durable music assets.");
    }
}
