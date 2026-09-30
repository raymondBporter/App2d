using App2d.Core.Mathematics;

namespace App2d.Tests.Mathematics;

public sealed class RandomExtensionsTests
{
    [Fact]
    public void RangesUseTheSuppliedSeededRandomStream()
    {
        var expected = new Random(42);
        var actual = new Random(42);

        Assert.Equal(expected.NextSingle() * 5f, actual.NextFloat(5f));
        Assert.Equal(-3f + expected.NextSingle() * 8f, actual.NextFloat(-3f, 5f));
        Assert.Equal(expected.NextSingle(), actual.NextSingle());
    }

    [Fact]
    public void RangesStayWithinTheirBounds()
    {
        var random = new Random(17);
        Assert.Equal(0f, random.NextFloat(0f));
        Assert.Equal(2f, random.NextFloat(2f, 2f));
        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(random.NextFloat(5f), 0f, 5f);
            Assert.InRange(random.NextFloat(-4f, -1f), -4f, -1f);
        }
    }

    [Fact]
    public void RejectsInvalidBounds()
    {
        var random = new Random(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextFloat(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextFloat(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextFloat(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextFloat(2f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextFloat(float.NegativeInfinity, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextFloat(0f, float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextFloat(float.MinValue, float.MaxValue));
    }
}
