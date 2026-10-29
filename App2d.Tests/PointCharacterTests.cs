using App2d.CharacterStudio.Importing;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests;

public sealed class PointCharacterTests
{
    private static PointLibrary Library(bool loop = true, double[]? times = null, string? pointsJson = null, string[]? names = null)
    {
        var spec = new
        {
            version = 2,
            format = "app2d-point-library",
            id = "test",
            label = "Test",
            anatomy = "test",
            pointNames = names ?? ["root"],
            drawing = new
            {
            },
            clips = new Dictionary<string, object>
            {
                ["motion"] = new
                {
                    label = "Motion",
                    loop,
                    duration = 1.0,
                    times = times ?? [0, .25, 1]
                }
            }
        };
        return new(JsonSerializer.Serialize(spec), _ => pointsJson ?? """
            {"root":[[-5,15,110],[0,20,115],[10,30,125]]}
            """);
    }
    [Fact]
    public void UsesActualAdaptiveTimesAndPlainXyzCoordinates()
    {
        var clip = Library().Clips["motion"];
        Span<Vector3> pose = stackalloc Vector3[1];
        clip.Sample(.125, pose);
        Assert.Equal(new Vector3(-2.5f, 17.5f, 112.5f), pose[0]);
        clip.Sample(.625, pose);
        Assert.Equal(new Vector3(5, 25, 120), pose[0]);
    }
    [Fact]
    public void LoopWrapAndExplicitEndpointHoldAreDifferentOperations()
    {
        var clip = Library().Clips["motion"];
        Span<Vector3> pose = stackalloc Vector3[1];
        clip.Sample(1, pose);
        Assert.Equal(clip.Read(0, 0), pose[0]);
        clip.Sample(1, pose, holdEnd: true);
        Assert.Equal(clip.Read(2, 0), pose[0]);
        clip.Sample(-.375, pose);
        Assert.Equal(new Vector3(5, 25, 120), pose[0]);
        var once = Library(false).Clips["motion"];
        once.Sample(100, pose);
        Assert.Equal(once.Read(2, 0), pose[0]);
    }
    [Fact]
    public void EndpointHoldUsesStoredPoseWhenFinalTimestampHasRoundingDrift()
    {
        foreach (var loop in new[] { false, true })
        {
            var clip = Library(loop, [0, 1 - 1e-10, 1 + 1e-10]).Clips["motion"];
            var pose = new Vector3[1];
            clip.Sample(1, pose, holdEnd: true);
            Assert.Equal(clip.Read(2, 0), pose[0]);
            clip.Sample(100, pose, holdEnd: true);
            Assert.Equal(clip.Read(2, 0), pose[0]);
            clip.Sample(1, pose);
            Assert.Equal(clip.Read(loop ? 0 : 2, 0), pose[0]);
        }
    }
    [Fact]
    public void RejectsInvalidTimingAndSampleArguments()
    {
        Assert.Throws<InvalidDataException>(() => Library(times: [0, 0, 1]));
        Assert.Throws<InvalidDataException>(() => Library(times: [0, .25, .9]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Library().Clips["motion"].Sample(double.NaN, new Vector3[1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Library().Clips["motion"].Read(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Library().Clips["motion"].Read(0, 1));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"wrong\":[[1,2,3],[4,5,6],[7,8,9]]}")]
    [InlineData("{\"root\":[[1,2,3],[4,5,6]]}")]
    [InlineData("{\"root\":[[1,2],[4,5,6],[7,8,9]]}")]
    [InlineData("{\"root\":[[1,2,3,4],[4,5,6],[7,8,9]]}")]
    [InlineData("{\"root\":[[1e100,2,3],[4,5,6],[7,8,9]]}")]
    [InlineData("{\"root\":[[null,2,3],[4,5,6],[7,8,9]]}")]
    [InlineData("{\"root\":[[1,2,3],[4,5,6],[7,8,9]],\"extra\":[]}")]
    public void RejectsMissingMismatchedOrInvalidPoints(string pointsJson)
    {
        Assert.Throws<InvalidDataException>(() => Library(pointsJson: pointsJson));
    }

    [Fact]
    public void NamedTracksIgnoreJsonPropertyOrderAndCoordinatesCanBeEditedDirectly()
    {
        const string points = """
            {
              "hand":[[1,2,3],[4,5,6],[7,8,9]],
              "root":[[-5,15,110],[0,20,115],[10,30,125]]
            }
            """;
        var original = Library(pointsJson: points, names: ["root", "hand"]).Clips["motion"];
        Assert.Equal(new Vector3(1, 2, 3), original.Read(0, 1));
        Assert.Equal(new Vector3(-5, 15, 110), original.Read(0, 0));
        var edited = Library(pointsJson: points.Replace("[1,2,3]", "[11,22,33]"), names: ["root", "hand"]).Clips["motion"];
        Assert.Equal(new Vector3(11, 22, 33), edited.Read(0, 1));
        Assert.Equal(original.Read(0, 0), edited.Read(0, 0));
    }

    [Fact]
    public void EveryCatalogLibraryLoadsFromJsonWithMatchingClipsAndPointTracks()
    {
        var catalogPath = TestAssets.GetPath("Characters", "catalog.json");
        using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
        foreach (var entry in catalog.RootElement.GetProperty("libraries").EnumerateArray())
        {
            var library = PointLibrary.Load(Path.Combine(Path.GetDirectoryName(catalogPath)!, entry.GetProperty("path").GetString()!));
            Assert.Equal(entry.GetProperty("clipCount").GetInt32(), library.Clips.Count);
            foreach (var clip in library.Clips.Values)
            {
                var pose = new Vector3[library.PointNames.Count];
                clip.Sample(clip.Duration / 2, pose);
                Assert.All(pose, p => Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)));
            }
        }
    }
    [Fact]
    public void SamplingDoesNotAllocatePerFrameArrays()
    {
        var clip = Library().Clips["motion"];
        var pose = new Vector3[1];
        for (var i = 0; i < 100; i++)
        {
            clip.Sample(.3, pose);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            clip.Sample(i * .013, pose);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
