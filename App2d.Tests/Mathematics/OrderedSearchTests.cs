namespace App2d.Tests.Mathematics;

public sealed class OrderedSearchTests
{
    [Theory]
    [InlineData(0d, 0, 0)]
    [InlineData(1d, 0, 1)]
    [InlineData(2d, 1, 4)]
    [InlineData(3d, 4, 4)]
    [InlineData(4d, 4, 5)]
    [InlineData(5d, 5, 5)]
    public void BoundsDistinguishExactValuesDuplicatesAndGaps(double query, int lower, int upper)
    {
        double[] values = [1d, 2d, 2d, 2d, 4d];
        Assert.Equal(lower, OrderedSearch.LowerBound(values, query));
        Assert.Equal(upper, OrderedSearch.UpperBound(values, query));
    }

    [Fact]
    public void EmptyListsReturnTheirEndIndex()
    {
        Assert.Equal(0, OrderedSearch.LowerBound(Array.Empty<float>(), 1f));
        Assert.Equal(0, OrderedSearch.UpperBound(Array.Empty<float>(), 1f));
    }

    [Fact]
    public void ProjectedKeysAreSearchedWithoutCopyingOrScanningTheTimeline()
    {
        var keys = Enumerable.Range(0, 4096).Select(index => (Time: index * 2f, Value: index)).ToArray();
        var reads = 0;
        Assert.Equal(1024, OrderedSearch.LowerBound(keys, 2048f, key => { reads++; return key.Time; }));
        Assert.InRange(reads, 1, 13);
        Assert.Equal(1025, OrderedSearch.UpperBound(keys, 2048f, static key => key.Time));
    }
}
