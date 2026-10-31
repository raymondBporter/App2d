using App2d.Core.Animation;

namespace App2d.Tests.Animation;

public sealed class KeyframeEditingTests
{
    private sealed record Key(float Time, int Value) : ITimedKey;

    [Fact]
    public void ReplacingNearbyImportedKeysKeepsTheListOrderedAndUnambiguous()
    {
        var tolerance = KeyframeEditing.TimeTolerance;
        List<Key> keys = [new(0, 1), new(tolerance / 2, 2), new(1, 3)];
        var replacement = new Key(tolerance * .75f, 4);

        keys.Put(replacement);

        Assert.Equal([replacement, new Key(1, 3)], keys);
        Assert.Same(replacement, keys.AtTime(0));
        Assert.True(KeyframeSampling.TrySegment(keys, .5f, out var start, out var end, out var phase));
        Assert.Same(replacement, start);
        Assert.Equal(keys[1], end);
        Assert.InRange(phase, 0, 1);
    }

    [Fact]
    public void AuthoringToleranceDoesNotSnapSamplingOrMergeKeysExactlyAtTheTolerance()
    {
        var tolerance = KeyframeEditing.TimeTolerance;
        List<Key> keys = [];
        keys.Put(new(tolerance, 2));
        keys.Put(new(0, 1));

        Assert.Equal(2, keys.Count);
        Assert.True(KeyframeSampling.TrySegment(keys, tolerance / 2, out var start, out var end, out var phase));
        Assert.Equal(1, start!.Value);
        Assert.Equal(2, end!.Value);
        Assert.Equal(.5f, phase);
        keys.DeleteAtTime(0);
        Assert.Equal(new(tolerance, 2), Assert.Single(keys));
    }
}
