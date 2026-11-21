namespace App2d.Tests.Mathematics;

public sealed class UniformSequenceTests
{
    [Fact]
    public void IndexingAndEnumerationComputeTheSameFloatAndDoubleValues()
    {
        var single = new UniformSequence<float>(.25f, .25f, 4);
        Assert.Equal<float>([.25f, .5f, .75f, 1f], single);
        Assert.Equal(single.ToArray(), Enumerable.Range(0, single.Count).Select(index => single[index]).ToArray());

        var precise = new UniformSequence<double>(1d, 1e-10d, 3);
        Assert.Equal<double>([1d, 1.0000000001d, 1.0000000002d], precise);
    }

    [Fact]
    public void HugeUniformSequencesSupportLazyEnumerationAndOrderedLookup()
    {
        var values = new UniformSequence<double>(.5d, .5d, int.MaxValue);
        Assert.Equal(int.MaxValue, values.Count);
        Assert.Equal<double>([.5d, 1d, 1.5d], values.Take(3));
        Assert.Equal(int.MaxValue / 2d, values[^1]);
        Assert.Equal(1999999999, OrderedSearch.LowerBound(values, 1e9d));
        Assert.Equal(2000000000, OrderedSearch.UpperBound(values, 1e9d));
    }

    [Fact]
    public void EmptyConstantAndDescendingSequencesRemainUsefulWithoutOrderedSearch()
    {
        Assert.Empty(new UniformSequence<float>(0f, 1f, 0));
        Assert.Equal<float>([2f, 2f, 2f], new UniformSequence<float>(2f, 0f, 3));
        Assert.Equal<float>([3f, 2f, 1f], new UniformSequence<float>(3f, -1f, 3));
        var values = new UniformSequence<float>(0f, 1f, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => values[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => values[2]);
    }

    [Fact]
    public void InvalidCountsAndNonfiniteRangesAreRejectedAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UniformSequence<float>(0f, 1f, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UniformSequence<float>(float.NaN, 1f, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UniformSequence<float>(0f, float.PositiveInfinity, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UniformSequence<float>(float.MaxValue, float.MaxValue, 2));
    }
}
