using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using App2d.Core.Characters;
using Xunit;

namespace App2d.Tests;

public sealed class PointCharacterTests
{
    private static PointLibrary Library(bool loop = true, double[]? times = null, int offset = 0, byte[]? dataOverride = null)
    {
        byte[] data = [0, 0, 10, 0, 20, 0, 10, 0, 20, 0, 30, 0, 30, 0, 40, 0, 50, 0];
        var spec = new
        {
            version = 1, format = "app2d-point-library", id = "test", label = "Test", anatomy = "test", pointNames = new[] { "root" }, drawing = new { },
            dataSha256 = Convert.ToHexString(SHA256.HashData(data)),
            clips = new Dictionary<string, object> { ["motion"] = new
            {
                label = "Motion", loop, duration = 1.0, times = times ?? [0, .25, 1], sampleCount = 3, byteOffset = offset, byteLength = 18,
                encoding = new { type = "uint16-le", layout = "sample,point,xyz", origin = new[] { -5f, 10f, 100f }, step = .5f }
            } }
        };
        return new(JsonSerializer.Serialize(spec), dataOverride ?? data);
    }
    [Fact]
    public void UsesActualAdaptiveTimesAndPerAxisOrigins()
    {
        var clip = Library().Clips["motion"]; Span<Vector3> pose = stackalloc Vector3[1];
        clip.Sample(.125, pose); Assert.Equal(new Vector3(-2.5f, 17.5f, 112.5f), pose[0]);
        clip.Sample(.625, pose); Assert.Equal(new Vector3(5, 25, 120), pose[0]);
    }
    [Fact]
    public void LoopWrapAndExplicitEndpointHoldAreDifferentOperations()
    {
        var clip = Library().Clips["motion"]; Span<Vector3> pose = stackalloc Vector3[1];
        clip.Sample(1, pose); Assert.Equal(clip.Read(0, 0), pose[0]);
        clip.Sample(1, pose, holdEnd: true); Assert.Equal(clip.Read(2, 0), pose[0]);
        clip.Sample(-.375, pose); Assert.Equal(new Vector3(5, 25, 120), pose[0]);
        var once = Library(false).Clips["motion"]; once.Sample(100, pose); Assert.Equal(once.Read(2, 0), pose[0]);
    }
    [Fact]
    public void EndpointHoldUsesStoredPoseWhenFinalTimestampHasRoundingDrift()
    {
        foreach (var loop in new[] { false, true })
        {
            var clip = Library(loop, [0, 1 - 1e-10, 1 + 1e-10]).Clips["motion"];
            var pose = new Vector3[1];
            clip.Sample(1, pose, holdEnd: true); Assert.Equal(clip.Read(2, 0), pose[0]);
            clip.Sample(100, pose, holdEnd: true); Assert.Equal(clip.Read(2, 0), pose[0]);
            clip.Sample(1, pose); Assert.Equal(clip.Read(loop ? 0 : 2, 0), pose[0]);
        }
    }
    [Fact]
    public void RejectsCorruptDataAndInvalidSampleRanges()
    {
        Assert.Throws<InvalidDataException>(() => Library(times: [0, 0, 1]));
        Assert.Throws<InvalidDataException>(() => Library(times: [0, .25, .9]));
        Assert.Throws<InvalidDataException>(() => Library(offset: 1));
        Assert.Throws<InvalidDataException>(() => Library(dataOverride: new byte[18]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Library().Clips["motion"].Sample(double.NaN, new Vector3[1]));
    }
    [Fact]
    public void SamplingDoesNotAllocateDecodedClipsOrPerFrameArrays()
    {
        var clip = Library().Clips["motion"]; var pose = new Vector3[1];
        for (var i = 0; i < 100; i++) clip.Sample(.3, pose);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) clip.Sample(i * .013, pose);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
    [Fact]
    public void SequenceCarriesTimeAndHoldsFinalPoseEvenForLoopClips()
    {
        var library = Library(); var player = new PointPlayback(library, "motion");
        player.StartSequence(["motion", "motion"], false); player.Advance(1.2);
        Assert.Equal(1, player.SequenceIndex); Assert.Equal(.2, player.Time, 6);
        player.Advance(1); Assert.True(player.Finished); Assert.False(player.Playing);
        player.Select("motion"); player.Advance(100.25); Assert.Equal(.25, player.Time);
    }
    [Fact]
    public void AdaptiveSteppingCanReachAndLeaveLastLoopSample()
    {
        var player = new PointPlayback(Library(), "motion"); player.Seek(.25); player.Step(1);
        Assert.True(player.Finished); player.Step(-1); Assert.Equal(.25, player.Time); Assert.False(player.Playing);
    }
}
