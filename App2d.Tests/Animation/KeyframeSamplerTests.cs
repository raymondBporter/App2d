using App2d.Core.Animation;
using App2d.Core.Mathematics;

namespace App2d.Tests.Animation;

public sealed class KeyframeSamplerTests
{
    private sealed record ScalarKey(float Time, float Value) : ITimedKey;

    private readonly struct ScalarInterpolation(float progressScale = 1, bool step = false) : IKeyframeInterpolation<ScalarKey, float>
    {
        public float GetValue(ScalarKey key) => key.Value;
        public bool IsHeld(ScalarKey key) => step;
        public float Interpolate(ScalarKey start, ScalarKey end, float phase) =>
            Interpolation.Lerp(start.Value, end.Value, phase * progressScale);
    }

    [Theory]
    [InlineData(-2, 10)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 30)]
    [InlineData(7, 60)]
    [InlineData(10, 90)]
    [InlineData(12, 90)]
    public void ScalarValuesUseTheLocalTimeIntervalAndHoldEndpoints(float time, float expected)
    {
        ScalarKey[] keys = [new(2, 10), new(4, 30), new(10, 90)];
        Assert.Equal(expected, KeyframeSampling.Sample(keys, time, -1f, new ScalarInterpolation(1)));
    }

    [Fact]
    public void EmptyTracksUseTheCallersDefaultAndSingleKeysAreHeld()
    {
        Assert.Equal(42, KeyframeSampling.Sample(Array.Empty<ScalarKey>(), 5, 42f, new ScalarInterpolation(1)));
        ScalarKey[] keys = [new(2, 10)];
        foreach (var time in new[] { -2, 2, 5 })
            Assert.Equal(10, KeyframeSampling.Sample(keys, time, -1f, new ScalarInterpolation(1)));
    }

    [Fact]
    public void InterpolationCanHoldValuesOrOvershootWithoutChangingKeyBoundaries()
    {
        ScalarKey[] keys = [new(2, 10), new(4, 30)];
        Assert.Equal(10, KeyframeSampling.Sample(keys, 3.5f, -1f, new ScalarInterpolation(1, step: true)));
        Assert.Equal(30, KeyframeSampling.Sample(keys, 4, -1f, new ScalarInterpolation(1, step: true)));
        Assert.Equal(40, KeyframeSampling.Sample(keys, 3.5f, -1f, new ScalarInterpolation(2)));
        Assert.Equal(30, KeyframeSampling.Sample(keys, 4, -1f, new ScalarInterpolation(2)));
    }

    [Fact]
    public void LargeNonuniformTimelinesPreserveExactKeysAndLocalFractions()
    {
        var keys = Enumerable.Range(0, 4096).Select(i => new ScalarKey(i * i, i)).ToArray();
        foreach (var i in new[] { 0, 1, 31, 1024, 2049, 4094 })
        {
            var at = keys[i].Time;
            Assert.Equal(i, KeyframeSampling.Sample(keys, at, -1f, new ScalarInterpolation(1)));
            var middle = (at + keys[i + 1].Time) / 2;
            Assert.Equal(i + .5f, KeyframeSampling.Sample(keys, middle, -1f, new ScalarInterpolation(1)));
        }
    }

    [Fact]
    public void HeldSamplingCanPreserveSetupUntilTheFirstKey()
    {
        ScalarKey[] keys = [new(2, 10), new(4, 30)];
        Assert.Null(KeyframeSampling.Held(keys, 1, holdBeforeFirst: false));
        Assert.Same(keys[0], KeyframeSampling.Held(keys, 1));
        Assert.Same(keys[0], KeyframeSampling.Held(keys, 2, holdBeforeFirst: false));
        Assert.Same(keys[0], KeyframeSampling.Held(keys, 3, holdBeforeFirst: false));
        Assert.Same(keys[1], KeyframeSampling.Held(keys, 4, holdBeforeFirst: false));
        Assert.Same(keys[1], KeyframeSampling.Held(keys, 8, holdBeforeFirst: false));
        Assert.Null(KeyframeSampling.Held(Array.Empty<ScalarKey>(), 1, holdBeforeFirst: false));
    }
}
