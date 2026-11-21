using App2d.Core.Animation;

namespace App2d.Tests.Animation;

public sealed class KeyframeSamplerTests
{
    private sealed record ScalarKey(float Time, float Value) : ITimedKey;

    private static float Sample(IReadOnlyList<ScalarKey> keys, float time, float emptyValue, Interpolator<float, float> interpolation) =>
        KeyframeSampling.Sample(keys, time, emptyValue, static key => key.Value,
            (start, end, phase) => interpolation.Evaluate(start.Value, end.Value, phase));

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
        Assert.Equal(expected, Sample(keys, time, -1f, Interpolation.Linear<float>()));
    }

    [Fact]
    public void EmptyTracksUseTheCallersDefaultAndSingleKeysAreHeld()
    {
        Assert.Equal(42, Sample(Array.Empty<ScalarKey>(), 5, 42f, Interpolation.Linear<float>()));
        ScalarKey[] keys = [new(2, 10)];
        foreach (var time in new[] { -2, 2, 5 })
            Assert.Equal(10, Sample(keys, time, -1f, Interpolation.Linear<float>()));
    }

    [Fact]
    public void InterpolationCanHoldValuesOrOvershootWithoutChangingKeyBoundaries()
    {
        ScalarKey[] keys = [new(2, 10), new(4, 30)];
        Assert.Equal(10, Sample(keys, 3.5f, -1f, Interpolation.Hold<float, float>()));
        Assert.Equal(30, Sample(keys, 4, -1f, Interpolation.Hold<float, float>()));
        var overshoot = Interpolation.Linear<float>().WithProgress(ProgressMapf.Affine(2f, 0f));
        Assert.Equal(40, Sample(keys, 3.5f, -1f, overshoot));
        Assert.Equal(30, Sample(keys, 4, -1f, overshoot));
    }

    [Fact]
    public void LargeNonuniformTimelinesPreserveExactKeysAndLocalFractions()
    {
        var keys = Enumerable.Range(0, 4096).Select(i => new ScalarKey(i * i, i)).ToArray();
        foreach (var i in new[] { 0, 1, 31, 1024, 2049, 4094 })
        {
            var at = keys[i].Time;
            Assert.Equal(i, Sample(keys, at, -1f, Interpolation.Linear<float>()));
            var middle = (at + keys[i + 1].Time) / 2;
            Assert.Equal(i + .5f, Sample(keys, middle, -1f, Interpolation.Linear<float>()));
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
