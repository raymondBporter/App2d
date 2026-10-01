using App2d.Core.Geometry;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class Size2DTests
{
    [Fact]
    public void DimensionsAreNamedAndConvertExplicitlyToVectors()
    {
        var size = new Size2D(12f, 7f);

        Assert.True(size.IsValid);
        Assert.Equal(12f, size.Width);
        Assert.Equal(7f, size.Height);
        Assert.Equal(new Vector2(12f, 7f), size.ToVector2());
        Assert.Equal(size, Size2D.FromVector2(new Vector2(12f, 7f)));
        Assert.Equal(size, JsonSerializer.Deserialize<Size2D>(JsonSerializer.Serialize(size)));
        Assert.False(default(Size2D).IsValid);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void DimensionsMustBePositiveAndFinite(float invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Size2D(invalid, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Size2D(1f, invalid));
    }
}
