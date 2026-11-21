using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Animation;

public sealed class InterpolationAssetMigrationTests
{
    private static MotionClip LegacyClip()
    {
        using var stream = typeof(InterpolationAssetMigrationTests).Assembly.GetManifestResourceStream("InterpolationTrial.PlayerIdleV4.json")!;
        using var reader = new StreamReader(stream);
        return MotionClip.FromJson(reader.ReadToEnd());
    }

    [Fact]
    public void MigratedPlayerIdleLoadsThroughTheCatalogAndPreservesChannelsAndEvaluatedPoses()
    {
        var legacy = LegacyClip();
        var catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);
        Assert.DoesNotContain(catalog.Errors, error => error.Contains("player-idle", StringComparison.Ordinal));
        var migrated = catalog.Animations["player-idle"];
        var restored = MotionClip.FromJson(migrated.ToJson());
        Assert.Equal(4, legacy.Version);
        Assert.Equal(5, migrated.Version);
        Assert.Equal(legacy.Duration, migrated.Duration);
        Assert.Equal(legacy.Tracks.Count, migrated.Tracks.Count);
        Assert.All(migrated.Tracks, track => Assert.All(track.Keys, key =>
        {
            Assert.True(track.Kind == MotionClip.RotateKind ? key.AngleInterpolator.HasValue : key.Interpolator.HasValue);
        }));

        for (var trackIndex = 0; trackIndex < legacy.Tracks.Count; trackIndex++)
        {
            var before = legacy.Tracks[trackIndex];
            var after = migrated.Tracks[trackIndex];
            Assert.Equal(before.Kind, after.Kind);
            Assert.Equal(before.Target, after.Target);
            Assert.Equal(before.Keys.Select(key => (key.Time, key.Value, key.Angle, key.Bend)),
                after.Keys.Select(key => (key.Time, key.Value, key.Angle, key.Bend)));
            var times = Enumerable.Range(0, 241).Select(index => migrated.Duration * index / 240f)
                .Concat(before.Keys.SelectMany(key => new[] { float.BitDecrement(key.Time), key.Time, float.BitIncrement(key.Time) }))
                .Append(-1f).Append(migrated.Duration + 1f);
            foreach (var time in times)
            {
                var expected = PoseEvaluator.Sample(before, time);
                foreach (var candidate in new[] { after, restored.Tracks[trackIndex] })
                {
                    var actual = PoseEvaluator.Sample(candidate, time);
                    Assert.InRange(Vector3f.Distance(expected.Value, actual.Value), 0f, 1e-7f);
                    Assert.InRange(MathF.Abs(expected.Angle - actual.Angle), 0f, 1e-7f);
                }
            }
        }

        var model = catalog.Resolve(migrated.Model);
        for (var index = 0; index <= 24; index++)
        {
            var time = migrated.Duration * index / 24d;
            var expected = PoseEvaluator.Sample(model, legacy, time);
            foreach (var candidate in new[] { migrated, restored })
            {
                var actual = PoseEvaluator.Sample(model, candidate, time);
                Assert.Equal(expected.Points.Keys, actual.Points.Keys);
                foreach (var (id, point) in expected.Points)
                {
                    Assert.InRange(Vector3f.Distance(point, actual.Points[id]), 0f, 2e-5f);
                    Assert.InRange(MathF.Abs(expected.Angles[id] - actual.Angles[id]), 0f, 2e-5f);
                    foreach (var marker in new[] { Vector2f.Zero, Vector2f.UnitX, Vector2f.UnitY })
                        Assert.InRange(Vector2f.Distance(Vector2f.Transform(marker, expected.Bones[id]), Vector2f.Transform(marker, actual.Bones[id])), 0f, 2e-5f);
                }
            }
        }
    }

    [Fact]
    public void VersionFiveColorKeysCanUseTheSameCoreRgbAndAlphaRecipes()
    {
        const string json = """
            {"format":"app2d-clip","version":5,"id":"tint","name":"Tint","model":"person","duration":1,
             "colors":[{"slot":"surface","keys":[{"r":0,"g":0,"b":0,"a":0.2,
               "interpolator":{"kind":"hold","operations":[
                 {"kind":"channel","channel":"rgb","interpolator":{"kind":"default","progress":"smooth"}},
                 {"kind":"channel","channel":"a","interpolator":{"kind":"linear","progress":{"kind":"power","degree":2}}}]}},
               {"time":1}]}]}
            """;
        var clip = MotionClip.FromJson(json);
        var restored = MotionClip.FromJson(clip.ToJson());
        var expected = new Vector4(.5f, .5f, .5f, .4f);
        Assert.Equal(expected, SlotColorTrack2D.Sample(clip.Colors[0].Keys, .5f));
        Assert.Equal(expected, SlotColorTrack2D.Sample(restored.Colors[0].Keys, .5f));
    }

    [Theory]
    [InlineData(4, "\"interpolator\":{\"kind\":\"default\"}")]
    [InlineData(5, "\"ease\":\"smooth\",\"interpolator\":{\"kind\":\"default\"}")]
    [InlineData(5, "\"angleInterpolator\":\"linear\"")]
    public void OldVersionsAndAmbiguousOrWrongChannelFieldsAreRejected(int version, string fields)
    {
        var json = "{\"format\":\"app2d-clip\",\"version\":" + version +
            ",\"id\":\"test\",\"name\":\"Test\",\"model\":\"person\",\"tracks\":[{\"target\":\"hips\",\"keys\":[{" + fields + "}]}]}";
        Assert.Throws<InvalidDataException>(() => MotionClip.FromJson(json));
    }
}
